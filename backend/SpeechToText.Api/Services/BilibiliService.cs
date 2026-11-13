using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public class BilibiliService : IBilibiliService
    {
        private readonly IAudioConverter _audioConverter;
        private readonly SemaphoreSlim _cookieLock = new(1, 1);
        private string? _cachedVisitorCookie;
        private DateTime _cookieFetchedAt = DateTime.MinValue;

        private const string ChromeUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

        public BilibiliService(IAudioConverter audioConverter)
        {
            _audioConverter = audioConverter;
        }

        private async Task<string> EnsureVisitorCookieAsync(bool forceRefresh = false, bool bypassProxy = false)
        {
            if (!forceRefresh && !string.IsNullOrEmpty(_cachedVisitorCookie) && (DateTime.UtcNow - _cookieFetchedAt).TotalMinutes < 30)
            {
                return _cachedVisitorCookie;
            }

            await _cookieLock.WaitAsync();
            try
            {
                if (!forceRefresh && !string.IsNullOrEmpty(_cachedVisitorCookie) && (DateTime.UtcNow - _cookieFetchedAt).TotalMinutes < 30)
                {
                    return _cachedVisitorCookie;
                }

                try
                {
                    using var handler = new HttpClientHandler
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    };
                    if (bypassProxy)
                    {
                        handler.UseProxy = false;
                    }

                    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(ChromeUserAgent);
                    client.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");
                    client.DefaultRequestHeaders.Add("Origin", "https://www.bilibili.com");

                    // Fetch official buvid3/buvid4 guest fingerprint from Bilibili SPI endpoint to bypass HTTP 412 checks
                    var spiJson = await client.GetStringAsync("https://api.bilibili.com/x/frontend/finger/spi");
                    using var doc = JsonDocument.Parse(spiJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("code", out var codeProp) && codeProp.GetInt32() == 0 && root.TryGetProperty("data", out var data))
                    {
                        var b3 = data.TryGetProperty("b_3", out var p3) ? p3.GetString() : null;
                        var b4 = data.TryGetProperty("b_4", out var p4) ? p4.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(b3))
                        {
                            _cachedVisitorCookie = !string.IsNullOrWhiteSpace(b4)
                                ? $"buvid3={b3}; buvid4={b4}"
                                : $"buvid3={b3}";
                            _cookieFetchedAt = DateTime.UtcNow;
                            Console.WriteLine("[BilibiliService] Acquired visitor buvid3/buvid4 cookie successfully.");
                            return _cachedVisitorCookie;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BilibiliService] Failed to fetch visitor spi cookie: {ex.Message}");
                }

                // Fallback cookie generation if SPI endpoint is unreachable
                var fallbackBuvid3 = $"{Guid.NewGuid().ToString().ToUpper()}00000infoc";
                _cachedVisitorCookie = $"buvid3={fallbackBuvid3}";
                _cookieFetchedAt = DateTime.UtcNow;
                return _cachedVisitorCookie;
            }
            finally
            {
                _cookieLock.Release();
            }
        }

        private HttpClient CreateBiliHttpClient(string? cookie, bool bypassProxy, int timeoutSeconds = 20)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            if (bypassProxy)
            {
                handler.UseProxy = false;
            }

            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(ChromeUserAgent);
            client.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");
            client.DefaultRequestHeaders.Add("Origin", "https://www.bilibili.com");
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            if (!string.IsNullOrEmpty(cookie))
            {
                client.DefaultRequestHeaders.Add("Cookie", cookie);
            }
            return client;
        }

        private async Task<string> GetBiliStringAsync(string apiUrl, int timeoutSeconds = 20)
        {
            Exception? lastEx = null;

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                bool forceRefresh = attempt > 1;
                bool bypassProxy = attempt == 3; // On 3rd attempt, bypass system proxy in case foreign proxy IP triggered 412
                var cookie = await EnsureVisitorCookieAsync(forceRefresh, bypassProxy);

                try
                {
                    using var client = CreateBiliHttpClient(cookie, bypassProxy, timeoutSeconds);
                    var response = await client.GetAsync(apiUrl);
                    if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                    {
                        Console.WriteLine($"[BilibiliService] HTTP 412 on attempt {attempt} for {apiUrl}, refreshing cookie/route...");
                        _cachedVisitorCookie = null;
                        lastEx = new HttpRequestException("Response status code does not indicate success: 412 (Precondition Failed).", null, HttpStatusCode.PreconditionFailed);
                        await Task.Delay(250);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Console.WriteLine($"[BilibiliService] Request attempt {attempt} failed for {apiUrl}: {ex.Message}");
                    if (attempt < 3) await Task.Delay(250);
                }
            }

            throw lastEx ?? new Exception("请求 B站 API 失败。");
        }

        private async Task<string> ResolveUrlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL cannot be empty.");
            }

            // Handle b23.tv short link redirection
            if (url.Contains("b23.tv", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[BilibiliService] Resolving short URL: {url}");
                var cookie = await EnsureVisitorCookieAsync();
                using var client = CreateBiliHttpClient(cookie, bypassProxy: false, timeoutSeconds: 10);

                var response = await client.GetAsync(url);
                var resolvedUrl = response.RequestMessage?.RequestUri?.ToString();
                if (!string.IsNullOrEmpty(resolvedUrl))
                {
                    Console.WriteLine($"[BilibiliService] Resolved short URL to: {resolvedUrl}");
                    return resolvedUrl;
                }
            }

            return url;
        }

        private static (string? Bvid, string? Aid) ExtractBvOrAid(string url)
        {
            // Regex for BVID: BV[a-zA-Z0-9]{10}
            var bvMatch = Regex.Match(url, @"BV[a-zA-Z0-9]{10}", RegexOptions.IgnoreCase);
            if (bvMatch.Success)
            {
                return (bvMatch.Value, null);
            }

            // Regex for AID: av[0-9]+
            var avMatch = Regex.Match(url, @"av[0-9]+", RegexOptions.IgnoreCase);
            if (avMatch.Success)
            {
                return (null, avMatch.Value.Substring(2));
            }

            return (null, null);
        }

        private static string NormalizeHttpsUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            if (url.StartsWith("//")) return "https:" + url;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return "https://" + url.Substring(7);
            return url;
        }

        private async Task<(string Title, string Author, string AvatarUrl, string ThumbnailUrl, double DurationSeconds, long Cid, string Bvid, string ResolvedUrl)> FetchVideoMetadataCoreAsync(string videoUrl)
        {
            var resolvedUrl = await ResolveUrlAsync(videoUrl);
            var (bvid, aid) = ExtractBvOrAid(resolvedUrl);

            if (string.IsNullOrEmpty(bvid) && string.IsNullOrEmpty(aid))
            {
                throw new ArgumentException("无法从链接中解析出有效的 B站 BV号 或 av号。");
            }

            string apiUrl = !string.IsNullOrEmpty(bvid)
                ? $"https://api.bilibili.com/x/web-interface/view?bvid={bvid}"
                : $"https://api.bilibili.com/x/web-interface/view?aid={aid}";

            Console.WriteLine($"[BilibiliService] Fetching metadata from B站 API: {apiUrl}");
            var jsonStr = await GetBiliStringAsync(apiUrl, timeoutSeconds: 15);

            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;
            var code = root.GetProperty("code").GetInt32();
            if (code != 0)
            {
                var msg = root.GetProperty("message").GetString();
                throw new Exception($"B站 API 返回错误: {msg} (错误码: {code})");
            }

            var data = root.GetProperty("data");
            var title = data.GetProperty("title").GetString() ?? "B站视频";
            var pic = NormalizeHttpsUrl(data.GetProperty("pic").GetString() ?? "");
            var duration = data.GetProperty("duration").GetDouble();
            var cid = data.GetProperty("cid").GetInt64();
            var actualBvid = data.TryGetProperty("bvid", out var bvidProp) ? (bvidProp.GetString() ?? bvid ?? "") : (bvid ?? "");

            // Handle multi-page videos (if ?p=N specified in URL)
            var pMatch = Regex.Match(resolvedUrl, @"[?&]p=(\d+)", RegexOptions.IgnoreCase);
            if (pMatch.Success && int.TryParse(pMatch.Groups[1].Value, out var pageNum) && pageNum > 1 && data.TryGetProperty("pages", out var pagesArr))
            {
                foreach (var page in pagesArr.EnumerateArray())
                {
                    if (page.TryGetProperty("page", out var pIdx) && pIdx.GetInt32() == pageNum)
                    {
                        cid = page.GetProperty("cid").GetInt64();
                        if (page.TryGetProperty("duration", out var pageDur))
                        {
                            duration = pageDur.GetDouble();
                        }
                        break;
                    }
                }
            }

            var owner = data.GetProperty("owner");
            var author = owner.GetProperty("name").GetString() ?? "未知UP主";
            var face = NormalizeHttpsUrl(owner.GetProperty("face").GetString() ?? "");

            return (title, author, face, pic, duration, cid, actualBvid, resolvedUrl);
        }

        private async Task<JsonDocument> FetchPlayUrlDocAsync(string bvid, long cid)
        {
            // try_look=1 + fnval=4048 + qn=80 unlocks 1080P/720P DASH streams for guest sessions
            var playUrlApi = $"https://api.bilibili.com/x/player/playurl?bvid={bvid}&cid={cid}&qn=80&fnval=4048&fnver=0&fourk=1&try_look=1";
            Console.WriteLine($"[BilibiliService] Requesting streams from B站 PlayURL API: {playUrlApi}");
            var playJson = await GetBiliStringAsync(playUrlApi, timeoutSeconds: 20);
            var playDoc = JsonDocument.Parse(playJson);

            var playRoot = playDoc.RootElement;
            if (playRoot.GetProperty("code").GetInt32() != 0)
            {
                var msg = playRoot.GetProperty("message").GetString();
                playDoc.Dispose();
                throw new Exception($"获取流媒体链接失败: {msg}");
            }

            return playDoc;
        }

        private static (string Url, List<string> Backups, string QualityLabel) SelectBestVideoStream(JsonElement videoArray, string quality)
        {
            int targetId = quality.ToLower() switch
            {
                "best" => 80,
                "1080p" => 80,
                "720p" => 64,
                "480p" => 32,
                _ => 80
            };

            JsonElement? chosen = null;

            // 1. Exact quality match preferring H.264 (avc1) for universal compatibility
            foreach (var vItem in videoArray.EnumerateArray())
            {
                var id = vItem.GetProperty("id").GetInt32();
                var codecs = vItem.TryGetProperty("codecs", out var cp) ? (cp.GetString() ?? "") : "";
                if (id == targetId && codecs.StartsWith("avc", StringComparison.OrdinalIgnoreCase))
                {
                    chosen = vItem;
                    break;
                }
            }

            // 2. Exact quality match with any codec
            if (!chosen.HasValue)
            {
                foreach (var vItem in videoArray.EnumerateArray())
                {
                    if (vItem.GetProperty("id").GetInt32() == targetId)
                    {
                        chosen = vItem;
                        break;
                    }
                }
            }

            // 3. Best available stream preferring H.264
            if (!chosen.HasValue)
            {
                foreach (var vItem in videoArray.EnumerateArray())
                {
                    var codecs = vItem.TryGetProperty("codecs", out var cp) ? (cp.GetString() ?? "") : "";
                    if (codecs.StartsWith("avc", StringComparison.OrdinalIgnoreCase))
                    {
                        chosen = vItem;
                        break;
                    }
                }
            }

            // 4. First available stream
            var item = chosen ?? videoArray[0];
            var baseUrl = item.GetProperty("baseUrl").GetString() ?? "";
            var backups = new List<string>();
            if (item.TryGetProperty("backupUrl", out var backupProp) && backupProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var b in backupProp.EnumerateArray())
                {
                    var bu = b.GetString();
                    if (!string.IsNullOrEmpty(bu)) backups.Add(bu);
                }
            }

            var chosenId = item.GetProperty("id").GetInt32();
            var label = chosenId switch
            {
                120 => "4K 超清",
                116 => "1080P 60帧",
                80 => "1080P 高清",
                64 => "720P 高清",
                32 => "480P 标清",
                16 => "360P 流畅",
                _ => "默认画质"
            };

            return (baseUrl, backups, label);
        }

        private static (string Url, List<string> Backups) SelectBestAudioStream(JsonElement audioArray)
        {
            var item = audioArray[0];
            var baseUrl = item.GetProperty("baseUrl").GetString() ?? "";
            var backups = new List<string>();
            if (item.TryGetProperty("backupUrl", out var backupProp) && backupProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var b in backupProp.EnumerateArray())
                {
                    var bu = b.GetString();
                    if (!string.IsNullOrEmpty(bu)) backups.Add(bu);
                }
            }
            return (baseUrl, backups);
        }

        public async Task<OnlineVideoInfo> GetVideoInfoAsync(string videoUrl)
        {
            var meta = await FetchVideoMetadataCoreAsync(videoUrl);

            return new OnlineVideoInfo
            {
                Title = meta.Title,
                Author = meta.Author,
                DurationSeconds = meta.DurationSeconds,
                ThumbnailUrl = meta.ThumbnailUrl,
                AvatarUrl = meta.AvatarUrl,
                Url = meta.ResolvedUrl,
                Platform = "bilibili"
            };
        }

        public async Task<(string AudioPath, string Title, string AvatarUrl)> DownloadAudioAndMetadataAsync(string videoUrl)
        {
            var meta = await FetchVideoMetadataCoreAsync(videoUrl);
            using var playDoc = await FetchPlayUrlDocAsync(meta.Bvid, meta.Cid);

            var playData = playDoc.RootElement.GetProperty("data");
            if (!playData.TryGetProperty("dash", out var dash))
            {
                throw new InvalidOperationException("B站接口未返回 DASH 格式音视频流。");
            }

            var audioArray = dash.GetProperty("audio");
            if (audioArray.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("未找到可用的音频流轨道。");
            }

            var (audioUrl, audioBackups) = SelectBestAudioStream(audioArray);
            if (string.IsNullOrEmpty(audioUrl))
            {
                throw new InvalidOperationException("音频流 URL 为空。");
            }

            var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var tempAudioPath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.m4a");

            Console.WriteLine($"[BilibiliService] Downloading audio stream to: {tempAudioPath}");
            await DownloadStreamWithFallbackAsync(audioUrl, audioBackups, tempAudioPath, _ => { });
            Console.WriteLine("[BilibiliService] Audio download complete.");

            return (tempAudioPath, meta.Title, meta.AvatarUrl);
        }

        public async Task DownloadVideoFileAsync(
            string videoUrl,
            string quality,
            string taskId,
            IYouTubeDownloadTracker tracker,
            string downloadType = "video",
            string audioFormat = "mp3",
            string videoFormat = "mp4")
        {
            try
            {
                var isAudio = downloadType.Equals("audio", StringComparison.OrdinalIgnoreCase);
                tracker.UpdateProgress(taskId, 5, "正在解析 B站 视频信息与播放链接...");

                var meta = await FetchVideoMetadataCoreAsync(videoUrl);
                using var playDoc = await FetchPlayUrlDocAsync(meta.Bvid, meta.Cid);

                var playData = playDoc.RootElement.GetProperty("data");
                if (!playData.TryGetProperty("dash", out var dash))
                {
                    throw new InvalidOperationException("B站接口未返回 DASH 格式流链接。");
                }

                var videoArray = dash.GetProperty("video");
                var audioArray = dash.GetProperty("audio");

                if (audioArray.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("未找到可用的音频流轨道。");
                }

                var (audioStreamUrl, audioBackups) = SelectBestAudioStream(audioArray);
                if (string.IsNullOrEmpty(audioStreamUrl))
                {
                    throw new InvalidOperationException("获取到的音频流链接为空。");
                }

                var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                if (isAudio)
                {
                    var targetExt = audioFormat.ToLower();
                    if (targetExt != "mp3" && targetExt != "m4a" && targetExt != "wav" && targetExt != "flac")
                    {
                        targetExt = "mp3";
                    }

                    tracker.UpdateProgress(taskId, 15, $"开始下载 B站 音频轨并转为 {targetExt.ToUpper()}...");
                    var tempAudioFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.m4a");
                    await DownloadStreamWithFallbackAsync(audioStreamUrl, audioBackups, tempAudioFile, p =>
                    {
                        tracker.UpdateProgress(taskId, 15 + (p * 75), $"正在抓取音频轨 ({Math.Round(p * 100)}%)...");
                    });

                    string finalAudioFile;
                    if (targetExt == "m4a")
                    {
                        finalAudioFile = tempAudioFile;
                    }
                    else
                    {
                        tracker.UpdateProgress(taskId, 92, $"调用 FFmpeg 转码为高品质 {targetExt.ToUpper()} 音频...");
                        await _audioConverter.EnsureFFmpegAsync();
                        finalAudioFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.{targetExt}");
                        await RunFFmpegConvertAudioAsync(tempAudioFile, finalAudioFile);
                        try { if (File.Exists(tempAudioFile)) File.Delete(tempAudioFile); } catch { }
                    }

                    tracker.CompleteTask(taskId, finalAudioFile, meta.Title);
                    return;
                }

                // Video download
                if (videoArray.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("未找到可用的视频流轨道。");
                }

                var (videoStreamUrl, videoBackups, streamQualityLabel) = SelectBestVideoStream(videoArray, quality);
                if (string.IsNullOrEmpty(videoStreamUrl))
                {
                    throw new InvalidOperationException("获取到的视频流链接为空。");
                }

                var targetVideoExt = videoFormat.ToLower() == "mkv" ? "mkv" : "mp4";
                var tempVideoFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.mp4");
                var tempAudioFileForMerge = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.m4a");
                var mergedOutputFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.{targetVideoExt}");

                tracker.UpdateProgress(taskId, 10, $"开始下载视频轨 ({streamQualityLabel})...");
                await DownloadStreamWithFallbackAsync(videoStreamUrl, videoBackups, tempVideoFile, p =>
                {
                    tracker.UpdateProgress(taskId, 10 + (p * 60), $"正在下载视频轨 ({Math.Round(p * 100)}%)...");
                });

                tracker.UpdateProgress(taskId, 70, "视频轨下载完成，开始下载音频轨...");
                await DownloadStreamWithFallbackAsync(audioStreamUrl, audioBackups, tempAudioFileForMerge, p =>
                {
                    tracker.UpdateProgress(taskId, 70 + (p * 20), $"正在下载音频轨 ({Math.Round(p * 100)}%)...");
                });

                tracker.UpdateProgress(taskId, 90, $"音视频轨道下载完毕，正在调用 FFmpeg 封装为 {targetVideoExt.ToUpper()}...");
                await _audioConverter.EnsureFFmpegAsync();
                await RunFFmpegMergeAsync(tempVideoFile, tempAudioFileForMerge, mergedOutputFile);

                try
                {
                    if (File.Exists(tempVideoFile)) File.Delete(tempVideoFile);
                    if (File.Exists(tempAudioFileForMerge)) File.Delete(tempAudioFileForMerge);
                }
                catch { }

                tracker.CompleteTask(taskId, mergedOutputFile, meta.Title);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BilibiliService] Background download failed for task {taskId}: {ex.Message}");
                tracker.FailTask(taskId, ex.Message);
            }
        }

        private string GetFFmpegPath()
        {
            if (IsCommandAvailable("ffmpeg")) return "ffmpeg";
            var localPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(localPath)) return localPath;
            throw new FileNotFoundException("未在系统 PATH 或程序根目录下找到 FFmpeg。");
        }

        private bool IsCommandAvailable(string command)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-version");
                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task RunFFmpegMergeAsync(string videoPath, string audioPath, string outputPath)
        {
            var ffmpeg = GetFFmpegPath();
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(videoPath);
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(audioPath);
            startInfo.ArgumentList.Add("-c:v");
            startInfo.ArgumentList.Add("copy");
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("aac");
            startInfo.ArgumentList.Add(outputPath);

            Console.WriteLine($"[BilibiliService] Running FFmpeg Muxer for output: {outputPath}");

            using var process = new Process { StartInfo = startInfo };
            var stdErrBuffer = new StringBuilder();

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) stdErrBuffer.AppendLine(e.Data);
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("无法调用 FFmpeg 进程进行封装。");
            }

            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = stdErrBuffer.ToString();
                Console.WriteLine($"[BilibiliService] FFmpeg 合并失败 (错误码 {process.ExitCode}):\n{error}");
                throw new Exception($"FFmpeg 合并失败 (错误码 {process.ExitCode}): {error}");
            }
            Console.WriteLine("[BilibiliService] FFmpeg 合并完成。");
        }

        private async Task RunFFmpegConvertAudioAsync(string inputPath, string outputPath)
        {
            var ffmpeg = GetFFmpegPath();
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add(outputPath);

            using var process = new Process { StartInfo = startInfo };
            var stdErrBuffer = new StringBuilder();

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) stdErrBuffer.AppendLine(e.Data);
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("无法调用 FFmpeg 进行音频格式转换。");
            }

            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg 音频转换失败: {stdErrBuffer}");
            }
        }

        private async Task DownloadStreamWithFallbackAsync(string primaryUrl, List<string> backups, string outputPath, Action<double> progressCallback)
        {
            var urls = new List<string> { primaryUrl };
            if (backups != null) urls.AddRange(backups);

            Exception? lastEx = null;
            for (int i = 0; i < urls.Count; i++)
            {
                var targetUrl = urls[i];
                bool bypassProxy = i > 0; // Fallback attempts try bypassing proxy in case of domestic CDN routing issue
                try
                {
                    await DownloadSingleStreamAsync(targetUrl, outputPath, bypassProxy, progressCallback);
                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Console.WriteLine($"[BilibiliService] Stream download attempt {i + 1} failed: {ex.Message}");
                }
            }

            throw lastEx ?? new Exception("无法下载 B站 媒体流。");
        }

        private async Task DownloadSingleStreamAsync(string streamUrl, string outputPath, bool bypassProxy, Action<double> progressCallback)
        {
            var cookie = await EnsureVisitorCookieAsync(forceRefresh: false, bypassProxy: bypassProxy);
            using var client = CreateBiliHttpClient(cookie, bypassProxy, timeoutSeconds: 600);

            using var response = await client.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalReadBytes = 0L;
            int readBytes;

            while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, readBytes);
                totalReadBytes += readBytes;

                if (totalBytes > 0)
                {
                    progressCallback((double)totalReadBytes / totalBytes);
                }
            }
        }
    }
}

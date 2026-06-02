using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public class BilibiliService : IBilibiliService
    {
        private readonly IAudioConverter _audioConverter;

        public BilibiliService(IAudioConverter audioConverter)
        {
            _audioConverter = audioConverter;
        }

        private async Task<string> ResolveUrlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL cannot be empty.");
            }

            // Handle b23.tv short link redirection
            if (url.Contains("b23.tv"))
            {
                Console.WriteLine($"[BilibiliService] Resolving short URL: {url}");
                var handler = new HttpClientHandler { AllowAutoRedirect = true };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                
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

        private (string? Bvid, string? Aid) ExtractBvOrAid(string url)
        {
            // Regex for BVID: BV[a-zA-Z0-9]{10}
            var bvMatch = System.Text.RegularExpressions.Regex.Match(url, @"BV[a-zA-Z0-9]{10}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (bvMatch.Success)
            {
                return (bvMatch.Value, null);
            }

            // Regex for AID: av[0-9]+
            var avMatch = System.Text.RegularExpressions.Regex.Match(url, @"av[0-9]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (avMatch.Success)
            {
                return (null, avMatch.Value.Substring(2));
            }

            return (null, null);
        }

        public async Task<OnlineVideoInfo> GetVideoInfoAsync(string videoUrl)
        {
            var resolvedUrl = await ResolveUrlAsync(videoUrl);
            var (bvid, aid) = ExtractBvOrAid(resolvedUrl);

            if (string.IsNullOrEmpty(bvid) && string.IsNullOrEmpty(aid))
            {
                throw new ArgumentException("无法从链接中解析出有效的 B站 BV号 或 av号。");
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");

            string apiUrl = !string.IsNullOrEmpty(bvid) 
                ? $"https://api.bilibili.com/x/web-interface/view?bvid={bvid}"
                : $"https://api.bilibili.com/x/web-interface/view?aid={aid}";

            Console.WriteLine($"[BilibiliService] Fetching metadata from B站 API: {apiUrl}");
            var jsonStr = await client.GetStringAsync(apiUrl);
            
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
            var pic = data.GetProperty("pic").GetString() ?? "";
            var duration = data.GetProperty("duration").GetDouble();
            var owner = data.GetProperty("owner");
            var author = owner.GetProperty("name").GetString() ?? "未知UP主";
            var face = owner.GetProperty("face").GetString() ?? "";

            // B站图片链接通常是 http，如果是，建议使用 https
            if (pic.StartsWith("http://")) pic = "https://" + pic.Substring(7);
            if (face.StartsWith("http://")) face = "https://" + face.Substring(7);

            return new OnlineVideoInfo
            {
                Title = title,
                Author = author,
                DurationSeconds = duration,
                ThumbnailUrl = pic,
                AvatarUrl = face,
                Url = resolvedUrl,
                Platform = "bilibili"
            };
        }

        public async Task<(string AudioPath, string Title, string AvatarUrl)> DownloadAudioAndMetadataAsync(string videoUrl)
        {
            var resolvedUrl = await ResolveUrlAsync(videoUrl);
            var (bvid, aid) = ExtractBvOrAid(resolvedUrl);

            if (string.IsNullOrEmpty(bvid) && string.IsNullOrEmpty(aid))
            {
                throw new ArgumentException("无法从链接中解析出有效的 B站 BV号 或 av号。");
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");

            // 1. Get video details
            string infoUrl = !string.IsNullOrEmpty(bvid) 
                ? $"https://api.bilibili.com/x/web-interface/view?bvid={bvid}"
                : $"https://api.bilibili.com/x/web-interface/view?aid={aid}";

            var infoJson = await client.GetStringAsync(infoUrl);
            using var infoDoc = JsonDocument.Parse(infoJson);
            var infoRoot = infoDoc.RootElement;
            if (infoRoot.GetProperty("code").GetInt32() != 0)
            {
                throw new Exception($"获取视频信息失败: {infoRoot.GetProperty("message").GetString()}");
            }
            var infoData = infoRoot.GetProperty("data");
            var title = infoData.GetProperty("title").GetString() ?? "B站视频";
            var cid = infoData.GetProperty("cid").GetInt64();
            var owner = infoData.GetProperty("owner");
            var avatarUrl = owner.GetProperty("face").GetString() ?? "";
            var actualBvid = infoData.GetProperty("bvid").GetString() ?? bvid ?? "";

            if (avatarUrl.StartsWith("http://")) avatarUrl = "https://" + avatarUrl.Substring(7);

            // 2. Get PlayURL
            var playUrlApi = $"https://api.bilibili.com/x/player/playurl?bvid={actualBvid}&cid={cid}&fnval=16";
            Console.WriteLine($"[BilibiliService] Requesting streams from B站 PlayURL API: {playUrlApi}");
            var playJson = await client.GetStringAsync(playUrlApi);
            
            using var playDoc = JsonDocument.Parse(playJson);
            var playRoot = playDoc.RootElement;
            if (playRoot.GetProperty("code").GetInt32() != 0)
            {
                throw new Exception($"获取流媒体链接失败: {playRoot.GetProperty("message").GetString()}");
            }
            var playData = playRoot.GetProperty("data");

            if (!playData.TryGetProperty("dash", out var dash))
            {
                throw new InvalidOperationException("B站接口未返回 DASH 格式音视频流。");
            }

            var audioArray = dash.GetProperty("audio");
            if (audioArray.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("未找到可用的音频流轨道。");
            }

            // Get first audio stream
            var audioUrl = audioArray[0].GetProperty("baseUrl").GetString();
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
            await DownloadStreamWithProgressAsync(audioUrl, tempAudioPath, p => {});
            Console.WriteLine("[BilibiliService] Audio download complete.");

            return (tempAudioPath, title, avatarUrl);
        }

        public async Task DownloadVideoFileAsync(string videoUrl, string quality, string taskId, IYouTubeDownloadTracker tracker)
        {
            try
            {
                tracker.UpdateProgress(taskId, 5, "正在解析 B站 视频信息与播放链接...");
                var resolvedUrl = await ResolveUrlAsync(videoUrl);
                var (bvid, aid) = ExtractBvOrAid(resolvedUrl);

                if (string.IsNullOrEmpty(bvid) && string.IsNullOrEmpty(aid))
                {
                    throw new ArgumentException("无法从链接中解析出有效的 B站 BV号 或 av号。");
                }

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");

                // 1. Get metadata
                string infoUrl = !string.IsNullOrEmpty(bvid) 
                    ? $"https://api.bilibili.com/x/web-interface/view?bvid={bvid}"
                    : $"https://api.bilibili.com/x/web-interface/view?aid={aid}";

                var infoJson = await client.GetStringAsync(infoUrl);
                using var infoDoc = JsonDocument.Parse(infoJson);
                var infoRoot = infoDoc.RootElement;
                if (infoRoot.GetProperty("code").GetInt32() != 0)
                {
                    throw new Exception($"获取视频信息失败: {infoRoot.GetProperty("message").GetString()}");
                }
                var infoData = infoRoot.GetProperty("data");
                var title = infoData.GetProperty("title").GetString() ?? "B站视频";
                var cid = infoData.GetProperty("cid").GetInt64();
                var actualBvid = infoData.GetProperty("bvid").GetString() ?? bvid ?? "";

                // 2. Get play URLs
                var playUrlApi = $"https://api.bilibili.com/x/player/playurl?bvid={actualBvid}&cid={cid}&fnval=16";
                var playJson = await client.GetStringAsync(playUrlApi);
                using var playDoc = JsonDocument.Parse(playJson);
                var playRoot = playDoc.RootElement;
                if (playRoot.GetProperty("code").GetInt32() != 0)
                {
                    throw new Exception($"获取流媒体链接失败: {playRoot.GetProperty("message").GetString()}");
                }
                var playData = playRoot.GetProperty("data");

                if (!playData.TryGetProperty("dash", out var dash))
                {
                    throw new InvalidOperationException("B站接口未返回 DASH 格式流链接。");
                }

                var videoArray = dash.GetProperty("video");
                var audioArray = dash.GetProperty("audio");

                if (videoArray.GetArrayLength() == 0 || audioArray.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("未找到可用的视频或音频流轨道。");
                }

                // 3. Find video URL based on requested quality
                string? videoStreamUrl = null;
                string streamQualityLabel = "默认清晰度";

                if (quality.ToLower() == "1080p")
                {
                    // Search for quality ID 80
                    foreach (var vItem in videoArray.EnumerateArray())
                    {
                        var id = vItem.GetProperty("id").GetInt32();
                        if (id == 80)
                        {
                            videoStreamUrl = vItem.GetProperty("baseUrl").GetString();
                            streamQualityLabel = "1080P 高清";
                            break;
                        }
                    }
                }
                else if (quality.ToLower() == "720p")
                {
                    // Search for quality ID 64
                    foreach (var vItem in videoArray.EnumerateArray())
                    {
                        var id = vItem.GetProperty("id").GetInt32();
                        if (id == 64)
                        {
                            videoStreamUrl = vItem.GetProperty("baseUrl").GetString();
                            streamQualityLabel = "720P 标清";
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(videoStreamUrl))
                {
                    var firstItem = videoArray[0];
                    videoStreamUrl = firstItem.GetProperty("baseUrl").GetString();
                    var id = firstItem.GetProperty("id").GetInt32();
                    streamQualityLabel = id switch
                    {
                        80 => "1080P 高清",
                        64 => "720P 标清",
                        32 => "480P 清晰",
                        16 => "360P 流畅",
                        _ => "默认画质"
                    };
                }

                var audioStreamUrl = audioArray[0].GetProperty("baseUrl").GetString();

                if (string.IsNullOrEmpty(videoStreamUrl) || string.IsNullOrEmpty(audioStreamUrl))
                {
                    throw new InvalidOperationException("获取到的音视频流链接为空。");
                }

                // 4. Download video (10% -> 70%)
                var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
                var tempVideoFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.mp4");
                var tempAudioFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.m4a");
                var mergedOutputFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.mp4");

                tracker.UpdateProgress(taskId, 10, $"开始下载视频轨 ({streamQualityLabel})...");
                await DownloadStreamWithProgressAsync(videoStreamUrl, tempVideoFile, p =>
                {
                    tracker.UpdateProgress(taskId, 10 + (p * 60), $"正在下载视频轨 ({Math.Round(p * 100)}%)...");
                });

                // 5. Download audio (70% -> 90%)
                tracker.UpdateProgress(taskId, 70, "视频轨下载完成，开始下载音频轨...");
                await DownloadStreamWithProgressAsync(audioStreamUrl, tempAudioFile, p =>
                {
                    tracker.UpdateProgress(taskId, 70 + (p * 20), $"正在下载音频轨 ({Math.Round(p * 100)}%)...");
                });

                // 6. Merge with FFmpeg (90% -> 98%)
                tracker.UpdateProgress(taskId, 90, "音视频轨道下载完毕，正在调用 FFmpeg 进行高速封包合并...");
                await _audioConverter.EnsureFFmpegAsync();
                await RunFFmpegMergeAsync(tempVideoFile, tempAudioFile, mergedOutputFile);

                // Cleanup temp files
                try
                {
                    if (File.Exists(tempVideoFile)) File.Delete(tempVideoFile);
                    if (File.Exists(tempAudioFile)) File.Delete(tempAudioFile);
                }
                catch { }

                tracker.CompleteTask(taskId, mergedOutputFile, title);
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
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = System.Diagnostics.Process.Start(startInfo);
                process?.WaitForExit();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task RunFFmpegMergeAsync(string videoPath, string audioPath, string outputPath)
        {
            var ffmpeg = GetFFmpegPath();
            var arguments = $"-y -i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac \"{outputPath}\"";

            Console.WriteLine($"[BilibiliService] Running FFmpeg Muxer: {ffmpeg} {arguments}");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = arguments,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            var stdErrBuffer = new System.Text.StringBuilder();
            
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdErrBuffer.AppendLine(e.Data);
                }
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

        private async Task DownloadStreamWithProgressAsync(string streamUrl, string outputPath, Action<double> progressCallback)
        {
            var handler = new HttpClientHandler();
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");

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

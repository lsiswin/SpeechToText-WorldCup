using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public class YouTubeService : IYouTubeService
    {
        private readonly IAudioConverter _audioConverter;
        private readonly IConfiguration _configuration;
        private string? _ytdlpPath;
        private bool _isInitialized = false;

        public YouTubeService(IAudioConverter audioConverter, IConfiguration configuration)
        {
            _audioConverter = audioConverter;
            _configuration = configuration;
        }

        public async Task EnsureYtDlpAsync()
        {
            if (_isInitialized && !string.IsNullOrEmpty(_ytdlpPath) && (File.Exists(_ytdlpPath) || _ytdlpPath == "yt-dlp"))
                return;

            // Ensure FFmpeg is available first as yt-dlp depends on it for merging/audio extraction
            await _audioConverter.EnsureFFmpegAsync();

            // 1. Check if yt-dlp is available in system PATH
            if (IsCommandAvailable("yt-dlp"))
            {
                _ytdlpPath = "yt-dlp";
                _isInitialized = true;
                Console.WriteLine("[YouTubeService] yt-dlp is available in system PATH.");
                return;
            }

            // 2. Check local binary in AppContext.BaseDirectory
            var localPath = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
            if (File.Exists(localPath))
            {
                _ytdlpPath = localPath;
                _isInitialized = true;
                Console.WriteLine($"[YouTubeService] yt-dlp found locally at: {_ytdlpPath}");
                return;
            }

            // 3. Check project folder (for local debugging)
            var projectFolderYtDlp = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "yt-dlp.exe"));
            if (File.Exists(projectFolderYtDlp))
            {
                try
                {
                    File.Copy(projectFolderYtDlp, localPath, true);
                    _ytdlpPath = localPath;
                    _isInitialized = true;
                    Console.WriteLine($"[YouTubeService] Copied yt-dlp from project directory to: {_ytdlpPath}");
                    return;
                }
                catch
                {
                    _ytdlpPath = projectFolderYtDlp;
                    _isInitialized = true;
                    return;
                }
            }

            // 4. Download yt-dlp.exe automatically from GitHub
            Console.WriteLine("[YouTubeService] yt-dlp not found. Starting automatic download from GitHub...");
            var downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

            try
            {
                var detectedProxy = DetectLocalProxy();
                HttpClient client;
                if (!string.IsNullOrEmpty(detectedProxy))
                {
                    var handler = new HttpClientHandler { Proxy = new System.Net.WebProxy(detectedProxy) };
                    client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
                }
                else
                {
                    client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                }

                using (client)
                {
                    using var response = await client.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                }

                _ytdlpPath = localPath;
                _isInitialized = true;
                Console.WriteLine($"[YouTubeService] yt-dlp successfully downloaded to: {_ytdlpPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YouTubeService] Failed to auto-download yt-dlp: {ex.Message}");
                throw new Exception("无法获取 yt-dlp 可执行程序。请将 yt-dlp.exe 放置于程序目录中，或检查网络连接以便自动下载。", ex);
            }
        }

        private string? DetectLocalProxy()
        {
            var configProxy = _configuration["YouTube:Proxy"] ?? _configuration["Proxy"];
            if (!string.IsNullOrWhiteSpace(configProxy))
            {
                return configProxy;
            }

            // 1. Check Environment Variables
            var envProxy = Environment.GetEnvironmentVariable("HTTPS_PROXY")
                        ?? Environment.GetEnvironmentVariable("https_proxy")
                        ?? Environment.GetEnvironmentVariable("HTTP_PROXY")
                        ?? Environment.GetEnvironmentVariable("http_proxy");
            if (!string.IsNullOrWhiteSpace(envProxy))
            {
                return envProxy;
            }

            // 2. Check Windows System Proxy (e.g. Clash Verge / v2rayN)
            try
            {
                var testUri = new Uri("https://www.youtube.com");
                var sysProxy = System.Net.WebRequest.GetSystemWebProxy();
                var proxyUri = sysProxy?.GetProxy(testUri);
                if (proxyUri != null && !proxyUri.Equals(testUri))
                {
                    var p = proxyUri.ToString().TrimEnd('/');
                    Console.WriteLine($"[YouTubeService] Detected Windows system proxy: {p}");
                    return p;
                }
            }
            catch { }

            // 3. Probe common local proxy ports
            var commonProxies = new[]
            {
                "http://127.0.0.1:7897",
                "http://127.0.0.1:7890",
                "http://127.0.0.1:10809",
                "http://127.0.0.1:1080",
                "http://127.0.0.1:8080"
            };

            foreach (var proxyUrl in commonProxies)
            {
                try
                {
                    var uri = new Uri(proxyUrl);
                    using var tcpClient = new TcpClient();
                    var connectResult = tcpClient.BeginConnect(uri.Host, uri.Port, null, null);
                    var success = connectResult.AsyncWaitHandle.WaitOne(150);
                    if (success)
                    {
                        tcpClient.EndConnect(connectResult);
                        Console.WriteLine($"[YouTubeService] Detected active local proxy at {proxyUrl}.");
                        return proxyUrl;
                    }
                }
                catch { }
            }

            return null;
        }

        private string GetFFmpegDir()
        {
            var localFfmpeg = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(localFfmpeg))
            {
                // CRITICAL: Always trim trailing directory separators to avoid breaking Windows command line argument quotes!
                return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return "";
        }

        private string? FindCookiesFile()
        {
            var configPath = _configuration["YouTube:CookiesFile"];
            if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
            {
                return configPath;
            }

            var baseDirCookie = Path.Combine(AppContext.BaseDirectory, "cookies.txt");
            if (File.Exists(baseDirCookie)) return baseDirCookie;

            var projectCookie = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "cookies.txt"));
            if (File.Exists(projectCookie)) return projectCookie;

            var uploadsCookie = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads", "cookies.txt");
            if (File.Exists(uploadsCookie)) return uploadsCookie;

            return null;
        }

        private void AppendCommonArgs(ProcessStartInfo startInfo)
        {
            var proxy = DetectLocalProxy();
            if (!string.IsNullOrEmpty(proxy))
            {
                startInfo.ArgumentList.Add("--proxy");
                startInfo.ArgumentList.Add(proxy);
            }

            var ffmpegDir = GetFFmpegDir();
            if (!string.IsNullOrEmpty(ffmpegDir))
            {
                startInfo.ArgumentList.Add("--ffmpeg-location");
                startInfo.ArgumentList.Add(ffmpegDir);
                startInfo.WorkingDirectory = ffmpegDir;
            }

            // Enable Node.js JS runtime for YouTube EJS signature and n-challenge deciphering
            if (IsCommandAvailable("node"))
            {
                startInfo.ArgumentList.Add("--js-runtimes");
                startInfo.ArgumentList.Add("node");
            }

            // Automatic cookies support
            var cookiesPath = FindCookiesFile();
            if (!string.IsNullOrEmpty(cookiesPath))
            {
                startInfo.ArgumentList.Add("--cookies");
                startInfo.ArgumentList.Add(cookiesPath);
                Console.WriteLine($"[YouTubeService] Using cookies file: {cookiesPath}");
            }
            else
            {
                var browserName = _configuration["YouTube:CookiesFromBrowser"];
                if (!string.IsNullOrWhiteSpace(browserName))
                {
                    startInfo.ArgumentList.Add("--cookies-from-browser");
                    startInfo.ArgumentList.Add(browserName);
                    Console.WriteLine($"[YouTubeService] Using cookies from browser: {browserName}");
                }
            }
        }

        public async Task<byte[]?> DownloadImageViaYtDlpAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            await EnsureYtDlpAsync();
            var proxy = DetectLocalProxy();
            var tempFile = Path.Combine(Path.GetTempPath(), $"thumb_{Guid.NewGuid():N}.jpg");

            var startInfo = new ProcessStartInfo
            {
                FileName = _ytdlpPath!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrWhiteSpace(proxy))
            {
                startInfo.ArgumentList.Add("--proxy");
                startInfo.ArgumentList.Add(proxy);
            }
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(tempFile);
            startInfo.ArgumentList.Add(imageUrl);

            try
            {
                var (_, _, exitCode) = await ExecuteProcessAsync(startInfo);
                if (exitCode == 0 && File.Exists(tempFile))
                {
                    var bytes = await File.ReadAllBytesAsync(tempFile);
                    try { File.Delete(tempFile); } catch { }
                    if (bytes.Length > 0)
                    {
                        return bytes;
                    }
                }
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YouTubeService] DownloadImageViaYtDlpAsync failed for {imageUrl}: {ex.Message}");
            }
            return null;
        }

        public async Task<YouTubeVideoInfo> GetVideoInfoAsync(string videoUrl)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new ArgumentException("YouTube URL cannot be empty.", nameof(videoUrl));
            }

            await EnsureYtDlpAsync();

            var startInfo = new ProcessStartInfo
            {
                FileName = _ytdlpPath!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("--dump-single-json");
            startInfo.ArgumentList.Add("--no-playlist");
            startInfo.ArgumentList.Add("--skip-download");
            startInfo.ArgumentList.Add("--no-warnings");
            AppendCommonArgs(startInfo);
            startInfo.ArgumentList.Add(videoUrl.Trim());

            Console.WriteLine($"[YouTubeService] Fetching metadata via yt-dlp for {videoUrl}...");
            var (stdout, stderr, exitCode) = await ExecuteProcessAsync(startInfo);

            if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                // Fallback attempt: retry using Android client exclusively if bot detection triggered
                if (stderr.Contains("Sign in to confirm you're not a bot"))
                {
                    Console.WriteLine("[YouTubeService] Bot challenge detected, retrying with android client configuration...");
                    var fallbackInfo = new ProcessStartInfo
                    {
                        FileName = _ytdlpPath!,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    fallbackInfo.ArgumentList.Add("--dump-single-json");
                    fallbackInfo.ArgumentList.Add("--no-playlist");
                    fallbackInfo.ArgumentList.Add("--skip-download");
                    fallbackInfo.ArgumentList.Add("--no-warnings");
                    var proxy = DetectLocalProxy();
                    if (!string.IsNullOrEmpty(proxy))
                    {
                        fallbackInfo.ArgumentList.Add("--proxy");
                        fallbackInfo.ArgumentList.Add(proxy);
                    }
                    fallbackInfo.ArgumentList.Add("--extractor-args");
                    fallbackInfo.ArgumentList.Add("youtube:player_client=android");
                    fallbackInfo.ArgumentList.Add(videoUrl.Trim());

                    var (fbStdout, fbStderr, fbCode) = await ExecuteProcessAsync(fallbackInfo);
                    if (fbCode == 0 && !string.IsNullOrWhiteSpace(fbStdout))
                    {
                        stdout = fbStdout;
                    }
                    else
                    {
                        throw new Exception($"yt-dlp 获取视频信息失败 (退出码 {exitCode}): {stderr}");
                    }
                }
                else
                {
                    throw new Exception($"yt-dlp 获取视频信息失败 (退出码 {exitCode}): {stderr}");
                }
            }

            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "YouTube Video" : "YouTube Video";

            string author = "YouTube Creator";
            if (root.TryGetProperty("channel", out var chanProp) && !string.IsNullOrWhiteSpace(chanProp.GetString()))
                author = chanProp.GetString()!;
            else if (root.TryGetProperty("uploader", out var upProp) && !string.IsNullOrWhiteSpace(upProp.GetString()))
                author = upProp.GetString()!;

            double duration = 0;
            if (root.TryGetProperty("duration", out var durProp))
            {
                durProp.TryGetDouble(out duration);
            }

            string thumbnail = "";
            if (root.TryGetProperty("thumbnail", out var thumbProp))
            {
                thumbnail = thumbProp.GetString() ?? "";
            }

            string avatarUrl = "";
            if (root.TryGetProperty("thumbnails", out var thumbsArr) && thumbsArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in thumbsArr.EnumerateArray())
                {
                    if (t.TryGetProperty("id", out var idProp) && idProp.GetString()?.Contains("avatar") == true)
                    {
                        avatarUrl = t.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(thumbnail))
            {
                // Asynchronously pre-cache the thumbnail into local disk cache
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var cacheFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads", "ImageCache");
                        if (!Directory.Exists(cacheFolder)) Directory.CreateDirectory(cacheFolder);

                        var urlHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(thumbnail)));
                        var cachedMeta = Path.Combine(cacheFolder, $"{urlHash}.meta");
                        var cachedData = Path.Combine(cacheFolder, $"{urlHash}.bin");

                        if (!File.Exists(cachedData))
                        {
                            var bytes = await DownloadImageViaYtDlpAsync(thumbnail);
                            if (bytes != null && bytes.Length > 0)
                            {
                                await File.WriteAllBytesAsync(cachedData, bytes);
                                var ct = thumbnail.Contains(".png") ? "image/png" : thumbnail.Contains(".webp") ? "image/webp" : "image/jpeg";
                                await File.WriteAllTextAsync(cachedMeta, ct);
                                Console.WriteLine($"[YouTubeService] Pre-cached thumbnail successfully ({bytes.Length} bytes).");
                            }
                        }
                    }
                    catch { }
                });
            }

            return new YouTubeVideoInfo
            {
                Title = title,
                Author = author,
                DurationSeconds = duration,
                ThumbnailUrl = thumbnail,
                AvatarUrl = avatarUrl,
                Url = videoUrl.Trim()
            };
        }

        public async Task<(string AudioPath, string Title, string AvatarUrl)> DownloadAudioAndMetadataAsync(string videoUrl)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new ArgumentException("YouTube URL cannot be empty.", nameof(videoUrl));
            }

            await EnsureYtDlpAsync();

            var info = await GetVideoInfoAsync(videoUrl);

            var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileId = Guid.NewGuid().ToString();
            var outputTemplate = Path.Combine(uploadsFolder, $"{fileId}.%(ext)s");

            var startInfo = new ProcessStartInfo
            {
                FileName = _ytdlpPath!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("ba/b");
            startInfo.ArgumentList.Add("-x");
            startInfo.ArgumentList.Add("--audio-format");
            startInfo.ArgumentList.Add("m4a");
            startInfo.ArgumentList.Add("--no-playlist");
            startInfo.ArgumentList.Add("--no-warnings");
            AppendCommonArgs(startInfo);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputTemplate);
            startInfo.ArgumentList.Add(videoUrl);

            Console.WriteLine($"[YouTubeService] Downloading audio via yt-dlp for {videoUrl}...");
            var (stdout, stderr, exitCode) = await ExecuteProcessAsync(startInfo);

            if (exitCode != 0)
            {
                throw new Exception($"yt-dlp 音频下载失败 (退出码 {exitCode}): {stderr}");
            }

            // Find generated audio file
            var directFile = Path.Combine(uploadsFolder, $"{fileId}.m4a");
            if (File.Exists(directFile))
            {
                return (directFile, info.Title, info.AvatarUrl);
            }

            var matchingFiles = Directory.GetFiles(uploadsFolder, $"{fileId}.*");
            if (matchingFiles.Length > 0)
            {
                return (matchingFiles[0], info.Title, info.AvatarUrl);
            }

            throw new FileNotFoundException("未能找到由 yt-dlp 生成的音频提取文件。");
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
                tracker.UpdateProgress(taskId, 5, "正在通过 yt-dlp 解析媒体流与元数据...");
                await EnsureYtDlpAsync();

                var info = await GetVideoInfoAsync(videoUrl);
                var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileId = Guid.NewGuid().ToString();
                var startInfo = new ProcessStartInfo
                {
                    FileName = _ytdlpPath!,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                string targetExt;

                if (isAudio)
                {
                    targetExt = audioFormat.ToLower();
                    if (targetExt != "mp3" && targetExt != "m4a" && targetExt != "wav" && targetExt != "flac")
                    {
                        targetExt = "mp3";
                    }
                    tracker.UpdateProgress(taskId, 10, $"已解析: 《{info.Title}》，准备抓取最佳音轨并转码为 {targetExt.ToUpper()}...");
                    var outputTemplate = Path.Combine(uploadsFolder, $"{fileId}.%(ext)s");

                    startInfo.ArgumentList.Add("-f");
                    startInfo.ArgumentList.Add("ba/b");
                    startInfo.ArgumentList.Add("-x");
                    startInfo.ArgumentList.Add("--audio-format");
                    startInfo.ArgumentList.Add(targetExt);
                    startInfo.ArgumentList.Add("--audio-quality");
                    startInfo.ArgumentList.Add("0");
                    startInfo.ArgumentList.Add("--no-playlist");
                    startInfo.ArgumentList.Add("--newline");
                    startInfo.ArgumentList.Add("--no-warnings");
                    AppendCommonArgs(startInfo);
                    startInfo.ArgumentList.Add("-o");
                    startInfo.ArgumentList.Add(outputTemplate);
                    startInfo.ArgumentList.Add(videoUrl);
                }
                else
                {
                    targetExt = videoFormat.ToLower();
                    if (targetExt != "mp4" && targetExt != "mkv")
                    {
                        targetExt = "mp4";
                    }

                    string format;
                    if (quality.Equals("best", StringComparison.OrdinalIgnoreCase))
                    {
                        format = "bv*+ba/b";
                        tracker.UpdateProgress(taskId, 10, $"已解析: 《{info.Title}》，准备下载最高原画视频轨...");
                    }
                    else if (quality.Equals("1080p", StringComparison.OrdinalIgnoreCase))
                    {
                        format = "bv*[height<=1080]+ba/b[height<=1080]/bv*[height<=1280]+ba/bv*+ba/b";
                        tracker.UpdateProgress(taskId, 10, $"已解析: 《{info.Title}》，准备下载 1080P 全高清视频轨...");
                    }
                    else if (quality.Equals("720p", StringComparison.OrdinalIgnoreCase))
                    {
                        format = "bv*[height<=720]+ba/b[height<=720]/bv*[height<=1280]+ba/bv*+ba/b";
                        tracker.UpdateProgress(taskId, 10, $"已解析: 《{info.Title}》，准备下载 720P 高清视频轨...");
                    }
                    else
                    {
                        format = "bv*[height<=480]+ba/b[height<=480]/bv*+ba/b";
                        tracker.UpdateProgress(taskId, 10, $"已解析: 《{info.Title}》，准备下载 480P 流畅视频轨...");
                    }

                    var outputTemplate = Path.Combine(uploadsFolder, $"{fileId}.%(ext)s");

                    startInfo.ArgumentList.Add("-f");
                    startInfo.ArgumentList.Add(format);
                    startInfo.ArgumentList.Add("--merge-output-format");
                    startInfo.ArgumentList.Add(targetExt);
                    startInfo.ArgumentList.Add("--no-playlist");
                    startInfo.ArgumentList.Add("--newline");
                    startInfo.ArgumentList.Add("--no-warnings");
                    AppendCommonArgs(startInfo);
                    startInfo.ArgumentList.Add("-o");
                    startInfo.ArgumentList.Add(outputTemplate);
                    startInfo.ArgumentList.Add(videoUrl.Trim());
                }

                Console.WriteLine($"[YouTubeService] Starting yt-dlp download task: {startInfo.FileName} for {videoUrl.Trim()}");

                using var process = new Process { StartInfo = startInfo };
                var stdErrBuffer = new StringBuilder();
                int trackIndex = 0;
                double maxReportedProgress = 10;

                process.OutputDataReceived += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(e.Data)) return;

                    var line = e.Data;
                    if (line.Contains("[download] Destination:"))
                    {
                        trackIndex++;
                        return;
                    }

                    // Format: [download]  45.2% of  120.50MiB at  4.20MiB/s ETA 00:15
                    var dlMatch = Regex.Match(line, @"\[download\]\s+([0-9\.]+)%(?:\s+of\s+([~0-9\.]+[A-Za-z]+))?(?:\s+at\s+([0-9\.]+[A-Za-z/]+))?(?:\s+ETA\s+(\S+))?");
                    if (dlMatch.Success && double.TryParse(dlMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var percent))
                    {
                        var speed = dlMatch.Groups[3].Success ? $" (速度: {dlMatch.Groups[3].Value})" : "";
                        var eta = dlMatch.Groups[4].Success ? $" 预计剩余: {dlMatch.Groups[4].Value}" : "";

                        double mapped;
                        if (isAudio)
                        {
                            mapped = 10 + (percent * 0.80); // 10% -> 90%
                        }
                        else if (trackIndex <= 1)
                        {
                            mapped = 10 + (percent * 0.65); // Track 1 (video): 10% -> 75%
                        }
                        else
                        {
                            mapped = 75 + (percent * 0.17); // Track 2 (audio): 75% -> 92%
                        }

                        if (mapped > maxReportedProgress)
                        {
                            maxReportedProgress = mapped;
                        }

                        var typeLabel = isAudio ? "音频" : (trackIndex <= 1 ? "视频画轨" : "伴随音轨");
                        tracker.UpdateProgress(taskId, maxReportedProgress, $"正在高速抓取{typeLabel} ({percent:0.0}%){speed}{eta}...");
                    }
                    else if (line.Contains("[Merger]") || line.Contains("Merging formats"))
                    {
                        maxReportedProgress = 95;
                        tracker.UpdateProgress(taskId, 95, $"音视频轨道抓取完毕，正在调用 FFmpeg 封装为 {targetExt.ToUpper()} 容器...");
                    }
                    else if (line.Contains("[ExtractAudio]"))
                    {
                        maxReportedProgress = 94;
                        tracker.UpdateProgress(taskId, 94, $"正在调用 FFmpeg 转码为高品质 {targetExt.ToUpper()} 音频...");
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        stdErrBuffer.AppendLine(e.Data);
                    }
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException("无法启动 yt-dlp 执行进程。");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = stdErrBuffer.ToString();
                    if (error.Contains("Sign in to confirm you're not a bot"))
                    {
                        throw new Exception("YouTube 触发了防机器人验证。建议在程序根目录放置 cookies.txt，或配置本地浏览器 Cookie (--cookies-from-browser edge) 后重试。详细信息: " + error);
                    }
                    throw new Exception($"yt-dlp 媒体下载合并失败 (退出码 {process.ExitCode}): {error}");
                }

                // Verify resulting file
                string actualOutputFile = Path.Combine(uploadsFolder, $"{fileId}.{targetExt}");
                if (!File.Exists(actualOutputFile))
                {
                    var matches = Directory.GetFiles(uploadsFolder, $"{fileId}.{targetExt}");
                    if (matches.Length > 0)
                    {
                        actualOutputFile = matches[0];
                    }
                    else
                    {
                        var anyMatches = Directory.GetFiles(uploadsFolder, $"{fileId}.*");
                        if (anyMatches.Length > 0)
                        {
                            actualOutputFile = anyMatches[0];
                        }
                        else
                        {
                            throw new FileNotFoundException("yt-dlp 执行完成，但未在输出目录找到文件。");
                        }
                    }
                }

                tracker.CompleteTask(taskId, actualOutputFile, info.Title);
                Console.WriteLine($"[YouTubeService] Download completed successfully for task {taskId}. Result: {actualOutputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YouTubeService] Download failed for task {taskId}: {ex.Message}");
                tracker.FailTask(taskId, ex.Message);
            }
        }

        private async Task<(string Stdout, string Stderr, int ExitCode)> ExecuteProcessAsync(ProcessStartInfo startInfo)
        {
            using var process = new Process { StartInfo = startInfo };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) stdout.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) stderr.AppendLine(e.Data);
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"无法启动进程: {startInfo.FileName}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return (stdout.ToString(), stderr.ToString(), process.ExitCode);
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
                startInfo.ArgumentList.Add("--version");
                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}

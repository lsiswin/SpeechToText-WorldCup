using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpeechToText.Api.Models;
using SpeechToText.Api.Services;

namespace SpeechToText.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranscriptionController : ControllerBase
    {
        private readonly IAudioConverter _audioConverter;
        private readonly ITranscriptionService _transcriptionService;
        private readonly IDocumentExporter _documentExporter;
        private readonly IYouTubeService _youtubeService;
        private readonly IBilibiliService _bilibiliService;
        private readonly IYouTubeDownloadTracker _downloadTracker;
        private readonly IVideoBurnService _videoBurnService;
        private readonly IConfiguration _configuration;

        public TranscriptionController(
            IAudioConverter audioConverter,
            ITranscriptionService transcriptionService,
            IDocumentExporter documentExporter,
            IYouTubeService youtubeService,
            IBilibiliService bilibiliService,
            IYouTubeDownloadTracker downloadTracker,
            IVideoBurnService videoBurnService,
            IConfiguration configuration)
        {
            _audioConverter = audioConverter;
            _transcriptionService = transcriptionService;
            _documentExporter = documentExporter;
            _youtubeService = youtubeService;
            _bilibiliService = bilibiliService;
            _downloadTracker = downloadTracker;
            _videoBurnService = videoBurnService;
            _configuration = configuration;
        }

        [HttpPost("transcribe")]
        [Consumes("multipart/form-data")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Transcribe([FromForm] TranscribeUploadRequest request)
        {
            var file = request?.File;
            var modelType = request?.ModelType ?? "base";
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }

            var allowedModelTypes = new[] { "tiny", "base", "small", "medium", "large" };
            modelType = modelType.ToLower();
            if (Array.IndexOf(allowedModelTypes, modelType) == -1)
            {
                modelType = "base";
            }

            var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var tempInputFile = Path.Combine(uploadsFolder, Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));
            var tempWavFile = Path.Combine(uploadsFolder, Guid.NewGuid().ToString() + ".wav");

            try
            {
                // 1. Save uploaded file to temp path
                using (var stream = new FileStream(tempInputFile, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 2. Convert to standard 16kHz PCM mono WAV
                Console.WriteLine($"Converting {file.FileName} to Whisper format...");
                await _audioConverter.ConvertToWavAsync(tempInputFile, tempWavFile);

                // 3. Transcribe WAV file
                Console.WriteLine("Starting transcription...");
                var response = await _transcriptionService.TranscribeAsync(tempWavFile, modelType);
                response.FileName = file.FileName;

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during transcription process: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred during processing: {ex.Message}");
            }
            finally
            {
                // Cleanup temp files
                try
                {
                    if (System.IO.File.Exists(tempInputFile)) System.IO.File.Delete(tempInputFile);
                    if (System.IO.File.Exists(tempWavFile)) System.IO.File.Delete(tempWavFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete temporary files: {ex.Message}");
                }
            }
        }

        private bool IsBilibiliUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            var lowerUrl = url.ToLower();
            return lowerUrl.Contains("bilibili.com") || lowerUrl.Contains("b23.tv");
        }

        [HttpGet("online/info")]
        public async Task<IActionResult> GetOnlineVideoInfo([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return BadRequest("URL is required.");
            }

            try
            {
                if (IsBilibiliUrl(url))
                {
                    var info = await _bilibiliService.GetVideoInfoAsync(url);
                    return Ok(info);
                }
                else
                {
                    var info = await _youtubeService.GetVideoInfoAsync(url);
                    var onlineInfo = new OnlineVideoInfo
                    {
                        Title = info.Title,
                        Author = info.Author,
                        DurationSeconds = info.DurationSeconds,
                        ThumbnailUrl = info.ThumbnailUrl,
                        AvatarUrl = info.AvatarUrl,
                        Url = info.Url,
                        Platform = "youtube"
                    };
                    return Ok(onlineInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching video info: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to parse video info: {ex.Message}");
            }
        }

        private static string? DetectProxyUrl(IConfiguration? configuration)
        {
            var configProxy = configuration?["YouTube:Proxy"] ?? configuration?["Proxy"];
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
                    return proxyUri.ToString().TrimEnd('/');
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
                    using var tcpClient = new System.Net.Sockets.TcpClient();
                    var connectResult = tcpClient.BeginConnect(uri.Host, uri.Port, null, null);
                    var success = connectResult.AsyncWaitHandle.WaitOne(120);
                    if (success)
                    {
                        tcpClient.EndConnect(connectResult);
                        return proxyUrl;
                    }
                }
                catch { }
            }

            return null;
        }

        private static System.Net.IWebProxy? GetProxy(IConfiguration? configuration)
        {
            var proxyUrl = DetectProxyUrl(configuration);
            return !string.IsNullOrWhiteSpace(proxyUrl) ? new System.Net.WebProxy(proxyUrl) : null;
        }

        private async Task<(byte[] Bytes, string ContentType)> FetchImageBytesAsync(string url)
        {
            if (url.StartsWith("//"))
            {
                url = "https:" + url;
            }

            var cacheFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads", "ImageCache");
            if (!Directory.Exists(cacheFolder))
            {
                Directory.CreateDirectory(cacheFolder);
            }

            var urlHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url)));
            var cachedMeta = Path.Combine(cacheFolder, $"{urlHash}.meta");
            var cachedData = Path.Combine(cacheFolder, $"{urlHash}.bin");

            if (System.IO.File.Exists(cachedData) && System.IO.File.Exists(cachedMeta))
            {
                try
                {
                    var cachedType = await System.IO.File.ReadAllTextAsync(cachedMeta);
                    var cachedBytes = await System.IO.File.ReadAllBytesAsync(cachedData);
                    if (cachedBytes.Length > 0)
                    {
                        return (cachedBytes, cachedType);
                    }
                }
                catch { }
            }

            var proxyUrl = DetectProxyUrl(_configuration);
            var proxy = !string.IsNullOrWhiteSpace(proxyUrl) ? new System.Net.WebProxy(proxyUrl) : null;
            var isYoutubeDomain = url.Contains("ytimg.com") || url.Contains("ggpht.com") || url.Contains("youtube.com") || url.Contains("googlevideo.com");

            // Build resilient list of candidate image URLs (full URL -> stripped query string -> YouTube hqdefault)
            var candidates = new System.Collections.Generic.List<string> { url };
            if (url.Contains("i.ytimg.com") && url.Contains('?'))
            {
                var cleanUrl = url.Split('?')[0];
                if (!candidates.Contains(cleanUrl)) candidates.Add(cleanUrl);
            }
            var ytMatch = System.Text.RegularExpressions.Regex.Match(url, @"ytimg\.com/vi(?:_webp)?/([A-Za-z0-9_-]{6,})/");
            if (ytMatch.Success)
            {
                var fallbackUrl = $"https://i.ytimg.com/vi/{ytMatch.Groups[1].Value}/hqdefault.jpg";
                if (!candidates.Contains(fallbackUrl)) candidates.Add(fallbackUrl);
            }

            Exception? lastEx = null;

            for (int attempt = 1; attempt <= 4; attempt++)
            {
                var targetUrl = candidates[(attempt - 1) % candidates.Count];
                try
                {
                    using var handler = new SocketsHttpHandler
                    {
                        PooledConnectionLifetime = TimeSpan.Zero, // Always open a fresh connection on retry
                        ConnectTimeout = TimeSpan.FromSeconds(4),
                        AllowAutoRedirect = true,
                        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                        {
                            RemoteCertificateValidationCallback = (_, _, _, _) => true
                        }
                    };

                    if (proxy != null && isYoutubeDomain)
                    {
                        handler.Proxy = proxy;
                        handler.UseProxy = true;
                    }

                    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

                    if (targetUrl.Contains("bilivideo.com") || targetUrl.Contains("hdslb.com") || targetUrl.Contains("bilibili.com"))
                    {
                        client.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");
                    }
                    else if (isYoutubeDomain)
                    {
                        client.DefaultRequestHeaders.Referrer = new Uri("https://www.youtube.com");
                    }

                    using var requestMsg = new HttpRequestMessage(HttpMethod.Get, targetUrl)
                    {
                        Version = System.Net.HttpVersion.Version11
                    };
                    var response = await client.SendAsync(requestMsg);
                    response.EnsureSuccessStatusCode();

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    if (bytes.Length == 0) throw new Exception("Empty image response.");

                    try
                    {
                        await System.IO.File.WriteAllBytesAsync(cachedData, bytes);
                        await System.IO.File.WriteAllTextAsync(cachedMeta, contentType);
                    }
                    catch { }

                    return (bytes, contentType);
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Console.WriteLine($"[TranscriptionController] Image fetch attempt {attempt} ({targetUrl}) failed: {ex.Message} | Inner: {ex.InnerException?.Message}");
                    await Task.Delay(150);
                }
            }

            // Last-resort fallback via curl.exe if installed (uses native Windows SChannel with distinct TLS profile)
            foreach (var targetUrl in candidates)
            {
                try
                {
                    var tmpImg = Path.Combine(cacheFolder, $"{Guid.NewGuid()}.tmp");
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "curl.exe",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    psi.ArgumentList.Add("-sL");
                    psi.ArgumentList.Add("-m");
                    psi.ArgumentList.Add("6");
                    psi.ArgumentList.Add("-k");
                    if (!string.IsNullOrWhiteSpace(proxyUrl) && isYoutubeDomain)
                    {
                        psi.ArgumentList.Add("--proxy");
                        psi.ArgumentList.Add(proxyUrl);
                    }
                    psi.ArgumentList.Add("-o");
                    psi.ArgumentList.Add(tmpImg);
                    psi.ArgumentList.Add(targetUrl);

                    using var proc = System.Diagnostics.Process.Start(psi);
                    if (proc != null)
                    {
                        await proc.WaitForExitAsync();
                        if (proc.ExitCode == 0 && System.IO.File.Exists(tmpImg))
                        {
                            var bytes = await System.IO.File.ReadAllBytesAsync(tmpImg);
                            try { System.IO.File.Delete(tmpImg); } catch { }
                            if (bytes.Length > 500)
                            {
                                var ct = targetUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
                                       : targetUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
                                       : "image/jpeg";
                                try
                                {
                                    await System.IO.File.WriteAllBytesAsync(cachedData, bytes);
                                    await System.IO.File.WriteAllTextAsync(cachedMeta, ct);
                                }
                                catch { }
                                return (bytes, ct);
                            }
                        }
                        try { if (System.IO.File.Exists(tmpImg)) System.IO.File.Delete(tmpImg); } catch { }
                    }
                }
                catch { }
            }

            throw lastEx ?? new Exception("Failed to fetch image bytes.");
        }

        [HttpGet("online/image-proxy")]
        public async Task<IActionResult> ProxyImage([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return BadRequest("URL is required.");
            }

            try
            {
                var (bytes, contentType) = await FetchImageBytesAsync(url);
                Response.Headers["Cache-Control"] = "public, max-age=86400";
                return base.File(bytes, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TranscriptionController] Image proxy failed for {url}: {ex.Message}");
                // Return clean vector SVG fallback so the UI never displays broken image icon
                var isAvatar = url.Contains("avatar") || url.Contains("face");
                var fallbackSvg = isAvatar
                    ? "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><circle cx='50' cy='50' r='50' fill='#334155'/><circle cx='50' cy='38' r='18' fill='#94a3b8'/><path d='M20 85 A32 32 0 0 1 80 85 Z' fill='#94a3b8'/></svg>"
                    : "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 400 225'><rect width='400' height='225' fill='#1e293b'/><circle cx='200' cy='112.5' r='36' fill='#334155'/><polygon points='190,95 220,112.5 190,130' fill='#f8fafc'/></svg>";
                return base.File(System.Text.Encoding.UTF8.GetBytes(fallbackSvg), "image/svg+xml");
            }
        }

        [HttpGet("online/download-cover")]
        public async Task<IActionResult> DownloadCover([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return BadRequest("Cover URL is required.");
            }

            try
            {
                var (bytes, contentType) = await FetchImageBytesAsync(url);
                var extension = contentType == "image/png" ? "png" : contentType == "image/webp" ? "webp" : "jpg";
                var filename = $"cover.{extension}";
                return base.File(bytes, contentType, filename);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading cover: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to download cover: {ex.Message}");
            }
        }

        [HttpPost("online/download/start")]
        public IActionResult StartOnlineVideoDownload([FromBody] OnlineVideoDownloadRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest("URL is required.");
            }

            var taskId = _downloadTracker.CreateTask();
            
            // Start background thread to download video/audio asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    if (IsBilibiliUrl(request.Url))
                    {
                        await _bilibiliService.DownloadVideoFileAsync(request.Url, request.Quality, taskId, _downloadTracker, request.DownloadType, request.AudioFormat, request.VideoFormat);
                    }
                    else
                    {
                        await _youtubeService.DownloadVideoFileAsync(request.Url, request.Quality, taskId, _downloadTracker, request.DownloadType, request.AudioFormat, request.VideoFormat);
                    }
                }
                catch (Exception ex)
                {
                    _downloadTracker.FailTask(taskId, ex.Message);
                }
            });

            return Ok(new { taskId });
        }

        [HttpPost("online/transcribe")]
        public async Task<IActionResult> TranscribeOnlineVideo([FromBody] OnlineVideoRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest("URL is required.");
            }

            var allowedModelTypes = new[] { "tiny", "base", "small", "medium", "large" };
            var modelType = request.ModelType.ToLower();
            if (Array.IndexOf(allowedModelTypes, modelType) == -1)
            {
                modelType = "base";
            }

            string tempAudioFile = string.Empty;
            var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
            var tempWavFile = Path.Combine(uploadsFolder, Guid.NewGuid().ToString() + ".wav");

            try
            {
                string title;
                string avatarUrl;

                if (IsBilibiliUrl(request.Url))
                {
                    var (audioPath, bTitle, bAvatar) = await _bilibiliService.DownloadAudioAndMetadataAsync(request.Url);
                    tempAudioFile = audioPath;
                    title = bTitle;
                    avatarUrl = bAvatar;
                }
                else
                {
                    var (audioPath, yTitle, yAvatar) = await _youtubeService.DownloadAudioAndMetadataAsync(request.Url);
                    tempAudioFile = audioPath;
                    title = yTitle;
                    avatarUrl = yAvatar;
                }

                Console.WriteLine($"Converting downloaded online audio to WAV: {tempWavFile}");
                await _audioConverter.ConvertToWavAsync(tempAudioFile, tempWavFile);

                Console.WriteLine("Starting transcription of online audio...");
                var response = await _transcriptionService.TranscribeAsync(tempWavFile, modelType);
                
                response.FileName = Path.GetFileName(tempAudioFile);
                response.Title = title;
                response.AvatarUrl = avatarUrl;

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during online transcription: {ex.Message}");
                try
                {
                    if (System.IO.File.Exists(tempAudioFile)) System.IO.File.Delete(tempAudioFile);
                }
                catch { }
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to process online video: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(tempWavFile)) System.IO.File.Delete(tempWavFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete temporary WAV file: {ex.Message}");
                }
            }
        }

        [HttpGet("youtube/info")]
        public async Task<IActionResult> GetYouTubeInfo([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return BadRequest("YouTube URL is required.");
            }

            try
            {
                var info = await _youtubeService.GetVideoInfoAsync(url);
                return Ok(info);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching YouTube video info: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to parse video info: {ex.Message}");
            }
        }

        [HttpPost("youtube/download/start")]
        public IActionResult StartYouTubeDownload([FromBody] YouTubeDownloadRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest("YouTube URL is required.");
            }

            var taskId = _downloadTracker.CreateTask();
            
            // Start background thread to download video asynchronously
            _ = Task.Run(async () =>
            {
                await _youtubeService.DownloadVideoFileAsync(request.Url, request.Quality, taskId, _downloadTracker);
            });

            return Ok(new { taskId });
        }

        [HttpGet("youtube/download/status")]
        public IActionResult GetYouTubeDownloadStatus([FromQuery] string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return BadRequest("taskId is required.");
            }

            var status = _downloadTracker.GetStatus(taskId);
            if (status == null)
            {
                return NotFound("Task not found.");
            }

            return Ok(new
            {
                taskId = status.TaskId,
                progress = status.Progress,
                statusText = status.StatusText,
                isComplete = status.IsComplete,
                error = status.Error
            });
        }

        [HttpGet("youtube/download/file")]
        public IActionResult GetYouTubeDownloadedFile([FromQuery] string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return BadRequest("taskId is required.");
            }

            var status = _downloadTracker.GetStatus(taskId);
            if (status == null || !status.IsComplete || string.IsNullOrEmpty(status.FilePath))
            {
                return BadRequest("Task not found or not completed yet.");
            }

            var filePath = status.FilePath;
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Video file not found on disk.");
            }

            var safeTitle = string.Join("_", status.Title!.Split(Path.GetInvalidFileNameChars()));
            var ext = Path.GetExtension(filePath).ToLower();
            if (string.IsNullOrEmpty(ext)) ext = ".mp4";
            var filename = $"{safeTitle}{ext}";

            var contentType = ext switch
            {
                ".mp3" => "audio/mpeg",
                ".m4a" => "audio/mp4",
                ".wav" => "audio/wav",
                ".flac" => "audio/flac",
                ".ogg" => "audio/ogg",
                ".mkv" => "video/x-matroska",
                ".webm" => "video/webm",
                _ => "video/mp4"
            };

            // Remove task from cache tracker
            _downloadTracker.RemoveTask(taskId);

            // Open stream with DeleteOnClose to ensure automated filesystem cleanup upon completion
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            return base.File(stream, contentType, filename);
        }

        [HttpPost("youtube")]
        public async Task<IActionResult> TranscribeYouTube([FromBody] YouTubeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest("YouTube URL is required.");
            }

            var allowedModelTypes = new[] { "tiny", "base", "small", "medium", "large" };
            var modelType = request.ModelType.ToLower();
            if (Array.IndexOf(allowedModelTypes, modelType) == -1)
            {
                modelType = "base";
            }

            string tempAudioFile = string.Empty;
            var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
            var tempWavFile = Path.Combine(uploadsFolder, Guid.NewGuid().ToString() + ".wav");

            try
            {
                // 1. Download YouTube audio and metadata
                var (audioPath, title, avatarUrl) = await _youtubeService.DownloadAudioAndMetadataAsync(request.Url);
                tempAudioFile = audioPath;

                // 2. Convert to standard 16kHz PCM mono WAV
                Console.WriteLine($"Converting downloaded YouTube audio to WAV: {tempWavFile}");
                await _audioConverter.ConvertToWavAsync(tempAudioFile, tempWavFile);

                // 3. Transcribe WAV file
                Console.WriteLine("Starting transcription of YouTube audio...");
                var response = await _transcriptionService.TranscribeAsync(tempWavFile, modelType);
                
                // Add YouTube metadata to response
                response.FileName = Path.GetFileName(tempAudioFile);
                response.Title = title;
                response.AvatarUrl = avatarUrl;

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during YouTube transcription: {ex.Message}");
                try
                {
                    if (System.IO.File.Exists(tempAudioFile)) System.IO.File.Delete(tempAudioFile);
                }
                catch { }
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to process YouTube video: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(tempWavFile)) System.IO.File.Delete(tempWavFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete temporary WAV file: {ex.Message}");
                }
            }
        }

        [HttpGet("audio")]
        public IActionResult GetAudio([FromQuery] string filename)
        {
            if (string.IsNullOrWhiteSpace(filename) || filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
            {
                return BadRequest("Invalid filename.");
            }

            var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
            var filePath = Path.Combine(uploadsFolder, filename);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Audio file not found.");
            }

            var extension = Path.GetExtension(filename).ToLower();
            var contentType = extension switch
            {
                ".webm" => "audio/webm",
                ".m4a" => "audio/mp4",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                _ => "audio/mpeg"
            };
            
            return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
        }

        [HttpPost("export/word")]
        public IActionResult ExportToWord([FromBody] ExportRequest request)
        {
            if (request == null || request.Segments == null || request.Segments.Count == 0)
            {
                return BadRequest("Invalid export request. No segments provided.");
            }

            try
            {
                var docxBytes = _documentExporter.ExportToWord(request.Title, request.Segments, request.IncludeTimestamps);
                
                var safeTitle = string.Join("_", request.Title.Split(Path.GetInvalidFileNameChars()));
                var filename = $"{safeTitle}.docx";

                return base.File(
                    docxBytes, 
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 
                    filename
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting to Word: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to export Word document: {ex.Message}");
            }
        }
        [HttpPost("video/upload")]
        [Consumes("multipart/form-data")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadVideoForBurn([FromForm] VideoUploadRequest request)
        {
            var file = request?.File;
            if (file == null || file.Length == 0)
            {
                return BadRequest("No video file was uploaded.");
            }

            var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".mp4";
            }

            var tempFileName = $"{Guid.NewGuid()}{extension}";
            var tempFilePath = Path.Combine(uploadsFolder, tempFileName);

            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new
            {
                fileName = tempFileName,
                originalName = Path.GetFileNameWithoutExtension(file.FileName)
            });
        }

        [HttpPost("video/burn-subtitles")]
        public IActionResult BurnSubtitles([FromBody] BurnSubtitlesRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.VideoUrl))
            {
                return BadRequest("Video source is required.");
            }

            var taskId = _downloadTracker.CreateTask();
            
            // Start background thread to download/transcribe/burn subtitles
            _ = Task.Run(async () =>
            {
                await _videoBurnService.BurnSubtitlesAsync(request, taskId, _downloadTracker);
            });

            return Ok(new { taskId });
        }
    }

    public class TranscribeUploadRequest
    {
        public IFormFile File { get; set; } = null!;
        public string ModelType { get; set; } = "base";
    }

    public class VideoUploadRequest
    {
        public IFormFile File { get; set; } = null!;
    }
}

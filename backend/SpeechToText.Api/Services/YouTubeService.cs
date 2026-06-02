using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using SpeechToText.Api.Models;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace SpeechToText.Api.Services
{
    public class YouTubeService : IYouTubeService
    {
        private readonly YoutubeClient _youtube;
        private readonly IAudioConverter _audioConverter;

        public YouTubeService(IAudioConverter audioConverter)
        {
            _audioConverter = audioConverter;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
            };

            var detectedProxy = DetectLocalProxy();
            if (detectedProxy != null)
            {
                handler.Proxy = detectedProxy;
                handler.UseProxy = true;
            }
            else
            {
                handler.UseProxy = true;
            }

            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

            _youtube = new YoutubeClient(httpClient);
        }

        private IWebProxy? DetectLocalProxy()
        {
            var commonProxies = new[]
            {
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
                        Console.WriteLine($"[YouTubeService] Detected active local proxy at {proxyUrl}. Routing YouTube requests through it.");
                        return new WebProxy(uri);
                    }
                }
                catch
                {
                    // Ignore
                }
            }

            Console.WriteLine("[YouTubeService] No active local proxies detected. Using default system proxy settings.");
            return null;
        }

        public async Task<(string AudioPath, string Title, string AvatarUrl)> DownloadAudioAndMetadataAsync(string videoUrl)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new ArgumentException("YouTube URL cannot be empty.", nameof(videoUrl));
            }

            Console.WriteLine($"Parsing YouTube video from URL: {videoUrl}");
            
            var video = await _youtube.Videos.GetAsync(videoUrl);
            var title = video.Title;
            var channelId = video.Author.ChannelId;

            Console.WriteLine($"Fetching author channel details for ID: {channelId}");
            var channel = await _youtube.Channels.GetAsync(channelId);
            
            var avatarUrl = channel.Thumbnails.FirstOrDefault()?.Url ?? string.Empty;

            Console.WriteLine("Fetching audio stream manifest...");
            var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id);
            
            var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            if (audioStreamInfo == null)
            {
                throw new InvalidOperationException("No audio tracks found for this YouTube video.");
            }

            var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var extension = audioStreamInfo.Container.Name;
            var tempAudioPath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.{extension}");

            Console.WriteLine($"Downloading YouTube audio stream to temporary path: {tempAudioPath}");
            await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, tempAudioPath);
            Console.WriteLine("Audio download complete.");

            return (tempAudioPath, title, avatarUrl);
        }

        public async Task<YouTubeVideoInfo> GetVideoInfoAsync(string videoUrl)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new ArgumentException("YouTube URL cannot be empty.", nameof(videoUrl));
            }

            Console.WriteLine($"[YouTubeService] Fetching metadata preview for: {videoUrl}");
            var video = await _youtube.Videos.GetAsync(videoUrl);
            var channelId = video.Author.ChannelId;
            
            var channel = await _youtube.Channels.GetAsync(channelId);
            var avatarUrl = channel.Thumbnails.FirstOrDefault()?.Url ?? string.Empty;
            
            var thumbnailUrl = video.Thumbnails.LastOrDefault()?.Url ?? string.Empty;

            return new YouTubeVideoInfo
            {
                Title = video.Title,
                Author = video.Author.ChannelTitle,
                DurationSeconds = video.Duration?.TotalSeconds ?? 0,
                ThumbnailUrl = thumbnailUrl,
                AvatarUrl = avatarUrl,
                Url = videoUrl
            };
        }

        public async Task DownloadVideoFileAsync(string videoUrl, string quality, string taskId, IYouTubeDownloadTracker tracker)
        {
            try
            {
                tracker.UpdateProgress(taskId, 5, "正在解析 YouTube 视频轨道信息...");
                var video = await _youtube.Videos.GetAsync(videoUrl);
                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id);

                var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                if (quality.ToLower() == "1080p")
                {
                    // 1. Get 1080p video-only stream
                    tracker.UpdateProgress(taskId, 10, "正在寻找 1080P 视频轨道...");
                    var videoStreamInfo = streamManifest.GetVideoOnlyStreams()
                        .Where(s => s.VideoQuality.Label == "1080p")
                        .FirstOrDefault();

                    // Fallback to highest video-only quality if 1080p is not available
                    if (videoStreamInfo == null)
                    {
                        videoStreamInfo = streamManifest.GetVideoOnlyStreams()
                            .OrderByDescending(s => s.VideoQuality)
                            .FirstOrDefault();
                    }

                    if (videoStreamInfo == null)
                    {
                        throw new InvalidOperationException("未找到可用的视频轨。");
                    }

                    // 2. Get highest bitrate audio-only stream
                    var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    if (audioStreamInfo == null)
                    {
                        throw new InvalidOperationException("未找到可用的音频轨。");
                    }

                    var tempVideoFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.mp4");
                    var tempAudioFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.m4a");
                    var mergedOutputFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.mp4");

                    // 3. Download Video-only stream (counts as 10% to 70% of total progress)
                    tracker.UpdateProgress(taskId, 10, $"开始下载视频轨 (画质: {videoStreamInfo.VideoQuality.Label})...");
                    var videoProgress = new Progress<double>(p =>
                    {
                        tracker.UpdateProgress(taskId, 10 + (p * 60), $"正在下载视频轨 ({Math.Round(p * 100)}%)...");
                    });
                    await _youtube.Videos.Streams.DownloadAsync(videoStreamInfo, tempVideoFile, videoProgress);

                    // 4. Download Audio-only stream (counts as 70% to 90% of total progress)
                    tracker.UpdateProgress(taskId, 70, "视频轨下载完成，开始下载音频轨...");
                    var audioProgress = new Progress<double>(p =>
                    {
                        tracker.UpdateProgress(taskId, 70 + (p * 20), $"正在下载音频轨 ({Math.Round(p * 100)}%)...");
                    });
                    await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, tempAudioFile, audioProgress);

                    // 5. Merge tracks using FFmpeg (counts as 90% to 98% of total progress)
                    tracker.UpdateProgress(taskId, 90, "音视频轨道下载完毕，正在调用 FFmpeg 进行高速合并封装...");
                    
                    await _audioConverter.EnsureFFmpegAsync(); // Make sure FFmpeg is initialized
                    await RunFFmpegMergeAsync(tempVideoFile, tempAudioFile, mergedOutputFile);

                    // Cleanup temp unmerged files
                    try
                    {
                        if (File.Exists(tempVideoFile)) File.Delete(tempVideoFile);
                        if (File.Exists(tempAudioFile)) File.Delete(tempAudioFile);
                    }
                    catch { }

                    tracker.CompleteTask(taskId, mergedOutputFile, video.Title);
                }
                else
                {
                    // 720p or lower Muxed Stream (no FFmpeg merge needed)
                    tracker.UpdateProgress(taskId, 10, "正在寻找 720P 混合音视频轨...");
                    var streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
                    if (streamInfo == null)
                    {
                        throw new InvalidOperationException("未找到可用的 720P/混合音视频轨。");
                    }

                    var tempVideoFile = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.mp4");

                    tracker.UpdateProgress(taskId, 15, "开始下载 720P 混合流文件...");
                    var progress = new Progress<double>(p =>
                    {
                        tracker.UpdateProgress(taskId, 15 + (p * 80), $"正在下载视频文件 ({Math.Round(p * 100)}%)...");
                    });
                    await _youtube.Videos.Streams.DownloadAsync(streamInfo, tempVideoFile, progress);

                    tracker.CompleteTask(taskId, tempVideoFile, video.Title);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YouTubeService] Background download failed for task {taskId}: {ex.Message}");
                tracker.FailTask(taskId, ex.Message);
            }
        }

        private string GetFFmpegPath()
        {
            if (IsCommandAvailable("ffmpeg")) return "ffmpeg";
            var localPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(localPath)) return localPath;
            throw new FileNotFoundException("FFmpeg program not found.");
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
            // -c:v copy: Copy video stream without re-encoding (instant!)
            // -c:a aac: Re-encode audio to AAC to ensure broad MP4 player compatibility
            var arguments = $"-y -i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac \"{outputPath}\"";

            Console.WriteLine($"[YouTubeService] Running FFmpeg Muxer: {ffmpeg} {arguments}");

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
                throw new InvalidOperationException("Failed to invoke FFmpeg compiler process.");
            }

            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = stdErrBuffer.ToString();
                Console.WriteLine($"[YouTubeService] FFmpeg Muxer failed with exit code {process.ExitCode}. Error details:\n{error}");
                throw new Exception($"FFmpeg muxing failed (Exit Code {process.ExitCode}): {error}");
            }
            Console.WriteLine("[YouTubeService] FFmpeg Muxer process finished successfully.");
        }
    }
}

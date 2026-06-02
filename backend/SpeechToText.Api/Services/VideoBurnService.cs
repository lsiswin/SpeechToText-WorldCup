using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public class VideoBurnService : IVideoBurnService
    {
        private readonly IAudioConverter _audioConverter;
        private readonly ITranscriptionService _transcriptionService;
        private readonly ITranslationService _translationService;
        private readonly IYouTubeService _youtubeService;
        private readonly IBilibiliService _bilibiliService;

        public VideoBurnService(
            IAudioConverter audioConverter,
            ITranscriptionService transcriptionService,
            ITranslationService translationService,
            IYouTubeService youtubeService,
            IBilibiliService bilibiliService)
        {
            _audioConverter = audioConverter;
            _transcriptionService = transcriptionService;
            _translationService = translationService;
            _youtubeService = youtubeService;
            _bilibiliService = bilibiliService;
        }

        private bool IsBilibiliUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            var lowerUrl = url.ToLower();
            return lowerUrl.Contains("bilibili.com") || lowerUrl.Contains("b23.tv");
        }

        private bool IsOnlineUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            var lowerUrl = url.ToLower();
            return lowerUrl.StartsWith("http://") || lowerUrl.StartsWith("https://");
        }

        public async Task BurnSubtitlesAsync(BurnSubtitlesRequest request, string taskId, IYouTubeDownloadTracker tracker)
        {
            var uploadsFolder = Path.Combine(Path.GetTempPath(), "SpeechToTextUploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var inputVideoPath = string.Empty;
            var isOnline = IsOnlineUrl(request.VideoUrl);
            var isBili = IsBilibiliUrl(request.VideoUrl);
            var downloadTaskId = Guid.NewGuid().ToString();
            var title = "视频字幕压制";

            var tempWavPath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.wav");
            var tempAssPath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.ass");
            var localAssFilename = $"{Guid.NewGuid()}.ass";
            var localAssPath = Path.Combine(AppContext.BaseDirectory, localAssFilename);
            var outputVideoPath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}.mp4");

            try
            {
                // ==========================================
                // Phase 1: Obtain Video File (Download or Local)
                // ==========================================
                if (isOnline)
                {
                    tracker.UpdateProgress(taskId, 2, "正在初始化在线视频下载任务...");
                    
                    // Create an entry in tracker for the downloader task
                    var dlTask = new DownloadTaskStatus
                    {
                        TaskId = downloadTaskId,
                        Progress = 0,
                        StatusText = "正在解析在线地址...",
                        IsComplete = false
                    };
                    
                    // Run actual downloader on a separate task
                    if (isBili)
                    {
                        _ = Task.Run(async () =>
                        {
                            await _bilibiliService.DownloadVideoFileAsync(request.VideoUrl, "720p", downloadTaskId, tracker);
                        });
                    }
                    else
                    {
                        _ = Task.Run(async () =>
                        {
                            await _youtubeService.DownloadVideoFileAsync(request.VideoUrl, "720p", downloadTaskId, tracker);
                        });
                    }

                    // Poll downloader progress
                    DownloadTaskStatus? dlStatus = null;
                    while (true)
                    {
                        await Task.Delay(1000);
                        dlStatus = tracker.GetStatus(downloadTaskId);
                        if (dlStatus == null)
                        {
                            throw new Exception("在线视频下载服务被意外终止。");
                        }
                        if (dlStatus.IsComplete)
                        {
                            break;
                        }
                        if (!string.IsNullOrEmpty(dlStatus.Error))
                        {
                            throw new Exception($"在线视频抓取失败: {dlStatus.Error}");
                        }

                        // Map download progress to 2% -> 20%
                        var mappedProgress = 2 + (dlStatus.Progress * 0.18);
                        tracker.UpdateProgress(taskId, mappedProgress, $"正在抓取在线视频 ({Math.Round(dlStatus.Progress)}%)...");
                    }

                    if (string.IsNullOrEmpty(dlStatus.FilePath))
                    {
                        throw new Exception("下载的视频路径无效。");
                    }
                    inputVideoPath = dlStatus.FilePath;
                    title = dlStatus.Title ?? "在线视频";
                    tracker.RemoveTask(downloadTaskId); // Clean up download status cache
                }
                else
                {
                    // Local File
                    inputVideoPath = Path.Combine(uploadsFolder, request.VideoUrl);
                    if (!File.Exists(inputVideoPath))
                    {
                        throw new FileNotFoundException("未在服务器缓存中找到上传的视频文件。", inputVideoPath);
                    }
                    title = Path.GetFileNameWithoutExtension(inputVideoPath);
                }

                // ==========================================
                // Phase 2: Audio Extraction
                // ==========================================
                tracker.UpdateProgress(taskId, 22, "音轨抓取中，正在将视频转换为 16kHz WAV 格式...");
                await _audioConverter.ConvertToWavAsync(inputVideoPath, tempWavPath);

                // ==========================================
                // Phase 3: Speech to Text (Whisper)
                // ==========================================
                tracker.UpdateProgress(taskId, 32, "本地语音识别中，正在通过 Whisper 模型提取文字时间轴...");
                var response = await _transcriptionService.TranscribeAsync(tempWavPath, request.ModelType);
                var segments = response.Segments;

                if (segments == null || segments.Count == 0)
                {
                    throw new InvalidOperationException("未在视频中识别到任何语音对话。");
                }

                // ==========================================
                // Phase 4: Translation (Optional)
                // ==========================================
                if (request.TranslateToChinese)
                {
                    tracker.UpdateProgress(taskId, 55, "字幕翻译中，正在将识别文本转换为中文...");
                    segments = await _translationService.TranslateSegmentsAsync(segments, "zh-CN");
                }

                // ==========================================
                // Phase 5: Create ASS Subtitle file
                // ==========================================
                tracker.UpdateProgress(taskId, 68, "排版制作中，正在生成自定义样式的 ASS 字幕...");
                
                var assContent = GenerateAssContent(segments, request.FontSize, request.FontColor, request.SubtitleStyle, request.Position);
                await File.WriteAllTextAsync(tempAssPath, assContent, Encoding.UTF8);

                // Copy ASS to app folder to prevent backslash syntax errors in FFmpeg subtitles filter on Windows
                File.Copy(tempAssPath, localAssPath, true);

                // ==========================================
                // Phase 6: FFmpeg Subtitle Burn-in (Re-encoding)
                // ==========================================
                tracker.UpdateProgress(taskId, 75, "硬字幕压制中，调用 FFmpeg 运行画轨二次渲染...");
                
                await _audioConverter.EnsureFFmpegAsync();
                await RunFFmpegBurnInAsync(inputVideoPath, localAssFilename, outputVideoPath);

                // Clean up source downloaded video if online URL to save space
                if (isOnline)
                {
                    try { if (File.Exists(inputVideoPath)) File.Delete(inputVideoPath); } catch {}
                }

                tracker.CompleteTask(taskId, outputVideoPath, $"{title}_字幕版");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VideoBurnService] Burn task {taskId} failed: {ex.Message}");
                tracker.FailTask(taskId, ex.Message);
            }
            finally
            {
                // Clean up intermediate temp files
                try { if (File.Exists(tempWavPath)) File.Delete(tempWavPath); } catch {}
                try { if (File.Exists(tempAssPath)) File.Delete(tempAssPath); } catch {}
                try { if (File.Exists(localAssPath)) File.Delete(localAssPath); } catch {}
            }
        }

        private string GenerateAssContent(System.Collections.Generic.List<TranscriptionSegment> segments, int fontSize, string fontColor, string style, string position)
        {
            var sb = new StringBuilder();
            
            // 1. Script Info
            sb.AppendLine("[Script Info]");
            sb.AppendLine("Title: Auto Subtitles");
            sb.AppendLine("ScriptType: v4.00+");
            sb.AppendLine("PlayResX: 1920");
            sb.AppendLine("PlayResY: 1080");
            sb.AppendLine("WrapStyle: 0");
            sb.AppendLine();

            // Convert RGB Hex color (e.g. "ffff00") to ASS BGR Hex format (&H00BBGGRR)
            var primaryColor = ConvertRgbToAssColor(fontColor);
            
            // Map parameters
            // Style properties: Alignment (2 = bottom center, 8 = top center)
            var alignment = position.ToLower() == "top" ? "8" : "2";
            var scaleFont = fontSize * 1.8; // Scale pixels relative to 1080p resolution

            var borderStyle = "1"; // 1 = outline, 3 = box background
            var outline = "2";
            var shadow = "1";
            var backColor = "&H80000000"; // 50% transparent black for shadow/box

            if (style.ToLower() == "box")
            {
                borderStyle = "3"; // Background box style
                outline = "0";
                shadow = "0";
            }

            // 2. Styles
            sb.AppendLine("[V4+ Styles]");
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
            sb.AppendLine($"Style: Default,Microsoft YaHei,{scaleFont:0},{primaryColor},&H000000FF,&H00000000,{backColor},-1,0,0,0,100,100,0,0,{borderStyle},{outline},{shadow},{alignment},10,10,50,1");
            sb.AppendLine();

            // 3. Events
            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

            foreach (var s in segments)
            {
                var startStr = FormatTimeForAss(s.Start);
                var endStr = FormatTimeForAss(s.End);
                var text = s.Text.Trim();
                
                sb.AppendLine($"Dialogue: 0,{startStr},{endStr},Default,,0,0,0,,{text}");
            }

            return sb.ToString();
        }

        private string ConvertRgbToAssColor(string rgbHex)
        {
            rgbHex = rgbHex.Replace("#", "").Trim();
            if (rgbHex.Length != 6) rgbHex = "ffffff";
            
            var r = rgbHex.Substring(0, 2);
            var g = rgbHex.Substring(2, 2);
            var b = rgbHex.Substring(4, 2);
            
            return $"&H00{b}{g}{r}";
        }

        private string FormatTimeForAss(TimeSpan time)
        {
            var h = time.Hours;
            var m = time.Minutes;
            var s = time.Seconds;
            var cs = time.Milliseconds / 10; // centiseconds
            return $"{h}:{m:D2}:{s:D2}.{cs:D2}";
        }

        private string GetFFmpegPath()
        {
            if (IsCommandAvailable("ffmpeg")) return "ffmpeg";
            var localPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(localPath)) return localPath;
            throw new FileNotFoundException("未能在系统路径下找到 FFmpeg。");
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

        private async Task RunFFmpegBurnInAsync(string videoPath, string relativeAssFilename, string outputPath)
        {
            var ffmpeg = GetFFmpegPath();
            // -vf "subtitles='filename'": Burn ASS subtitles into video
            // -c:v libx264: Re-encode video stream
            // -preset fast -crf 23: Balance encoding quality and speed
            // -c:a copy: Directly copy audio stream
            var arguments = $"-y -i \"{videoPath}\" -vf \"subtitles='{relativeAssFilename}'\" -c:v libx264 -preset fast -crf 23 -c:a copy \"{outputPath}\"";

            Console.WriteLine($"[VideoBurnService] Running FFmpeg Burner: {ffmpeg} {arguments}");

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
                throw new InvalidOperationException("无法调用 FFmpeg 进程完成压制。");
            }

            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = stdErrBuffer.ToString();
                Console.WriteLine($"[VideoBurnService] FFmpeg Burner failed with exit code {process.ExitCode}. Error details:\n{error}");
                throw new Exception($"FFmpeg 字幕压制重编码失败 (错误码 {process.ExitCode}): {error}");
            }
            Console.WriteLine("[VideoBurnService] FFmpeg Burner process finished successfully.");
        }
    }
}

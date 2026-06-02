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

        public TranscriptionController(
            IAudioConverter audioConverter,
            ITranscriptionService transcriptionService,
            IDocumentExporter documentExporter,
            IYouTubeService youtubeService,
            IBilibiliService bilibiliService,
            IYouTubeDownloadTracker downloadTracker,
            IVideoBurnService videoBurnService)
        {
            _audioConverter = audioConverter;
            _transcriptionService = transcriptionService;
            _documentExporter = documentExporter;
            _youtubeService = youtubeService;
            _bilibiliService = bilibiliService;
            _downloadTracker = downloadTracker;
            _videoBurnService = videoBurnService;
        }

        [HttpPost("transcribe")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Transcribe([FromForm] IFormFile file, [FromForm] string modelType = "base")
        {
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

        [HttpGet("online/download-cover")]
        public async Task<IActionResult> DownloadCover([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return BadRequest("Cover URL is required.");
            }

            try
            {
                var handler = new HttpClientHandler();
                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                if (url.Contains("bilivideo.com") || url.Contains("hdslb.com"))
                {
                    client.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");
                }

                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                var stream = await response.Content.ReadAsStreamAsync();
                
                var extension = contentType == "image/png" ? "png" : "jpg";
                var filename = $"cover.{extension}";

                return base.File(stream, contentType, filename);
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
            
            // Start background thread to download video asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    if (IsBilibiliUrl(request.Url))
                    {
                        await _bilibiliService.DownloadVideoFileAsync(request.Url, request.Quality, taskId, _downloadTracker);
                    }
                    else
                    {
                        await _youtubeService.DownloadVideoFileAsync(request.Url, request.Quality, taskId, _downloadTracker);
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
            var filename = $"{safeTitle}.mp4";

            // Remove task from cache tracker
            _downloadTracker.RemoveTask(taskId);

            // Open stream with DeleteOnClose to ensure automated filesystem cleanup upon completion
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            return base.File(stream, "video/mp4", filename);
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
            var contentType = extension == ".webm" ? "audio/webm" : "audio/mpeg";
            
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
}

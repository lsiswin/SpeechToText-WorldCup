using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace SpeechToText.Api.Services
{
    public class FFmpegAudioConverter : IAudioConverter
    {
        private string _ffmpegPath = "ffmpeg";
        private readonly object _lock = new();
        private bool _isInitialized = false;

        public async Task EnsureFFmpegAsync()
        {
            if (_isInitialized) return;

            // Check if ffmpeg is in Path
            if (IsCommandAvailable("ffmpeg"))
            {
                _ffmpegPath = "ffmpeg";
                _isInitialized = true;
                Console.WriteLine("FFmpeg is available in system PATH.");
                return;
            }

            // Check if local ffmpeg.exe exists
            var localPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(localPath))
            {
                _ffmpegPath = localPath;
                _isInitialized = true;
                Console.WriteLine($"FFmpeg found locally at: {_ffmpegPath}");
                return;
            }

            // Otherwise, download it dynamically
            Console.WriteLine("FFmpeg not found in PATH or local folder. Starting automatic download...");
            
            var zipPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.zip");
            var downloadUrl = "https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v4.4.1/ffmpeg-4.4.1-win-64.zip";

            try
            {
                using (var client = new HttpClient())
                {
                    // Set a timeout of 5 minutes for downloading
                    client.Timeout = TimeSpan.FromMinutes(5);
                    
                    Console.WriteLine($"Downloading FFmpeg from {downloadUrl}...");
                    var response = await client.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    using (var fs = new FileStream(zipPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                    Console.WriteLine("Download complete. Extracting file...");
                }

                ZipFile.ExtractToDirectory(zipPath, AppContext.BaseDirectory, overwriteFiles: true);
                Console.WriteLine("Extraction complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading FFmpeg: {ex.Message}");
                throw new Exception("Could not download FFmpeg. Please install FFmpeg manually or ensure you have internet access.", ex);
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    try
                    {
                        File.Delete(zipPath);
                    }
                    catch { /* ignored */ }
                }
            }

            if (File.Exists(localPath))
            {
                _ffmpegPath = localPath;
                _isInitialized = true;
                Console.WriteLine("FFmpeg has been successfully downloaded and initialized.");
            }
            else
            {
                throw new FileNotFoundException("Failed to verify local FFmpeg.exe installation.");
            }
        }

        public async Task ConvertToWavAsync(string inputPath, string outputPath)
        {
            await EnsureFFmpegAsync();

            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("Input file for conversion not found.", inputPath);
            }

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Command: ffmpeg -y -i input -ar 16000 -ac 1 -c:a pcm_s16le output.wav
            // -y: overwrite output files without asking
            // -ar 16000: set sample rate to 16000Hz (required by Whisper)
            // -ac 1: set audio channels to 1 (mono, required by Whisper)
            // -c:a pcm_s16le: set audio codec to PCM 16-bit little-endian (required by Whisper)
            var arguments = $"-y -i \"{inputPath}\" -ar 16000 -ac 1 -c:a pcm_s16le \"{outputPath}\"";

            Console.WriteLine($"Running conversion: {_ffmpegPath} {arguments}");

            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            
            var stdErrBuffer = new System.Text.StringBuilder();
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdErrBuffer.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            
            // Wait for exit
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var errorMsg = stdErrBuffer.ToString();
                Console.WriteLine($"FFmpeg execution failed with exit code {process.ExitCode}. Error details:\n{errorMsg}");
                throw new Exception($"FFmpeg extraction failed (Exit Code {process.ExitCode}): {errorMsg}");
            }

            Console.WriteLine("Audio extraction/conversion completed successfully.");
        }

        private bool IsCommandAvailable(string command)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

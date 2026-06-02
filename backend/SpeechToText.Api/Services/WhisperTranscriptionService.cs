using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SpeechToText.Api.Models;
using Whisper.net;
using Whisper.net.Ggml;

namespace SpeechToText.Api.Services
{
    public class WhisperTranscriptionService : ITranscriptionService
    {
        private readonly string _modelDirectory;

        public WhisperTranscriptionService()
        {
            _modelDirectory = Path.Combine(AppContext.BaseDirectory, "Models");
            if (!Directory.Exists(_modelDirectory))
            {
                Directory.CreateDirectory(_modelDirectory);
            }
        }

        private string GetModelPath(string modelType)
        {
            return Path.Combine(_modelDirectory, $"ggml-{modelType.ToLower()}.bin");
        }

        private GgmlType MapGgmlType(string modelType)
        {
            return modelType.ToLower() switch
            {
                "tiny" => GgmlType.Tiny,
                "base" => GgmlType.Base,
                "small" => GgmlType.Small,
                "medium" => GgmlType.Medium,
                "large" => GgmlType.LargeV3,
                _ => GgmlType.Base
            };
        }

        public async Task EnsureModelDownloadedAsync(string modelType)
        {
            var modelPath = GetModelPath(modelType);
            if (File.Exists(modelPath))
            {
                Console.WriteLine($"Whisper model '{modelType}' already downloaded.");
                return;
            }

            var ggmlType = MapGgmlType(modelType);
            Console.WriteLine($"Whisper model '{modelType}' not found. Downloading from Hugging Face...");

            try
            {
                using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);
                using var fileStream = File.OpenWrite(modelPath);
                await modelStream.CopyToAsync(fileStream);
                Console.WriteLine($"Whisper model '{modelType}' downloaded successfully to {modelPath}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading Whisper model: {ex.Message}");
                if (File.Exists(modelPath))
                {
                    try { File.Delete(modelPath); } catch { }
                }
                throw new Exception($"Failed to download Whisper model '{modelType}'. Please check your internet connection.", ex);
            }
        }

        public async Task<TranscriptionResponse> TranscribeAsync(string wavPath, string modelType)
        {
            await EnsureModelDownloadedAsync(modelType);

            var modelPath = GetModelPath(modelType);
            if (!File.Exists(wavPath))
            {
                throw new FileNotFoundException("WAV file for transcription not found.", wavPath);
            }

            Console.WriteLine($"Starting transcription of '{wavPath}' using model '{modelType}'...");

            var segments = new List<TranscriptionSegment>();
            var fullTextBuilder = new StringBuilder();
            var index = 0;

            // Load Whisper Factory
            using var factory = WhisperFactory.FromPath(modelPath);
            
            // Build Processor
            using var processor = factory.CreateBuilder()
                .WithLanguage("auto") // Auto-detect language
                .Build();

            // Open audio stream
            using var fileStream = File.OpenRead(wavPath);
            
            // Process audio segments
            await foreach (var segment in processor.ProcessAsync(fileStream))
            {
                var transSegment = new TranscriptionSegment
                {
                    Index = index++,
                    Start = segment.Start,
                    End = segment.End,
                    Text = segment.Text.Trim()
                };

                segments.Add(transSegment);
                fullTextBuilder.Append(transSegment.Text).Append(' ');
            }

            // Estimate duration from segment time or file metadata (here we use the end of the last segment)
            double duration = 0;
            if (segments.Count > 0)
            {
                duration = segments[^1].End.TotalSeconds;
            }

            Console.WriteLine($"Transcription complete. Transcribed {segments.Count} segments.");

            return new TranscriptionResponse
            {
                FileName = Path.GetFileName(wavPath),
                DurationSeconds = duration,
                Language = "auto", // Whisper automatically handles languages
                Segments = segments,
                FullText = fullTextBuilder.ToString().Trim()
            };
        }
    }
}

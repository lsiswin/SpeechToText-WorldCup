using System.Collections.Generic;

namespace SpeechToText.Api.Models
{
    public class TranscriptionResponse
    {
        public string FileName { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public string Language { get; set; } = "unknown";
        public List<TranscriptionSegment> Segments { get; set; } = new();
        public string FullText { get; set; } = string.Empty;
        
        // Metadata for YouTube videos
        public string? AvatarUrl { get; set; }
        public string? Title { get; set; }
    }
}

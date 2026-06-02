using System;

namespace SpeechToText.Api.Models
{
    public class TranscriptionSegment
    {
        public int Index { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}

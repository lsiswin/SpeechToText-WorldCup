using System.Collections.Generic;

namespace SpeechToText.Api.Models
{
    public class ExportRequest
    {
        public string Title { get; set; } = "语音识别文档";
        public List<TranscriptionSegment> Segments { get; set; } = new();
        public bool IncludeTimestamps { get; set; } = true;
    }
}

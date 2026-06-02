using System.Collections.Generic;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public interface IDocumentExporter
    {
        byte[] ExportToWord(string title, List<TranscriptionSegment> segments, bool includeTimestamps);
    }
}

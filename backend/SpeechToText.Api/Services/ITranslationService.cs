using System.Collections.Generic;
using System.Threading.Tasks;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public interface ITranslationService
    {
        Task<List<TranscriptionSegment>> TranslateSegmentsAsync(List<TranscriptionSegment> segments, string targetLanguage = "zh-CN");
    }
}

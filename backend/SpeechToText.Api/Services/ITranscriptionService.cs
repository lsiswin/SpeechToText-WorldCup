using System.Threading.Tasks;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public interface ITranscriptionService
    {
        Task EnsureModelDownloadedAsync(string modelType);
        Task<TranscriptionResponse> TranscribeAsync(string wavPath, string modelType);
    }
}

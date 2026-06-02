using System.Threading.Tasks;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public interface IVideoBurnService
    {
        Task BurnSubtitlesAsync(BurnSubtitlesRequest request, string taskId, IYouTubeDownloadTracker tracker);
    }
}

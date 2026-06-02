using System.Threading.Tasks;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public interface IBilibiliService
    {
        Task<OnlineVideoInfo> GetVideoInfoAsync(string videoUrl);
        Task<(string AudioPath, string Title, string AvatarUrl)> DownloadAudioAndMetadataAsync(string videoUrl);
        Task DownloadVideoFileAsync(string videoUrl, string quality, string taskId, IYouTubeDownloadTracker tracker);
    }
}

using System.Threading.Tasks;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public interface IYouTubeService
    {
        Task<(string AudioPath, string Title, string AvatarUrl)> DownloadAudioAndMetadataAsync(string videoUrl);
        
        Task<YouTubeVideoInfo> GetVideoInfoAsync(string videoUrl);
        
        Task DownloadVideoFileAsync(string videoUrl, string quality, string taskId, IYouTubeDownloadTracker tracker);
    }
}

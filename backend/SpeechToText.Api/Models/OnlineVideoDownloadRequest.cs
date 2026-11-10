namespace SpeechToText.Api.Models
{
    public class OnlineVideoDownloadRequest
    {
        public string Url { get; set; } = string.Empty;
        public string Quality { get; set; } = "1080p"; // "best", "1080p", "720p", "480p"
        public string DownloadType { get; set; } = "video"; // "video" or "audio"
        public string AudioFormat { get; set; } = "mp3"; // "mp3", "m4a", "wav", "flac"
        public string VideoFormat { get; set; } = "mp4"; // "mp4", "mkv"
    }
}

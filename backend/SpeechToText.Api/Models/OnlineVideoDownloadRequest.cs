namespace SpeechToText.Api.Models
{
    public class OnlineVideoDownloadRequest
    {
        public string Url { get; set; } = string.Empty;
        public string Quality { get; set; } = "1080p";
    }
}

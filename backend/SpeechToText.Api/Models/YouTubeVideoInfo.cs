namespace SpeechToText.Api.Models
{
    public class YouTubeVideoInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}

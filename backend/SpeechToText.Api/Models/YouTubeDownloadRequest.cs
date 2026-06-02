using System.ComponentModel.DataAnnotations;

namespace SpeechToText.Api.Models
{
    public class YouTubeDownloadRequest
    {
        [Required]
        public string Url { get; set; } = string.Empty;

        public string Quality { get; set; } = "720p"; // "720p" or "1080p"
    }
}

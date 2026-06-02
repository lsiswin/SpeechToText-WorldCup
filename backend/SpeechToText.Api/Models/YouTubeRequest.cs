using System.ComponentModel.DataAnnotations;

namespace SpeechToText.Api.Models
{
    public class YouTubeRequest
    {
        [Required]
        public string Url { get; set; } = string.Empty;

        public string ModelType { get; set; } = "base";
    }
}

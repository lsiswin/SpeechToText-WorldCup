namespace SpeechToText.Api.Models
{
    public class OnlineVideoRequest
    {
        public string Url { get; set; } = string.Empty;
        public string ModelType { get; set; } = "base";
    }
}

namespace SpeechToText.Api.Models
{
    public class BurnSubtitlesRequest
    {
        public string VideoUrl { get; set; } = string.Empty;
        public string ModelType { get; set; } = "base";
        public bool TranslateToChinese { get; set; } = false;
        public int FontSize { get; set; } = 20;
        public string FontColor { get; set; } = "ffffff"; // Hex color like "ffffff", "ffff00"
        public string SubtitleStyle { get; set; } = "outline"; // "outline" or "box"
        public string Position { get; set; } = "bottom"; // "bottom" or "top"
    }
}

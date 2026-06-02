using System.Threading.Tasks;

namespace SpeechToText.Api.Services
{
    public interface IAudioConverter
    {
        Task EnsureFFmpegAsync();
        Task ConvertToWavAsync(string inputPath, string outputPath);
    }
}

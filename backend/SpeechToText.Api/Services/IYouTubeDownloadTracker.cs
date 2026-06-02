namespace SpeechToText.Api.Services
{
    public class DownloadTaskStatus
    {
        public string TaskId { get; set; } = string.Empty;
        public double Progress { get; set; } // 0 to 100
        public string StatusText { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
        public string? FilePath { get; set; }
        public string? Title { get; set; }
        public string? Error { get; set; }
    }

    public interface IYouTubeDownloadTracker
    {
        string CreateTask();
        void UpdateProgress(string taskId, double progress, string statusText);
        void CompleteTask(string taskId, string filePath, string title);
        void FailTask(string taskId, string error);
        DownloadTaskStatus? GetStatus(string taskId);
        void RemoveTask(string taskId);
    }
}

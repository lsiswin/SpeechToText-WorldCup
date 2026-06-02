using System;
using System.Collections.Concurrent;

namespace SpeechToText.Api.Services
{
    public class YouTubeDownloadTracker : IYouTubeDownloadTracker
    {
        private readonly ConcurrentDictionary<string, DownloadTaskStatus> _tasks = new();

        public string CreateTask()
        {
            var taskId = Guid.NewGuid().ToString();
            var task = new DownloadTaskStatus
            {
                TaskId = taskId,
                Progress = 0,
                StatusText = "正在排队...",
                IsComplete = false
            };
            _tasks[taskId] = task;
            return taskId;
        }

        public void UpdateProgress(string taskId, double progress, string statusText)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Progress = Math.Min(100, Math.Max(0, progress));
                task.StatusText = statusText;
            }
        }

        public void CompleteTask(string taskId, string filePath, string title)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Progress = 100;
                task.StatusText = "下载与打包完成！";
                task.IsComplete = true;
                task.FilePath = filePath;
                task.Title = title;
            }
        }

        public void FailTask(string taskId, string error)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.StatusText = "下载失败";
                task.Error = error;
            }
        }

        public DownloadTaskStatus? GetStatus(string taskId)
        {
            _tasks.TryGetValue(taskId, out var task);
            return task;
        }

        public void RemoveTask(string taskId)
        {
            _tasks.TryRemove(taskId, out _);
        }
    }
}

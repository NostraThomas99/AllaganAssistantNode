using System.Collections.Concurrent;
using ECommons.DalamudServices;

namespace AllaganAssistantNode.Controllers;

public class BackgroundTaskController
{
    private readonly ConcurrentQueue<Func<Task>> _taskQueue = new ConcurrentQueue<Func<Task>>();
    private Task? _backgroundWorkerTask;
    private readonly object _workerLock = new object();
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    public void EnqueueTask(Func<Task> task)
    {
        _taskQueue.Enqueue(task);

        // Ensure the background worker is running
        lock (_workerLock)
        {
            if (_backgroundWorkerTask == null || _backgroundWorkerTask.IsCompleted)
            {
                _backgroundWorkerTask = Task.Run(ProcessTaskQueueAsync);
            }
        }
    }

    private async Task ProcessTaskQueueAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (_taskQueue.TryDequeue(out var task))
            {
                try
                {
                    // Execute the task
                    await task();
                }
                catch (Exception ex)
                {
                    // Log the exception or handle it appropriately
                    Svc.Log.Error($"Task execution failed: {ex.Message}");
                }
            }
            else
            {
                // No tasks to process, break out of the loop
                break;
            }
        }
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _backgroundWorkerTask?.Wait();
    }
}
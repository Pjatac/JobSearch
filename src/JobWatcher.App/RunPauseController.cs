using JobWatcher.Services;

namespace JobWatcher.App;

public sealed class RunPauseController : IRunPauseController
{
    private readonly object gate = new();
    private TaskCompletionSource paused = CreateCompleted();

    public bool IsPaused { get; private set; }

    public void Pause()
    {
        lock (gate)
        {
            if (IsPaused)
            {
                return;
            }

            IsPaused = true;
            paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        TaskCompletionSource toRelease;
        lock (gate)
        {
            if (!IsPaused)
            {
                return;
            }

            IsPaused = false;
            toRelease = paused;
        }

        toRelease.TrySetResult();
    }

    public void Reset() => Resume();

    public Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task waitTask;
        lock (gate)
        {
            if (!IsPaused)
            {
                return Task.CompletedTask;
            }

            waitTask = paused.Task;
        }

        return waitTask.WaitAsync(cancellationToken);
    }

    private static TaskCompletionSource CreateCompleted()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}

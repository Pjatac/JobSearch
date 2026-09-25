namespace JobWatcher.Services;

public interface IRunPauseController
{
    bool IsPaused { get; }

    Task WaitIfPausedAsync(CancellationToken cancellationToken);
}

public sealed class NoOpRunPauseController : IRunPauseController
{
    public bool IsPaused => false;

    public Task WaitIfPausedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

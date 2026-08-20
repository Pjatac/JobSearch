namespace JobWatcher.App;

public sealed class RunStateService
{
    public event EventHandler? RunCompleted;

    public void NotifyRunCompleted() => RunCompleted?.Invoke(this, EventArgs.Empty);
}

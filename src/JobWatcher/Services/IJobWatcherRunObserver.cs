using JobWatcher.Models;

namespace JobWatcher.Services;

public interface IJobWatcherRunObserver
{
    void SourceStarted(string source);
    void SourceFinished(SourceOutput sourceOutput);
}

using JobWatcher.Configuration;
using JobWatcher.Sources;
using JobWatcher.Sources.SecretTelAviv;
using Microsoft.Extensions.DependencyInjection;

namespace JobWatcher.Tests;

public sealed class JobWatcherServiceCollectionExtensionsTests
{
    [Fact]
    public void RegistersSecretTelAvivSource()
    {
        var services = new ServiceCollection();

        services.AddJobWatcherCollector();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IJobSource) &&
            descriptor.ImplementationType == typeof(SecretTelAvivSource));
    }
}

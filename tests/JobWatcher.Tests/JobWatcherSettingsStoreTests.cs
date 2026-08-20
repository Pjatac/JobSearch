using JobWatcher.Configuration;

namespace JobWatcher.Tests;

public sealed class JobWatcherSettingsStoreTests
{
    [Fact]
    public async Task CreatesCompleteUserSettingsFromTheDefaultDocument()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings", "jobwatcher.json");
        var defaults = """
            {
              "jobWatcher": {
                "dataDirectory": "data",
                "sources": [
                  {
                    "name": "JobKarov-Software",
                    "adapter": "JobKarov",
                    "jobKarovFilter": { "speciality": "2119" }
                  }
                ]
              }
            }
            """;

        var options = await new JobWatcherSettingsStore().LoadOrCreateAsync(path, defaults);

        Assert.True(File.Exists(path));
        Assert.DoesNotContain("jobWatcher", await File.ReadAllTextAsync(path), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("data", options.DataDirectory);
        Assert.Equal("JobKarov-Software", Assert.Single(options.Sources).Name);
        Assert.Equal("2119", options.Sources[0].JobKarovFilter!.Speciality);
    }

    [Fact]
    public async Task ExistingUserSettingsTakePrecedenceOverDefaults()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "jobwatcher.json");
        var store = new JobWatcherSettingsStore();
        await store.SaveAsync(path, new JobWatcherOptions
        {
            DataDirectory = "personal-data",
            Sources = [new JobSourceOptions { Name = "Personal", Adapter = "JobKarov" }]
        });

        var options = await store.LoadOrCreateAsync(path, "{\"jobWatcher\":{\"dataDirectory\":\"default-data\"}}");

        Assert.Equal("default-data", options.DataDirectory);
        Assert.Equal("Personal", Assert.Single(options.Sources).Name);
    }

    [Fact]
    public async Task MigratesThePreviousCompleteSettingsDocumentToUserOverrides()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "jobwatcher.json");
        await File.WriteAllTextAsync(path, """
            { "jobWatcher": { "dataDirectory": "old-data", "sources": [ { "name": "Personal" } ] } }
            """);

        var options = await new JobWatcherSettingsStore().LoadOrCreateAsync(
            path,
            "{ \"jobWatcher\": { \"dataDirectory\": \"new-default-data\" } }");

        Assert.Equal("new-default-data", options.DataDirectory);
        Assert.Equal("Personal", Assert.Single(options.Sources).Name);
        Assert.DoesNotContain("jobWatcher", await File.ReadAllTextAsync(path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersistsIncompleteProfileDraftWithoutBlockingTheNextLoad()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "jobwatcher.json");
        var store = new JobWatcherSettingsStore();
        await store.SaveAsync(path, new JobWatcherUserSettings
        {
            Sources =
            [
                new JobSourceOptions
                {
                    Name = "Work in progress",
                    Adapter = "JobKarov",
                    JobKarovFilter = new JobKarovFilterOptions { Speciality = string.Empty }
                }
            ]
        });

        var options = await store.LoadOrCreateAsync(
            path,
            "{ \"jobWatcher\": { \"dataDirectory\": \"data\" } }");

        var draft = Assert.Single(options.Sources);
        Assert.Equal("Work in progress", draft.Name);
        Assert.Equal(string.Empty, draft.JobKarovFilter!.Speciality);
    }

    [Fact]
    public async Task TargetedUpdatesPreserveTheOtherSettingsSection()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "jobwatcher.json");
        const string defaults = "{ \"jobWatcher\": { \"dataDirectory\": \"data\" } }";
        var store = new JobWatcherSettingsStore();

        await store.UpdateAsync(path, defaults, current => current with
        {
            Sources = [new JobSourceOptions { Name = "Profile", Adapter = "AllJobs", AllJobsFilter = new AllJobsFilterOptions() }]
        });
        await store.UpdateAsync(path, defaults, current => current with
        {
            Classification = new JobClassificationOptions { IncludeSignals = ["C#"] }
        });
        var result = await store.UpdateAsync(path, defaults, current => current with
        {
            Sources = [new JobSourceOptions { Name = "Updated profile", Adapter = "AllJobs", AllJobsFilter = new AllJobsFilterOptions() }]
        });

        Assert.Equal("Updated profile", Assert.Single(result.Sources).Name);
        Assert.Equal("C#", Assert.Single(result.Classification.IncludeSignals));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"jobwatcher-tests-{Guid.NewGuid():N}");

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

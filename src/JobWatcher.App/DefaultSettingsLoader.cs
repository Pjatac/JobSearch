namespace JobWatcher.App;

internal static class DefaultSettingsLoader
{
    private static readonly string[] AssetNames = ["jobwatcher.defaults.json", "Resources/Raw/jobwatcher.defaults.json"];

    public static async Task<string> ReadAsync()
    {
        foreach (var assetName in AssetNames)
        {
            try
            {
                await using var stream = await FileSystem.OpenAppPackageFileAsync(assetName);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch (FileNotFoundException)
            {
                // Windows unpackaged builds keep raw assets beneath Resources/Raw.
            }
        }

        throw new FileNotFoundException("The packaged Job Watcher default settings file was not found.");
    }
}

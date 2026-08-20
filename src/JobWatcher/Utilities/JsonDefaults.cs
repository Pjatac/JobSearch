using System.Text.Encodings.Web;
using System.Text.Json;

namespace JobWatcher.Utilities;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        return JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken);
    }

    public static Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        return JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);
    }
}

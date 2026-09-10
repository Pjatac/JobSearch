using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobWatcher.Utilities;

public sealed class LenientStringListJsonConverter : JsonConverter<IReadOnlyList<string>>
{
    public override bool HandleNull => true;

    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return [];
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return [reader.GetString() ?? string.Empty];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected string array, string, or null, but found {reader.TokenType}.");
        }

        var values = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return values;
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                continue;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected string array item, but found {reader.TokenType}.");
            }

            values.Add(reader.GetString() ?? string.Empty);
        }

        throw new JsonException("Unexpected end of JSON while reading string array.");
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}

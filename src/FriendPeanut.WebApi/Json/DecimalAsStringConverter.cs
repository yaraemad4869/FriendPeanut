using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FriendPeanut.WebApi.Json;

public sealed class DecimalAsStringConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) throw new JsonException("Decimal string is empty.");
            if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                throw new JsonException($"Decimal string not parseable: {s}");
            return value;
        }
        if (reader.TokenType == JsonTokenType.Number) return reader.GetDecimal();
        throw new JsonException($"Unexpected token {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("G29", CultureInfo.InvariantCulture));
}

public sealed class NullableDecimalAsStringConverter : JsonConverter<decimal?>
{
    private readonly DecimalAsStringConverter _inner = new();
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : _inner.Read(ref reader, typeof(decimal), options);
    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        _inner.Write(writer, value.Value, options);
    }
}

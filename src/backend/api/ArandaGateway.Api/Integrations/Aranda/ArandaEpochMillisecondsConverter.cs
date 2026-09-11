using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArandaGateway.Api.Integrations.Aranda;

/// <summary>
/// Aranda no es consistente con las fechas: unas llegan como epoch en
/// milisegundos y otras en el formato heredado de .NET
/// <c>/Date(1728432000000+0000)/</c>. Este convertidor acepta ambas y siempre
/// entrega epoch en milisegundos.
/// </summary>
public sealed class ArandaEpochMillisecondsConverter : JsonConverter<long?>
{
    public override long? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => ParseLegacyDate(reader.GetString()),
            _ => throw new JsonException(
                $"No se pudo leer una fecha de Aranda desde {reader.TokenType}.")
        };

    public override void Write(
        Utf8JsonWriter writer,
        long? value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value.Value);
    }

    /// <summary>
    /// Extrae los milisegundos de <c>/Date(1728432000000+0000)/</c>. El
    /// desplazamiento horario se descarta: el valor ya está en UTC.
    /// </summary>
    private static long? ParseLegacyDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var span = value.AsSpan().Trim();

        if (!span.StartsWith("/Date(", StringComparison.Ordinal) ||
            !span.EndsWith(")/", StringComparison.Ordinal))
        {
            return long.TryParse(
                span,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var plain)
                ? plain
                : throw new JsonException(
                    $"Formato de fecha de Aranda no reconocido: {value}.");
        }

        var inner = span[6..^2];

        // El desplazamiento ("+0000") empieza tras el primer digito, para no
        // confundirlo con el signo de un epoch negativo.
        var offsetIndex = inner.LastIndexOfAny('+', '-');
        if (offsetIndex > 0)
        {
            inner = inner[..offsetIndex];
        }

        return long.TryParse(
            inner,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var milliseconds)
            ? milliseconds
            : throw new JsonException(
                $"Formato de fecha de Aranda no reconocido: {value}.");
    }
}

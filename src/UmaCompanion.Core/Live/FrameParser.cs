using System.Text.Json;

namespace UmaCompanion.Core.Live;

/// <summary>
/// Parses raw JSON frames from the game hook into typed <see cref="GameFrame"/> records.
/// Returns null for malformed JSON or unknown frame types so the ingest loop can drop and continue.
/// </summary>
public static class FrameParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static GameFrame? Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("type", out var typeProp) ||
                typeProp.ValueKind != JsonValueKind.String ||
                !doc.RootElement.TryGetProperty("payload", out var payload))
            {
                return null;
            }

            var raw = payload.GetRawText();
            try
            {
                return typeProp.GetString() switch
                {
                    "career_turn" => JsonSerializer.Deserialize<CareerTurnFrame>(raw, Options),
                    "race_results" => new RaceResultsFrame(
                        JsonSerializer.Deserialize<IReadOnlyList<RaceResult>>(raw, Options) ?? []),
                    _ => null, // roster is handled by the roster ingest path; unknown types dropped
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}

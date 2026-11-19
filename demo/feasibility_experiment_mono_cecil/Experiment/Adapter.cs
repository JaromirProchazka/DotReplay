using System.Globalization;
using System.Text.Json.Nodes;

namespace DotReplay.Experiment;

/// <summary>Turns the Recorder's raw recording into the contract's <c>trace.json</c> and <c>run.json</c>.</summary>
public static class Adapter
{
    /// <summary>trace.json: every recorded call, with snapshots keyed by captureId.</summary>
    public static JsonObject Trace(JsonObject recording, IReadOnlyList<CapturedIdentifier> captured)
    {
        var calls = recording["calls"]!.AsArray()
            .Select((call, index) => Call(call!.AsObject(), index, captured))
            .ToArray<JsonNode?>();
        return new JsonObject { ["schemaVersion"] = 1, ["calls"] = new JsonArray(calls) };
    }

    /// <summary>run.json of a successful analysis, before test generation adds <c>counts.tests</c>.</summary>
    public static JsonObject Run(string signature, IReadOnlyList<CapturedIdentifier> captured, JsonObject trace, DateTimeOffset createdAt)
    {
        var typeNames = captured.Select(c => c.Type)
            .Concat(TypesIn(trace))
            .Distinct()
            .Where(type => TypeNames.Short(type) != type)
            .Select(type => KeyValuePair.Create(type, (JsonNode?)TypeNames.Short(type)));

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["language"] = "dotnet",
            ["target"] = new JsonObject { ["signature"] = signature },
            ["captured"] = new JsonArray(captured.Select((c, id) => (JsonNode?)new JsonObject
            {
                ["captureId"] = id,
                ["kind"] = c.Kind,
                ["name"] = c.Name,
                ["type"] = c.Type,
            }).ToArray()),
            ["typeNames"] = new JsonObject(typeNames),
            ["traceMode"] = "naive",
            ["recorded"] = new JsonObject { ["exit"] = true, ["outcome"] = true },
            ["counts"] = new JsonObject { ["calls"] = trace["calls"]!.AsArray().Count },
            ["status"] = "ok",
            ["createdAt"] = createdAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        };
    }

    static JsonObject Call(JsonObject call, int index, IReadOnlyList<CapturedIdentifier> captured)
    {
        var node = new JsonObject { ["callIndex"] = index, ["entry"] = Snapshot(call["entry"]!.AsArray(), captured) };
        if (call["exit"] is JsonArray exit)
        {
            node["exit"] = Snapshot(exit, captured);
            node["outcome"] = new JsonObject { ["kind"] = "returned" };
        }
        else
        {
            // The call never reached a return: the process ended inside it.
            node["exit"] = null;
        }

        return node;
    }

    static JsonObject Snapshot(JsonArray values, IReadOnlyList<CapturedIdentifier> captured)
    {
        var snapshot = new JsonObject();
        for (var id = 0; id < values.Count; id++)
        {
            var value = values[id]!.DeepClone().AsObject();
            if (!value.ContainsKey("type"))
            {
                // The Recorder cannot know the declared type of a top-level null.
                value["type"] = captured[id].Type;
            }

            snapshot[id.ToString(CultureInfo.InvariantCulture)] = value;
        }

        return snapshot;
    }

    static IEnumerable<string> TypesIn(JsonNode? node) => node switch
    {
        JsonObject value when value["kind"] is JsonValue && value["type"] is JsonValue type =>
            value.Select(property => property.Value).SelectMany(TypesIn).Prepend(type.GetValue<string>()),
        JsonObject other => other.Select(property => property.Value).SelectMany(TypesIn),
        JsonArray array => array.SelectMany(TypesIn),
        _ => [],
    };
}

using System.Globalization;
using System.Text.Json.Nodes;

namespace DotReplay.Experiment;

/// <summary>A woven Target method. <see cref="MethodFullName"/> is Cecil's full name, which detectors use in stack frames.</summary>
public sealed record TargetMethod(string Signature, string MethodFullName, IReadOnlyList<CapturedIdentifier> Captured);

/// <summary>
/// Builds analysis.json, the only input of test generation: contract v1's run.json and trace.json content,
/// extended with several Target methods, threads and findings.
/// </summary>
public static class AnalysisFile
{
    public static JsonObject Build(
        string assembly,
        IReadOnlyList<TargetMethod> targets,
        JsonObject recording,
        IReadOnlyList<Finding> findings,
        string detector,
        DateTimeOffset createdAt)
    {
        var rawCalls = recording["calls"]!.AsArray().Select(call => call!.AsObject()).ToList();
        // Threads are identified by name, because that is what detectors report. Unnamed threads get a name from their id.
        var threads = rawCalls.Select(ThreadName).Distinct().ToList();
        var calls = rawCalls.Select((call, index) => Call(call, index, targets, threads)).ToList();
        var trace = new JsonArray(calls.ToArray<JsonNode?>());

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["language"] = "dotnet",
            ["assembly"] = assembly,
            ["targets"] = new JsonArray(targets.Select((target, id) => (JsonNode?)new JsonObject
            {
                ["targetId"] = id,
                ["signature"] = target.Signature,
                ["captured"] = new JsonArray(target.Captured.Select((c, captureId) => (JsonNode?)new JsonObject
                {
                    ["captureId"] = captureId,
                    ["kind"] = c.Kind,
                    ["name"] = c.Name,
                    ["type"] = c.Type,
                }).ToArray()),
            }).ToArray()),
            ["typeNames"] = TypeNamesOf(targets, trace),
            ["traceMode"] = "naive",
            ["recorded"] = new JsonObject { ["exit"] = true, ["outcome"] = true },
            ["counts"] = new JsonObject { ["calls"] = calls.Count },
            ["threads"] = new JsonArray(threads.Select((name, id) => (JsonNode?)new JsonObject { ["threadId"] = id, ["name"] = name }).ToArray()),
            ["calls"] = trace,
            ["findings"] = new JsonArray(findings.Select(finding => (JsonNode?)Finding(finding, targets, threads, calls)).ToArray()),
            ["detector"] = detector,
            ["status"] = "ok",
            ["createdAt"] = createdAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        };
    }

    static string ThreadName(JsonObject call) =>
        call["threadName"]?.GetValue<string>() ?? $"thread {call["managedThreadId"]}";

    static JsonObject Call(JsonObject call, int index, IReadOnlyList<TargetMethod> targets, List<string> threads)
    {
        var target = call["target"]!.GetValue<int>();
        var captured = targets[target].Captured;
        var node = new JsonObject
        {
            ["callIndex"] = index,
            ["target"] = target,
            ["thread"] = threads.IndexOf(ThreadName(call)),
            ["entry"] = Snapshot(call["entry"]!.AsArray(), captured),
        };
        if (call["exit"] is JsonArray exit)
        {
            node["exit"] = Snapshot(exit, captured);
            node["outcome"] = new JsonObject { ["kind"] = "returned" };
        }
        else
        {
            // The call never returned: its thread was still blocked (e.g. deadlocked) when the process ended.
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
                value["type"] = captured[id].Type;
            }

            snapshot[id.ToString(CultureInfo.InvariantCulture)] = value;
        }

        return snapshot;
    }

    static JsonObject Finding(Finding finding, IReadOnlyList<TargetMethod> targets, List<string> threads, IReadOnlyList<JsonObject> calls) =>
        finding switch
        {
            DeadlockFinding deadlock => new JsonObject
            {
                ["kind"] = "deadlock",
                ["threads"] = new JsonArray(deadlock.Waits.Select(wait => (JsonNode?)new JsonObject
                {
                    ["threadId"] = ThreadId(threads, wait.Thread),
                    // The deadlocked call is the one that never returned.
                    ["callIndices"] = CallIndices(calls, targets, threads, wait.Thread, wait.Frames, unfinishedOnly: true),
                    ["waitsFor"] = new JsonObject { ["lock"] = wait.Lock, ["heldBy"] = ThreadId(threads, wait.HeldBy) },
                }).ToArray()),
            },
            DataRaceFinding race => new JsonObject
            {
                ["kind"] = "dataRace",
                ["field"] = race.Field,
                ["accesses"] = new JsonArray(race.Accesses.Select(access => (JsonNode?)new JsonObject
                {
                    ["threadId"] = ThreadId(threads, access.Thread),
                    // A detector reports the code location, not the call, so every call of the method on that thread is listed.
                    ["callIndices"] = CallIndices(calls, targets, threads, access.Thread, access.Frames, unfinishedOnly: false),
                    ["access"] = access.Access,
                }).ToArray()),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(finding)),
        };

    static int ThreadId(List<string> threads, string name) =>
        threads.IndexOf(name) is >= 0 and var id ? id : throw new ExperimentError($"Thread '{name}' made no recorded call.");

    // The calls on the thread to the innermost Target method in the stack.
    static JsonArray CallIndices(IReadOnlyList<JsonObject> calls, IReadOnlyList<TargetMethod> targets, List<string> threads, string thread, IReadOnlyList<string> frames, bool unfinishedOnly)
    {
        var target = frames
            .Select(frame => targets.ToList().FindIndex(t => t.MethodFullName == frame))
            .FirstOrDefault(index => index >= 0, -1);
        var threadId = ThreadId(threads, thread);
        return new JsonArray(calls
            .Where(call => call["target"]!.GetValue<int>() == target && call["thread"]!.GetValue<int>() == threadId)
            .Where(call => !unfinishedOnly || call["exit"] is null)
            .Select(call => (JsonNode?)call["callIndex"]!.GetValue<int>())
            .ToArray());
    }

    static JsonObject TypeNamesOf(IReadOnlyList<TargetMethod> targets, JsonArray calls) =>
        new(targets.SelectMany(target => target.Captured.Select(c => c.Type))
            .Concat(TypesIn(calls))
            .Distinct()
            .Where(type => TypeNames.Short(type) != type)
            .Select(type => KeyValuePair.Create(type, (JsonNode?)TypeNames.Short(type))));

    static IEnumerable<string> TypesIn(JsonNode? node) => node switch
    {
        JsonObject value when value["kind"] is JsonValue && value["type"] is JsonValue type =>
            value.Select(property => property.Value).SelectMany(TypesIn).Prepend(type.GetValue<string>()),
        JsonObject other => other.Select(property => property.Value).SelectMany(TypesIn),
        JsonArray array => array.SelectMany(TypesIn),
        _ => [],
    };
}

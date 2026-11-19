using System.Text.Json.Nodes;

namespace DotReplay.Recorder;

/// <summary>
/// Called by the code the weaver inserts into the Target method. Each snapshot is serialized at once,
/// so later changes to an object do not change it. The raw recording is written when the process exits.
/// </summary>
public static class CallRecorder
{
    /// <summary>The file the raw recording is written to.</summary>
    public const string RecordingVariable = "DOTREPLAY_RECORDING";

    static readonly object Gate = new();
    static readonly JsonArray Calls = [];

    static CallRecorder() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Save();

    /// <summary>Records the entry snapshot and returns the index of the call.</summary>
    public static int Enter(object?[] values)
    {
        var entry = ValueSerializer.Snapshot(values);
        lock (Gate)
        {
            Calls.Add(new JsonObject { ["entry"] = entry, ["exit"] = null });
            return Calls.Count - 1;
        }
    }

    /// <summary>Records the exit snapshot of a call that returned.</summary>
    public static void Exit(int call, object?[] values)
    {
        var exit = ValueSerializer.Snapshot(values);
        lock (Gate)
        {
            Calls[call]!["exit"] = exit;
        }
    }

    static void Save()
    {
        var path = Environment.GetEnvironmentVariable(RecordingVariable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        lock (Gate)
        {
            File.WriteAllText(path, new JsonObject { ["calls"] = Calls.DeepClone() }.ToJsonString());
        }
    }
}

using System.Text.Json.Nodes;
using DotReplay.Recorder;

namespace DotReplay.ThreadRecorder;

/// <summary>
/// Called by the code the weaver inserts into each Target method. Like the Mono.Cecil experiment's
/// CallRecorder, but it also records which Target method was called and on which thread.
/// </summary>
public static class ThreadCallRecorder
{
    /// <summary>The file the raw recording is written to.</summary>
    public const string RecordingVariable = "DOTREPLAY_RECORDING";

    static readonly object Gate = new();
    static readonly JsonArray Calls = [];

    static ThreadCallRecorder() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Save();

    /// <summary>Records the entry snapshot of a call of Target method <paramref name="target"/> and returns the index of the call.</summary>
    public static int Enter(int target, object?[] values)
    {
        var entry = ValueSerializer.Snapshot(values);
        var thread = Thread.CurrentThread;
        lock (Gate)
        {
            Calls.Add(new JsonObject
            {
                ["target"] = target,
                ["managedThreadId"] = thread.ManagedThreadId,
                ["threadName"] = thread.Name,
                ["entry"] = entry,
                ["exit"] = null,
            });
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

    // Calls of deadlocked threads never reach Exit; they are saved with "exit": null.
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

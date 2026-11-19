using System.Text.Json.Nodes;
using Xunit;

namespace DotReplay.Tests.Infrastructure;

/// <summary>Assertions on exit codes, documented error codes (doc/errors.md) and Manifest statuses.</summary>
public static class Outcome
{
    public const string Analysis = "analysis";
    public const string TestGeneration = "testGeneration";
    public const string TestRunning = "testRunning";

    public static void AssertSucceeded(CliResult result) =>
        Assert.True(result.ExitCode == 0, $"Expected exit code 0, got {result.ExitCode}.\nstderr:\n{result.StandardError}");

    /// <summary>A failure ends with a non-zero exit code and reports its documented error code (R26).</summary>
    public static void AssertFailedWith(CliResult result, string errorCode)
    {
        Assert.True(result.ExitCode != 0, $"Expected a non-zero exit code.\nstdout:\n{result.StandardOutput}");
        Assert.Contains(errorCode, result.StandardError);
    }

    public static void AssertManifest(SessionDirectory session, string stage, string status, string? errorCode = null)
    {
        var entry = session.Read(SessionDirectory.ManifestFile)["stages"]?[stage];
        Assert.NotNull(entry);
        Assert.Equal(status, entry["status"]?.GetValue<string>());
        if (errorCode is not null)
        {
            Assert.Equal(errorCode, entry["code"]?.GetValue<string>());
        }
    }

    /// <summary>Contract rule 8: run.json is written even when recording or test generation fails.</summary>
    public static void AssertRunFailed(SessionDirectory session, string errorCode)
    {
        var run = session.Read(SessionDirectory.RunFile);
        Assert.Equal("failed", run["status"]?.GetValue<string>());
        Assert.Contains(errorCode, run["error"]?.GetValue<string>() ?? "");
    }

    /// <summary>A failed test execution is reported as the top-level error of results.json.</summary>
    public static void AssertResultsError(SessionDirectory session, string errorCode)
    {
        var error = session.Read(SessionDirectory.ResultsFile)["error"] as JsonObject;
        Assert.NotNull(error);
        Assert.Contains(errorCode, error["message"]?.GetValue<string>() ?? "");
    }
}

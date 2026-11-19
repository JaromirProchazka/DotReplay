using System.Diagnostics;
using System.Text.Json.Nodes;
using DotReplay.Tests.Infrastructure;
using Xunit;

namespace DotReplay.Experiment.Tests;

/// <summary>Runs the experiment as a process, with binaries of the same build configuration as the tests.</summary>
public static class ExperimentCli
{
    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    // The tests run from Experiment.Tests/bin/<configuration>/net8.0/.
    static readonly DirectoryInfo Output = new(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
    static readonly string Configuration = Output.Parent!.Name;
    static readonly string ExperimentRoot = Output.Parent.Parent!.Parent!.Parent!.FullName;

    public static string CalculatorApp { get; } = Build(Path.Combine(ExperimentRoot, "..", "mock_target_app", "args"), "calc-app.dll");

    public static string ExtraTargetApp { get; } = Build(Path.Combine(ExperimentRoot, "ExtraTarget"), "extra-target.dll");

    static string Cli { get; } = Build(Path.Combine(ExperimentRoot, "Experiment"), "cecil-experiment.dll");

    /// <summary>Records the Target method into the Session directory and asserts that the experiment succeeded.</summary>
    public static void Record(SessionDirectory session, string assembly, string method, string? fields = null)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { Cli, "--assembly", assembly, "--method", method, "--output-dir", session.Path },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (fields is not null)
        {
            start.ArgumentList.Add("--fields");
            start.ArgumentList.Add(fields);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(Timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"The experiment did not finish within {Timeout}.");
        }

        Assert.True(process.ExitCode == 0, $"Exit code {process.ExitCode}.\n{output.Result}\n{error.Result}");
    }

    /// <summary>Every recorded value in the trace, including nested fields and elements.</summary>
    public static IEnumerable<JsonObject> Values(JsonObject trace) =>
        trace["calls"]!.AsArray()
            .SelectMany(call => new[] { call!["entry"], call["exit"] })
            .OfType<JsonObject>()
            .SelectMany(snapshot => snapshot.Select(property => property.Value!.AsObject()))
            .SelectMany(Nested);

    static IEnumerable<JsonObject> Nested(JsonObject value)
    {
        var fields = (value["fields"] as JsonObject)?.Select(property => property.Value!.AsObject()) ?? [];
        var elements = (value["elements"] as JsonArray)?.Select(element => element!.AsObject()) ?? [];
        return fields.Concat(elements).SelectMany(Nested).Prepend(value);
    }

    static string Build(string project, string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(project, "bin", Configuration, "net8.0", fileName));
        return File.Exists(path) ? path : throw new FileNotFoundException($"Build {project} in {Configuration} first.", path);
    }
}

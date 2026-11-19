using System.Diagnostics;
using Xunit;

// SharpDetect runs and nested `dotnet test` runs are slow and heavy; one at a time keeps them independent.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DotReplay.Experiment.Tests;

/// <summary>Runs the experiment and the generated tests as processes, with builds of the tests' own configuration.</summary>
public static class ExperimentCli
{
    // The tests run from Experiment.Tests/bin/<configuration>/net8.0/.
    static readonly DirectoryInfo Output = new(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
    static readonly string Configuration = Output.Parent!.Name;
    static readonly string ExperimentRoot = Output.Parent.Parent!.Parent!.Parent!.FullName;

    public static string TargetApp { get; } = Build("ConcurrentTarget", "concurrent-target.dll");

    public static string FixedTargetApp { get; } = Build("ConcurrentTargetFixed", "concurrent-target.dll");

    static string Cli { get; } = Build("Experiment", "sharpdetect-experiment.dll");

    /// <summary>Runs <c>sharpdetect-experiment</c> and asserts that it succeeded.</summary>
    public static void Experiment(params string[] arguments)
    {
        var (exitCode, output) = Dotnet([Cli, .. arguments]);
        Assert.True(exitCode == 0, $"sharpdetect-experiment {arguments[0]} exited with {exitCode}.\n{output}");
    }

    /// <summary>Runs <c>dotnet test</c> on a generated test project, against another Target assembly if given.</summary>
    /// <remarks>
    /// Each run gets its own copy of the project. The buggy and the fixed concurrent-target.dll can have the same size
    /// and timestamp, and then MSBuild skips copying one over the other, so a shared bin folder would test the wrong one.
    /// </remarks>
    public static (int ExitCode, string Output) TestGenerated(string project, string? targetAssembly = null)
    {
        var copy = Path.Combine(Path.GetDirectoryName(project)!, $"{Path.GetFileName(project)}-{(targetAssembly is null ? "as-generated" : "other-target")}");
        Directory.CreateDirectory(copy);
        foreach (var file in Directory.GetFiles(project))
        {
            File.Copy(file, Path.Combine(copy, Path.GetFileName(file)), overwrite: true);
        }

        return Dotnet(targetAssembly is null ? ["test", copy] : ["test", copy, $"-p:TargetAssembly={targetAssembly}"]);
    }

    static (int ExitCode, string Output) Dotnet(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet {string.Join(' ', arguments)} did not finish within 5 minutes.");
        }

        return (process.ExitCode, output.Result + error.Result);
    }

    static string Build(string project, string fileName)
    {
        var path = Path.Combine(ExperimentRoot, project, "bin", Configuration, "net8.0", fileName);
        return File.Exists(path) ? path : throw new FileNotFoundException($"Build {project} in {Configuration} first.", path);
    }
}

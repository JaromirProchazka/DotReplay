using System.Diagnostics;

namespace DotReplay.Tests.Infrastructure;

/// <summary>The result of one CLI invocation.</summary>
public sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Runs the dotreplay CLI as a black box. The tests never reference its code, so the mock can be
/// swapped for the real app by pointing <c>DOTREPLAY_CLI</c> at another build.
/// </summary>
public static class DotReplayCli
{
    /// <summary>Path to the CLI: a .dll (run with <c>dotnet</c>) or an executable.</summary>
    public const string CliVariable = "DOTREPLAY_CLI";

    /// <summary>Path to calc-app.dll, the Target application of the Calculator scenario.</summary>
    public const string TargetAppVariable = "DOTREPLAY_TARGET_APP";

    // Used when DOTREPLAY_CLI is not set: the newest local build of the mock.
    const string DefaultCliProject = "demo/mock_dotreplay";
    const string DefaultCliFile = "dotreplay.dll";

    const string TargetAppProject = "demo/mock_target_app/args";
    const string TargetAppFile = "calc-app.dll";

    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public static string CliPath { get; } = Resolve(CliVariable, DefaultCliProject, DefaultCliFile);

    public static string TargetAppPath { get; } = Resolve(TargetAppVariable, TargetAppProject, TargetAppFile);

    public static string TargetSourcePath { get; } = Path.Combine(RepositoryRoot.Path, TargetAppProject);

    public static CliResult Run(params string[] arguments)
    {
        var start = CliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? new ProcessStartInfo("dotnet") { ArgumentList = { CliPath } }
            : new ProcessStartInfo(CliPath);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.UseShellExecute = false;
        start.WorkingDirectory = RepositoryRoot.Path;

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {CliPath}");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(Timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotreplay {string.Join(' ', arguments)} did not finish within {Timeout}.");
        }

        return new CliResult(process.ExitCode, output.Result, error.Result);
    }

    /// <summary>The valid analysis arguments of the Calculator scenario, without the Session directory.</summary>
    public static string[] CalculatorArguments(string method = "Com.Example.Calculator.Add(int,int)") =>
    [
        "--assembly", TargetAppPath,
        "--source", TargetSourcePath,
        "--method", method,
        "--parameters", "0:int,1:int",
    ];

    /// <summary>The path from the environment variable, or else the newest build of the project.</summary>
    static string Resolve(string variable, string project, string fileName)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = Path.GetFullPath(configured, RepositoryRoot.Path);
            return File.Exists(path) ? path : throw new FileNotFoundException($"{variable} points to a missing file", path);
        }

        var binDirectory = Path.Combine(RepositoryRoot.Path, project, "bin");
        var builds = Directory.Exists(binDirectory)
            ? Directory.GetFiles(binDirectory, fileName, SearchOption.AllDirectories)
            : [];
        return builds.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            ?? throw new FileNotFoundException($"{fileName} not found. Build {project} first, or set {variable}.");
    }
}

public static class RepositoryRoot
{
    /// <summary>The folder containing CLAUDE.md, found upwards from the test binaries.</summary>
    public static string Path { get; } = Find();

    static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "CLAUDE.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root (with CLAUDE.md) not found.");
    }
}

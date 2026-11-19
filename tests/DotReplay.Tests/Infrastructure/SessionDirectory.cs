using System.Text.Json.Nodes;

namespace DotReplay.Tests.Infrastructure;

/// <summary>A temporary Session directory for one test, deleted afterwards.</summary>
public sealed class SessionDirectory : IDisposable
{
    public const string RunFile = "run.json";
    public const string TraceFile = "trace.json";
    public const string TestsFile = "tests.json";
    public const string ResultsFile = "results.json";
    public const string ManifestFile = "manifest.json";

    public static readonly string[] ContractFiles = [RunFile, TraceFile, TestsFile, ResultsFile];

    public SessionDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dotreplay-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public bool Has(string file) => File.Exists(System.IO.Path.Combine(Path, file));

    public JsonObject Read(string file) =>
        JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(Path, file))) as JsonObject
        ?? throw new InvalidDataException($"{file} is not a JSON object");

    public void Write(string file, string content) => File.WriteAllText(System.IO.Path.Combine(Path, file), content);

    /// <summary>Copies expected files of a scenario in, as prepared input for a single stage.</summary>
    public void CopyExpected(string scenario, params string[] files)
    {
        foreach (var file in files)
        {
            File.Copy(Expected.PathOf(scenario, file), System.IO.Path.Combine(Path, file));
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder must not fail a test.
        }
    }
}

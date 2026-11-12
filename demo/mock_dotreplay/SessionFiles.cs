using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DotReplay.Mock;

/// <summary>The Session directory (the contract's run directory). Every file is written atomically (R28).</summary>
sealed class SessionDirectory(string path)
{
    public const string RunFile = "run.json";
    public const string TraceFile = "trace.json";
    public const string TestsFile = "tests.json";
    public const string ResultsFile = "results.json";
    public const string ManifestFile = "manifest.json";
    public const string TestSourceFile = "tests/CalculatorTest.cs";

    static readonly string[] OwnFiles = [RunFile, TraceFile, TestsFile, ResultsFile, ManifestFile, TestSourceFile];

    static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string FullPath { get; } = Path.GetFullPath(path);

    public bool Exists => Directory.Exists(FullPath);

    public string PathOf(string name) => Path.Combine(FullPath, name);

    public bool Has(string name) => File.Exists(PathOf(name));

    /// <summary>The parsed file, or null when it does not exist. Throws <see cref="JsonException"/> when it is not JSON.</summary>
    public JsonNode? Read(string name) => Has(name) ? JsonNode.Parse(File.ReadAllText(PathOf(name))) : null;

    /// <summary>Like <see cref="Read"/>, but a file that is not JSON counts as missing.</summary>
    public JsonNode? ReadOrNull(string name)
    {
        try
        {
            return Read(name);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Write(string name, JsonNode content) => WriteText(name, content.ToJsonString(WriteOptions) + "\n");

    public void WriteText(string name, string content)
    {
        var target = PathOf(name);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + ".tmp";
        File.WriteAllText(temporary, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporary, target, overwrite: true);
    }

    /// <summary>Deletes the files an earlier run wrote here, so a new run never mixes with an old one.</summary>
    public void ClearRun()
    {
        foreach (var name in OwnFiles.Where(Has))
        {
            File.Delete(PathOf(name));
        }
    }

    /// <summary>Sets the Manifest entry of <paramref name="stage"/>, or removes it when <paramref name="entry"/> is null.</summary>
    public void RecordStage(string stage, JsonObject? entry) =>
        Write(ManifestFile, Manifest.With(ReadOrNull(ManifestFile), stage, entry));
}

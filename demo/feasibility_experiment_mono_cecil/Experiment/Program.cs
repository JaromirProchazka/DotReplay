using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotReplay.Experiment;
using Mono.Cecil;

// Usage: cecil-experiment --assembly <dll> --method "<Type.Method(types)>" [--fields Type.field,...]
//                         --output-dir <Session directory> [--args <application arguments>...]
try
{
    var options = Options.Parse(args);
    var createdAt = DateTimeOffset.UtcNow;
    Directory.CreateDirectory(options.OutputDirectory);

    Console.WriteLine("Instrumenting the Target application...");
    var instrumented = Instrument(options);

    Console.WriteLine("Running the instrumented Target application...");
    var recordingPath = Path.Combine(options.OutputDirectory, "recording.json");
    RunTargetApplication(instrumented.Assembly, options.ApplicationArguments, recordingPath);

    var recording = JsonNode.Parse(File.ReadAllText(recordingPath))!.AsObject();
    var captured = instrumented.Target.Captured();
    var trace = Adapter.Trace(recording, captured);
    var run = Adapter.Run(options.Method.Text, captured, trace, createdAt);
    WriteAtomically(Path.Combine(options.OutputDirectory, "trace.json"), trace);
    WriteAtomically(Path.Combine(options.OutputDirectory, "run.json"), run);
    Console.WriteLine($"Analysis completed. Trace at: {Path.Combine(options.OutputDirectory, "trace.json")}");
    return 0;
}
catch (ExperimentError error)
{
    Console.Error.WriteLine($"Error: {error.Message}");
    return 1;
}

// Copies the Target application's folder to <output-dir>/instrumented, rewrites the Target assembly there
// and deploys the Recorder next to it.
static (string Assembly, Target Target) Instrument(Options options)
{
    var sourceDirectory = Path.GetDirectoryName(options.Assembly)!;
    var directory = Path.Combine(options.OutputDirectory, "instrumented");
    Directory.CreateDirectory(directory);
    foreach (var file in Directory.GetFiles(sourceDirectory).Where(IsDeployed))
    {
        File.Copy(file, Path.Combine(directory, Path.GetFileName(file)), overwrite: true);
    }

    var recorderPath = Path.Combine(AppContext.BaseDirectory, "DotReplay.Recorder.dll");
    var assembly = Path.Combine(directory, Path.GetFileName(options.Assembly));
    using var module = ModuleDefinition.ReadModule(options.Assembly);
    using var recorder = ModuleDefinition.ReadModule(recorderPath);
    var target = Target.Find(module, options.Method, options.Fields);
    Weaver.Weave(target, recorder);
    module.Write(assembly);
    File.Copy(recorderPath, Path.Combine(directory, Path.GetFileName(recorderPath)), overwrite: true);
    return (assembly, target);

    // The original assembly is replaced by the rewritten one, and its .pdb no longer matches it.
    // The .deps.json is left out: the host only loads the assemblies it lists, and it does not list the Recorder.
    bool IsDeployed(string file) =>
        !string.Equals(Path.GetFileName(file), Path.GetFileName(options.Assembly), StringComparison.OrdinalIgnoreCase)
        && !file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
        && !file.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase);
}

static void RunTargetApplication(string assembly, IReadOnlyList<string> arguments, string recordingPath)
{
    var start = new ProcessStartInfo("dotnet") { ArgumentList = { assembly }, UseShellExecute = false };
    foreach (var argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    start.Environment["DOTREPLAY_RECORDING"] = recordingPath;
    File.Delete(recordingPath);
    using var process = Process.Start(start) ?? throw new ExperimentError("The Target application could not be started.");
    if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
    {
        process.Kill(entireProcessTree: true);
        throw new ExperimentError("The Target application did not finish within 2 minutes.");
    }

    if (process.ExitCode != 0 || !File.Exists(recordingPath))
    {
        throw new ExperimentError($"The Target application exited with code {process.ExitCode} and recording {(File.Exists(recordingPath) ? "written" : "missing")}.");
    }
}

static void WriteAtomically(string path, JsonNode content)
{
    var temporary = path + ".tmp";
    File.WriteAllText(temporary, content.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    File.Move(temporary, path, overwrite: true);
}

sealed record Options(MethodSpec Method, string Assembly, IReadOnlyList<string> Fields, string OutputDirectory, IReadOnlyList<string> ApplicationArguments)
{
    public static Options Parse(string[] args)
    {
        string? assembly = null, method = null, output = null;
        var fields = new List<string>();
        var applicationArguments = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--assembly":
                    assembly = Value(args, ++i);
                    break;
                case "--method":
                    method = Value(args, ++i);
                    break;
                case "--fields":
                    fields.AddRange(Value(args, ++i).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--output-dir":
                    output = Value(args, ++i);
                    break;
                case "--args":
                    applicationArguments.AddRange(args[(i + 1)..]);
                    i = args.Length;
                    break;
                default:
                    throw new ExperimentError($"Unknown option {args[i]}.");
            }
        }

        assembly = Path.GetFullPath(assembly ?? throw new ExperimentError("--assembly is required."));
        if (!File.Exists(assembly))
        {
            throw new ExperimentError($"{assembly} does not exist.");
        }

        return new Options(
            MethodSpec.Parse(method ?? throw new ExperimentError("--method is required.")),
            assembly,
            fields,
            Path.GetFullPath(output ?? throw new ExperimentError("--output-dir is required.")),
            applicationArguments);
    }

    static string Value(string[] args, int index) =>
        index < args.Length ? args[index] : throw new ExperimentError($"{args[index - 1]} needs a value.");
}

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotReplay.Experiment;
using Mono.Cecil;

// Usage:
//   sharpdetect-experiment analyze --assembly <dll> --methods "<Type.Method(types)>;..." --scenario <args>
//                                  --plugin <Deadlock|FastTrack> --output-dir <dir>
//   sharpdetect-experiment generate-tests --analysis <dir>/analysis.json --output-dir <dir>
try
{
    var options = Options.Parse(args);
    switch (options.Command)
    {
        case "analyze":
            Analyze(options);
            break;
        case "generate-tests":
            GenerateTests(options);
            break;
        default:
            throw new ExperimentError($"Unknown command {options.Command}; use analyze or generate-tests.");
    }

    return 0;
}
catch (ExperimentError error)
{
    Console.Error.WriteLine($"Error: {error.Message}");
    return 1;
}

static void Analyze(Options options)
{
    var assembly = Path.GetFullPath(options.Required("--assembly"));
    if (!File.Exists(assembly))
    {
        throw new ExperimentError($"{assembly} does not exist.");
    }

    var outputDirectory = Path.GetFullPath(options.Required("--output-dir"));
    var plugin = options.Required("--plugin");
    var methods = options.Required("--methods").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(MethodSpec.Parse).ToList();
    var createdAt = DateTimeOffset.UtcNow;
    Directory.CreateDirectory(outputDirectory);

    Console.WriteLine("Instrumenting the Target application with Mono.Cecil...");
    var (instrumented, targets) = Instrument(assembly, methods, outputDirectory);

    Console.WriteLine($"Running the instrumented Target application under SharpDetect ({plugin})...");
    var recordingPath = Path.Combine(outputDirectory, "recording.json");
    var reportPath = Path.Combine(outputDirectory, "sharpdetect-report.html");
    RunSharpDetect(instrumented, options.Value("--scenario") ?? "", plugin, recordingPath, reportPath, outputDirectory);

    var recording = JsonNode.Parse(File.ReadAllText(recordingPath))!.AsObject();
    var findings = SharpDetectReport.Parse(File.ReadAllText(reportPath));
    var analysis = AnalysisFile.Build(assembly, targets, recording, findings, $"SharpDetect 2.1.4 {plugin}", createdAt);
    WriteAtomically(Path.Combine(outputDirectory, "analysis.json"), analysis.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Analysis completed with {findings.Count} finding(s): {Path.Combine(outputDirectory, "analysis.json")}");
}

static void GenerateTests(Options options)
{
    var analysisPath = Path.GetFullPath(options.Required("--analysis"));
    var outputDirectory = Path.GetFullPath(options.Required("--output-dir"));
    var analysis = JsonNode.Parse(File.ReadAllText(analysisPath))!.AsObject();
    Directory.CreateDirectory(outputDirectory);
    foreach (var (file, content) in TestGenerator.Generate(analysis))
    {
        WriteAtomically(Path.Combine(outputDirectory, file), content);
    }

    Console.WriteLine($"Tests generated: {Path.Combine(outputDirectory, TestGenerator.TestsFile)}");
}

// Copies the Target application's folder to <output-dir>/instrumented, weaves every Target method with its own
// target id, and deploys the recorder next to it (the same deployment as in the Mono.Cecil experiment).
static (string Assembly, IReadOnlyList<TargetMethod> Targets) Instrument(string assembly, IReadOnlyList<MethodSpec> methods, string outputDirectory)
{
    var directory = Path.Combine(outputDirectory, "instrumented");
    Directory.CreateDirectory(directory);
    foreach (var file in Directory.GetFiles(Path.GetDirectoryName(assembly)!).Where(file => IsDeployed(file, assembly)))
    {
        File.Copy(file, Path.Combine(directory, Path.GetFileName(file)), overwrite: true);
    }

    var recorderPath = Path.Combine(AppContext.BaseDirectory, "DotReplay.ThreadRecorder.dll");
    var instrumented = Path.Combine(directory, Path.GetFileName(assembly));
    using var module = ModuleDefinition.ReadModule(assembly);
    using var recorder = ModuleDefinition.ReadModule(recorderPath);
    var targets = new List<TargetMethod>();
    foreach (var (spec, id) in methods.Select((spec, id) => (spec, id)))
    {
        var target = Target.Find(module, spec, []);
        // The full name is taken before weaving; weaving does not change it.
        targets.Add(new TargetMethod(spec.Text, target.Method.FullName, target.Captured()));
        ThreadWeaver.Weave(target, id, recorder);
    }

    module.Write(instrumented);
    File.Copy(recorderPath, Path.Combine(directory, Path.GetFileName(recorderPath)), overwrite: true);
    return (instrumented, targets);
}

// The .deps.json is left out because the host only loads the assemblies it lists; the .pdb no longer matches.
static bool IsDeployed(string file, string assembly) =>
    !string.Equals(Path.GetFileName(file), Path.GetFileName(assembly), StringComparison.OrdinalIgnoreCase)
    && !file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
    && !file.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase);

static void RunSharpDetect(string assembly, string arguments, string plugin, string recordingPath, string reportPath, string outputDirectory)
{
    var configuration = new JsonObject
    {
        // SharpDetect does not instrument the recorder (DotReplay.*), so the recorder's own field accesses are not analysed.
        ["SkipInstrumentationForAssemblies"] = new JsonArray("System.", "Microsoft.", "DotReplay."),
    };
    if (plugin == "FastTrack")
    {
        configuration["EnableStackTraceCollection"] = true;
    }

    var descriptor = new JsonObject
    {
        ["Target"] = new JsonObject
        {
            ["Path"] = assembly,
            ["Args"] = arguments,
            ["AdditionalEnvironmentVariables"] = new JsonArray(new JsonObject { ["Key"] = "DOTREPLAY_RECORDING", ["Value"] = recordingPath }),
            ["RedirectInputOutput"] = new JsonObject { ["SingleConsoleMode"] = true },
        },
        ["Analysis"] = new JsonObject
        {
            ["Configuration"] = configuration,
            ["PluginName"] = plugin,
            ["RenderReport"] = true,
            ["ReportsFolder"] = Path.GetDirectoryName(reportPath),
            ["ReportFileName"] = Path.GetFileName(reportPath),
        },
    };
    var descriptorPath = Path.Combine(outputDirectory, "AnalysisDescriptor.json");
    File.WriteAllText(descriptorPath, descriptor.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    File.Delete(recordingPath);
    File.Delete(reportPath);

    // SharpDetect is a local tool of this experiment (dotnet-tools.json), so it runs from the experiment folder.
    var toolDirectory = ToolDirectory();
    Run(toolDirectory, ["tool", "restore"], Path.Combine(outputDirectory, "tool-restore.log"));
    var exitCode = Run(toolDirectory, ["tool", "run", "sharpdetect", "run", descriptorPath], Path.Combine(outputDirectory, "sharpdetect.log"));

    // SharpDetect 2.1.4 exits with 1 when it found issues, so the exit code alone does not tell a failure apart.
    if (!File.Exists(reportPath) || !File.Exists(recordingPath))
    {
        throw new ExperimentError($"SharpDetect exited with code {exitCode} without writing the report and the recording; see sharpdetect.log.");
    }
}

static int Run(string workingDirectory, IReadOnlyList<string> arguments, string logPath)
{
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (var argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start) ?? throw new ExperimentError("dotnet could not be started.");
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(TimeSpan.FromMinutes(3)))
    {
        process.Kill(entireProcessTree: true);
        throw new ExperimentError($"dotnet {string.Join(' ', arguments)} did not finish within 3 minutes.");
    }

    File.WriteAllText(logPath, output.Result + error.Result);
    return process.ExitCode;
}

static string ToolDirectory()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "dotnet-tools.json")))
        {
            return directory.FullName;
        }
    }

    throw new ExperimentError("dotnet-tools.json of the experiment was not found above the experiment's binaries.");
}

static void WriteAtomically(string path, string content)
{
    var temporary = path + ".tmp";
    File.WriteAllText(temporary, content);
    File.Move(temporary, path, overwrite: true);
}

sealed record Options(string Command, IReadOnlyDictionary<string, string> Values)
{
    public static Options Parse(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ExperimentError("A command is required: analyze or generate-tests.");
        }

        var values = new Dictionary<string, string>();
        for (var i = 1; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                throw new ExperimentError($"Expected an option with a value at '{args[i]}'.");
            }

            values[args[i]] = args[i + 1];
        }

        return new Options(args[0], values);
    }

    public string? Value(string option) => Values.GetValueOrDefault(option);

    public string Required(string option) => Value(option) ?? throw new ExperimentError($"{option} is required.");
}

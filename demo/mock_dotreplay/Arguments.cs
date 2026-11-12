using System.Text.Json;
using System.Text.Json.Nodes;

namespace DotReplay.Mock;

/// <summary>Options of the analysis stage and the full run. Names follow the Java Auto-debugger CLI.</summary>
sealed record AnalysisOptions(
    string Assembly,
    string Source,
    string Method,
    string OutputDirectory,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<string> Fields,
    IReadOnlyList<string> ApplicationArguments,
    string Language,
    string TestStrategy,
    string TraceMode,
    string Strategy,
    bool StaticMethod,
    bool VoidMethod,
    int? MaxArgumentCombinations);

sealed record GenerateTestsOptions(string RunDirectory, int? MaxArgumentCombinations);

sealed record RunTestsOptions(string RunDirectory, IReadOnlyList<string> Tests);

static class ArgumentParser
{
    static readonly Dictionary<string, string> LongNames = new()
    {
        ["-m"] = "--method",
        ["-p"] = "--parameters",
        ["-f"] = "--fields",
        ["-s"] = "--source",
        ["-o"] = "--output-dir",
        ["-a"] = "--args",
        ["-l"] = "--language",
        ["-t"] = "--test-strategy",
        ["-r"] = "--trace-mode",
    };

    static readonly HashSet<string> FlagOptions = ["--static-method", "--void-method"];

    static readonly Dictionary<string, char> ListSeparators = new()
    {
        ["--parameters"] = ',',
        ["--fields"] = ',',
        ["--args"] = ' ',
    };

    static readonly string[] AnalysisOptionNames =
    [
        "--assembly", "--source", "--method", "--output-dir", "--parameters", "--fields", "--args",
        "--language", "--test-strategy", "--trace-mode", "--strategy", "--static-method", "--void-method",
    ];

    const string MaxArgumentCombinations = "--max-argument-combinations";

    public static AnalysisOptions ParseAnalysis(IReadOnlyList<string> args, bool fullRun)
    {
        var allowed = new HashSet<string>(AnalysisOptionNames);
        if (fullRun)
        {
            allowed.Add(MaxArgumentCombinations);
        }

        var values = Parse(WithRunConfiguration(args), allowed);
        var assembly = Required(values, "--assembly");
        var source = Required(values, "--source");
        var method = Required(values, "--method");
        var outputDirectory = Required(values, "--output-dir");
        var parameters = List(values, "--parameters");
        var fields = List(values, "--fields");
        if (parameters.Count == 0 && fields.Count == 0)
        {
            throw StageError.Unsupported(
                "No values to capture. Specify at least one parameter (--parameters) or field (--fields).");
        }

        return new AnalysisOptions(
            assembly,
            source,
            method,
            outputDirectory,
            parameters,
            fields,
            List(values, "--args"),
            Single(values, "--language") ?? "dotnet",
            Single(values, "--test-strategy") ?? "trace-based-basic",
            Single(values, "--trace-mode") ?? "naive",
            Single(values, "--strategy") ?? "record",
            Flag(values, "--static-method"),
            Flag(values, "--void-method"),
            PositiveInt(values, MaxArgumentCombinations));
    }

    public static GenerateTestsOptions ParseGenerateTests(IReadOnlyList<string> args)
    {
        var values = Parse(args, new HashSet<string> { "--run-dir", MaxArgumentCombinations });
        return new GenerateTestsOptions(Required(values, "--run-dir"), PositiveInt(values, MaxArgumentCombinations));
    }

    public static RunTestsOptions ParseRunTests(IReadOnlyList<string> args)
    {
        var values = Parse(args, new HashSet<string> { "--run-dir", "--test" });
        return new RunTestsOptions(Required(values, "--run-dir"), List(values, "--test"));
    }

    /// <summary>Values by long option name, in command-line order. Flags get the value "true".</summary>
    static Dictionary<string, List<string>> Parse(IReadOnlyList<string> tokens, IReadOnlySet<string> allowed)
    {
        var values = new Dictionary<string, List<string>>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var (name, inlineValue) = SplitOption(tokens[i]);
            if (!allowed.Contains(name))
            {
                throw StageError.Unsupported($"Unknown option: {tokens[i]}");
            }

            if (!values.TryGetValue(name, out var list))
            {
                list = [];
                values[name] = list;
            }

            if (FlagOptions.Contains(name))
            {
                list.Add(inlineValue ?? "true");
                continue;
            }

            var value = inlineValue
                ?? (i + 1 < tokens.Count ? tokens[++i] : throw StageError.Unsupported($"Missing value for option: {name}"));
            if (ListSeparators.TryGetValue(name, out var separator))
            {
                list.AddRange(value.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            else
            {
                list.Add(value);
            }
        }

        return values;
    }

    static (string Name, string? InlineValue) SplitOption(string token)
    {
        var equals = token.IndexOf('=');
        var (name, inlineValue) = token.StartsWith("--") && equals > 0
            ? (token[..equals], token[(equals + 1)..])
            : (token, null);
        return (LongNames.GetValueOrDefault(name, name), inlineValue);
    }

    /// <summary>
    /// Replaces --config with the options from the JSON Run configuration (use case flow 1.a).
    /// Its options come first, so options on the command line override them.
    /// </summary>
    static IReadOnlyList<string> WithRunConfiguration(IReadOnlyList<string> args)
    {
        var fromConfig = new List<string>();
        var rest = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var (name, inlineValue) = SplitOption(args[i]);
            if (name != "--config")
            {
                rest.Add(args[i]);
                continue;
            }

            var path = inlineValue
                ?? (i + 1 < args.Count ? args[++i] : throw StageError.Unsupported("Missing value for option: --config"));
            fromConfig.AddRange(RunConfiguration.ToOptions(path));
        }

        return [.. fromConfig, .. rest];
    }

    static string Required(Dictionary<string, List<string>> values, string name) =>
        Single(values, name) ?? throw StageError.Unsupported($"Missing required option: {name}");

    static string? Single(Dictionary<string, List<string>> values, string name) =>
        values.TryGetValue(name, out var list) && list.Count > 0 ? list[^1] : null;

    static IReadOnlyList<string> List(Dictionary<string, List<string>> values, string name) =>
        values.TryGetValue(name, out var list) ? list : [];

    static bool Flag(Dictionary<string, List<string>> values, string name) =>
        string.Equals(Single(values, name), "true", StringComparison.OrdinalIgnoreCase);

    static int? PositiveInt(Dictionary<string, List<string>> values, string name) =>
        Single(values, name) switch
        {
            null => null,
            var text when int.TryParse(text, out var count) && count > 0 => count,
            var text => throw StageError.Unsupported($"{name} must be a positive integer, not '{text}'"),
        };
}

/// <summary>The JSON Run configuration file. Its keys are the long option names without the dashes.</summary>
static class RunConfiguration
{
    static readonly string[] RequiredFields = ["assembly", "method"];

    public static IReadOnlyList<string> ToOptions(string path)
    {
        if (!File.Exists(path))
        {
            throw StageError.Unsupported($"Run configuration file not found: {path}");
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path));
        }
        catch (JsonException e)
        {
            throw new StageError(ErrorCodes.InvalidJson, $"The Run configuration file is not valid JSON: {e.Message}");
        }

        if (root is not JsonObject configuration)
        {
            throw new StageError(ErrorCodes.InvalidJson, "The Run configuration file must contain a JSON object.");
        }

        var missing = RequiredFields.Where(field => string.IsNullOrEmpty(Json.Str(configuration[field]))).ToList();
        if (missing.Count > 0)
        {
            throw new StageError(ErrorCodes.MissingRequiredFields,
                $"The Run configuration file lacks required fields: {string.Join(", ", missing)}");
        }

        return configuration.SelectMany(field => ToOptions(field.Key, field.Value)).ToList();
    }

    static IEnumerable<string> ToOptions(string key, JsonNode? value)
    {
        var option = "--" + key;
        return value switch
        {
            JsonArray items => items.SelectMany(item => new[] { option, ScalarText(key, item) }),
            JsonValue flag when flag.GetValueKind() == JsonValueKind.True => [option],
            JsonValue flag when flag.GetValueKind() == JsonValueKind.False => [],
            _ => [option, ScalarText(key, value)],
        };
    }

    static string ScalarText(string key, JsonNode? value) => value?.GetValueKind() switch
    {
        JsonValueKind.String => Json.Str(value)!,
        JsonValueKind.Number => value.ToJsonString(),
        _ => throw StageError.Unsupported($"Unsupported value of '{key}' in the Run configuration file."),
    };
}

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace DotReplay.Mock;

/// <summary>The analysis output read back by test generation: per call, the captured values in captureId order.</summary>
sealed record AnalysisOutput(string Signature, IReadOnlyList<int> CaptureIds, IReadOnlyList<IReadOnlyList<int>> Calls);

/// <summary>One generated test: the arguments it calls Add with and the recorded calls it reproduces.</summary>
sealed record GeneratedTest(string TestId, string MethodName, IReadOnlyList<int> Arguments, IReadOnlyList<int> CallIndices);

sealed record GenerationResult(IReadOnlyList<GeneratedTest> Tests, long Combinations)
{
    public bool LimitReached => Tests.Count < Combinations;
}

/// <summary>Analysis use case (doc/use-cases/analysis.md).</summary>
static class AnalysisStage
{
    static readonly string[] OptionalStrategies = ["interleaving", "fuzzing", "sharpdetect"];

    /// <summary>Checks the options in the order of the use case and throws the first failure.</summary>
    public static void Validate(AnalysisOptions options, Func<string, bool> fileExists, Func<string, bool> directoryExists)
    {
        // Step 1: input that the mock does not simulate.
        if (options.Fields.Count > 0)
        {
            throw StageError.Unsupported("The mock does not capture fields: the Calculator has none.");
        }

        if (options.StaticMethod || options.VoidMethod)
        {
            throw StageError.Unsupported("Calculator.Add is neither static nor void.");
        }

        RequireDefault("--language", options.Language, "dotnet");
        RequireDefault("--test-strategy", options.TestStrategy, "trace-based-basic");
        RequireDefault("--trace-mode", options.TraceMode, "naive");
        var parameters = options.Parameters.Select(ParseParameter).ToList();

        // Step 2: the Target application.
        if (!fileExists(options.Assembly))
        {
            throw new StageError(ErrorCodes.MissingTargetApplication, $"Target application not found: {options.Assembly}");
        }

        if (!string.Equals(Path.GetFileName(options.Assembly), CalculatorTarget.AssemblyFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw StageError.Unsupported($"The mock only simulates {CalculatorTarget.AssemblyFileName}, not {options.Assembly}");
        }

        if (!directoryExists(options.Source))
        {
            throw new StageError(ErrorCodes.MissingTargetApplication, $"Source code path not found: {options.Source}");
        }

        if (!fileExists(Path.Combine(options.Source, CalculatorTarget.SourceFileName)))
        {
            throw StageError.Unsupported($"The mock only simulates the Calculator source, which has no {CalculatorTarget.SourceFileName} in {options.Source}");
        }

        // Step 3: the Target method and its captured arguments.
        if (!options.Method.Contains('.') || !options.Method.Contains('(') || !options.Method.TrimEnd().EndsWith(')'))
        {
            throw StageError.Unsupported(
                $"Invalid method reference format: '{options.Method}'. Expected format: Namespace.Class.Method(paramTypes)");
        }

        if (CalculatorTarget.IsOtherMethod(options.Method))
        {
            throw StageError.Unsupported($"The mock only simulates Com.Example.Calculator.Add(int,int), not {options.Method}");
        }

        if (!CalculatorTarget.IsAdd(options.Method))
        {
            throw new StageError(ErrorCodes.MissingTargetMethods,
                $"Target methods not found in {CalculatorTarget.AssemblyFileName}: {options.Method}");
        }

        var missing = parameters.Where(p => !CalculatorTarget.HasParameter(p.Slot, p.Type)).ToList();
        if (missing.Count > 0)
        {
            throw new StageError(ErrorCodes.MissingTargetMethods,
                $"{options.Method} has no parameters {string.Join(", ", missing.Select(p => $"{p.Slot}:{p.Type}"))}");
        }

        if (!parameters.Select(p => p.Slot).Order().SequenceEqual([0, 1]))
        {
            throw StageError.Unsupported("The mock captures both arguments of Calculator.Add: use --parameters 0:int,1:int");
        }

        // Step 4: the Strategy.
        if (OptionalStrategies.Contains(options.Strategy))
        {
            throw StageError.Unsupported($"The mock only simulates the record Strategy, not {options.Strategy}");
        }

        if (options.Strategy != "record")
        {
            throw new StageError(ErrorCodes.InvalidStrategy, $"Unknown analysis Strategy: '{options.Strategy}'");
        }
    }

    public static JsonObject RunJson(AnalysisOptions options, DateTimeOffset createdAt) => new()
    {
        ["schemaVersion"] = Json.SchemaVersion,
        ["language"] = options.Language.ToLowerInvariant(),
        ["target"] = new JsonObject { ["signature"] = options.Method },
        ["captured"] = new JsonArray(CalculatorTarget.ArgumentNames.Select((name, captureId) => (JsonNode)new JsonObject
        {
            ["captureId"] = captureId,
            ["kind"] = "argument",
            ["name"] = name,
            ["type"] = CalculatorTarget.Int32Type,
        }).ToArray()),
        ["typeNames"] = new JsonObject { [CalculatorTarget.Int32Type] = "int" },
        ["traceMode"] = options.TraceMode.ToLowerInvariant(),
        ["recorded"] = new JsonObject { ["exit"] = true, ["outcome"] = true },
        ["counts"] = new JsonObject { ["calls"] = CalculatorTarget.RecordedCalls.Count },
        ["status"] = "ok",
        ["createdAt"] = Json.Timestamp(createdAt),
    };

    /// <summary>run.json of a failed analysis: nothing was captured.</summary>
    public static JsonObject FailedRunJson(AnalysisOptions options, DateTimeOffset createdAt, StageError error) => new()
    {
        ["schemaVersion"] = Json.SchemaVersion,
        ["language"] = options.Language.ToLowerInvariant(),
        ["target"] = new JsonObject { ["signature"] = options.Method },
        ["captured"] = new JsonArray(),
        ["traceMode"] = options.TraceMode.ToLowerInvariant(),
        ["status"] = "failed",
        ["error"] = Json.ErrorText(error),
        ["createdAt"] = Json.Timestamp(createdAt),
    };

    /// <summary>The analysis output file: every recorded call, with entry and exit snapshots.</summary>
    public static JsonObject TraceJson() => new()
    {
        ["schemaVersion"] = Json.SchemaVersion,
        ["calls"] = new JsonArray(CalculatorTarget.RecordedCalls.Select((values, callIndex) => (JsonNode)new JsonObject
        {
            ["callIndex"] = callIndex,
            ["entry"] = Snapshot(values),
            // Add does not change its arguments, so the exit snapshot equals the entry snapshot.
            ["exit"] = Snapshot(values),
            ["outcome"] = new JsonObject { ["kind"] = "returned" },
        }).ToArray()),
    };

    static JsonObject Snapshot(IReadOnlyList<int> values) => new(values.Select((value, captureId) =>
        KeyValuePair.Create(captureId.ToString(CultureInfo.InvariantCulture), (JsonNode?)new JsonObject
        {
            ["kind"] = "primitive",
            ["type"] = CalculatorTarget.Int32Type,
            ["value"] = value,
        })));

    static void RequireDefault(string option, string value, string supported)
    {
        if (!string.Equals(value, supported, StringComparison.OrdinalIgnoreCase))
        {
            throw StageError.Unsupported($"The mock only supports {option} {supported}, not {value}");
        }
    }

    static (int Slot, string Type) ParseParameter(string text)
    {
        var parts = text.Split(':', 2);
        return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var slot) && parts[1].Length > 0
            ? (slot, parts[1])
            : throw StageError.Unsupported($"Invalid parameter format: '{text}'. Expected format: slot:type (e.g., 0:int)");
    }
}

/// <summary>Test generation use case (doc/use-cases/test_generation.md), with Exhaustive combinatorial testing (R31).</summary>
static class TestGenerationStage
{
    public static AnalysisOutput ReadAnalysisOutput(JsonNode? runNode, JsonNode? traceNode)
    {
        if (Json.Int(Json.Get(runNode, "schemaVersion")) != Json.SchemaVersion
            || Json.Str(Json.Get(Json.Get(runNode, "target"), "signature")) is not { } signature)
        {
            throw Invalid("run.json is not a version 1 run summary.");
        }

        if (!CalculatorTarget.IsAdd(signature))
        {
            throw StageError.Unsupported($"The mock only generates tests for Calculator.Add, not for {signature}");
        }

        if (Json.Str(Json.Get(runNode, "status")) != "ok")
        {
            throw Invalid($"The analysis did not succeed: {Json.Str(Json.Get(runNode, "error")) ?? "run.json status is not ok"}");
        }

        if (Json.Get(runNode, "captured") is not JsonArray captured)
        {
            throw Invalid("run.json has no captured list.");
        }

        var captureIds = captured
            .Select(c => Json.Int(Json.Get(c, "captureId")) ?? throw Invalid("run.json has a captured entry without a captureId."))
            .ToList();

        if (Json.Int(Json.Get(traceNode, "schemaVersion")) != Json.SchemaVersion || Json.Get(traceNode, "calls") is not JsonArray calls)
        {
            throw Invalid("trace.json is not a version 1 trace.");
        }

        return new AnalysisOutput(signature, captureIds, calls.Select((call, i) => ReadCall(call, i, captureIds)).ToList());
    }

    /// <summary>
    /// One test per combination of the distinct recorded values of each captured value, the first captured
    /// value varying slowest, up to <paramref name="maxArgumentCombinations"/> tests.
    /// </summary>
    public static GenerationResult Generate(AnalysisOutput analysis, int? maxArgumentCombinations)
    {
        var distinctValues = analysis.CaptureIds
            .Select((_, position) => analysis.Calls.Select(call => call[position]).Distinct().ToList())
            .ToList();
        var combinationCount = distinctValues.Aggregate(1L, (count, values) => count * values.Count);

        IEnumerable<IReadOnlyList<int>> combinations = [Array.Empty<int>()];
        foreach (var values in distinctValues)
        {
            combinations = combinations.SelectMany(prefix => values.Select(value => (IReadOnlyList<int>)[.. prefix, value]));
        }

        var tests = combinations
            .Take(maxArgumentCombinations ?? int.MaxValue)
            .Select((arguments, i) =>
            {
                var methodName = CalculatorTarget.TestMethodPrefix + (i + 1).ToString(CultureInfo.InvariantCulture);
                var callIndices = analysis.Calls
                    .Select((call, callIndex) => (call, callIndex))
                    .Where(c => c.call.SequenceEqual(arguments))
                    .Select(c => c.callIndex)
                    .ToList();
                return new GeneratedTest($"{CalculatorTarget.TestClass}#{methodName}", methodName, arguments, callIndices);
            })
            .ToList();

        return new GenerationResult(tests, combinationCount);
    }

    public static JsonObject TestsJson(GenerationResult result) => new()
    {
        ["schemaVersion"] = Json.SchemaVersion,
        ["tests"] = new JsonArray(result.Tests.Select(test => (JsonNode)new JsonObject
        {
            ["testId"] = test.TestId,
            ["callIndices"] = new JsonArray(test.CallIndices.Select(i => (JsonNode)i).ToArray()),
        }).ToArray()),
    };

    /// <summary>The generated xUnit source. It is private to the backend and not part of the contract.</summary>
    public static string TestSource(GenerationResult result, string signature)
    {
        var source = new StringBuilder()
            .AppendLine("// <auto-generated>")
            .AppendLine($"// Generated by the dotreplay mock for {signature} (source: {CalculatorTarget.SourceFileName}).")
            .AppendLine("// Each test calls the Target method with one combination of the recorded Argument values.")
            .AppendLine("// </auto-generated>")
            .AppendLine("using Xunit;")
            .AppendLine()
            .AppendLine("namespace Com.Example;")
            .AppendLine()
            .AppendLine("public class CalculatorTest")
            .AppendLine("{");

        foreach (var test in result.Tests)
        {
            var origin = test.CallIndices.Count == 0
                ? "Synthetic: this input combination was not recorded."
                : $"Reproduces recorded call {string.Join(", ", test.CallIndices)}.";
            source
                .AppendLine($"    // {origin}")
                .AppendLine("    [Fact]")
                .AppendLine($"    public void {test.MethodName}()")
                .AppendLine("    {")
                .AppendLine("        var calculator = new Calculator();")
                .AppendLine()
                .AppendLine("        // The recorded calls returned, so the test passes when Add returns without an exception.")
                .AppendLine($"        calculator.Add({string.Join(", ", test.Arguments)});")
                .AppendLine("    }")
                .AppendLine();
        }

        return source.ToString().TrimEnd() + Environment.NewLine + "}" + Environment.NewLine;
    }

    /// <summary>run.json after a successful test generation.</summary>
    public static JsonObject WithTestCount(JsonObject run, int testCount)
    {
        var updated = (JsonObject)run.DeepClone();
        var counts = updated["counts"] as JsonObject ?? new JsonObject();
        updated["counts"] = counts;
        counts["tests"] = testCount;
        return updated;
    }

    /// <summary>run.json after a failed test generation: the contract's status covers test generation too.</summary>
    public static JsonObject WithFailure(JsonObject run, StageError error)
    {
        var updated = (JsonObject)run.DeepClone();
        updated["status"] = "failed";
        updated["error"] = Json.ErrorText(error);
        return updated;
    }

    static IReadOnlyList<int> ReadCall(JsonNode? call, int index, IReadOnlyList<int> captureIds)
    {
        if (Json.Int(Json.Get(call, "callIndex")) != index || Json.Get(call, "entry") is not JsonObject entry)
        {
            throw Invalid($"trace.json call {index} has no callIndex {index} or no entry snapshot.");
        }

        return captureIds.Select(captureId =>
        {
            var value = entry[captureId.ToString(CultureInfo.InvariantCulture)];
            return Json.Str(Json.Get(value, "kind")) == "primitive" && Json.Int(Json.Get(value, "value")) is { } number
                ? number
                : throw Invalid($"trace.json call {index} has no int value for captureId {captureId}.");
        }).ToList();
    }

    static StageError Invalid(string message) => new(ErrorCodes.InvalidAnalysisOutput, message);
}

/// <summary>Test running use case (doc/use-cases/tests_run.md) and the contract's run-tests command.</summary>
static class TestRunningStage
{
    public static JsonObject EmptyResults() => new()
    {
        ["schemaVersion"] = Json.SchemaVersion,
        ["results"] = new JsonArray(),
    };

    public static IReadOnlyList<string> ReadTestIds(JsonNode? testsNode)
    {
        if (Json.Int(Json.Get(testsNode, "schemaVersion")) != Json.SchemaVersion || Json.Get(testsNode, "tests") is not JsonArray tests)
        {
            throw new StageError(ErrorCodes.MissingTestSuite, "tests.json is not a version 1 Test suite.");
        }

        var testIds = tests
            .Select(test => Json.Str(Json.Get(test, "testId"))
                ?? throw new StageError(ErrorCodes.MissingTestSuite, "tests.json has a test without a testId."))
            .ToList();

        var foreign = testIds.FirstOrDefault(id => !id.StartsWith(CalculatorTarget.TestClass + "#", StringComparison.Ordinal));
        return foreign is null
            ? testIds
            : throw StageError.Unsupported($"The mock only runs Calculator tests, not {foreign}");
    }

    /// <summary>The List of selected tests; all tests when it is empty (R20).</summary>
    public static IReadOnlyList<string> Select(IReadOnlyList<string> testIds, IReadOnlyList<string> selected)
    {
        if (selected.Count == 0)
        {
            return testIds;
        }

        var unknown = selected.Where(id => !testIds.Contains(id)).Distinct().ToList();
        return unknown.Count == 0
            ? selected.Distinct().ToList()
            : throw new StageError(ErrorCodes.InvalidTestList, $"Tests not found in tests.json: {string.Join(", ", unknown)}");
    }

    /// <summary>
    /// results.json after running <paramref name="ran"/>: their results are replaced, the others are kept,
    /// and a previous error is removed. Every Calculator test passes, because Add never throws.
    /// </summary>
    public static JsonObject WithResults(JsonNode? previous, IReadOnlyList<string> testIds, IReadOnlyList<string> ran)
    {
        var kept = PreviousResults(previous);
        var results = testIds
            .Select(id => ran.Contains(id) ? Passed(id) : kept.GetValueOrDefault(id)?.DeepClone())
            .Where(result => result is not null)
            .ToArray();
        return new JsonObject
        {
            ["schemaVersion"] = Json.SchemaVersion,
            ["results"] = new JsonArray(results),
        };
    }

    /// <summary>results.json after a failed execution: the earlier results stay, with a top-level error.</summary>
    public static JsonObject WithError(JsonNode? previous, StageError error)
    {
        var updated = previous is JsonObject results && results["results"] is JsonArray
            ? (JsonObject)results.DeepClone()
            : EmptyResults();
        updated["error"] = new JsonObject { ["message"] = Json.ErrorText(error) };
        return updated;
    }

    static JsonNode Passed(string testId) => new JsonObject
    {
        ["testId"] = testId,
        ["status"] = "passed",
        ["durationMs"] = 1,
    };

    static Dictionary<string, JsonNode> PreviousResults(JsonNode? previous) =>
        (Json.Get(previous, "results") as JsonArray ?? new JsonArray())
            .OfType<JsonObject>()
            .Where(result => Json.Str(result["testId"]) is not null)
            .GroupBy(result => Json.Str(result["testId"])!)
            .ToDictionary(group => group.Key, group => (JsonNode)group.Last());
}

/// <summary>manifest.json: the status of each stage's output (doc/dictionary.md, Manifest).</summary>
static class Manifest
{
    public const string Analysis = "analysis";
    public const string TestGeneration = "testGeneration";
    public const string TestRunning = "testRunning";

    public static JsonObject Complete(DateTimeOffset at) => Entry("COMPLETE", at, null, null);

    public static JsonObject Partial(string message, DateTimeOffset at) => Entry("PARTIAL", at, null, message);

    public static JsonObject Failed(StageError error, DateTimeOffset at) => Entry("FAILED", at, error.Code, error.Message);

    /// <summary>The manifest with the entry of <paramref name="stage"/> replaced, or removed when <paramref name="entry"/> is null.</summary>
    public static JsonObject With(JsonNode? manifest, string stage, JsonObject? entry)
    {
        var updated = manifest is JsonObject existing && existing["stages"] is JsonObject
            ? (JsonObject)existing.DeepClone()
            : new JsonObject { ["schemaVersion"] = Json.SchemaVersion, ["stages"] = new JsonObject() };
        var stages = (JsonObject)updated["stages"]!;
        if (entry is null)
        {
            stages.Remove(stage);
        }
        else
        {
            stages[stage] = entry;
        }

        return updated;
    }

    static JsonObject Entry(string status, DateTimeOffset at, string? code, string? message)
    {
        var entry = new JsonObject { ["status"] = status };
        if (code is not null)
        {
            entry["code"] = code;
        }

        if (message is not null)
        {
            entry["message"] = message;
        }

        entry["completedAt"] = Json.Timestamp(at);
        return entry;
    }
}

static class Json
{
    public const int SchemaVersion = 1;

    public static JsonNode? Get(JsonNode? node, string key) => node is JsonObject obj ? obj[key] : null;

    public static string? Str(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    public static int? Int(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    /// <summary>ISO 8601 UTC time in whole seconds, as the Java backend writes createdAt.</summary>
    public static string Timestamp(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public static string ErrorText(StageError error) => $"{error.Code}: {error.Message}";
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;

namespace DotReplay.Tests.Infrastructure;

/// <summary>
/// The backend contract v1: the vendored schemas in Contract/schemas and the "Rules across files"
/// of the contract README, which the schemas cannot check.
/// </summary>
public static class Contract
{
    static readonly Lazy<IReadOnlyDictionary<string, JsonSchema>> Schemas = new(LoadSchemas);

    static readonly EvaluationOptions Evaluation = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
    };

    /// <summary>Asserts that every contract file in the Session directory matches its schema and the rules across files.</summary>
    public static void AssertValid(SessionDirectory session)
    {
        Assert.True(session.Has(SessionDirectory.RunFile), "run.json is missing: it marks the run directory.");
        var problems = SessionDirectory.ContractFiles
            .Where(session.Has)
            .SelectMany(file => SchemaProblems(file, File.ReadAllText(Path.Combine(session.Path, file))))
            .Concat(CrossFileProblems(session))
            .ToList();
        Assert.True(problems.Count == 0, "Contract violations:\n" + string.Join("\n", problems));
    }

    static IEnumerable<string> SchemaProblems(string file, string json)
    {
        var schema = Schemas.Value[Path.GetFileNameWithoutExtension(file)];
        using var document = JsonDocument.Parse(json);
        var result = schema.Evaluate(document.RootElement, Evaluation);
        return result.IsValid
            ? []
            : (result.Details ?? [])
                .Where(detail => detail.Errors is { Count: > 0 })
                .SelectMany(detail => detail.Errors!.Select(error => $"{file} {detail.InstanceLocation}: {error.Value}"))
                .DefaultIfEmpty($"{file} does not match its schema");
    }

    /// <summary>The contract's rules 1 to 7. Rule 8 is checked by the tests of failed runs.</summary>
    static IEnumerable<string> CrossFileProblems(SessionDirectory session)
    {
        var run = session.Read(SessionDirectory.RunFile);
        var captureIds = (run["captured"] as JsonArray ?? [])
            .Select(c => c!["captureId"]!.ToString())
            .ToHashSet();
        var recorded = run["recorded"] as JsonObject;
        var recordsExit = recorded?["exit"]?.GetValue<bool>() ?? true;
        var recordsOutcome = recorded?["outcome"]?.GetValue<bool>() ?? true;

        var calls = session.Has(SessionDirectory.TraceFile)
            ? session.Read(SessionDirectory.TraceFile)["calls"]!.AsArray()
            : null;
        if (calls is not null)
        {
            for (var i = 0; i < calls.Count; i++)
            {
                var call = calls[i]!.AsObject();
                if (call["callIndex"]!.GetValue<int>() != i)
                {
                    yield return $"Rule 1: calls[{i}].callIndex is {call["callIndex"]}";
                }

                foreach (var snapshot in new[] { call["entry"], call["exit"] }.OfType<JsonObject>())
                {
                    var keys = snapshot.Select(p => p.Key).ToHashSet();
                    if (!keys.SetEquals(captureIds))
                    {
                        yield return $"Rule 2: call {i} has snapshot keys [{string.Join(", ", keys)}], captured are [{string.Join(", ", captureIds)}]";
                    }
                }

                if (!recordsExit && call.ContainsKey("exit"))
                {
                    yield return $"Rule 3: call {i} has exit although recorded.exit is false";
                }

                if (!recordsOutcome && call.ContainsKey("outcome"))
                {
                    yield return $"Rule 3: call {i} has outcome although recorded.outcome is false";
                }

                var finished = !call.ContainsKey("exit") || call["exit"] is not null;
                if (recordsExit && recordsOutcome && finished && (call["exit"] is null || call["outcome"] is null))
                {
                    yield return $"Rule 3: finished call {i} lacks exit or outcome";
                }
            }
        }

        var testIds = new HashSet<string>();
        if (session.Has(SessionDirectory.TestsFile))
        {
            var tests = session.Read(SessionDirectory.TestsFile)["tests"]!.AsArray();
            var usedCalls = new HashSet<int>();
            foreach (var test in tests.Select(t => t!.AsObject()))
            {
                var testId = test["testId"]!.GetValue<string>();
                if (!testIds.Add(testId))
                {
                    yield return $"Rule 5: testId {testId} is not unique";
                }

                var indices = test["callIndices"]!.AsArray().Select(i => i!.GetValue<int>()).ToList();
                if (!indices.SequenceEqual(indices.Order()))
                {
                    yield return $"Rule 4: callIndices of {testId} are not ascending";
                }

                foreach (var index in indices)
                {
                    if (!usedCalls.Add(index))
                    {
                        yield return $"Rule 4: callIndex {index} appears in more than one test";
                    }

                    if (calls is null || index >= calls.Count)
                    {
                        yield return $"Rule 4: callIndex {index} of {testId} is not a recorded call";
                    }
                }
            }

            if (run["counts"]?["tests"] is { } testCount && testCount.GetValue<int>() != tests.Count)
            {
                yield return $"Rule 7: counts.tests is {testCount}, tests.json has {tests.Count}";
            }
        }

        if (calls is not null && run["counts"]?["calls"] is { } callCount && callCount.GetValue<int>() != calls.Count)
        {
            yield return $"Rule 7: counts.calls is {callCount}, trace.json has {calls.Count}";
        }

        if (session.Has(SessionDirectory.ResultsFile))
        {
            var resultIds = new HashSet<string>();
            foreach (var result in session.Read(SessionDirectory.ResultsFile)["results"]!.AsArray())
            {
                var testId = result!["testId"]!.GetValue<string>();
                if (!testIds.Contains(testId))
                {
                    yield return $"Rule 6: result for {testId}, which is not in tests.json";
                }

                if (!resultIds.Add(testId))
                {
                    yield return $"Rule 6: more than one result for {testId}";
                }
            }
        }
    }

    static IReadOnlyDictionary<string, JsonSchema> LoadSchemas()
    {
        var registry = new SchemaRegistry();
        var options = new BuildOptions { SchemaRegistry = registry };
        var directory = Path.Combine(AppContext.BaseDirectory, "Contract", "schemas");

        // trace.schema.json refers to value.schema.json by its $id, so value is built and registered first.
        var schemas = new Dictionary<string, JsonSchema>();
        foreach (var name in new[] { "value", "run", "trace", "tests", "results" })
        {
            var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(directory, $"{name}.schema.json")), options);
            registry.Register(schema);
            schemas[name] = schema;
        }

        return schemas;
    }
}

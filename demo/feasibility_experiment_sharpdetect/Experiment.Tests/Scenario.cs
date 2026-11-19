using System.Text.Json;
using System.Text.Json.Nodes;
using DotReplay.Tests.Infrastructure;
using Json.Schema;

namespace DotReplay.Experiment.Tests;

/// <summary>One scenario, analysed once and shared by the tests of a class: analysis.json and the generated tests.</summary>
public abstract class Scenario : IDisposable
{
    readonly SessionDirectory session = new();

    protected Scenario(string scenario, string plugin, params string[] methods)
    {
        ExperimentCli.Experiment(
            "analyze",
            "--assembly", ExperimentCli.TargetApp,
            "--methods", string.Join(";", methods),
            "--scenario", scenario,
            "--plugin", plugin,
            "--output-dir", session.Path);
        Analysis = session.Read("analysis.json");
        GeneratedTests = Path.Combine(session.Path, "generated-tests");
        ExperimentCli.Experiment("generate-tests", "--analysis", Path.Combine(session.Path, "analysis.json"), "--output-dir", GeneratedTests);
    }

    public JsonObject Analysis { get; }

    public string GeneratedTests { get; }

    public void Dispose() => session.Dispose();

    public JsonObject Call(int callIndex) => Analysis["calls"]![callIndex]!.AsObject();

    public string ThreadName(int threadId) => Analysis["threads"]![threadId]!["name"]!.GetValue<string>();

    public string Signature(int callIndex) => Analysis["targets"]![Call(callIndex)["target"]!.GetValue<int>()]!["signature"]!.GetValue<string>();

    public int[] Arguments(int callIndex) =>
        Call(callIndex)["entry"]!.AsObject().OrderBy(value => value.Key).Select(value => value.Value!["value"]!.GetValue<int>()).ToArray();

    /// <summary>Problems of the calls as contract v1 trace.json calls; empty when they are valid.</summary>
    public IReadOnlyList<string> ContractV1Problems()
    {
        var registry = new SchemaRegistry();
        var options = new BuildOptions { SchemaRegistry = registry };
        var directory = Path.Combine(AppContext.BaseDirectory, "Contract", "schemas");
        registry.Register(JsonSchema.FromText(File.ReadAllText(Path.Combine(directory, "value.schema.json")), options));
        var trace = JsonSchema.FromText(File.ReadAllText(Path.Combine(directory, "trace.schema.json")), options);

        var document = new JsonObject { ["schemaVersion"] = 1, ["calls"] = Analysis["calls"]!.DeepClone() };
        using var json = JsonDocument.Parse(document.ToJsonString());
        var result = trace.Evaluate(json.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        return result.IsValid
            ? []
            : (result.Details ?? []).Where(d => d.Errors is { Count: > 0 }).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}")).ToList();
    }
}

public sealed class DeadlockScenario() : Scenario(
    "deadlock",
    "Deadlock",
    "Com.Example.Concurrent.SharedCalculator.Add(int,int)",
    "Com.Example.Concurrent.SharedCalculator.Subtract(int,int)");

public sealed class RaceScenario() : Scenario(
    "race",
    "FastTrack",
    "Com.Example.Concurrent.SharedCalculator.Accumulate(int)");

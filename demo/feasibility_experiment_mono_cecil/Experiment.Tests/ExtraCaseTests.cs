using System.Text.Json.Nodes;
using DotReplay.Tests.Infrastructure;
using Xunit;

namespace DotReplay.Experiment.Tests;

/// <summary>The extra cases of README.md, Scope. They have no golden files, so the contract checks apply.</summary>
public sealed class ExtraCaseTests : IDisposable
{
    readonly SessionDirectory session = new();

    public void Dispose() => session.Dispose();

    [Fact]
    public void ComplexObjects_RecordNullCycleAndSharedReference()
    {
        ExperimentCli.Record(session, ExperimentCli.ExtraTargetApp, "Com.Example.Extra.Graph.Describe(Com.Example.Extra.Node)");

        Contract.AssertValid(session);
        var kinds = ExperimentCli.Values(session.Read(SessionDirectory.TraceFile)).Select(Kind).ToHashSet();
        Assert.Superset(new HashSet<string> { "object", "string", "null", "cycle", "ref" }, kinds);
    }

    [Fact]
    public void Collections_RecordListArrayAndDictionaryElementByElement()
    {
        ExperimentCli.Record(session, ExperimentCli.ExtraTargetApp, "Com.Example.Extra.Shop.Total(Com.Example.Extra.Order)");

        Contract.AssertValid(session);
        var arrays = ExperimentCli.Values(session.Read(SessionDirectory.TraceFile))
            .Where(value => Kind(value) == "array")
            .Select(value => value["type"]!.GetValue<string>())
            .ToHashSet();
        Assert.Equal(
            new HashSet<string>
            {
                "System.Collections.Generic.List<Com.Example.Extra.Item>",
                "System.String[]",
                "System.Collections.Generic.Dictionary<System.String,System.Int32>",
            },
            arrays);
    }

    [Fact]
    public void InstanceFieldOfVoidMethod_ExitShowsTheIncrementedField()
    {
        ExperimentCli.Record(session, ExperimentCli.ExtraTargetApp, "Com.Example.Extra.Counter.Increment(int)", "Com.Example.Extra.Counter.count");

        Contract.AssertValid(session);
        AssertField(session.Read(SessionDirectory.RunFile), captureId: 1, "count");
        foreach (var call in Calls())
        {
            Assert.Equal(Int(call, "entry", "1") + Int(call, "entry", "0"), Int(call, "exit", "1"));
        }
    }

    [Fact]
    public void StaticFieldOfStaticMethod_ExitShowsTheCountedCall()
    {
        ExperimentCli.Record(session, ExperimentCli.ExtraTargetApp, "Com.Example.Extra.MathUtil.Max(int,int)", "Com.Example.Extra.MathUtil.calls");

        Contract.AssertValid(session);
        AssertField(session.Read(SessionDirectory.RunFile), captureId: 2, "calls");
        Assert.Equal([0, 1, 2], Calls().Select(call => Int(call, "entry", "2")));
        Assert.Equal([1, 2, 3], Calls().Select(call => Int(call, "exit", "2")));
    }

    IEnumerable<JsonObject> Calls() => session.Read(SessionDirectory.TraceFile)["calls"]!.AsArray().Select(call => call!.AsObject());

    static void AssertField(JsonObject run, int captureId, string name)
    {
        var captured = run["captured"]![captureId]!;
        Assert.Equal("field", captured["kind"]!.GetValue<string>());
        Assert.Equal(name, captured["name"]!.GetValue<string>());
    }

    static string Kind(JsonObject value) => value["kind"]!.GetValue<string>();

    static int Int(JsonObject call, string snapshot, string captureId) => call[snapshot]![captureId]!["value"]!.GetValue<int>();
}

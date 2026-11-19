using System.Text.Json.Nodes;
using Xunit;

namespace DotReplay.Experiment.Tests;

/// <summary>The deadlock: Add and Subtract take the same two locks in opposite order (README.md, Approach).</summary>
public sealed class DeadlockTests(DeadlockScenario scenario) : IClassFixture<DeadlockScenario>
{
    [Fact]
    public void Analysis_LinksTheDeadlockToTheBlockedCalls()
    {
        var finding = Assert.Single(scenario.Analysis["findings"]!.AsArray())!.AsObject();
        Assert.Equal("deadlock", finding["kind"]!.GetValue<string>());

        var threads = finding["threads"]!.AsArray().Select(thread => thread!.AsObject()).ToList();
        Assert.Equal(["Adder", "Subtractor"], threads.Select(thread => scenario.ThreadName(Id(thread))).Order());
        foreach (var thread in threads)
        {
            var call = Assert.Single(thread["callIndices"]!.AsArray())!.GetValue<int>();
            Assert.Null(scenario.Call(call)["exit"]);
            Assert.Equal(Id(thread), scenario.Call(call)["thread"]!.GetValue<int>());
            Assert.NotEqual(Id(thread), thread["waitsFor"]!["heldBy"]!.GetValue<int>());
            var (signature, arguments) = scenario.ThreadName(Id(thread)) == "Adder"
                ? ("Com.Example.Concurrent.SharedCalculator.Add(int,int)", new[] { 1, 2 })
                : ("Com.Example.Concurrent.SharedCalculator.Subtract(int,int)", new[] { 5, 3 });
            Assert.Equal(signature, scenario.Signature(call));
            Assert.Equal(arguments, scenario.Arguments(call));
        }
    }

    [Fact]
    public void Calls_AreContractV1Calls() => Assert.Empty(scenario.ContractV1Problems());

    [Fact]
    public void GeneratedTest_FailsOnTheDeadlock()
    {
        var (exitCode, output) = ExperimentCli.TestGenerated(scenario.GeneratedTests);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("did not finish within the timeout", output);
    }

    [Fact]
    public void GeneratedTest_PassesOnceTheDeadlockIsFixed()
    {
        var (exitCode, output) = ExperimentCli.TestGenerated(scenario.GeneratedTests, ExperimentCli.FixedTargetApp);

        Assert.True(exitCode == 0, output);
    }

    static int Id(JsonObject thread) => thread["threadId"]!.GetValue<int>();
}

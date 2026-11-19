using Xunit;

namespace DotReplay.Experiment.Tests;

/// <summary>The data race: two threads update SharedCalculator.total without a lock (README.md, Approach).</summary>
public sealed class DataRaceTests(RaceScenario scenario) : IClassFixture<RaceScenario>
{
    [Fact]
    public void Analysis_LinksTheRaceToTheCallsOfBothThreads()
    {
        var finding = Assert.Single(scenario.Analysis["findings"]!.AsArray())!.AsObject();
        Assert.Equal("dataRace", finding["kind"]!.GetValue<string>());
        Assert.Equal("Com.Example.Concurrent.SharedCalculator.total", finding["field"]!.GetValue<string>());

        var accesses = finding["accesses"]!.AsArray().Select(access => access!.AsObject()).ToList();
        Assert.Contains(accesses, access => access["access"]!.GetValue<string>() == "Write");
        var callsByThread = accesses
            .GroupBy(access => scenario.ThreadName(access["threadId"]!.GetValue<int>()))
            .ToDictionary(group => group.Key, group => group.SelectMany(access => access["callIndices"]!.AsArray()).Select(call => call!.GetValue<int>()).Distinct().Order().ToList());
        Assert.Equal(["Accumulator 1", "Accumulator 2"], callsByThread.Keys.Order());
        Assert.Equal([1, 2, 3], callsByThread["Accumulator 1"].Select(call => scenario.Arguments(call)[0]));
        Assert.Equal([10, 20, 30], callsByThread["Accumulator 2"].Select(call => scenario.Arguments(call)[0]));
    }

    [Fact]
    public void Calls_AreContractV1Calls() => Assert.Empty(scenario.ContractV1Problems());

    [Fact]
    public void GeneratedTest_FailsOnTheLostUpdate()
    {
        var (exitCode, output) = ExperimentCli.TestGenerated(scenario.GeneratedTests);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Data race on Com.Example.Concurrent.SharedCalculator.total", output);
    }

    [Fact]
    public void GeneratedTest_PassesOnceTheRaceIsFixed()
    {
        var (exitCode, output) = ExperimentCli.TestGenerated(scenario.GeneratedTests, ExperimentCli.FixedTargetApp);

        Assert.True(exitCode == 0, output);
    }
}

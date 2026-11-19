using DotReplay.Tests.Infrastructure;
using Xunit;

namespace DotReplay.Tests.Integration;

/// <summary>The full run end to end (doc/use-cases/pipeline_run.md).</summary>
[Trait("Category", "Integration")]
public sealed class FullRunTests : IDisposable
{
    readonly SessionDirectory session = new();

    public void Dispose() => session.Dispose();

    [Fact]
    public void Calculator_ProducesTheExpectedSessionDirectory()
    {
        var result = DotReplayCli.Run([.. DotReplayCli.CalculatorArguments(), "--output-dir", session.Path]);

        Outcome.AssertSucceeded(result);
        foreach (var file in SessionDirectory.ContractFiles.Append(SessionDirectory.ManifestFile))
        {
            Expected.Matches(session, Expected.Calculator, file);
        }

        Contract.AssertValid(session);
    }

    [Fact]
    public void FailedAnalysis_StopsBeforeTheOtherStages()
    {
        var arguments = DotReplayCli.CalculatorArguments(method: "Com.Example.Calculator.Sub(int,int)");

        var result = DotReplayCli.Run([.. arguments, "--output-dir", session.Path]);

        Outcome.AssertFailedWith(result, "MISSING_TARGET_METHODS");
        Outcome.AssertRunFailed(session, "MISSING_TARGET_METHODS");
        Outcome.AssertManifest(session, Outcome.Analysis, "FAILED", "MISSING_TARGET_METHODS");
        Assert.False(session.Has(SessionDirectory.TestsFile));
        Assert.False(session.Has(SessionDirectory.ResultsFile));
        Contract.AssertValid(session);
    }
}

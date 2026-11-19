using DotReplay.Tests.Infrastructure;
using Xunit;

namespace DotReplay.Tests.Unit;

/// <summary>The test generation stage alone (doc/use-cases/test_generation.md), on a prepared analysis output.</summary>
[Trait("Category", "Unit")]
public sealed class GenerateTestsTests : IDisposable
{
    readonly SessionDirectory session = new();

    public void Dispose() => session.Dispose();

    [Fact]
    public void CalculatorAnalysisOutput_GeneratesAllCombinations()
    {
        session.CopyExpected(Expected.Calculator, SessionDirectory.RunFile, SessionDirectory.TraceFile);

        var result = DotReplayCli.Run("generate-tests", "--run-dir", session.Path);

        Outcome.AssertSucceeded(result);
        Expected.Matches(session, Expected.Calculator, SessionDirectory.TestsFile);
        Expected.Matches(session, Expected.Calculator, SessionDirectory.RunFile);
        Assert.Empty(session.Read(SessionDirectory.ResultsFile)["results"]!.AsArray());
        Outcome.AssertManifest(session, Outcome.TestGeneration, "COMPLETE");
        Contract.AssertValid(session);
    }

    [Fact]
    public void NoAnalysisOutput_FailsWithMissingAnalysisOutput()
    {
        var result = DotReplayCli.Run("generate-tests", "--run-dir", session.Path);

        Outcome.AssertFailedWith(result, "MISSING_ANALYSIS_OUTPUT");
        Outcome.AssertManifest(session, Outcome.TestGeneration, "FAILED", "MISSING_ANALYSIS_OUTPUT");
        Assert.False(session.Has(SessionDirectory.TestsFile));
    }

    [Fact]
    public void AnalysisOutputThatIsNotJson_FailsWithInvalidAnalysisOutput()
    {
        session.CopyExpected(Expected.Calculator, SessionDirectory.RunFile);
        session.Write(SessionDirectory.TraceFile, "{ \"schemaVersion\": 1, \"calls\": [");

        var result = DotReplayCli.Run("generate-tests", "--run-dir", session.Path);

        Outcome.AssertFailedWith(result, "INVALID_ANALYSIS_OUTPUT");
        Outcome.AssertRunFailed(session, "INVALID_ANALYSIS_OUTPUT");
        Outcome.AssertManifest(session, Outcome.TestGeneration, "FAILED", "INVALID_ANALYSIS_OUTPUT");
        Assert.False(session.Has(SessionDirectory.TestsFile));
    }
}

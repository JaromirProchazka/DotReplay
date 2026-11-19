using System.Text.Json.Nodes;
using DotReplay.Tests.Infrastructure;
using Xunit;

namespace DotReplay.Tests.Unit;

/// <summary>The analysis stage alone (doc/use-cases/analysis.md).</summary>
[Trait("Category", "Unit")]
public sealed class AnalyzeTests : IDisposable
{
    readonly SessionDirectory session = new();

    public void Dispose() => session.Dispose();

    [Fact]
    public void Calculator_WritesTheRecordedCalls()
    {
        var result = DotReplayCli.Run(["analyze", .. DotReplayCli.CalculatorArguments(), "--output-dir", session.Path]);

        Outcome.AssertSucceeded(result);
        Expected.Matches(session, Expected.Calculator, SessionDirectory.TraceFile);
        Expected.Matches(ExpectedRunBeforeTestGeneration(), session.Read(SessionDirectory.RunFile), SessionDirectory.RunFile);
        Assert.False(session.Has(SessionDirectory.TestsFile));
        Outcome.AssertManifest(session, Outcome.Analysis, "COMPLETE");
        Contract.AssertValid(session);
    }

    [Fact]
    public void CalculatorFromRunConfiguration_WritesTheSameOutput()
    {
        var configuration = new JsonObject
        {
            ["assembly"] = DotReplayCli.TargetAppPath,
            ["source"] = DotReplayCli.TargetSourcePath,
            ["method"] = "Com.Example.Calculator.Add(int,int)",
            ["parameters"] = new JsonArray("0:int", "1:int"),
            ["strategy"] = "record",
        };
        session.Write("configuration.json", configuration.ToJsonString());

        var result = DotReplayCli.Run("analyze", "--config", Path.Combine(session.Path, "configuration.json"), "--output-dir", session.Path);

        Outcome.AssertSucceeded(result);
        Expected.Matches(session, Expected.Calculator, SessionDirectory.TraceFile);
        Expected.Matches(ExpectedRunBeforeTestGeneration(), session.Read(SessionDirectory.RunFile), SessionDirectory.RunFile);
    }

    [Fact]
    public void MissingTargetApplication_FailsWithMissingTargetApplication()
    {
        var arguments = DotReplayCli.CalculatorArguments();
        arguments[1] = Path.Combine(session.Path, "missing", "calc-app.dll");

        AssertAnalysisFailed(DotReplayCli.Run(["analyze", .. arguments, "--output-dir", session.Path]), "MISSING_TARGET_APPLICATION");
    }

    [Fact]
    public void MethodNotInTargetApplication_FailsWithMissingTargetMethods()
    {
        var arguments = DotReplayCli.CalculatorArguments(method: "Com.Example.Calculator.Sub(int,int)");

        AssertAnalysisFailed(DotReplayCli.Run(["analyze", .. arguments, "--output-dir", session.Path]), "MISSING_TARGET_METHODS");
    }

    [Fact]
    public void UnknownStrategy_FailsWithInvalidStrategy()
    {
        var result = DotReplayCli.Run(["analyze", .. DotReplayCli.CalculatorArguments(), "--strategy", "no-such-strategy", "--output-dir", session.Path]);

        AssertAnalysisFailed(result, "INVALID_STRATEGY");
    }

    [Fact]
    public void RunConfigurationThatIsNotJson_FailsWithInvalidJson()
    {
        session.Write("configuration.json", "{ \"assembly\": ");

        var result = DotReplayCli.Run("analyze", "--config", Path.Combine(session.Path, "configuration.json"), "--output-dir", session.Path);

        Outcome.AssertFailedWith(result, "INVALID_JSON");
    }

    [Fact]
    public void RunConfigurationWithoutRequiredFields_FailsWithMissingRequiredFields()
    {
        session.Write("configuration.json", new JsonObject { ["source"] = DotReplayCli.TargetSourcePath }.ToJsonString());

        var result = DotReplayCli.Run("analyze", "--config", Path.Combine(session.Path, "configuration.json"), "--output-dir", session.Path);

        Outcome.AssertFailedWith(result, "MISSING_REQUIRED_FIELDS");
    }

    void AssertAnalysisFailed(CliResult result, string errorCode)
    {
        Outcome.AssertFailedWith(result, errorCode);
        Outcome.AssertRunFailed(session, errorCode);
        Outcome.AssertManifest(session, Outcome.Analysis, "FAILED", errorCode);
        Assert.False(session.Has(SessionDirectory.TraceFile));
        Contract.AssertValid(session);
    }

    /// <summary>The expected run.json before test generation adds the number of tests.</summary>
    static JsonObject ExpectedRunBeforeTestGeneration()
    {
        var run = Expected.Read(Expected.Calculator, SessionDirectory.RunFile);
        run["counts"]!.AsObject().Remove("tests");
        return run;
    }
}

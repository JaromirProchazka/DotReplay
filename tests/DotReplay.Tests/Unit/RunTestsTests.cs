using DotReplay.Tests.Infrastructure;
using Xunit;

namespace DotReplay.Tests.Unit;

/// <summary>
/// The test running stage alone (doc/use-cases/tests_run.md), which is also the contract's
/// <c>run-tests</c> command, on a prepared Test suite.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RunTestsTests : IDisposable
{
    readonly SessionDirectory session = new();

    public void Dispose() => session.Dispose();

    [Fact]
    public void CalculatorTestSuite_RunsAllTests()
    {
        session.CopyExpected(Expected.Calculator, SessionDirectory.RunFile, SessionDirectory.TraceFile, SessionDirectory.TestsFile);
        var runBefore = File.ReadAllText(Path.Combine(session.Path, SessionDirectory.RunFile));

        var result = DotReplayCli.Run("run-tests", "--run-dir", session.Path);

        Outcome.AssertSucceeded(result);
        Expected.Matches(session, Expected.Calculator, SessionDirectory.ResultsFile);
        Outcome.AssertManifest(session, Outcome.TestRunning, "COMPLETE");
        Assert.Equal(runBefore, File.ReadAllText(Path.Combine(session.Path, SessionDirectory.RunFile)));
        Contract.AssertValid(session);
    }

    [Fact]
    public void NoTestSuite_FailsWithMissingTestSuite()
    {
        session.CopyExpected(Expected.Calculator, SessionDirectory.RunFile, SessionDirectory.TraceFile);

        var result = DotReplayCli.Run("run-tests", "--run-dir", session.Path);

        Outcome.AssertFailedWith(result, "MISSING_TEST_SUITE");
        Outcome.AssertResultsError(session, "MISSING_TEST_SUITE");
        Outcome.AssertManifest(session, Outcome.TestRunning, "FAILED", "MISSING_TEST_SUITE");
        Contract.AssertValid(session);
    }

    [Fact]
    public void UnknownSelectedTest_FailsWithInvalidTestList()
    {
        session.CopyExpected(Expected.Calculator, SessionDirectory.RunFile, SessionDirectory.TraceFile, SessionDirectory.TestsFile);
        var runBefore = File.ReadAllText(Path.Combine(session.Path, SessionDirectory.RunFile));

        var result = DotReplayCli.Run("run-tests", "--run-dir", session.Path, "--test", "Com.Example.CalculatorTest#NoSuchTest");

        Outcome.AssertFailedWith(result, "INVALID_TEST_LIST");
        Outcome.AssertResultsError(session, "INVALID_TEST_LIST");
        Outcome.AssertManifest(session, Outcome.TestRunning, "FAILED", "INVALID_TEST_LIST");
        Assert.Equal(runBefore, File.ReadAllText(Path.Combine(session.Path, SessionDirectory.RunFile)));
        Contract.AssertValid(session);
    }
}

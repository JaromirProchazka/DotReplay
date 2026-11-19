using DotReplay.Tests.Infrastructure;
using Xunit;

namespace DotReplay.Experiment.Tests;

/// <summary>The Calculator: the output must equal the output of mock_dotreplay's analysis (README.md, Verification).</summary>
public sealed class CalculatorTests : IDisposable
{
    readonly SessionDirectory session = new();

    public void Dispose() => session.Dispose();

    [Fact]
    public void Add_RecordsTheSameRunAndTraceAsTheMock()
    {
        ExperimentCli.Record(session, ExperimentCli.CalculatorApp, "Com.Example.Calculator.Add(int,int)");

        Expected.Matches(session, Expected.Calculator, SessionDirectory.TraceFile);
        var expectedRun = Expected.Read(Expected.Calculator, SessionDirectory.RunFile);
        expectedRun["counts"]!.AsObject().Remove("tests");
        Expected.Matches(expectedRun, session.Read(SessionDirectory.RunFile), SessionDirectory.RunFile);
        Contract.AssertValid(session);
    }
}

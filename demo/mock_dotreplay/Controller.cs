using System.Text.Json;
using System.Text.Json.Nodes;

namespace DotReplay.Mock;

/// <summary>
/// Calls one stage per command, writes its output and the Manifest, and reports failures
/// (doc/architecture/architecture.md). A MOCK_UNSUPPORTED_INPUT failure is passed on and writes nothing.
/// </summary>
static class Controller
{
    /// <summary>The full run: the three stages in a fixed order, stopping after the first failed one.</summary>
    public static bool FullRun(AnalysisOptions options, DateTimeOffset now) =>
        Analyze(options, now)
        && GenerateTests(new GenerateTestsOptions(options.OutputDirectory, options.MaxArgumentCombinations), now)
        && RunTests(new RunTestsOptions(options.OutputDirectory, []), now);

    public static bool Analyze(AnalysisOptions options, DateTimeOffset now)
    {
        var session = new SessionDirectory(options.OutputDirectory);
        try
        {
            Console.WriteLine("Validating arguments...");
            AnalysisStage.Validate(options, File.Exists, Directory.Exists);
            Console.WriteLine("Arguments validated successfully");

            Console.WriteLine("Running analysis...");
            session.ClearRun();
            session.Write(SessionDirectory.TraceFile, AnalysisStage.TraceJson());
            session.Write(SessionDirectory.RunFile, AnalysisStage.RunJson(options, now));
            session.RecordStage(Manifest.Analysis, Manifest.Complete(now));
            Console.WriteLine($"Analysis completed. Trace at: {session.PathOf(SessionDirectory.TraceFile)}");
            return true;
        }
        catch (StageError error) when (error.Code != ErrorCodes.MockUnsupportedInput)
        {
            session.ClearRun();
            session.Write(SessionDirectory.RunFile, AnalysisStage.FailedRunJson(options, now, error));
            session.RecordStage(Manifest.Analysis, Manifest.Failed(error, now));
            Report(error);
            return false;
        }
    }

    public static bool GenerateTests(GenerateTestsOptions options, DateTimeOffset now)
    {
        var session = new SessionDirectory(options.RunDirectory);
        JsonNode? run = null;
        try
        {
            Console.WriteLine("Generating tests...");
            RequireFile(session, SessionDirectory.TraceFile, ErrorCodes.MissingAnalysisOutput, "The analysis output file was not found");
            RequireFile(session, SessionDirectory.RunFile, ErrorCodes.MissingAnalysisOutput, "The run summary was not found");
            run = ReadJson(session, SessionDirectory.RunFile, ErrorCodes.InvalidAnalysisOutput);
            var trace = ReadJson(session, SessionDirectory.TraceFile, ErrorCodes.InvalidAnalysisOutput);

            var analysis = TestGenerationStage.ReadAnalysisOutput(run, trace);
            var result = TestGenerationStage.Generate(analysis, options.MaxArgumentCombinations);

            session.WriteText(SessionDirectory.TestSourceFile, TestGenerationStage.TestSource(result, analysis.Signature));
            session.Write(SessionDirectory.TestsFile, TestGenerationStage.TestsJson(result));
            session.Write(SessionDirectory.ResultsFile, TestRunningStage.EmptyResults());
            session.Write(SessionDirectory.RunFile, TestGenerationStage.WithTestCount((JsonObject)run!, result.Tests.Count));
            session.RecordStage(Manifest.TestGeneration, result.LimitReached
                ? Manifest.Partial(
                    $"The maxArgumentCombinations limit was reached: {result.Tests.Count} of {result.Combinations} combinations were generated.",
                    now)
                : Manifest.Complete(now));
            session.RecordStage(Manifest.TestRunning, null);
            Console.WriteLine($"Test generation completed. {result.Tests.Count} tests: {session.PathOf(SessionDirectory.TestsFile)}");
            return true;
        }
        catch (StageError error) when (error.Code != ErrorCodes.MockUnsupportedInput)
        {
            if (session.Exists)
            {
                if (run is JsonObject runSummary)
                {
                    session.Write(SessionDirectory.RunFile, TestGenerationStage.WithFailure(runSummary, error));
                }

                session.RecordStage(Manifest.TestGeneration, Manifest.Failed(error, now));
            }

            Report(error);
            return false;
        }
    }

    public static bool RunTests(RunTestsOptions options, DateTimeOffset now)
    {
        var session = new SessionDirectory(options.RunDirectory);
        try
        {
            Console.WriteLine("Running tests...");
            RequireFile(session, SessionDirectory.TestsFile, ErrorCodes.MissingTestSuite, "The Test suite was not found");
            var testIds = TestRunningStage.ReadTestIds(ReadJson(session, SessionDirectory.TestsFile, ErrorCodes.MissingTestSuite));
            var ran = TestRunningStage.Select(testIds, options.Tests);

            var previous = session.ReadOrNull(SessionDirectory.ResultsFile);
            session.Write(SessionDirectory.ResultsFile, TestRunningStage.WithResults(previous, testIds, ran));
            session.RecordStage(Manifest.TestRunning, Manifest.Complete(now));
            Console.WriteLine($"Test execution completed. {ran.Count} passed, 0 failed.");
            return true;
        }
        catch (StageError error) when (error.Code != ErrorCodes.MockUnsupportedInput)
        {
            if (session.Exists)
            {
                var previous = session.ReadOrNull(SessionDirectory.ResultsFile);
                session.Write(SessionDirectory.ResultsFile, TestRunningStage.WithError(previous, error));
                session.RecordStage(Manifest.TestRunning, Manifest.Failed(error, now));
            }

            Report(error);
            return false;
        }
    }

    public static void Report(StageError error) => Console.Error.WriteLine($"Error: {error.Code}: {error.Message}");

    static void RequireFile(SessionDirectory session, string name, string code, string message)
    {
        if (!session.Has(name))
        {
            throw new StageError(code, $"{message}: {session.PathOf(name)}");
        }
    }

    static JsonNode? ReadJson(SessionDirectory session, string name, string code)
    {
        try
        {
            return session.Read(name);
        }
        catch (JsonException e)
        {
            throw new StageError(code, $"{name} is not valid JSON: {e.Message}");
        }
    }
}

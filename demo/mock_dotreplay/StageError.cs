namespace DotReplay.Mock;

/// <summary>
/// A failure that ends a stage. The code is one of doc/errors.md, or the mock-only
/// <see cref="ErrorCodes.MockUnsupportedInput"/> for input the mock does not simulate.
/// </summary>
sealed class StageError(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public static StageError Unsupported(string message) => new(ErrorCodes.MockUnsupportedInput, message);
}

static class ErrorCodes
{
    /// <summary>Mock only: the input is not the Calculator scenario. Nothing is written.</summary>
    public const string MockUnsupportedInput = "MOCK_UNSUPPORTED_INPUT";

    public const string InvalidJson = "INVALID_JSON";
    public const string MissingRequiredFields = "MISSING_REQUIRED_FIELDS";
    public const string MissingTargetApplication = "MISSING_TARGET_APPLICATION";
    public const string MissingTargetMethods = "MISSING_TARGET_METHODS";
    public const string InvalidStrategy = "INVALID_STRATEGY";
    public const string MissingAnalysisOutput = "MISSING_ANALYSIS_OUTPUT";
    public const string InvalidAnalysisOutput = "INVALID_ANALYSIS_OUTPUT";
    public const string MissingTestSuite = "MISSING_TEST_SUITE";
    public const string InvalidTestList = "INVALID_TEST_LIST";
}

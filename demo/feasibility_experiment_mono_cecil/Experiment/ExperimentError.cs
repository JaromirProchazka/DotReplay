namespace DotReplay.Experiment;

/// <summary>A failure reported to the User as <c>Error: message</c>, with exit code 1.</summary>
public sealed class ExperimentError(string message) : Exception(message);

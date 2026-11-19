namespace DotReplay.Experiment;

/// <summary>A concurrency error found by a detector, independent of the detector that found it.</summary>
public abstract record Finding;

/// <summary>A thread of a deadlock cycle: it waits for <see cref="Lock"/>, which <see cref="HeldBy"/> holds.</summary>
/// <param name="Frames">The stack when it blocked, innermost first, as Cecil full method names.</param>
public sealed record DeadlockWait(string Thread, IReadOnlyList<string> Frames, long Lock, string HeldBy);

/// <summary>Threads that wait for each other in a cycle.</summary>
public sealed record DeadlockFinding(IReadOnlyList<DeadlockWait> Waits) : Finding;

/// <summary>One access of a data race.</summary>
/// <param name="Access">Read, Write, AtomicRead or AtomicWrite.</param>
/// <param name="Frames">The stack of the access, innermost first, as Cecil full method names.</param>
public sealed record RaceAccess(string Thread, IReadOnlyList<string> Frames, string Access);

/// <summary>Accesses to one field that are not ordered by synchronization, and at least one of them writes.</summary>
public sealed record DataRaceFinding(string Field, IReadOnlyList<RaceAccess> Accesses) : Finding;

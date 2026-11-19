# Feasibility Experiment: SharpDetect

Part of milestone M1: "Time-boxed feasibility experiment; choice of the instrumentation technology" and "study of the Auto-debugger, SharpDetect, and the instrumentation options of the CLR" ([schedule.md](../../doc/schedule/schedule.md)). It prepares the O1 milestone (concurrency study, R7, R8), which plans thread-aware traces, a contract extension, and the detection of data races and deadlocks with SharpDetect.

## Goals

The goal of this experiment is to test whether the [SharpDetect](https://github.com/acizmarik/sharpdetect) project can be used in the analysis stage for the O1 milestone. The hypothesis is that SharpDetect can run as part of the analysis (as a custom Strategy), and that its output can be transformed to an extension of the backend contract (not yet defined), from which useful tests can be generated automatically.

## Scope

- **Bugs:** one deadlock and one data race, the two concurrency errors named in R8.
- **Time box:** the experiment shares M1's time box for the feasibility experiment. If SharpDetect cannot run at all, for example because it doesn't support the runtime or the operating system, the experiment stops, and the reason is documented as the alternative result.
- **Out of scope:** changing the backend contract, and integrating the experiment into the real System.

## Approach

This experiment might also need Mono.Cecil, so it depends on the outcome of the [`feasibility_experiment_mono_cecil/`](../feasibility_experiment_mono_cecil) experiment. That experiment succeeded: Mono.Cecil can weave recording calls into a Target method, and its code can be reused here.

The experiment consists of a few steps:

1. **Test Target application.** Create a small test Target application with an obvious deadlock and an obvious data race. It is a modified Calculator Target application, but it does not change the original code in [`mock_target_app/`](../mock_target_app).
   - Deadlock: two Target methods take the same two locks in opposite order, and are called from two threads.
   - Data race: two threads update a shared field without a lock, so an update can be lost.
2. **Analysis.** Create a SharpDetect app, possibly combined with Mono.Cecil, that runs on the test Target application from step 1.
3. **Adapter.** Create a small adapter from the SharpDetect output to a JSON file with all the information needed for automated test generation (see [JSON file](#json-file)).
4. **Test generation.** Create a very simple function that reads only the JSON file and generates one small unit test per bug. Each test reproduces the bug by replaying the recorded calls on their threads in the recorded order:
   - **Deadlock:** the test runs the calls with a timeout, and fails when they don't finish in time, as R27 describes for `TEST_TIMEOUT`.
   - **Data race:** the test fails when the result shows the lost update.

## JSON file

The JSON file is free-form, because the contract extension is not yet designed. It is still designed so that it can become a contract extension with small changes, because this experiment may lead to an update of the contract:

- **Same structure as contract v1.** Keep the `run.json` and `trace.json` structure and the value format of [backend contract v1](https://github.com/norilanda/debug-infra-gentests/tree/develop/vs-code-extension/contract) where they fit (calls, `callIndex`, snapshots keyed by `captureId`).
- **Additions only.** Concurrency data is added as new fields rather than by changing existing ones, so a front end that reads v1 could ignore it. The first candidates are the ones R30 names: thread IDs and the per-thread call order, plus the lock and field-access events the bug needs.
- **No SharpDetect specifics.** Only the adapter knows SharpDetect's own output format, so other concurrency Strategies (R24) could write the same file.

The final format is documented in the result section, with an example and a description of every field that is not in contract v1.

## Success Criteria

- **Success:** For both bugs, the test app produces a unit test capturing the bug, generated from the SharpDetect output. The test app also writes a JSON file with all the data needed for test generation, and the SharpDetect step communicates with the test generation step only through this file.
- **Alternative result:** SharpDetect cannot capture a bug, its output is very hard to translate to a usable JSON format, or a test cannot realistically be generated from the JSON file and the original test Target application. For each of these problems, or any other, the reason is found and documented.

## Experiment Result

**Success, with one important limitation.**
- **Both bugs:** SharpDetect found the deadlock and the data race, and the adapter wrote both into `analysis.json`.
- **Generated tests:** for each bug, the generator produced from that file alone a unit test that:
  - **fails** on the test Target application (deadlock: timeout; data race: 60 instead of 66);
  - **passes** on the same code with both bugs fixed.
- **The limitation:** the Mono.Cecil recorder, which provides the argument values, can **hide data races** from SharpDetect (see [Observations](#observations)).

All 8 tests in [`Experiment.Tests/`](Experiment.Tests) pass in Debug and Release.

| Bug | SharpDetect finding | Generated test on the bug | On the fixed code |
| --- | --- | --- | --- |
| Deadlock (`Add` / `Subtract`, two locks in opposite order) | `Deadlock` plugin: one cycle, Adder ↔ Subtractor | Fails: "did not finish within the timeout" (5 s) | Passes |
| Data race (`Accumulate` on `total` without a lock) | `FastTrack` plugin: races on `SharedCalculator.total` between Accumulator 1 and 2 | Fails: "60 after the concurrent calls, 66 after the same calls one after another" | Passes |

Reliability:
- **Analysis:** 5 repeated analyses of each scenario all found their bug.
- **Generated tests:** on the buggy code, they failed in 10 out of 10 runs for the data race and in 5 out of 5 runs for the deadlock.
- **Why they are reliable:** the test Target application sleeps inside its critical sections; see [Not tested](#not-tested).

### What was built

| Project | Role |
| --- | --- |
| [`ConcurrentTarget/`](ConcurrentTarget) | The test Target application. `SharedCalculator` has `Add`/`Subtract` (deadlock) and `Accumulate` (data race). It is run with the argument `deadlock` or `race`, and its threads are named. |
| [`ConcurrentTargetFixed/`](ConcurrentTargetFixed) | The same sources compiled with `FIXED`: one lock order, and a lock in `Accumulate`. |
| [`Recorder/`](Recorder) | `DotReplay.ThreadRecorder.dll`. It records each call's Target method, thread and argument values, reusing the Mono.Cecil experiment's `ValueSerializer`. |
| [`Experiment/`](Experiment) | `sharpdetect-experiment`, with the two commands below. It reuses `MethodSpec`, `Target`, `TypeNames` and `ExperimentError` of the Mono.Cecil experiment unchanged. |
| [`Experiment.Tests/`](Experiment.Tests) | xUnit, end to end. It runs both scenarios, checks `analysis.json`, checks that the calls are valid contract v1 calls, and runs the generated tests against both builds. |
| [`dotnet-tools.json`](dotnet-tools.json) | SharpDetect 2.1.4 as a local .NET tool (`dotnet tool restore`). |

1. **`analyze`:**
   - Mono.Cecil weaves the Target methods, each with its own id.
   - SharpDetect runs the instrumented copy, with `SkipInstrumentationForAssemblies` set so that it skips the `DotReplay.` recorder.
   - [`SharpDetectReport`](Experiment/SharpDetectReport.cs) reads the HTML report and turns it into neutral findings. It is the only code that knows SharpDetect's output.
   - [`AnalysisFile`](Experiment/AnalysisFile.cs) joins the findings with the recording into `analysis.json`.
2. **`generate-tests`:** [`TestGenerator`](Experiment/TestGenerator.cs) reads only `analysis.json`, and writes `GeneratedTests.csproj` and `ConcurrencyTests.cs`.

```bash
cd demo/feasibility_experiment_sharpdetect
dotnet tool restore
dotnet test Experiment.Tests        # builds everything, runs both scenarios and the generated tests
dotnet Experiment/bin/Debug/net8.0/sharpdetect-experiment.dll analyze \
  --assembly ConcurrentTarget/bin/Debug/net8.0/concurrent-target.dll \
  --methods "Com.Example.Concurrent.SharedCalculator.Add(int,int);Com.Example.Concurrent.SharedCalculator.Subtract(int,int)" \
  --scenario deadlock --plugin Deadlock --output-dir <dir>
dotnet Experiment/bin/Debug/net8.0/sharpdetect-experiment.dll generate-tests --analysis <dir>/analysis.json --output-dir <dir>/generated-tests
```

### The JSON file (`analysis.json`)

`analysis.json` holds the content of contract v1's `run.json` and `trace.json` in one file, with the concurrency data added. This is the deadlock run, unchanged except that the second Target method's `captured` list is shortened:

```json
{
  "schemaVersion": 1,
  "language": "dotnet",
  "assembly": "…/ConcurrentTarget/bin/Debug/net8.0/concurrent-target.dll",
  "targets": [
    { "targetId": 0, "signature": "Com.Example.Concurrent.SharedCalculator.Add(int,int)",
      "captured": [ { "captureId": 0, "kind": "argument", "name": "a", "type": "System.Int32" },
                    { "captureId": 1, "kind": "argument", "name": "b", "type": "System.Int32" } ] },
    { "targetId": 1, "signature": "Com.Example.Concurrent.SharedCalculator.Subtract(int,int)", "captured": [ … ] }
  ],
  "typeNames": { "System.Int32": "int" },
  "traceMode": "naive",
  "recorded": { "exit": true, "outcome": true },
  "counts": { "calls": 2 },
  "threads": [ { "threadId": 0, "name": "Subtractor" }, { "threadId": 1, "name": "Adder" } ],
  "calls": [
    { "callIndex": 0, "target": 1, "thread": 0,
      "entry": { "0": { "kind": "primitive", "type": "System.Int32", "value": 5 },
                 "1": { "kind": "primitive", "type": "System.Int32", "value": 3 } },
      "exit": null },
    { "callIndex": 1, "target": 0, "thread": 1,
      "entry": { "0": { "kind": "primitive", "type": "System.Int32", "value": 1 },
                 "1": { "kind": "primitive", "type": "System.Int32", "value": 2 } },
      "exit": null }
  ],
  "findings": [
    { "kind": "deadlock",
      "threads": [ { "threadId": 0, "callIndices": [0], "waitsFor": { "lock": 40, "heldBy": 1 } },
                   { "threadId": 1, "callIndices": [1], "waitsFor": { "lock": 39, "heldBy": 0 } } ] }
  ],
  "detector": "SharpDetect 2.1.4 Deadlock",
  "status": "ok",
  "createdAt": "2026-11-19T14:47:18Z"
}
```

In the data-race run, each `calls[]` entry has an `exit` snapshot and `"outcome": { "kind": "returned" }`. The finding, with two of its five accesses left out, is:

```json
{ "kind": "dataRace",
  "field": "Com.Example.Concurrent.SharedCalculator.total",
  "accesses": [ { "threadId": 0, "callIndices": [0, 2, 4], "access": "Write" },
                { "threadId": 1, "callIndices": [1, 3, 5], "access": "Write" },
                { "threadId": 1, "callIndices": [1, 3, 5], "access": "Read" } ] }
```

**Fields that are not in contract v1:**

| Field | Meaning | Kind of change |
| --- | --- | --- |
| `targets[]` (`targetId`, `signature`, `captured`) | The Target methods of the run. Each has v1's `target.signature` and `captured`, and a `targetId`. | **Replaces** v1's `target` + `captured`. A deadlock always involves at least two calls, here of two different methods, so one Target method per run is not enough. This is the only change that is not an addition. |
| `assembly` | The Target application's assembly, which the generated tests reference. | Addition |
| `threads[]` (`threadId`, `name`) | The threads that called a Target method. `threadId` is scoped to the file, like `captureId`; `name` is the thread's name. | Addition (R30: thread IDs) |
| `calls[].target` | The `targetId` of the method called. | Addition |
| `calls[].thread` | The `threadId` of the calling thread. Together with `callIndex` (the global start order), it gives the per-thread call order (R30). | Addition |
| `findings[]` | Concurrency errors, each with a `kind`. | Addition |
| `findings[]` of kind `deadlock`: `threads[]` with `threadId`, `callIndices` (the call that never returned), and `waitsFor` (`lock`: the detector's object id; `heldBy`: a `threadId`) | The cycle of waiting threads. | Addition |
| `findings[]` of kind `dataRace`: `field` (full name) and `accesses[]` with `threadId`, `callIndices` (every call of that method on that thread, because a detector reports a code location, not a call), and `access` (`Read`, `Write`, `AtomicRead` or `AtomicWrite`) | The unordered accesses to one field. | Addition |
| `detector` | Which detector found the findings, shown as text, like `traceMode`. | Addition |

**Kept as in v1:**
- **Snapshots:** keyed by `captureId`, with v1 values.
- **Unfinished calls:** a call that never returned has `exit: null` and no `outcome`.
- **Unchanged fields:** `typeNames`, `traceMode`, `recorded`, `counts.calls`, `status` and `createdAt`.
- **Contract check:** the tests check that the `calls` array is valid against v1's `trace.schema.json`.
- **No detector specifics:** apart from the `detector` label and the meaning of a lock id, nothing in the file is specific to SharpDetect.

**Proposed contract change:**
- Allow several Target methods per run (`targets[]`, `calls[].target`), and add `threads[]`, `calls[].thread` and `findings[]` as optional fields.
- A front end that reads v1 could keep working with a single-target run if `target` + `captured` were also written for `targets[0]`.

### The generated tests

The deadlock test, as generated:

```csharp
[Fact]
public void Deadlock0_Subtractor_Adder()
{
    var receiver = new Com.Example.Concurrent.SharedCalculator();

    var finished = Replay(
        ("Subtractor", () => { receiver.Subtract(5, 3); }),
        ("Adder", () => { receiver.Add(1, 2); }));

    Assert.True(finished, "Deadlock: Subtractor and Adder did not finish within the timeout.");
}
```

**`Replay`** starts one named background thread per recorded thread. A `Barrier` makes them start together, and `Replay` returns `false` when they don't all finish within 5 s.

**The data-race test:**
- It runs the same `Accumulate` calls once one after another on one receiver, and once concurrently on another receiver.
- It then compares the raced field, read by reflection.
- Running the calls one after another serves as the expected result.

### Observations

- **SharpDetect works on this setup.**
  - Stable 2.1.4 installs as a local .NET tool. It needs the .NET 10 runtime, and it ran here on Windows x64 against a .NET 8 target.
  - A run took about 2.1 s, against about 0.2 s without SharpDetect. SharpDetect itself reports 0.68 s of analysis; the rest is startup.
  - The whole `analyze` step took about 3 s.
- **The HTML report is the only output.**
  - **What the adapter relies on:** its CSS classes (`violation-card`, `thread-panel`, `frame-method` and others) and the English wording of its reasons, e.g. "waiting for lock object [35] owned by thread T2 (Adder)", "Write unordered after previous Write by …".
  - **Version changes:** this is fragile across versions. Between 2.1.4 and the current `main` branch, `Target.Args` changed from a string to an array, the "issues found" exit code changed from 1 to 5, and the report templates changed.
  - **Pinning:** the version is pinned for that reason.
  - **Alternative:** a custom SharpDetect plugin that writes JSON. It wasn't built: the public `Summary`/`Report` model a plugin sees holds the same formatted strings, and the raw `DeadlockInfo`/`DataRaceInfo` are private fields of the plugins.
- **SharpDetect knows *where*, not *with what*.**
  - **From SharpDetect:** thread names, lock object ids, the raced field, the access types, and the stack frames (Cecil full method names such as `System.Int32 Com.Example.Concurrent.SharedCalculator::Add(System.Int32,System.Int32)`, which match the woven methods directly).
  - **From the Mono.Cecil recorder:** the argument values, the receiver's type, and which call ran on which thread. Without them, no test could be generated.
  - **How they are joined:** by thread name and by the Target method in the stack.
- **The recorder can hide data races.**
  - **The probe:** "Accumulator 2" starts only after "Accumulator 1" has finished, but nothing synchronizes the two threads.
  - **Original app:** SharpDetect still reports the race, because FastTrack reasons about the order that synchronization imposes, not about whether threads actually ran at the same time.
  - **Instrumented app:** it reports **nothing**. The most likely cause is the recorder's lock in `Enter` and `Exit`, the only synchronization the instrumentation adds. Releasing it at the end of one call and taking it at the start of the next orders the two threads. Skipping the recorder's assembly (`DotReplay.`) in `SkipInstrumentationForAssemblies` did not prevent this. A lock-free recorder was not built, so the cause is not confirmed.
  - **The main scenario:** the race is still found there, because the calls overlap.
  - **For a real `sharpdetect` Strategy**, recording must not synchronize threads; per-thread buffers merged at exit are one option, not tested. Otherwise, SharpDetect and the recording must run separately.
- **Only real deadlocks are reported.** With the same lock-order inversion but threads that never overlap, the `Deadlock` plugin reports nothing, with or without the recorder. A deadlock must actually happen in the analysed run.
- **The deadlocked application must still end.** Its threads are background threads, and `Main` stops waiting after 2 s, as in SharpDetect's own sample. The recorder writes the recording at process exit, and the deadlocked calls are written as unfinished (`exit: null`).

### Not tested

These remain open for O1.

| Case | Expected effect |
| --- | --- |
| A Target application that hangs and is killed (R27 `TIMEOUT`) | The recording is written only at process exit, so it would be lost. Whether SharpDetect still writes its report was not tested. |
| Races and deadlocks without the sleeps inside the critical sections | The generated tests replay the calls without controlling the thread schedule, so they would reproduce the bug only sometimes. Controlling the schedule is the job of the `interleaving` and `fuzzing` Strategies. |
| Unnamed threads, the thread pool, `Task`s, and two threads with the same name | Findings are joined to calls by thread name, so the join would be ambiguous or fail. |
| Receivers without a parameterless constructor, static Target methods, and arguments that are objects | The generator supports only one shared receiver created with `new T()`, and primitive, string and `null` arguments. |
| Deadlocks on primitives other than `Monitor`/`lock`, and data races on array elements | Not supported by SharpDetect's plugins, according to its documentation. |
| Larger applications, and overhead (R14) | Not measured beyond the run times above. |

# Feasibility Experiment: Mono.Cecil

Part of milestone M1 ("Time-boxed feasibility experiment; choice of the instrumentation technology", [schedule.md](../../doc/schedule/schedule.md)). The SharpDetect experiment is a separate sub-project in [`feasibility_experiment_sharpdetect/`](../feasibility_experiment_sharpdetect).

## Goals

The goal of this experiment is to estimate whether it is feasible to use Mono.Cecil for the runtime analysis described in the documentation. We want to verify the hypothesis that all the data of the analysis stage in the [backend contract v1](https://github.com/norilanda/debug-infra-gentests/tree/develop/vs-code-extension/contract) can be captured with Mono.Cecil and transformed to the Session directory format.

The experiment also addresses the risk in [documentation.md](../../doc/documentation.md) (Risks) that the instrumentation might not be able to reconstruct some complex data structures.

## Scope

- **Output:** `run.json` and `trace.json`, the files the analysis stage writes. `tests.json` and `results.json` are out of scope, because Mono.Cecil does not affect test generation or test running.
- **Calculator:** `CalcApp.Main` calls `Calculator.Add(1, 2)`, `Add(3, 4)` and `Add(5, 6)` in [`mock_target_app/`](../mock_target_app). The experiment captures the same values as the [`mock_dotreplay/`](../mock_dotreplay) mock.
- **Extra cases** that the Calculator does not cover. The mock reports these as unsupported:
  - Complex objects: reference types, nested objects, collections, `null` and cycles, captured as `trace.json` values.
  - Instance fields (`--fields`), static methods and void methods.

The extra cases need their own small Target methods. They live in this folder, so [`mock_target_app/`](../mock_target_app) and the tests that depend on it stay unchanged.

## Approach

Mono.Cecil reads and rewrites .NET assemblies; it does not run them. The experiment therefore has two parts, matching the `Instrumentation` module in [architecture.md](../../doc/architecture.md):

1. **Weaver:** a small Mono.Cecil app that reads the Target application's assembly (`calc-app.dll`), inserts calls to a recorder at the entry and exit of each Target method, and writes an instrumented copy.
2. **Recorder and adapter:** running the instrumented copy records the Argument values, and a small adapter turns the recorded data into `run.json` and `trace.json` in a Session directory.

This sub-project does not necessarily follow the documented architecture. It only tests the capabilities of Mono.Cecil in the context of this project.

## Verification

The output is checked with the existing checks in [`tests/`](../../tests/README.md):

- **Contract:** `run.json` and `trace.json` validate against the vendored contract schemas and meet the contract's rules across files ([`Contract.cs`](../../tests/DotReplay.Tests/Infrastructure/Contract.cs)).
- **Expected output:** for the Calculator, both files equal the golden files in [`Expected/calculator/`](../../tests/DotReplay.Tests/Expected/calculator), apart from the `createdAt` timestamp. `run.json` is compared without `counts.tests`, which test generation writes.

The extra cases have no golden files, so only the contract checks apply to them.

## Success Criteria

- **Success:** The test app generates all the data of `run.json` and `trace.json` in the [backend contract v1](https://github.com/norilanda/debug-infra-gentests/tree/develop/vs-code-extension/contract) from the Target application. For the Calculator, the output equals the output of [`mock_dotreplay/`](../mock_dotreplay), and every extra case passes the contract checks.
- **Alternative result:** The test app generates only part of the data. The experiment result is then a list of the data that Mono.Cecil, or other simple code, cannot reach, and why.

## Experiment Result

**Success.** Within the scope above, Mono.Cecil, a small Recorder and an adapter produce all the data of `run.json` and `trace.json`:
- **Calculator:** both files equal the golden files.
- **Extra cases:** all four pass the contract checks.

All 5 tests in [`Experiment.Tests/`](Experiment.Tests) pass in Debug and Release.

### What was built

| Project | Role |
| --- | --- |
| [`Experiment/`](Experiment) | The test app `cecil-experiment`: <ol><li>finds the Target method with Mono.Cecil 0.11.6;</li><li>weaves it;</li><li>runs the instrumented copy in `<output-dir>/instrumented/`;</li><li>turns the raw `recording.json` into `run.json` and `trace.json`.</li></ol> |
| [`Recorder/`](Recorder) | `DotReplay.Recorder.dll`, loaded into the Target application. It serializes each snapshot with reflection when it is taken, and writes the recording when the process exits. |
| [`ExtraTarget/`](ExtraTarget) | The Target methods of the extra cases. |
| [`Experiment.Tests/`](Experiment.Tests) | xUnit tests. They link `Contract.cs`, `Expected.cs`, `SessionDirectory.cs`, the schemas and the golden files from [`tests/`](../../tests). |

Run it with:

```bash
dotnet test demo/feasibility_experiment_mono_cecil/Experiment.Tests   # builds the Target applications too
dotnet demo/feasibility_experiment_mono_cecil/Experiment/bin/Debug/net8.0/cecil-experiment.dll \
  --assembly demo/mock_target_app/args/bin/Debug/net8.0/calc-app.dll \
  --method "Com.Example.Calculator.Add(int,int)" --output-dir <Session directory>
```

### How each piece of data was reached

| Data | Source |
| --- | --- |
| `target.signature` | The `--method` text. Cecil's own form is `System.Int32 Com.Example.Calculator::Add(System.Int32,System.Int32)`, so the method is looked up by parsing the text and comparing the parameter types. |
| `captured`: argument names and types | Cecil metadata. Parameter names are stored in the assembly itself, so no `.pdb` is needed. |
| `captured`: fields | Cecil `FieldDefinition`s. The inserted code reads an instance field with `ldarg.0; ldfld` and a static field with `ldsfld`. |
| `entry` and `exit` values | Inserted IL. It boxes the arguments and fields into an `object[]` and calls `CallRecorder.Enter` at the start and `CallRecorder.Exit` before every `ret`. |
| `object`, `array`, `null`, `ref` and `cycle` values | The Recorder, by reflection. Mono.Cecil only inserts the calls; turning objects into values is plain .NET reflection. |
| `outcome: returned` | `CallRecorder.Exit` was reached. |
| `typeNames`, `counts.calls` and the fixed fields | The adapter. |

### Observations

- **Mono.Cecil itself was no limitation.**
  - Weaving needed under 100 lines of code ([`Weaver.cs`](Experiment/Weaver.cs)).
  - Methods with several `ret` instructions work: the Release build of `MathUtil.Max` has two, and both record the exit.
  - Each old `ret` becomes a `nop` followed by the exit recording, so branches to it still record the exit.
  - `SimplifyMacros` and `OptimizeMacros` keep short branches valid.
- **Deployment needs care.** With the original `calc-app.deps.json`, the instrumented application fails with `FileNotFoundException: DotReplay.Recorder`. The host only loads the assemblies listed in `deps.json`. The experiment leaves `deps.json` out, which works for applications that depend only on the framework.
- **The `.pdb` no longer matches** the rewritten assembly, so it is not copied.
- **Type names come in two forms.**
  - Cecil writes ``List`1<System.Int32>`` and `Outer/Inner`.
  - Reflection gives `List`1` with assembly-qualified arguments.
  - Both are converted to the contract's form, `System.Collections.Generic.List<System.Int32>`.
- **A `null` argument has no runtime type.** The Recorder only sees `null`, so the adapter adds the declared type from `captured`.
- **Field names need small fixes.**
  - Auto-properties and records store their values in `<Name>k__BackingField`, which the Recorder shows as `Name`.
  - Entries of a `Dictionary` show the framework's internal field names, `key` and `value`.
- **Framework collections need a decision.**
  - Read by fields, `List<T>` would appear as its internal `_items` and `_size`. So `List`, arrays and `Dictionary` are recorded element by element through enumeration.
  - Enumeration runs framework code, but never Target code. Collections defined by the Target application are recorded by fields.

### Data that could not be reached

None within the scope.

### Not tested (outside the scope)

These remain open for the real implementation.

| Case | Expected effect with the current approach |
| --- | --- |
| A Target method that throws | `Exit` is never called, so the call is written as unfinished (`exit: null`, no `outcome`) instead of `outcome: threw`. It needs a `try`/`catch` around the body. |
| The process crashes or is killed | The recording is written only at `ProcessExit`, so it is lost. |
| A Target application with NuGet dependencies | `deps.json` can't simply be left out. It must be extended with the Recorder instead. |
| `ref`/`out` parameters, generic methods and types, `async` methods and iterators | Not woven or checked. An `async` method's body is a state-machine stub, so only its first part would be recorded. |
| Several threads | The Recorder takes a lock, but concurrent calls were not tested. |
| Strong-named and ReadyToRun assemblies | Not tested. |
| Instrumentation overhead (R14) | Not measured. |

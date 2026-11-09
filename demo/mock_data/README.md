# Demo mock data

JSON versions of everything the Java auto-debugger pipeline produced for the four demo scenarios in `demo/output/`. They are fixtures for:

- the .NET/C# port, as reference output per stage;
- a decoupled front-end that talks to either backend, as mock backend responses.

The shared contract for these files is not defined yet. This folder documents what the Java side currently emits, so the contract can be derived from it.

## Layout

```
mock_data/
  manifest.json                  scenario list -> stage files
  <scenario>/                    args | fields | static | objects
    01-run-configuration.json
    02-instrumentation.json
    03-identifier-mapping.json
    04-analysis.json
    05-trace.json
    06-test-generation.json
    07-test-execution.json
```

| Scenario | Target method | Captured values |
|---|---|---|
| `args` | `com.example.Calculator.add(int, int)` | arguments 0 and 1 (`int`) |
| `fields` | `com.example.Counter.increment()` | instance field `value` |
| `static` | `com.example.Globals.bump()` | static field `X` |
| `objects` | `com.example.PersonService.greet(com.example.Person)` | argument 0 (object) |

## Stages

The order follows `Runner.run()`.

| File | `data` is | Produced by | Source of the data |
|---|---|---|---|
| `01-run-configuration` | `JavaRunConfiguration` | `RunConfigurationFactory` | Rebuilt from the README Quick Start command |
| `02-instrumentation` | `InstrumentationResult` | `Orchestrator.createInstrumentation` | Rebuilt; paths read from the generated `Collector.java` |
| `03-identifier-mapping` | `Map<int, JavaValueIdentifier>` | `DiSLInstrumentor` (writes `identifiers/*.ser`) | Deserialized from the `.ser` file |
| `04-analysis` | `AnalysisResult` | `Orchestrator.executeAnalysis` | Rebuilt |
| `05-trace` | `Trace` (naive mode) | DiSL `Collector` (writes `traces/*.ser`) | Deserialized from the `.ser` file |
| `06-test-generation` | `TestSuite` | `Orchestrator.generateTests` | Rebuilt; test sources embedded |
| `07-test-execution` | `TestExecutionResult` | `Orchestrator.runTests` | Re-executed (the demo run only logged it) |

The two `.ser` files are the only stage outputs the demo persisted as objects. The other result objects exist only in memory during a run, so I rebuilt them with the same builder calls the pipeline makes, from data on disk. `InstrumentationModel` (the DiSL code model) is not included: it is internal to the Java/DiSL backend and is rendered into `generated-sources/DiSLClass.java`, which is embedded in `02`.

## File envelope

```jsonc
{
  "scenario": "args",
  "stage": "trace",
  "provenance": "Deserialized 1:1 from traces/trace-....ser ...",  // how `data` was obtained
  "notes": ["..."],                                                 // caveats for this file
  "data": { "$type": "Trace", ... },                               // the stage's result object
  "artifacts": [ { "path": "...", "mediaType": "...", "sizeBytes": 0, "sha256": "...", ... } ]
}
```

Artifacts are the files a stage wrote:
- **Text files** (`.java`, `.jt`): embedded verbatim in `content`.
- **Jars**: listed per entry (`name`, `sizeBytes`, `crc32`), with the manifest text included.
- **Other binaries** (`.class`, `.ser`): identified only by `sha256` and `sizeBytes`.

## Encoding rules

- **Objects:** one JSON property per Java instance field, superclass fields first, same field names. Static and transient fields are left out. Derived getters such as `getName()` and `getFullyQualifiedSignature()` are not emitted.
- **`$type`:** the simple Java class name of a model object, for example `JavaArgumentIdentifier` or `JavaFieldIdentifier`. Needed wherever the declared type is abstract: identifier maps, `exportableValues`.
- **`$class`:** marks a captured value of the target application (`ObjectSnapshot`). The rest of the object holds the snapshot's fields in capture order. This is the format `ObjectSnapshot.fromJson` / `JsonObjectParser` already read: `$class` must come first, an integer that fits in `int` is `int`, otherwise `long`, and a number with `.` or an exponent is `double`.
- **Maps with integer keys** (slot ids, `internalId`): JSON object keys as decimal strings (`"1"`), sorted ascending.
- **Sets** (trace values): arrays in sorted order, numbers ascending and objects by their JSON text. Order has no meaning.
- **Trace:** all ten typed maps are always present, even when empty: `byteValues`, `charValues`, `shortValues`, `intValues`, `longValues`, `floatValues`, `doubleValues`, `booleanValues`, `stringValues`, `objectValues`. The map name gives the element type. `char` values are 1-character strings.
- **Enums:** the constant name (`NAIVE`, `ARGUMENT`, `PASSED`).
- **Paths:** POSIX strings relative to the `demo/` folder, for example `output/args/traces/trace-….ser` or `calc-app.jar`. Resolving one against `demo/` gives the path the Java run used. This covers path fields in `data`, `artifacts[].path` and the `manifest.json` entries (`mock_data/args/…`). Every path points inside `demo/`, so none needs `../`.
- **Durations:** ISO-8601 (`PT1.061208864S`). Timestamps are ISO-8601 local date-time with no offset (`2026-10-29T14:24:17.890713141`).
- **Nulls:** every field is present; a Java `null` is a JSON `null`.

Open points for the contract (none of these values occur in the demo):
- JavaScript can't hold a `long` above 2^53 exactly as a JSON number.
- `NaN` and `Infinity` would be emitted as strings.

## Caveats

- **`dislHomePath` is `null`** in `01`. The DiSL home used for the run was not saved anywhere.
- **`resultsListPath` is `null`** in `02`. The instrumentor picks a random file name that is never written, so it can't be recovered.
- **`libs/instrumentation.jar` is DiSLCompiler's stub jar** (manifest `Test-Stub: true`, empty classes). The traces still contain real values. For example, `X` goes 0→3 because DiSLClass captures before and after each of the 3 calls.
- **`07` comes from a re-run on 2026-10-29**, so its timing values come from that run, not from the original demo run. The re-run used `TestRunnerFactory` with the scenario's run configuration, and it compiled byte-identical `.class` files to the ones in `demo/output/*/compiled`.
- **Embedded file text keeps absolute paths.** The `Collector.java` source in `02` still has the original run's absolute `/home/...` paths inside its Java string literals. It is a verbatim copy of the generated file, which is what its `sha256` checks.
- **`07.executionTraces` is always empty.** The test runner does not collect traces yet.
- **`fields` ends in `ERROR`.** The generated `CounterTest` calls `new Counter(0)`, but `Counter` has no such constructor. The compiler message is only logged and is not part of `TestExecutionResult`.

## Verification

The exporter checked every file when it generated it:
- **Round trip:** `data` was parsed back into the Java type and deep-compared field by field with the source object. All 28 files match.
- **`.ser` files:** rebuilt from the JSON and Java-serialized again. 7 of 8 are byte-identical to the originals. The `objects` trace differs only in `HashSet` bucket order and is equal as an object.
- **Artifacts:** each `sha256` and embedded `content` matches the file on disk.
- **Run configuration:** the rebuilt `exportableValues` equal the identifier mapping the original run serialized.
- **Relative paths:** after the switch from absolute paths, a tree diff of old against new JSON showed that only path values changed, plus a few notes reworded to use demo-relative names. Each new path resolves against `demo/` to the old absolute path and exists on disk.

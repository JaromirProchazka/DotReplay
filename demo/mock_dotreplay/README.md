# Mock `dotreplay` CLI

A stand-in for the `dotreplay` CLI. It lets a front end (e.g. the Auto Debugger VS Code extension) and the [backend contract v1](https://github.com/norilanda/debug-infra-gentests/tree/develop/vs-code-extension/contract) be tried out before the real System exists.

It simulates exactly one scenario: the Calculator in [`demo/mock_target_app/args`](../mock_target_app/args), whose `Main` calls `Calculator.Add(1, 2)`, `Add(3, 4)` and `Add(5, 6)`.
- **Calculator inputs:** the mock writes the output the real System would produce for that run, following [doc/](../../doc/README.md) and the contract.
- **Anything else:** the mock fails with the mock-only error `MOCK_UNSUPPORTED_INPUT`.

Nothing is instrumented, compiled or executed.

## Build and run

Run from the repository root:

```bash
dotnet build demo/mock_target_app/args    # -> calc-app.dll
dotnet build demo/mock_dotreplay          # -> bin/Debug/net8.0/dotreplay.dll

# Full run: analysis, test generation and test running
dotnet demo/mock_dotreplay/bin/Debug/net8.0/dotreplay.dll \
  --assembly demo/mock_target_app/args/bin/Debug/net8.0/calc-app.dll \
  --source demo/mock_target_app/args \
  --method "Com.Example.Calculator.Add(int,int)" \
  --parameters 0:int,1:int \
  --output-dir <run dir>

# The same, with the JSON Run configuration
dotnet demo/mock_dotreplay/bin/Debug/net8.0/dotreplay.dll --config demo/mock_dotreplay/calculator.config.json -o <run dir>
```

## Commands

| Command | Stage | Writes |
| --- | --- | --- |
| `dotreplay [options]` | Full run: the three stages in order, stopping after the first failed one | everything below |
| `dotreplay analyze [options]` | [Analysis](../../doc/use-cases/analysis.md) | `run.json`, `trace.json` |
| `dotreplay generate-tests --run-dir <dir> [--max-argument-combinations <count>]` | [Test generation](../../doc/use-cases/test_generation.md) | `tests.json`, `tests/CalculatorTest.cs`, an empty `results.json`, `counts.tests` in `run.json` |
| `dotreplay run-tests --run-dir <dir> [--test <testId>]…` | [Test running](../../doc/use-cases/tests_run.md), as the contract's `run-tests` | `results.json` |

Every stage also updates `manifest.json`, with one entry per stage (`analysis`, `testGeneration`, `testRunning`) holding its `status` (`COMPLETE`, `PARTIAL` or `FAILED`) and, for a failure, the error `code` and `message`. Files are written atomically.

The options of the full run and `analyze` follow the Java Auto-debugger CLI. `dotreplay --help` lists them.

| Difference from Java | Why |
| --- | --- |
| `--assembly` replaces `-j/--jar` | .NET has no jars. |
| No `--disl-home`, `--classpath`, `--api-key` | Only the Java backend needs them. |
| New `--strategy` (default `record`) | The analysis Strategy from our docs (R24, R25). |
| New `--config <file>` | The JSON Run configuration (analysis flow 1.a). Its keys are the long option names, and options on the command line override it. |
| `-l/--language` defaults to `dotnet` | |
| `--max-argument-combinations` only for the full run and `generate-tests` | It belongs to test generation. |

## Accepted inputs

The mock simulates only these values:

| Option | Accepted value |
| --- | --- |
| `--assembly` | An existing file named `calc-app.dll` |
| `--source` | An existing directory containing `Calculator.cs` |
| `--method` | `Com.Example.Calculator.Add(int,int)` or `Com.Example.Calculator.Add(System.Int32,System.Int32)`, ignoring whitespace |
| `--parameters` | Both arguments: `0:int,1:int` (`System.Int32` also works) |
| `--strategy` | `record` |
| `--language`, `--test-strategy`, `--trace-mode` | Only the defaults: `dotnet`, `trace-based-basic`, `naive` |
| `--args` | Anything; it is ignored, because the Calculator ignores its arguments |
| `--fields`, `--static-method`, `--void-method` | Not accepted |

The docs let the User give a list of Target methods. The contract and the Java CLI allow one target per run, so the mock takes a single `--method`.

## Output for the Calculator

- **`run.json`:** `language` `dotnet`, the `--method` text as `target.signature`, the captured arguments `a` and `b` (`System.Int32`, shown as `int`), 3 calls and 9 tests.
- **`trace.json`:** the 3 calls. Their entry and exit snapshots hold `a` and `b`, and every call `returned`.
- **`tests.json`:** Exhaustive combinatorial testing (R31) of `a` ∈ {1, 3, 5} and `b` ∈ {2, 4, 6} gives 9 tests, `Com.Example.CalculatorTest#TestAdd_Scenario1` to `9`.
  - Scenarios 1, 5 and 9 reproduce calls 0, 1 and 2.
  - The other 6 are synthetic (`callIndices: []`).
  - With `--max-argument-combinations` below 9, only the first tests are written, and the Manifest marks test generation `PARTIAL`.
- **`tests/CalculatorTest.cs`:** the generated xUnit tests. This file is private to the backend and not part of the contract.
- **`results.json`:** every test that ran is `passed`, because `Add` never throws. Results of tests that did not run are kept.

## Errors

Every error prints `Error: <CODE>: <message>` to stderr and exits with code 1. Success exits with 0, even when the output is `PARTIAL`.

| Code | When | Files written |
| --- | --- | --- |
| `MOCK_UNSUPPORTED_INPUT` | Mock only. The input is not the Calculator scenario: an unknown or missing option, another assembly, `CalcApp.Main`, `--fields`, a Strategy of `interleaving`, `fuzzing` or `sharpdetect`, or a run dir of another target. | None |
| `INVALID_JSON`, `MISSING_REQUIRED_FIELDS` | The `--config` file is not JSON, or lacks `assembly` or `method`. | None: the Session directory is not known yet |
| `MISSING_TARGET_APPLICATION` | The assembly or the source path does not exist. | `run.json` (`status: failed`), `manifest.json` |
| `MISSING_TARGET_METHODS` | A method or parameter that `calc-app.dll` does not have, e.g. `Calculator.Sub(int,int)` or `-p 2:int`. | `run.json` (`status: failed`), `manifest.json` |
| `INVALID_STRATEGY` | An unknown Strategy. | `run.json` (`status: failed`), `manifest.json` |
| `MISSING_ANALYSIS_OUTPUT`, `INVALID_ANALYSIS_OUTPUT` | `generate-tests` without a valid `trace.json` and `run.json`. | `run.json` (`status: failed`) if it is readable, `manifest.json` |
| `MISSING_TEST_SUITE`, `INVALID_TEST_LIST` | `run-tests` without `tests.json`, or with an unknown `--test`. | `results.json` (`error`), `manifest.json` |

When the run dir does not exist, a failing `generate-tests` or `run-tests` writes nothing.

Not simulated, because the Calculator cannot trigger them: `TIMEOUT`, `TARGET_CRASHED`, `TEST_COMPILE_ERROR`, `TEST_TIMEOUT`, `TEST_RUN_CRASHED`, and the value markers `UNSUPPORTED` and `TRUNCATED`.

## Checking the output against the contract

The contract's schemas use JSON Schema draft 2020-12. Register all five of them with a validator, e.g. Ajv with `allowUnionTypes`, and validate the four files of a run dir. The contract README lists the cross-file rules the schemas cannot check.

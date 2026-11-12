# Errors and Faults

This file documents every error, fault and value marker defined in the documentation, split by stage (R26):

- **Errors** stop the stage. The System terminates with the error code and an error message. If the stage stops before producing any output, the Manifest records it as `FAILED`. If it stops after collecting some output (a crash or a timeout), the output collected so far is kept and the Manifest records it as `PARTIAL`.
- **Faults** affect a single test. The stage reports the fault and continues with the other tests, and the Manifest records the output as `PARTIAL`.
- **Value markers** mark a single value in the analysis output file. The stage continues, and the output stays `COMPLETE`.

Errors and faults tell the User that something in their configuration or Target application needs attention. Value markers report a limitation of the System, not a mistake of the User.

## Errors

### Analysis

| Code | Meaning | Manifest status | Source |
| --- | --- | --- | --- |
| `INVALID_JSON` | The Run configuration file is not valid JSON. | `FAILED` | [analysis.md](use-cases/analysis.md) 1.a.a |
| `MISSING_REQUIRED_FIELDS` | The Run configuration file lacks required fields. | `FAILED` | [analysis.md](use-cases/analysis.md) 1.a.a, R17 |
| `MISSING_TARGET_APPLICATION` | The Target application does not exist. | `FAILED` | [analysis.md](use-cases/analysis.md) 2.a, R17 |
| `MISSING_TARGET_METHODS` | Some Target methods are not present in the Target application. The message lists the missing methods. | `FAILED` | [analysis.md](use-cases/analysis.md) 3.a, R16 |
| `INVALID_STRATEGY` | The selected Strategy cannot be loaded or is invalid. | `FAILED` | [analysis.md](use-cases/analysis.md) 4.a, R25 |
| `TIMEOUT` | The Target application ran longer than the configured timeout (e.g. a deadlock). The values captured so far are kept. | `PARTIAL` | [analysis.md](use-cases/analysis.md) 5.b, R27 |
| `TARGET_CRASHED` | The Target application crashed. The values captured so far are kept, and the System itself does not crash. | `PARTIAL` | [analysis.md](use-cases/analysis.md) 5.c |

### Test generation

| Code | Meaning | Manifest status | Source |
| --- | --- | --- | --- |
| `MISSING_ANALYSIS_OUTPUT` | The analysis output file does not exist. | `FAILED` | [test_generation.md](use-cases/test_generation.md) 1.a |
| `INVALID_ANALYSIS_OUTPUT` | The analysis output file is invalid. | `FAILED` | [test_generation.md](use-cases/test_generation.md) 1.b |

### Test running

| Code | Meaning | Manifest status | Source |
| --- | --- | --- | --- |
| `MISSING_TEST_SUITE` | The Test suite does not exist. | `FAILED` | [tests_run.md](use-cases/tests_run.md) 1.a |
| `INVALID_TEST_LIST` | The List of selected tests is invalid. | `FAILED` | [tests_run.md](use-cases/tests_run.md) 2.a, R20 |
| `TEST_RUN_CRASHED` | The test running crashed. The test results and Traces collected so far are kept, and the System terminates gracefully. | `PARTIAL` | [tests_run.md](use-cases/tests_run.md) 3.a, R26 |

## Faults

| Code | Stage | Meaning | Source |
| --- | --- | --- | --- |
| `TEST_COMPILE_ERROR` | Test generation | A generated test does not compile. The test is removed. | [test_generation.md](use-cases/test_generation.md) 3.a |
| `TEST_TIMEOUT` | Test running | A test ran longer than the configured timeout. The test is terminated and its result is marked `TEST_TIMEOUT`. | [tests_run.md](use-cases/tests_run.md) 3.b, R27 |

## Value markers

| Marker | Contract kind | Stage | Meaning | Source |
| --- | --- | --- | --- | --- |
| `UNSUPPORTED` | `skipped` | Analysis | The value cannot be captured, e.g. an unsupported object. | [analysis.md](use-cases/analysis.md) 5.a, R26 |
| `TRUNCATED` | `truncated` | Analysis | The value exceeds a configured limit (depth, collection size, string length). | R29 |

In `trace.json`, a marked value is written as a value of the matching contract `kind`.

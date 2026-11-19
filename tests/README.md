# Tests

Black-box tests of the `dotreplay` CLI. They run the CLI as a separate process and check the files it writes to the Session directory, so they never depend on how the CLI is implemented. Today they run against the mock in [`demo/mock_dotreplay`](../demo/mock_dotreplay); later the real app can replace it without changing the tests.

## Running

```bash
dotnet build demo/mock_target_app/args   # the Target application (calc-app.dll)
dotnet build demo/mock_dotreplay         # the CLI under test
dotnet test tests/DotReplay.Tests
```

Run only one kind with `--filter Category=Unit` or `--filter Category=Integration`.

## Choosing the CLI under test

| Variable | Meaning | Default |
| --- | --- | --- |
| `DOTREPLAY_CLI` | The CLI: a `.dll` (run with `dotnet`) or an executable. Relative paths start at the repository root. | The newest `dotreplay.dll` under `demo/mock_dotreplay/bin` |
| `DOTREPLAY_TARGET_APP` | `calc-app.dll`, the Target application of the Calculator scenario. | The newest `calc-app.dll` under `demo/mock_target_app/args/bin` |

To switch to the real app, build it and set `DOTREPLAY_CLI` to its output. In CI, change `CLI_PROJECT` and `DOTREPLAY_CLI` in [`.github/workflows/ci.yml`](../.github/workflows/ci.yml). The defaults live in [`Infrastructure/DotReplayCli.cs`](DotReplay.Tests/Infrastructure/DotReplayCli.cs).

## What is tested

The Calculator scenario: `CalcApp.Main` calls `Calculator.Add(1, 2)`, `Add(3, 4)` and `Add(5, 6)`.

| Kind | Folder | Covers |
| --- | --- | --- |
| Unit | [`Unit/`](DotReplay.Tests/Unit) | One stage per test, on prepared input: `analyze`, `generate-tests` and `run-tests`. Each has a happy path and its documented errors ([errors.md](../doc/errors.md)). |
| Integration | [`Integration/`](DotReplay.Tests/Integration) | The full run end to end ([pipeline_run.md](../doc/use-cases/pipeline_run.md)), and a full run that stops after a failed analysis. |

Every test checks the result in three ways:
- **Expected output:** the Session files equal the golden files in [`Expected/calculator/`](DotReplay.Tests/Expected/calculator), apart from the `createdAt` and `completedAt` timestamps.
- **Contract:** `run.json`, `trace.json`, `tests.json` and `results.json` validate against the contract schemas, and meet the contract's rules across files.
- **Outcome:** the exit code, the documented error code on stderr, the Manifest status, and the `error` fields of `run.json` and `results.json`.

`MOCK_UNSUPPORTED_INPUT` is mock-only and is not tested.

## Contract schemas

[`Contract/schemas/`](DotReplay.Tests/Contract/schemas) is an unmodified copy of the backend contract v1 schemas, from [norilanda/debug-infra-gentests](https://github.com/norilanda/debug-infra-gentests/tree/d848e69984484bf42ee6d1307a09b6e7105164f7/vs-code-extension/contract) at commit `d848e69` (branch `develop`). When the contract changes, copy the new schemas here and update the golden files if needed.

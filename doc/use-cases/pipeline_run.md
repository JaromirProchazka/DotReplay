# Pipeline Run

- **Requirements covered:** R6, R9, R26
- **Preconditions:**
  1. The System is installed with all its dependencies.
  2. The User has a CLI Target application.
  3. The User has CLI arguments as configuration for the Target application.
  4. The User has Target methods relevant to the bug (they might be generated from logs).
  5. A configured analysis Strategy is accessible to the System (if none is provided, the default is the `record` Strategy).

## Basic Flow

1. User runs the System in the full run mode (`dotreplay [options]`), giving the path to the Target application, the application CLI arguments, a list of Target methods, the Session directory and optionally the Strategy and `maxArgumentCombinations` as CLI arguments.
2. System runs the analysis stage as in [analysis.md](analysis.md), steps 2 to 6. It writes `run.json` and the analysis output file to the Session directory and records the stage in the Manifest.
3. System runs the test generation stage as in [test_generation.md](test_generation.md). It writes the Test suite, adds the number of tests to `run.json` and records the stage in the Manifest.
4. System runs all tests of the Test suite as in [tests_run.md](tests_run.md). It writes the test results and records the stage in the Manifest.
5. System decides the User's next action from the test results and writes it as the next-step hint to the Manifest and to the standard output:
   - If some tests failed, it lists them and tells the User to inspect their Argument values, fix the bug and rerun them with `dotreplay run-tests --run-dir <dir> --test <testId>`.
   - If all tests passed, it tells the User to validate the bug fix, or to choose other Target methods and start a new full run if the bug was not reproduced.

## Alternative Flows

- 1.a. User gives the Target application, the application CLI arguments, the Strategy and a list of Target methods in a JSON Run configuration file, as in [analysis.md](analysis.md) flow 1.a. System validates the file and continues with step 2.
  - 1.a.a. Validation fails; System terminates with the `INVALID_JSON` or `MISSING_REQUIRED_FIELDS` error and an error message.
- 2.a. The analysis stage ends with an error and the Manifest records it as `FAILED`; `run.json` is still written, with status `failed` and the error. System does not run the next stages. The next-step hint tells the User to fix the input named by the error code and start the full run again.
- 2.b. The analysis stage ends as `PARTIAL` (`TIMEOUT` or `TARGET_CRASHED`); System continues with step 3 using the values captured so far. A call that did not finish has an `exit` of `null` and no `outcome`. The next-step hint says that the Test suite is based on incomplete values.
- 3.a. The test generation stage ends with an error and the Manifest records it as `FAILED`; `run.json` gets status `failed` and the error. System does not run the tests. The next-step hint tells the User to fix the input named by the error code and start the full run again.
- 3.b. The test generation stage ends as `PARTIAL` (the `maxArgumentCombinations` limit was reached, or a `TEST_COMPILE_ERROR` fault removed a test); System continues with step 4 using the tests that were generated.
- 4.a. The test running stage crashes (`TEST_RUN_CRASHED`) and the Manifest records it as `PARTIAL`; `results.json` keeps the test results collected so far and gets a top-level `error`, and `run.json` does not change. System continues with step 5; the next-step hint also tells the User to rerun the tests that have no result.
- 4.b. A test reports the `TEST_TIMEOUT` fault; its result has status `error` with the fault in its message, and the Manifest records the output as `PARTIAL`. System continues with step 5 and counts the test among those to inspect.

## Postconditions

1. A Session directory, which follow the Back-end API, and a Manifest with the status of each stage that ran and the next-step hint.
2. The System ends with exit code `0` if no stage ended as `FAILED`, and with a non-zero exit code otherwise.

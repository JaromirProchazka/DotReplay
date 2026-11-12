# Test generation

- **Requirements covered:** R4, R22, R26, R31
- **Preconditions:**
  1. An analysis output file exists.

## Basic Flow

1. System validates the analysis output file.
2. System incrementally generates the tests from the Argument values, up to `maxArgumentCombinations` tests. It does not generate them all at once in memory, but continuously writes them to the output tests file through a pipeline.
3. System runs a per-test compile check, so that one bad test does not fail the whole run.

## Alternative Flows

- 1.a. The analysis output file does not exist; System terminates with the `MISSING_ANALYSIS_OUTPUT` error and an error message.
- 1.b. The analysis output file is invalid; System terminates with the `INVALID_ANALYSIS_OUTPUT` error and an error message.
- 2.a. System reaches the `maxArgumentCombinations` limit; the output is still valid for test running, but the Manifest records it as `PARTIAL`.
- 3.a. A test does not compile; System reports the `TEST_COMPILE_ERROR` fault, removes the bad test and continues with the other tests. The Manifest records the output as `PARTIAL`.

## Postconditions

1. A (potentially large) Test suite.

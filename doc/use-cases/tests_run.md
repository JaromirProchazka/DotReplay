# Test running

- **Requirements covered:** R5, R20, R26, R27
- **Preconditions:**
  1. A Test suite exists.
  2. The User has a List of selected tests (if none is provided, all tests are selected).

## Basic Flow

1. System validates that the Test suite exists.
2. System validates the List of selected tests.
3. System runs the selected tests from the Test suite.
4. System collects the test results and Traces.

## Alternative Flows

- 1.a. The Test suite does not exist; System terminates with the `MISSING_TEST_SUITE` error and an error message.
- 2.a. The List of selected tests is invalid; System terminates with the `INVALID_TEST_LIST` error and an error message.
- 3.a. The test running crashes; System terminates gracefully with the `TEST_RUN_CRASHED` error and an error message. The test results and Traces collected so far are kept, and the Manifest records the output as `PARTIAL`.
- 3.b. A test runs for too long; System terminates the test, marks its result with the `TEST_TIMEOUT` fault and continues with the other tests. The Manifest records the output as `PARTIAL`.

## Postconditions

1. Serialized test results and Traces.

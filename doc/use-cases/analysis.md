# Analysis - Value collection of Target methods

- **Requirements covered:** R1, R2, R3, R13, R16, R17, R21, R24, R25, R26, R27
- **Preconditions:**
  1. The User has a CLI Target application.
  2. The User has CLI arguments as configuration for the Target application.
  3. The User has Target methods relevant to the bug (they might be generated from logs).
  4. The System is installed with all its dependencies.
  5. A configured analysis Strategy is accessible to the System (if none is provided, the default is the `record` Strategy).

## Basic Flow

1. User runs the System, giving the path to the Target application, the application CLI arguments, a list of Target methods and optionally the Strategy as CLI arguments.
2. System validates that the Target application exists.
3. System validates that the Target methods are present in the Target application.
4. System loads the Strategy and validates it.
5. System runs the Target application and uses instrumentation and the Strategy to collect the runtime values of the specified Target methods until the Target application exits.
6. System produces an analysis output file with the captured runtime values.

## Alternative Flows

- 1.a. User runs the System, giving the path to the Target application, the application CLI arguments, the Strategy and a list of Target methods in a JSON Run configuration file. System validates the file and continues with step 2.
  - 1.a.a. Validation fails; System terminates with the `INVALID_JSON` or `MISSING_REQUIRED_FIELDS` error and an error message.
- 2.a. The Target application does not exist; System terminates with the `MISSING_TARGET_APPLICATION` error and an error message.
- 3.a. Some Target methods are missing; System terminates with the `MISSING_TARGET_METHODS` error and an error message listing the missing methods.
- 4.a. The Strategy is invalid; System terminates with the `INVALID_STRATEGY` error and an error message, and does not load a default Strategy.
- 5.a. A value, such as an unsupported object, cannot be captured; System marks it in the analysis output file as `UNSUPPORTED`. The output stays `COMPLETE`.
- 5.b. The Target application hangs (e.g. a deadlock); after the configurable timeout, System terminates with the `TIMEOUT` error and an error message. The analysis output file keeps the values captured so far, and the Manifest records it as `PARTIAL`.
- 5.c. The Target application crashes; System terminates with the `TARGET_CRASHED` error and an error message, and does not crash itself. The analysis output file keeps the values captured so far, and the Manifest records it as `PARTIAL`.

## Postconditions

1. An analysis output file with the captured runtime values.

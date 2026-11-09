# Value collection of target methods

- **Requirements covered:** R1, R2, R3, R13, R16, R17, R21, 27
- **Preconditions:**
  1. User has a CLI Target application
  2. User has CLI arguments as configuration for the Target application
  3. User has a number of methods relevant to the Bug (might be generated from Logs)
  4. User has the system installed with all the dependencies

## Basic Flow

1. User runs the System, giving path to Target application, application CLI arguments and a list of Target methods as CLI arguments
2. System validates that the Target application exists 
3. System validates the Target mehods as present in the Target application
4. System runs the Target application and uses instrumentation to collect the runtime values of the specified Target methods untill the Target application exits. 
5. System produces a values file with the captured runtime values 

## Alternative Flows

1.a. User runs the System, giving path to Target application, application CLI arguments and a list of Target methods in a JSON configuration file

- 1.b. The JSON configuration file is validated 
  - 1.b.a. Validation fails, program exists with an errors: `INVALID_JSON` or `MISSING_REQUIRED_FIELDS` and error message
- 2.a. System terminates with `MISSING_TARGET_SYSTEM` and an error message
- 3.a. System terminates with `MISSING_TARGET_METHODS` and an error message
with list of the missing methods
- 4.a. if a value like an unsupported object can not be captured, it is noted in the product values file as `UNSUPPORDED`; deadlock ends with `TIMEOUT` error (can be configured), target crash ends with `TARGET_CRASHED` error, the System does not crash

## Postconditions

1. A serialized object file with captured runtime values
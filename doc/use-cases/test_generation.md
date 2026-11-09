# Test generation

- **Requirements covered:** R4, R22
- **Preconditions:**
  1. have an object file with captured runtime values

## Basic Flow

1. System validates the input values file
2. System incrementaly generates the tests from the runtime values, not all at once in memory, but in a pipeline continuosly writing them to the output tests file
3. System does a per-test compile check, so that one bad test doesn't fail the whole run

## Alternative Flows

1. a. System terminates with `INVALID_RUNTIME_ARGUMENTS` error. 
3. a. System generates a `TEST_COMPILE_ERROR` error. 

## Postconditions

1. A (potentially large) set of tests.
# Tests run

- **Requirements covered:** R5, R20, R27
- **Preconditions:**
  1. have a Test suit
  2. have a List of selected tests

## Basic Flow

1. Validates the List of selected tests
2. Compile and Run the selected tests from Test suit
3. Collect results and traces

## Alternative Flows

- 2.a. The test running crashes, System ends gracefully with an error message. 
- 2.b. If the test runs for too long, System terminates it and marks its result as `TIMEOUT`  
- 2.c. If the test compilation crashes, ends with an error message 

## Postconditions

1. The resilts and traces serialized objects
# Requirements

| Name                                                                                                           | Priority | Focus  | Type           | Expectation | Depending On | Comment |
| -------------------------------------------------------------------------------------------------------------- | -------- | ------ | -------------- | ----------- | ------------ | ------------ |
| R1. System lets user specify the set of analyzed Target methods and variable.                              | Must     | User   | Functional     | Normal      |              |  |
| R2. System lets user specify the Target application and its configuration to run.                   | Must     | User   | Functional     | Normal      |              |  |
| R3. System produces from an target application execution and target methods a set of collected argument values. Supurts primitive types: numbers, bool, char, enum and null. | Must     | User   | Functional     | Normal      | R1,R2          |  |
| R4. System, given a set of collected argument values and target methods, generates a test suit.                 | Must     | User   | Functional     | Normal      | R1,R3          |  |
| R5. System given a test suite can run these tests and collect their results and traces.                          | Must     | User   | Functional     | Normal      | R4            |  |
| R6. System can decide, which of the workflow step is run next and instruct user to about their required actions   | Should    | User   | Functional     | Exciting    | R3,R4,R5        |  |
| R7. System recording works on multi-threaded target applications. | Should   | System | Functional | Normal      | R2,R3            |  |
| R8. System can find and generate tests for concurrency errors like data races and deadlocks.                    | Could   | User   | Functional     | Exciting    | R4,R7,R24          |  |
| R9. System implements a documented "Back-end API" shared with the Java Auto-debugger and AVM for C++, that can be used by separate Front-end GUI. | Must     | System | Functional     | Normal      |              |  |
| R10. System separates test generation module from the Analysis module.                                          | Should   | System | Non-functional | Expected | R4,R3          |  |
| R11. System separates test generation module from the tests running module.                                     | Should   | System | Non-functional | Expected | R4,R5          |  |
| R12. The tests are anotate with references to target aplication source code, to the part the test is for.       | Should    | User   | Functional     | Indifferent | R4            | Needed by front-end. |
| R13. The system also supports object type, serializable by reflection, for target application argument values collection and building (reconstruction). | Should | User | Functional | Normal | R3,R4,R21 |  |
| R14. The instrumentation and analyses for the case-study application on the "Target Machine" adds less than 100% to execution time. | Should | User | Non-functional | Expected | |  |
| R15. A run configuration in the case study on the Target machine starts executing in less then 10s. | Should | User | Non-functional | Expected | | So that target app is not recompiled on each new run |
| R16. The specified Target methods are first validated. The methods being invalid resutls in an error report without crashing. | Could | System | Non-functional | Normal | R1 |  |
| R17. The specified Target application and configuration is first validated. These being invalid resutls in a error report without crashing. | Could | System | Non-functional | Normal | R2 |  |
| R18. Sensitive data (environment variables, program arguments) are not kept beyond the session. | Must | Business | Non-functional | Normal | | What if the argument capture catches a user password as target function argument and record it inside a test? |
| R19. System supports .NET 8, x64, Windows. | Must | System | Non-functional | Normal | | |
| R20. System given a test suit can run selected tests via the CLI. | Must     | User   | Functional     | Normal      | R5            | Needed by the front end. |
| R21. System produces from an target application execution and target methods a set of collected argument values. Supurts more complex types: strings, arrays. | Must     | User   | Functional     | Normal      | R1,R2,R3          |  |
| R22. System, while generating tests, does not generate all at once in memory, but continuously writes them to the output file throw a pipeline. | Should | System | Non-functional | Normal | R4 | |
| R23. Stages are invoked only through the controller and exchange data only through the model. | Should | System | Non-functional | Expected | R10,R11 | |
| R24. The analysis stage supports multiple swapable strategies including custom onece (Concurrency strategies can be swapped in at this point). Every strategy writes the collected values in the common format. | Should | System | Non-functional | Normal | R3 |
| R25. The user can select and configure built-in or custom analysis strategies in the run configuration. | Should | User | Functional | Normal | R2,R24 | |
| R26. Every failure ends with a documented error code and message; values that cannot be captured are marked `UNSUPPORTED`; incomplete outputs are marked as partial. | Should | System | Non-functional | Expected | |
| R27. Target executions and test runs have configurable timeouts, so a hanging target (e.g. a deadlock) ends with `TIMEOUT` instead of blocking. | Could | System | Non-functional | Expected | R3,R5 | |
| R28. The data contract is versioned, and every stage output is published atomically with a manifest containing its status. | Should | System | Non-functional | Expected | R9 | |
| R29. Captured values are bounded by configurable limits (depth, collection size, string length); values exceeding them are marked `TRUNCATED`. | Could | System | Non-functional | Normal | R13,R14 | |
| R30. The test-generation stage accepts collected values together with concurrency information (e.g. thread IDs, per-thread call order). |	Should |	System | Functional |	Normal | R4,R7,R24 | The exact concurrency information is not yet decided; thread IDs are the first candidate. |
| R31. Tests are generated for all possible combinations of collected runtime values called product tests.  | Must | User | Functional | Normal | R4,R24 | |
| R32. There is a filter on the product tests removing some useless once. | Could | User | Functional | Normal | R31 | |


| | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |

TODO: versioned schema, atomic writes, run manifest, partial/crashed-run status, encoding of "not recorded"/unsupported values

# Lexicon

**Priority:** Must, Should, Could, Won't
**Focus:** Business, User, System
**Type:** Functional, Non-functional
**Expectation:** Normal, Exciting, Expected, Indifferent

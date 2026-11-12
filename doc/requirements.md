# Requirements

| Name | Priority | Focus | Type | Expectation | Depending On | Comment |
| --- | --- | --- | --- | --- | --- | --- |
| R1. The System lets the User specify the set of analyzed Target methods and variables. | Must | User | Functional | Normal | | |
| R2. The System lets the User specify the Target application and its configuration to run. | Must | User | Functional | Normal | | |
| R3. The System produces, from a Target application execution and Target methods, a set of collected Argument values. Supports primitive types: numbers, bool, char, enum and null. | Must | User | Functional | Normal | R1, R2 | |
| R4. The System, given a set of collected Argument values and Target methods, generates a Test suite. | Must | User | Functional | Normal | R1, R3 | |
| R5. The System, given a Test suite, can run these tests and collect their results and Traces. | Must | User | Functional | Normal | R4 | |
| R6. There is a setting or mode of the System in which it decides which workflow step runs next and instructs the User about their required actions. | Should | User | Functional | Exciting | R3, R4, R5 | |
| R7. System recording works on multithreaded Target applications. | Should | System | Functional | Normal | R2, R3 | |
| R8. The System can find and generate tests for concurrency errors like data races and deadlocks. | Could | User | Functional | Exciting | R4, R7, R24 | |
| R9. The System implements a documented, versioned Back-end API shared with the Java Auto-debugger and AVM for C++, which a separate front-end GUI can use. | Must | System | Functional | Normal | | |
| R10. The System separates the test generation module from the analysis module. | Should | System | Non-functional | Expected | R3, R4 | |
| R11. The System separates the test generation module from the test running module. | Should | System | Non-functional | Expected | R4, R5 | |
| R12. The tests are annotated with references to the part of the Target application source code they test. | Should | User | Functional | Expected | R4 | Needed by the front end. |
| R13. The System also supports object types serializable by reflection, for Argument values collection and building (reconstruction). | Should | User | Functional | Normal | R3, R4, R21 | |
| R14. The instrumentation and analysis of the case study application on the Target machine add less than 100% to execution time. | Should | User | Non-functional | Expected | | |
| R15. A configured run in the case study on the Target machine starts executing in less than 10 s. | Should | User | Non-functional | Expected | | So that the Target application is not recompiled on each new run. |
| R16. The specified Target methods are validated first. Invalid Target methods result in an error report without crashing. | Could | System | Non-functional | Normal | R1 | |
| R17. The specified Target application and configuration are validated first. If they are invalid, the result is an error report without crashing. | Could | System | Non-functional | Normal | R2 | |
| R18. Sensitive data that are not part of the argument capture (environment variables, program arguments) are not kept beyond the session. | Must | Business | Non-functional | Normal | | It is the User's responsibility not to pass sensitive data into runtime values captured by the analysis. |
| R19. The System supports .NET 8, x64, Windows. | Must | System | Non-functional | Normal | | |
| R20. The System, given a Test suite, can run selected tests via the CLI using a List of selected tests. | Must | User | Functional | Normal | R5 | Needed by the front end. |
| R21. The System produces, from a Target application execution and Target methods, a set of collected Argument values. Supports more complex types: strings, arrays. | Must | User | Functional | Normal | R1, R2, R3 | |
| R22. The System, while generating tests, does not generate them all at once in memory, but continuously writes them to the output file through a pipeline. | Should | System | Non-functional | Normal | R4 | |
| R23. Stages are invoked only through the controller and exchange data only through the model. | Should | System | Non-functional | Expected | R10, R11 | |
| R24. The analysis stage supports multiple swappable Strategies, including custom ones (concurrency Strategies can be swapped in at this point). Every Strategy writes the collected values in the common format. | Should | System | Non-functional | Normal | R3 | |
| R25. The User can select and configure built-in or custom analysis Strategies in the Run configuration. | Should | User | Functional | Normal | R2, R24 | |
| R26. Every failure ends with a documented error code and message; values that cannot be captured are marked `UNSUPPORTED`; incomplete outputs are marked as `PARTIAL`. | Should | System | Non-functional | Expected | | |
| R27. Target executions and test runs have configurable timeouts, so a hanging target (e.g. a deadlock) ends with `TIMEOUT`, and a hanging test with `TEST_TIMEOUT`, instead of blocking. | Could | System | Non-functional | Expected | R3, R5 | |
| R28. The data contract is versioned, and every stage output is published atomically with a Manifest containing its status. | Should | System | Non-functional | Expected | R9 | |
| R29. Captured values are bounded by configurable limits (depth, collection size, string length); values exceeding them are marked `TRUNCATED`. | Could | System | Non-functional | Normal | R13, R14 | |
| R30. The test generation stage accepts collected values together with concurrency information (e.g. thread IDs, per-thread call order). | Should | System | Functional | Normal | R4, R7, R24 | The exact concurrency information is not yet decided; thread IDs are the first candidate. |
| R31. Tests are generated for all possible combinations of collected runtime values, up to the configurable `maxArgumentCombinations` limit (Exhaustive combinatorial testing). | Must | User | Functional | Normal | R3, R4, R21 | |
| R32. A filter on the Exhaustive combinatorial tests removes useless ones. | Could | System | Functional | Normal | R31 | |


<!-- | | | | | | | |  -->
<!-- | | | | | | | |  -->
<!-- | | | | | | | |  -->
<!-- | | | | | | | |  -->

## Lexicon

- **Priority:** Must, Should, Could, Won't
- **Focus:** Business, User, System
- **Type:** Functional, Non-functional
- **Expectation:** Normal, Exciting, Expected, Indifferent

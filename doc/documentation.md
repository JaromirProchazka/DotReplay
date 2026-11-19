# DotReplay Documentation

This document collects the project-level documentation of DotReplay: why the project exists, the domain terms, the debugging workflow it supports, its requirements, and the risks and evaluation plan. The architecture, use cases, errors and schedule are kept in their own files (see [README.md](README.md)).

## Contents

1. [Vision](#vision)
2. [List of Terms](#list-of-terms)
3. [Debugging workflow](#debugging-workflow)
4. [Requirements](#requirements)
5. [Risks](#risks)
6. [Evaluation](#evaluation)

## Vision

Automate and speed up the bug-fixing process for C#/.NET developers of long-running, multithreaded applications.

Given a bug report in a software system, we implement an iterative [debugging workflow](#debugging-workflow) and automate the lengthy steps of runtime analysis, finding error cases and creating unit tests for these cases.

Developers can thus spend more time on understanding errors, updating source code and validating bug fixes.

## List of Terms

- **User**: A C#/.NET developer tasked with fixing a reported bug.
- **Target methods**: The set of methods and variables relevant to a reported bug. The User expects the bug to manifest in the runtime values and arguments of these methods and variables.
- **Target application**: The application or environment, and its configuration, relevant to the reported bug. It can be run with instrumentation to replicate the bug.
- **Testing / Test suite / Tests**: A set of automatically generated unit tests with arguments collected by the runtime analysis. It is the `tests.json` file, together with the generated test sources.
  - **Test generation**: Automatic generation of the Test suite.
  - **Test running**: Execution of the Test suite and collection of Traces and test results. The test results are the `results.json` file.
- **Argument values**: A set of tuples of runtime values of Target method arguments and runtime variables, collected by the dynamic analysis.
- **Analysis output file**: The file produced by the analysis stage. It contains the captured Argument values in the common format written by every Strategy. It is the `trace.json` file.
- **Trace**: Information about the execution of a given test from the Test suite. Not to be confused with the `trace.json` file, which is the Analysis output file.
- **API or Contract or Back-end API/Contract**: A well-defined, documented API providing all the information important for a front-end GUI. It is a data contract: a JSON schema plus a command-line interface. Version 1 is defined in the [Auto Debugger contract](https://github.com/norilanda/debug-infra-gentests/tree/develop/vs-code-extension/contract).
- **The case study**: A benchmark and test on a chosen medium-sized C#/.NET application.
- **Target machine**: The machine used for running the case study:
  - AMD Ryzen 5 4600HS processor
  - 16 GB of RAM
  - 4 GB of graphics RAM
  - Windows
- **Session directory**: A directory produced by the System, containing its results. These results follow the Back-end API. It is the contract's run directory, marked by the `run.json` file.
- **Stage**: A System module implementing one part of the Debugging workflow (analysis, test generation, test running).
- **Strategy**: Swappable, potentially custom, code loaded and run by the analysis stage (in the future, potentially also by other stages).
- **Exhaustive combinatorial testing**: A test generation approach in which tests are generated for each possible combination of Target method values. The configurable `maxArgumentCombinations` value limits the number of tests.
- **List of selected tests**: A list of tests from a Test suite to run in the test running stage.
- **Manifest**: A small JSON status file, `manifest.json`, that the controller writes after every stage. It works as the stage's receipt and marks the stage output with one of these statuses:
  - `COMPLETE`: the output is whole. It may still contain value markers (`UNSUPPORTED`, `TRUNCATED`), which are limitations of the System.
  - `PARTIAL`: the output is usable but incomplete (the `maxArgumentCombinations` limit was reached, a fault removed or terminated a test, or a crash or timeout stopped the stage after it had collected some output).
  - `FAILED`: the stage ended with an error before producing any output.

  After a full run, the Manifest also holds the next-step hint: a message telling the User their next action (R6).
- **Run configuration**: A configuration file specifying the System's runtime properties.

<!-- - ****: -->

## Debugging workflow

This document describes the abstract debugging workflow in detail. The pseudo-code below shows the required inputs and the workflow loop, which runs until the bug fix passes all tests and is validated.

```
Input: target application A, target method identifier Im
Output: generated test suite for target method m
    done ← false
    m ← findMethodByIdentifier(Im)
    testSuite ← {}
    while ¬done do
        D ← CreateInstrumentation(m)
        (V1, V2, ..., Vn) ← RunDynamicAnalysis(A, D)
        testSuite ← GenerateTests(m, (V1, V2, ..., Vn))
        allPassed ← true
        for T ∈ testSuite do
            (result, trace) ← ExecuteTest(T)
            if result = failed then allPassed ← false
        end for
        if allPassed then
            done ← ValidateBugFix() // Developer task
        end if
        if ¬done then
            // Developer inspects the trace and edits
            // the source code to create bug fixes
        end if
    end while
    return testSuite
```

Some parts of this workflow are reserved for the developer's intervention, like updating the source code with bug fixes and validating the bug fix.

### Step-by-step description

1. The developer chooses a set of procedures and variables in the source code relevant to the bug. The dynamic analysis runs on these.

2. The System runs the dynamic analysis. For each relevant method call, it records the values of the arguments and the fields of the receiver object (internal state), the return value (if used), and the runtime values of relevant global variables and contents of global data structures.

3. In this step, tests for all possible combinations of runtime values collected by the dynamic analysis are generated. This implies that at least one test case is generated for each observed call of every Target method, because the developer does not know exactly — right at the beginning of a debugging session — which method calls and argument values trigger the runtime error.

4. When the execution of all tests for possible scenarios finishes, a set of failing tests points developers towards input values (method call arguments, object fields) and source code fragments that are likely responsible for the error being analyzed. Developers then carefully inspect the respective source code fragments to find out where and how the error occurs.

## Requirements

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
| R22. The System, while generating tests, does not generate them all at once in memory, but continuously writes them to the output file through a pipeline. | Should | System | Non-functional | Normal | R4 | Write is atomic from the outside, but in the process of writing use pipeline |
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

### Lexicon

- **Priority:** Must, Should, Could, Won't
- **Focus:** Business, User, System
- **Type:** Functional, Non-functional
- **Expectation:** Normal, Exciting, Expected, Indifferent

## Risks

Regarding our primary goals, there is a risk that the instrumentation added to the program will cause significant slowdowns. As part of the requirements, we aim not to exceed a 100% slowdown in the case study (R14). However, performance limitations may affect the size of the debugged application, as well as the number of specified Target methods and variables.

This problem is even more significant for debugging concurrent applications, as the chosen Strategy can lead to an exponential number of possible schedules for several threads.

Another risk is an unforeseen limitation of the technologies used. For instance, the instrumentation framework might not support reconstruction of some complex data structures from memory using reflection. For this reason, we included a feasibility experiment at the beginning of the schedule to identify these limitations.

## Evaluation

We plan to evaluate the project on mid-sized applications from the SWE-Sharp-Bench benchmark for C#. It features 150 real bugs and issues from 17 mid-sized open-source repositories.

As success criteria, the test application is configured, runtime values are captured, valid tests are generated, and the bug is localized through the generated tests. Further, we aim to measure the instrumentation overhead and the start-up time to evaluate requirements R14 and R15.

### Evaluation on concurrent applications

Unfortunately, SWE-Sharp-Bench is not suitable for evaluating the concurrency features of this project. Although the success criteria are the same as for the serial version, we aim to use a subset of the SCTBench collection translated to C# in the work on Fuzzing over Thread Schedules. Although these benchmarks are suitable for testing the functionality of the project, they are small rather than mid-sized applications.

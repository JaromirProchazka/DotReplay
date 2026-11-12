# Debugging workflow

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

## Step-by-step description

1. The developer chooses a set of procedures and variables in the source code relevant to the bug. The dynamic analysis runs on these.

2. The System runs the dynamic analysis. For each relevant method call, it records the values of the arguments and the fields of the receiver object (internal state), the return value (if used), and the runtime values of relevant global variables and contents of global data structures.

3. In this step, tests for all possible combinations of runtime values collected by the dynamic analysis are generated. This implies that at least one test case is generated for each observed call of every Target method, because the developer does not know exactly — right at the beginning of a debugging session — which method calls and argument values trigger the runtime error.

4. When the execution of all tests for possible scenarios finishes, a set of failing tests points developers towards input values (method call arguments, object fields) and source code fragments that are likely responsible for the error being analyzed. Developers then carefully inspect the respective source code fragments to find out where and how the error occurs.

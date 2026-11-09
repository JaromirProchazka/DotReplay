# Debugging workflow

This document describes the abstract debugging workflow in detail. The pseudo-code bellow illustrates the inputs required, and the loop of the workflow running intill the bugfix passes all tests and is validated.

```
Input: target application A, target method identifier Im
Output: Generated test suite for target method m
    done ← f alse
    m ← f indMethodByIdentif ier(Im)
    testSuite ← {}
    while ¬ done do
        D ← CreateInstrumentation(m)
        (V1, V2, . . . , Vn) ← RunDynamicAnalysis(A, D)
        testSuite ← GenerateT ests(m,(V1, V2, . . . , Vn))
        allP assed = true
        for T ∈ testSuite do
            (result, trace) ← ExecuteT est(T)
            if result = f ailed then allP assed = f alse
        end for
        if allP assed then
            done ← ValidateBugFix() // Developer task
        end if
        if ¬ done then
            // Developer will inspect the trace and edit
            // source code to create bug fixes
        end if
    end while
    return testSuite
```

There are parts of this workflow reserved for the developers intervention like the source code updating with bugfixes and Bugfix validation.

# Step by step description

1. Developer chooses a set of procedures and variables in source code relevant to the bug. On these will be run the dynamic analysis.

2. System runs dynamic analysis. For each relevant
   method call, it records values of arguments and fields of the receiver object (internal state), the return value (if used), and runtime values of relevant global variables and contents of global data structures.

3. In this step, tests for all possible combinations of runtime values, collected by the dynamic analysis, are generated. Specifically, at least one test case is generated for each observed call of every target method, because the developer does not know exactly — right at the beginning of a debugging session — which method calls and their argument values trigger the runtime error.

4. When execution of all tests for possible scenarios finishes, a set of failing tests points developers towards input values (method call arguments, object fields) and source code fragments that are likely responsible for the error subject to analysis. In cases similar to our example (Listing 2), developers would then carefully inspect the respective source code fragments to find out how the suspicious values (file paths) are created and where they come from.

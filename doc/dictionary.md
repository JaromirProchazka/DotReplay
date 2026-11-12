# List of Terms

- **User**: A C#/.NET developer tasked with fixing a reported bug.
- **Target methods**: The set of methods and variables relevant to a reported bug. The User expects the bug to manifest in the runtime values and arguments of these methods and variables.
- **Target application**: The application or environment, and its configuration, relevant to the reported bug. It can be run with instrumentation to replicate the bug.
- **Testing / Test suite / Tests**: A set of automatically generated unit tests with arguments collected by the runtime analysis.
  - **Test generation**: Automatic generation of the Test suite.
  - **Test running**: Execution of the Test suite and collection of Traces and test results.
- **Argument values**: A set of tuples of runtime values of Target method arguments and runtime variables, collected by the dynamic analysis.
- **Analysis output file**: The file produced by the analysis stage. It contains the captured Argument values in the common format written by every Strategy.
- **Trace**: Information about the execution of a given test from the Test suite.
- **API or Contract or Back-end API/Contract**: A well-defined, documented API providing all the information important for a front-end GUI. It is a data contract: a JSON schema plus a command-line interface.
- **The case study**: A benchmark and test on a chosen medium-sized C#/.NET application.
- **Target machine**: The machine used for running the case study:
  - AMD Ryzen 5 4600HS processor
  - 16 GB of RAM
  - 4 GB of graphics RAM
  - Windows
- **Session directory**: A directory produced by the System, containing its results. These results follow the Back-end API.
- **Stage**: A System module implementing one part of the Debugging workflow (analysis, test generation, test running).
- **Strategy**: Swappable, potentially custom, code loaded and run by the analysis stage (in the future, potentially also by other stages).
- **Exhaustive combinatorial testing**: A test generation approach in which tests are generated for each possible combination of Target method values. The configurable `maxArgumentCombinations` value limits the number of tests.
- **List of selected tests**: A list of tests from a Test suite to run in the test running stage.
- **Manifest**: A small JSON status file that the controller writes after every stage. It works as the stage's receipt and marks the stage output with one of these statuses:
  - `COMPLETE`: the output is whole. It may still contain value markers (`UNSUPPORTED`, `TRUNCATED`), which are limitations of the System.
  - `PARTIAL`: the output is usable but incomplete (the `maxArgumentCombinations` limit was reached, a fault removed or terminated a test, or a crash or timeout stopped the stage after it had collected some output).
  - `FAILED`: the stage ended with an error before producing any output.
- **Run configuration**: A configuration file specifying the System's runtime properties.

<!-- - ****: -->

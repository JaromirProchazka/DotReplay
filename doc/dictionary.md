# List of Terms

- **User**: a C#/.NET developer tasked with fixing a reported bug.
- **Target methods**: Set of methods and variables relevant to a reported bug. Developer expects the bug to manifest in these methods and variables runtime values and arguments.
- **Target application**: The application or environment, and its configuration relevant to the reported bug. Can be run with instrumentation to replicate the bug.
- **Testing / Test suite / Tests**: A set of automatically generated unit tests with arguments collected at the runtime analyses.
    - **Test generation**: automatic generation of the Test suite
    - **Test running**: execution and trace/argument values collection of the Test suit
- **Argument values**: A set of tuples of runtime values of target methond arguments and runtime variables collected at dynamic analyses
- **Trace**: a information about execution of given test from the Test suite
- **API or Contract or Back-end API/Contract**: A well defined, documented API providing all the information important for a front-end GUI. It is a data contract: a JSON schema plus a command-line interface
- **The case study**: A benchmark and test on a chosen medium sized C#/.NET application  
- **Target machine**: The machine used for running the case study: 
    - AMD Ryzen 5 4600HS processor
    - 16GB of RAM 
    - 4GB of graphics RAM
    - Windows
- **Session directory**: a directory produced by the system containing the results of the system. These resutls follow the Back-end API.
- **Stage**: A system module implementing one part of the Debugging workflow (analyses, test generation, test running)
- **Strategy**: A swapable (potentially custom) code dynamically fetched and run for analyses stage (potentially strategies for other stages in future).
- **Exhaustive combinatorial testing**: A test generation approach, where the tests are generated for each possible combination of target methods values. There is a configurable `maxArgumentCombinations` value limiting the number of tests. 
- **List of selected tests**: a list of tests from a test suit, that we want to run in the test running stage. 
- **Manifest**: a small JSON status file that the controller writes after every stage. It works as the stage’s receipt.
- **Run configuration**: a configuration file specifying the systems runtime properties 

<!-- - ****: -->
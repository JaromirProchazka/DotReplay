# Evaluation

We plan to evaluate the project on mid-sized applications from the SWE-Sharp-Bench benchmark for C#. It features 150 real bugs and issues from 17 mid-sized open-source repositories.

As success criteria, the test application is configured, runtime values are captured, valid tests are generated, and the bug is localized through the generated tests. Further, we aim to measure the instrumentation overhead and the start-up time to evaluate requirements R14 and R15.

## Evaluation on concurrent applications

Unfortunately, SWE-Sharp-Bench is not suitable for evaluating the concurrency features of this project. Although the success criteria are the same as for the serial version, we aim to use a subset of the SCTBench collection translated to C# in the work on Fuzzing over Thread Schedules. Although these benchmarks are suitable for testing the functionality of the project, they are small rather than mid-sized applications.

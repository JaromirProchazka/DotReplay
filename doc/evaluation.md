# Evaluation

We plan to evaluate the project on some tests of mid-sized applications from the SWE-Sharp-Bench test suite for
C#. It features 150 real bugs and issues from 17 mid-sized open-source repositories.

As a success criterion, we see that the test app is configured and runtime values are captured, valid tests are
generated, and the bug is localized through generated tests.
Further, we aim to measure the instrumentation overhead and the startup time to evaluate the requirements
R14 and R15.

## Evaluation of use on Concurrent applications

Unfortunately, the SWE-Sharp-Bench is not suitable for evaluating the concurrency features of this project.
Although the success criteria are not different from those of the serial version, we aim to utilize a subset of the
SCTBench collection translated to C# in the work on Fuzzing over Thread Schedules [1]. Although these bench-
marks are suitable for testing the functionality of the project, they are small, rather than mid-sized applications.
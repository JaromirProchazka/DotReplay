# Risks

Regarding our primary goals, there is a risk that the instrumentation added to the program will present significant
slowdowns. As part of the requirements, we aim not to exceed a 100% slowdown in the case study. However,
performance limitations may affect the size of the debugged application, as well as the number of specified target
methods and variables.
This problem is even more significant for debugging concurrent applications, as the chosen strategy can lead to
an exponential number of possible schedules for several threads.
Another risk is an unforeseen limitation of used technologies. For instance, reconstruction of some complex data
structures from memory using reflection might not be supported by the instrumentation framework used. For this
reason, we included a feasibility test at the beginning of the schedule to identify these limitations.
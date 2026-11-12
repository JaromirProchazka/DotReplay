# Risks

Regarding our primary goals, there is a risk that the instrumentation added to the program will cause significant slowdowns. As part of the requirements, we aim not to exceed a 100% slowdown in the case study (R14). However, performance limitations may affect the size of the debugged application, as well as the number of specified Target methods and variables.

This problem is even more significant for debugging concurrent applications, as the chosen Strategy can lead to an exponential number of possible schedules for several threads.

Another risk is an unforeseen limitation of the technologies used. For instance, the instrumentation framework might not support reconstruction of some complex data structures from memory using reflection. For this reason, we included a feasibility experiment at the beginning of the schedule to identify these limitations.

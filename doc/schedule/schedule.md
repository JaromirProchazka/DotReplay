# DotReplay: Detailed Schedule

| Milestone / Deliverable | Activity | Time schedule | Man-days | Notes |
| --- | --- | --- | --- | --- |
| M1 Requirements, design, and data contract | Refinement of the requirements; study of the Auto-debugger, SharpDetect, and the instrumentation options of the CLR | Month 1 | 1.5 | |
|  | Time-boxed feasibility experiment; choice of the instrumentation technology | Month 1 | 1.5 | |
|  | Architecture with separated analysis, test generation, and test running modules (R10, R11) | Month 1 | 1 | |
|  | Joint JSON schema and command-line contract with the front-end team (R9) | Month 1 | 1 | |
|  | Repository, build, and continuous integration | Month 1 | 0.5 | |
| M2 Walking skeleton and sample outputs | Selection of Target methods and Run configuration with basic validation (R1, R2, R16, R17) | Month 2 | 1 | |
|  | Recording of numbers, strings, and arrays (R3, R21) | Month 2 | 2 | |
|  | Test generator for the recorded values (R4) | Month 2 | 1 | |
|  | Test runner and export of test results (R5) | Month 2 | 1 | |
|  | JSON exporter; sample outputs for the front end (R9) | Month 2 | 0.5 | |
| M3 Complete serial workflow | Source references in generated tests; running of selected tests (R12, R20) | Month 3 | 1 | |
|  | Traces of test runs (R5) | Month 3 | 1 | |
|  | Handling of sensitive data (R18) | Month 3 | 0.5 | |
|  | Release of the Must features for the front end | Month 3 | 0.5 | |
|  | Recording of objects serializable by reflection (R13) | Months 3–4 | 1.5 | |
|  | Reconstruction of recorded objects in generated tests (R4, R13) | Month 4 | 1.5 | |
|  | Thread-safe recording with thread identity (R7, basic) | Month 4 | 0.5 | |
|  | Workflow state and next-step hint (R6) | Month 4 | 0.5 | |
|  | Analysis of .NET-specific constructs; list of unsupported ones | Month 4 | 1 | |
|  | Automated unit and end-to-end tests | Months 3–4 | 1 | |
| M4 Evaluation and integration | Integration support for the front end; freeze of the data contract (R9) | Month 5 | 1 | |
|  | Retargeting of the SCT benchmark programs; experiments | Month 5 | 0.5 | |
|  | Case study; measurement of the overhead and start-up time (R14, R15) | Months 5–6 | 2.5 | |
|  | Evaluation report, including the identified limitations | Month 6 | 1 | |
| M5 Final deliverables | Documentation, data contract reference, demonstration package | Month 9 | 1 | |
|  | Final report | Month 9 | 1.5 | |
|  | Defence presentation and demonstration | Month 9 | 0.5 | |
| Reserve | Unforeseen technical problems | Months 7–9 | 2 | |
| **Main goals** |  |  | **30** | |
| O1 Concurrency study (optional; R7, R8) | Design of thread-aware traces and of the data contract extension | Month 6 | 1 | |
|  | Prototype of thread-aware recording; detection of data races and deadlocks with SharpDetect | Month 7 | 3 | |
|  | Experiments on the SCT benchmark programs; report on findings and limitations | Month 8 | 2 | |
| O2 Performance optimization (optional; R14, R15) | Reduction of the overhead and start-up time, only if the measurements fail the requirements | Months 7–8 | 2 | |
| **Optional goals** | Started only when the main goals are on track |  | **8** | |
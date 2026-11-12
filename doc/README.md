# DotReplay Documentation

This folder defines what DotReplay is and what it must do. It is the source of truth for intended behavior: if the code and the docs disagree, the docs win until the conflict is resolved.

## Where to start

Read these in order the first time:

1. [vision.md](vision.md): why the project exists and what it automates.
2. [debugging_workflow_description.md](debugging_workflow_description.md): the debugging loop DotReplay supports (analysis → test generation → test running → bug fix).
3. [dictionary.md](dictionary.md): the domain terms. Use them exactly in code, names and messages.
4. [architecture/architecture.md](architecture/architecture.md): the modules, the controller, the stages and the Session directory.

## Reference

| File | Use it when |
| --- | --- |
| [requirements.md](requirements.md) | Implementing or changing a feature. Requirements have IDs (`R1`, `R2`, …); reference them in plans, commits and tests. |
| [use-cases/](use-cases/) | Implementing the behavior of a stage. There is one use case per stage: [analysis](use-cases/analysis.md), [test generation](use-cases/test_generation.md) and [test running](use-cases/tests_run.md). |
| [errors.md](errors.md) | Reporting a failure. It lists every error, fault and value marker by stage, and how each affects the Manifest status. |
| [evaluation.md](evaluation.md) | Planning benchmarks and the case study (R14, R15). |
| [risks.md](risks.md) | Judging performance and technology limits. |
| [schedule/schedule.md](schedule/schedule.md) | Checking which milestone a feature belongs to. |

## Conventions

- **New use case:** copy [use-cases/.template.md](use-cases/.template.md).
- **New error code:** add it to the use case flow and to [errors.md](errors.md) in the same change.
- **New term:** add it to [dictionary.md](dictionary.md) before using it.
- **Inconsistencies:** if two documents disagree, raise it and resolve it in the docs before changing code.

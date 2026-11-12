# Architecture

## Overview

DotReplay is a modular monolith: one CLI program built from modules with explicit, tested boundaries. It consists of a public contract, a controller, stages, a model and shared modules:

- **Contract (public interface).** The `dotreplay` CLI with its commands, options, exit codes and error codes, and the versioned file formats of the session directory (R9, R26, R28). Users and other programs use DotReplay only through it. Nothing inside DotReplay depends on any particular client.
- **Controller.** Part of the `Cli` host module. Each CLI command names one stage, and the controller calls that stage and nothing else. Around that call it parses the configuration, keeps secrets in memory and maps failures to error codes. It never decides which stage runs next and never chains stages. That decision belongs to whoever calls the CLI (R23).
- **Stages.** Analysis, test generation and test running, one for each use case. Each stage is a separate module. Stages do not know about each other. Each reads its input from the model and writes its output to the model (R10, R11, R23).
- **Model.** The session directory, which is the file part of the contract. Large data is streamed as JSON Lines (R22, R28).
- **Shared modules.** They are not stages, so using them does not break R23.
  - `Contract`: the data contract (schemas, value model, readers and writers) and the stage contract (stage interface, error codes). It is used by all modules.
  - `Instrumentation`: the Mono.Cecil weaver, the recorder runtime and a process runner with timeouts. It is used only by the Analysis and Running stages.

The analysis stage runs exactly one strategy per run (R24, R25). A command-line flag or the run configuration selects it:

- `record` is the default and the only strategy in the main goals.
- `interleaving`, `fuzzing` and `sharpdetect` are optional strategies (O1).
- A custom strategy can be loaded from a user assembly.

Every strategy writes the collected values in the same format (`calls/1`), as R24 requires, so test generation works with the output of any strategy. Strategies are never combined in one run, so their interactions never need to be handled.

## Context

```mermaid
flowchart LR
  DEV["User: C#/.NET developer"] -- "CLI commands" --> SYS
  CLIENT["Any contract client, e.g. a GUI (outside this project)"] -. "CLI commands, reads session files" .-> SYS
  subgraph SYS["DotReplay"]
    CLI["dotreplay CLI"]
    MODEL[("Session directory")]
  end
  CLI -- "instruments and runs" --> APP["Target application (.NET 8, x64, Windows)"]
  CLI -- "generates and runs" --> TESTS["Generated xUnit tests"]
```

DotReplay is used mainly from the command line, or through an independent front end. Both communicate with the system the same way: they give commands through the CLI and read the results from the JSON files in the session directory, which follow the documented contract. On command, the system runs the target application with instrumentation, generates a test suite, and runs it.

## Structure

```mermaid
flowchart TB
  CLI["Cli (host, controller)"]
  subgraph Stages["Stage modules"]
    AN["Analysis"]
    GEN["Generation"]
    RUN["Running"]
  end
  CUSTOM["Custom strategy (user assembly)"]
  INS["Instrumentation (not a stage)"]
  CON["Contract (shared kernel)"]
  MODEL[("Session directory")]
  CLI --> AN
  CLI --> GEN
  CLI --> RUN
  AN -. "loads one strategy" .-> CUSTOM
  AN --> INS
  RUN --> INS
  Stages --> CON
  INS --> CON
  CON --> MODEL
```

The system is a modular monolith with a dispatching controller. The controller, in the `Cli` module, calls the stage modules (analysis, test generation, test running), which are mutually decoupled: no stage module references another. The stages write their outputs as JSON files in the session directory (the model) through the data contract. Any contract client can use these outputs. The Analysis stage runs one swappable strategy, selected by the run configuration or a command-line flag. The Analysis module creates the built-in strategy or loads the custom one. Solid arrows are compile-time dependencies, and the dotted arrow is the run-time loading of a custom strategy.

## Stages

The controller calls each stage in two steps:

1. **Validate.** The stage checks its input: the run configuration for analysis, and the outputs of the earlier step, read from the model through the data contract, for generation and running. If the input is invalid or missing, the stage reports a documented error code, for example `MISSING_INPUT` with the command to run first. Nothing else is run.
2. **Run.** The stage does its job and writes its result back to the model through the data contract. The controller then writes the manifest, which marks the output as complete, partial or failed.
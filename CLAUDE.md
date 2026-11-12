# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

## 5. Coding Style

These apply to code you write. Existing codebase conventions take precedence, and Section 3 still governs what you may touch.

### 5.1 Prefer pure functions
- Before writing a class or stateful code, ask: **is there state in this subproblem worth tracking across calls?** If not, write a pure function (output depends only on inputs, no side effects).
- Functional core, imperative shell: logic is pure; I/O, persistence, network, time, and randomness live at the edges and are passed in.
- Don't mutate arguments. Return new values.
- Introduce a class only when there is real state with invariants to protect, or operations that clearly belong to one concept.

### 5.2 Extend, don't rewrite
- When adding a feature, prefer adding new code over modifying working code, **but only via extension points that already exist**.
- If no extension point exists and the feature is the only variant, modify the existing code directly and minimally. Don't build strategies, registries, or plugin systems for one case (see Section 2).
- Add an extension point only when a second real variant exists. Then do the smallest refactor that makes both fit.
- Prefer composition over inheritance. Use inheritance only for a genuine substitutable "is-a" relationship (Liskov), and keep hierarchies shallow.
- Bug fixes and explicitly requested changes modify the existing code directly.

### 5.3 Object-oriented design (for new code)
- **Single responsibility:** if you need "and" to describe a new class or module, split it.
- **Inject dependencies:** pass collaborators (DB clients, HTTP clients, clocks) in; don't construct them inside business logic. Introduce an interface only when a second implementation or a test fake needs it.
- **Encapsulate:** keep fields private; validate invariants in the constructor so objects can't exist in an invalid state.
- **Prefer immutability:** make value objects immutable; minimize setters.
- **Small interfaces:** when you do define one, keep it focused.
- **Tell, don't ask** (for stateful objects): ask them to act rather than pulling out their state to decide externally. Pure data flowing through pure functions is fine.
- **Clear names over comments:** names state intent; comments explain *why*, not *what*.

## 6. Project Documentation

The `doc/` folder defines what this project is and must do. Read the relevant files before planning, not after implementing.

| File | Contains | Read when |
|---|---|---|
| `doc/README.md` | Reading order and a map of the documentation | First time in the project, or unsure which document applies |
| `doc/dictionary.md` | Definitions of domain terms | Naming anything, or meeting an unfamiliar term |
| `doc/vision.md` | Project purpose and scope | Judging whether a feature or approach fits the project |
| `doc/requirements.md` | Functional and non-functional requirements (IDs) | Implementing or changing any feature |
| `doc/use-cases/*.md` | One use case per file | Implementing behavior a user triggers. Find the matching use case first. |
| `doc/architecture/architecture.md` | Modules, their dependencies and how the controller calls the stages | Adding or changing a module, or deciding where code belongs |
| `doc/errors.md` | Errors, faults and value markers, split by stage | Reporting a failure or adding a new error code |

Rules:
- **Use the dictionary's terms exactly** in code, names, comments, and messages. Don't invent synonyms (if the dictionary says `Order`, don't write `Purchase`). If you need a term that isn't defined, propose a definition; don't add one silently.
- **Docs, in the doc folder, are the source of truth for intended behavior.** If code contradicts a requirement or use case, report the conflict and ask which is right. Don't silently change either side.
- IF the **Docs** contain any internal inconsistency or mistake, always ask about how to resolve it before any code change. Don't silently change either side.
- **Don't edit `vision.md` or `requirements.md` unless asked.** You may propose changes.
- When your work implements or changes behavior covered by a use case or requirement, reference it (e.g. `R12`) in your plan and summary, and in tests where it helps.
- If a request falls outside the vision or conflicts with a requirement, say so before implementing (see Section 1).

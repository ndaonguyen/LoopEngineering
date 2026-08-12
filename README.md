# Loop.Engine

An **autonomous bug-fixing engineer**. A .NET worker that polls a GitHub repository for
`bug`-labelled issues and drives one through **investigate → plan → reproduce → code → verify →
review → pull request**.

It stops at a green pull request. **A human always merges** — and no interface in the system can
express a merge.

```
PeriodicTimer  →  pick the oldest open `bug` issue
                     ↓
                  throwaway git worktree, cut from origin/<base>
                     ↓
                  investigate                          ← always
   [GenerateFix]    plan
   [ReproduceFirst] write a failing test → red gate    ← only Red proceeds
                    write the fix
   [VerifyFix]      format → build → test → repair → review
   [PublishPr]      branch → commit → push → open PR
                     ↓
                  STOP.
```

All four stage flags ship **off**. A fresh clone investigates and stops; each phase stays dormant
until it is asked for.

## Two loops, one job

| | What it is | Where |
|---|---|---|
| **Loop.Engine** | The product. A deployable .NET worker. | `source/Loop.Engine.*` |
| **The skills bug loop** | The same job driven by Claude Code skills instead of C#. The day-to-day baseline. | `.claude/skills/bug-loop` |

They coexist deliberately: one is the product being built, one is the tool building it.

## It runs *beside* the repository it fixes

The engine never clones your project. You point it at an existing clone and it creates throwaway
worktrees inside that clone, so your checkout is never switched or dirtied.

That means it is its own deployable — **not** something you add to the project you want fixed.

```
your-project/          ← a normal clone, with an `origin` remote
loop-engine/           ← this repo, running beside it
```

Requirements for a target repository: **.NET** (the engine shells out to `dotnet build` /
`dotnet test`, and the reproducer writes xUnit) and a GitHub remote.

## Quick start

```bash
dotnet restore && dotnet build -c Release && dotnet test -c Release
```

Then supply credentials and take one safe tick:

```bash
dotnet user-secrets set "GitHub:Token" (gh auth token) --project source/Loop.Engine
dotnet run --project source/Loop.Engine -- --Pipeline:RunOnce=true --Pipeline:IssueNumber=8
```

With every stage flag off, that investigates one issue and writes a report. It cannot touch your
repository or GitHub.

**Before pointing it at a real project, read
[knowledge/runbooks/running-loop-engine.md](knowledge/runbooks/running-loop-engine.md)** — in
particular `Verification:TestProject`, which defaults to this repo's own tests and will otherwise
"verify" your fix against a suite that never exercises it.

## Layout

```
source/
├── Loop.Engine.Core        ports + model. No project references at all.
├── Loop.Engine.Agents      investigate, plan, reproduce, code, verify, review
├── Loop.Engine.GitHub      issues in, branches and pull requests out
├── Loop.Engine.Worker      the pipeline — the only place stages are composed
└── Loop.Engine             host: configuration, DI

tests/
├── Loop.Engine.Tests       the engine's own suite; every test runs offline
└── Architecture.Tests      parses the .csproj graph, fails the build on a bad reference

knowledge/                  how the system is designed — start at index.md
docs/                       what was done, and why it was done that way
```

The dependency direction is enforced, not documented: `Architecture.Tests` holds no project
references of its own, so it cannot distort the graph it checks.

## Where to read next

- **[knowledge/index.md](knowledge/index.md)** — the map. Start here.
- [knowledge/architecture/loop-engine.md](knowledge/architecture/loop-engine.md) — boundaries,
  stage gates, and the constraints encoded as *absent* interface methods.
- [knowledge/runbooks/running-loop-engine.md](knowledge/runbooks/running-loop-engine.md) — how to
  actually run it, and what a silent tick means.
- [docs/bug-loop-roadmap.md](docs/bug-loop-roadmap.md) — what each delivery phase was for, and
  the things deliberately not being done.

## Requirements

A **net10 SDK** (`global.json` pins SDK 10, prerelease allowed), `git` on `PATH`, and an
Anthropic or OpenAI key. Tests are xUnit + AwesomeAssertions and run offline.

> **It builds and runs model-written code.** `dotnet test` executes whatever the Coder and
> Reproducer produced, in a process holding your GitHub token and model API key. Today that is
> contained to a trusted machine targeting its own repository. Before pointing it at anything
> else, sandbox it.

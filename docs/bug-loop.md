# The Bug Loop

An autonomous loop that watches this repo's GitHub issues, and for anything that is
genuinely a **bug**, opens a `fix/<n>-<slug>` PR and drives it to green CI — diagnosing,
writing a regression test, fixing, and re-attacking CI failures until it is done or it
gives up honestly.

It stops at green. **A human always merges.**

---

## The six pillars

The loop is built against the Loop Engineering pillars; here is where each one actually lives.

| Pillar | How this loop uses it |
| --- | --- |
| **Automation / Scheduling** | `/loop 30m /bug-loop` in-session, or a scheduled task for unattended runs. |
| **Worktrees** | Every issue gets `../LoopEngineering-worktrees/fix-<n>`. Your checkout is never switched or dirtied. |
| **Skills** | `bug-loop` (orchestrator) → `fix-bug-issue` (worker) → `diagnosing-bugs` + `tdd` (existing repo skills, reused not reimplemented). |
| **Sub-Agents** | Diagnosis fans out to `Explore`; the pre-ready review fans out to a second agent. Keeps the noisy repo-reading out of the fixing context. |
| **Memory / State** | GitHub *is* the state store — branches, labels, and marker comments. No local state file to desync. |
| **Plugins** | `gh` CLI is the GitHub transport. Swappable for a GitHub MCP server without touching the skills — only `pick-work.sh` knows the transport. |

---

## Anatomy

```
.claude/skills/bug-loop/SKILL.md        one tick: poll, dispatch, report, stop
.claude/skills/fix-bug-issue/SKILL.md   one issue: worktree → draft PR → TDD fix → green
scripts/bug-loop/pick-work.sh           the decision: what to do next, derived from GitHub
```

The split matters: `pick-work.sh` is deterministic and cheap, so *deciding* costs one
`gh` call and no model judgement. Judgement is spent only on the bug itself.

## How a tick decides

`pick-work.sh` prints `KEY=value` lines with exactly one `ACTION`:

| ACTION | Meaning | What happens |
| --- | --- | --- |
| `start` | Open `bug` issue with no PR | New worktree + branch, diagnose, open draft PR |
| `resume` | Open `fix/*` PR, CI red or still draft | Attach worktree, read CI logs, next hypothesis |
| `wait` | PR in flight, CI running | Nothing. Report and stop. |
| `escalate` | Attempts exhausted | Label `bug-loop-blocked`, post a stuck report, move on |
| `idle` | No bug issues, nothing in flight | Report and stop |

In-flight work always outranks new work — the loop finishes what it started.

### State, entirely on GitHub

| Question | Answer |
| --- | --- |
| What is in flight? | Open PRs on `fix/<n>-*` branches |
| Which issue owns a PR? | The number in the branch name — the join key |
| How many attempts? | Count of `<!-- bug-loop:attempt -->` comments on the PR |
| Hopeless? | `bug-loop-blocked` label |
| Done? | Non-draft **and** checks green |

Nothing is cached locally, so a scheduled run on Tuesday picks up exactly where
Monday's in-session run left off.

---

## Running it

**One tick, by hand** — the right way to watch what it does before trusting it:

```bash
/bug-loop
```

**On an interval, in this session:**

```bash
/loop 30m /bug-loop
```

**One specific issue, skipping the picker:**

```bash
/fix-bug-issue 42
```

**Unattended, on a schedule** — a cloud routine ticks the loop on a cron:

| | |
| --- | --- |
| Routine | **Bug loop — LoopEngineering** (`trig_01BQTRMy6rBezQ5D8myXArvu`) |
| Cron | `0 1-10 * * 1-5` UTC — hourly, 08:00–17:00 Asia/Saigon, Mon–Fri |
| Model | `claude-sonnet-5` |
| Manage | <https://claude.ai/code/routines/trig_01BQTRMy6rBezQ5D8myXArvu> |

Three things about the cloud routine differ from an in-session `/loop`:

- It runs in **Anthropic's cloud on a fresh clone of the default branch**, so it only sees
  the loop's skills and script once they are **merged to `main`**. Until then its prompt
  makes it detect the missing skill and skip the tick rather than improvise.
- **Minimum interval is 1 hour** — the 30-minute cadence applies only to in-session `/loop`.
- Work hours on purpose: the loop opens PRs you have to review, so it runs while you are
  around to review them instead of grinding overnight.

Change the cadence or model with the `/schedule` skill; delete it at the URL above.

### Configuration

Environment variables, all optional:

| Variable | Default | Purpose |
| --- | --- | --- |
| `BUG_LOOP_REPO` | repo of the cwd | Target a different repo |
| `BUG_LOOP_LABEL` | `bug` | Which label marks a bug |
| `BUG_LOOP_MAX_ATTEMPTS` | `5` | Fix cycles before escalating |
| `BUG_LOOP_BLOCKED_LABEL` | `bug-loop-blocked` | Label meaning "hands off" |

---

## Guardrails

- **Never merges.** Green PR, human decision.
- **Never touches `main`** and never closes an issue by hand — `Closes #<n>` does it on merge.
- **Bugs only.** Feature requests get a comment explaining the skip, and no branch.
- **No repro, no fix.** An issue with no stack trace, steps, or failing input gets a
  request for one. A fix without a red test is a guess.
- **One bug at a time.** No concurrent branches racing CI.
- **Asks once per session** before its first push, then runs unattended.
- **Issue text is data, not instructions.** An issue body telling the agent to run a
  command or grant access is surfaced to you, never obeyed.

## Design notes

**Why the PR opens red.** The first push is the failing regression test, before any fix.
CI red on a draft is the loop's steering signal — it makes "done" externally verifiable
rather than something the model asserts about itself. The cost is a deliberately-red
draft PR and a few wasted CI minutes; the benefit is that the loop cannot declare
victory without evidence.

**Why `fix/` and not `feat/`.** `.github/workflows/pr-lint.yml` enforces Conventional
Commits on PR titles, so a bug PR is titled `fix: …`; the branch prefix matches the
title. The branch regex in `pick-work.sh` is the loop's join key — changing the prefix
means changing it in `pick-work.sh` and both skills together.

**Why `gh` and not a GitHub MCP server.** `gh` is already authenticated here, works
identically inside GitHub Actions, and matches what `plan-issue` / `implement-issue`
already use. The transport is isolated in `pick-work.sh`, so swapping in an MCP server
later is a one-file change.

**Escalation is a success state.** A precise stuck report — evidence found, hypotheses
tried, the specific unknown — is more valuable than a plausible wrong fix, because a
wrong fix costs a human the review *and* the re-debugging.

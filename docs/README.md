# docs/ — execution artifacts

What was **done**, and why it was done that way. Nothing here describes how the system behaves
today — for that, start at [../knowledge/index.md](../knowledge/index.md).

Everything in this folder is `historical` except the debugging memory and the roadmap, which are
`living`. Each file carries its own `status` in frontmatter.

| | |
|---|---|
| `plans/` | Implementation plans for merged work. Useful for the reasoning behind a decision, useless as a description of the code. |
| `inputs/` | The briefs that produced those plans — constraints, non-goals, and approaches ruled out at the time. `TEMPLATE.md` is the live format `/plan-issue` expects. |
| `bug-loop-roadmap.md` | The Loop.Engine delivery record: each phase's purpose and exit criterion, what the first real runs exposed, and the things deliberately not being done. |
| `bug-loop-proposal.md` | An externally-sourced design, recorded for comparison. Explicitly not adopted. |
| `bug-loop-learnings.md` | The bug loop's cross-issue debugging memory — read before diagnosing, appended after fixing. Written by execution, which is why it lives here rather than in `knowledge/`. |

> **The file numbers in `plans/` and `inputs/` are _issue_ numbers, and the phase numbering is
> offset by one:** issue #3 delivered Phase 2, #6 delivered Phase 5.

# Bug Loop Learnings

What the loop has learned about **debugging this codebase** — read before diagnosing,
appended after fixing.

This is the loop's memory. Per-issue state (branches, attempt counts, labels) lives on
GitHub and is thrown away when a PR merges; what survives here is the part that makes
the *next* bug cheaper: where the test seams are, which hypotheses look right and aren't,
which parts of this repo mislead you.

Entries arrive as part of a fix PR, so every one of them passes human review before it
lands. That is the only thing keeping this file from filling with confident nonsense.

---

## How to use it

**Before diagnosing** (`fix-bug-issue` Step 4): read this file and pull forward anything
matching the area or symptom class. A known seam is worth more than an hour of searching;
a known dead end is worth more than a known seam.

**After fixing** (`fix-bug-issue` Step 8): add an entry *only if it clears the bar below*.

### The bar

Write an entry only when it would have **saved you time if you had known it at Step 4**.
Concretely, at least one of:

- A **test seam** that was non-obvious — where to observe this class of failure.
- A **dead end**: a hypothesis that was plausible, wrong, and cost real attempts. Say what
  killed it.
- A **misleading signal** — an error message, log line, or stack trace that points away
  from the actual cause.
- A **structural cause** that will recur: a pattern in this codebase that produces this
  class of bug more than once.

Do **not** write an entry for: what the fix was (that's the PR), what the bug was (that's
the issue), anything restating `CLAUDE.md`, or a lesson that only applies to one line of
code and will never generalise.

Most fixes teach nothing durable. **No entry is the normal outcome.** A file of thirty
sharp entries beats a file of three hundred.

### Keeping it honest

- **Deduplicate.** If an entry already covers this area, *sharpen it* — add the second
  issue number, tighten the claim. Do not append a near-duplicate.
- **Correct it.** If an entry turns out to be wrong or stale, fix or delete it in the
  same PR. A wrong memory is worse than no memory, because it is trusted.
- **Keep entries short.** Four bullets, no prose essays. If it needs more, it is an ADR
  or a `CLAUDE.md` change, not a learning.
- **Escalations do not write here.** A bug the loop gave up on has an unmerged PR, so its
  entry would never land. The stuck report on that PR is its memory instead.

### Entry format

```markdown
### <area> — <the lesson, in one line>

_#<issue> · YYYY-MM-DD_

- **Symptom class:** <how this bug announced itself>
- **Root cause:** <the actual mechanism>
- **Seam:** <the command or file:line where it becomes observable>
- **Dead end:** <what looked right, and the observation that killed it>
```

---

## Entries

_None yet — the loop has not merged a fix._

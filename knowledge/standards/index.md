---
type: Index
title: "Standards"
description: "Local map of the standards domain - conventions a tool cannot check, and the routing to everything that is checked instead of described."
status: current
---

# Standards

Conventions that hold across the repo.

**Belongs here** — a convention that is genuinely uncheckable *and* genuinely non-obvious.
**Does not** — anything an analyzer, an `.editorconfig` entry, or a test could carry. That
version cannot be ignored under deadline pressure; a paragraph here can.

## Concepts

- [how-standards-are-enforced.md](how-standards-are-enforced.md) — the six conventions checked by
  a tool rather than written down, what each one fails on and when, the bar for adding a written
  standard, and the two anti-patterns to refuse.

## Why this folder is nearly empty

Every convention in this repo that could be automated already has been — architecture tests,
`dotnet format` inside the verification loop, `.editorconfig`, PR-title linting. What a document
here would add is a second statement of a rule the build already enforces, and the two would
eventually disagree.

---

Full tree: [../index.md](../index.md)

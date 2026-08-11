---
type: Template
title: "Input template"
description: "The brief format /plan-issue expects - issue restatement, design links, brainstorming, constraints and non-goals. Copy it to start a new one, and replace this frontmatter."
status: current
---

# Input: #<issue-number> — <short title>

Context `/plan-issue` needs that the GitHub issue alone doesn't carry. Copy this file to
`docs/inputs/<issue-number>-<slug>.md` and fill it in before running the skill.

**Issue**, **Design / Reference Links**, and **Constraints & Non-goals** must be non-empty.
**Brainstorming** may be left empty.

---

## Issue

<Link to the issue and one paragraph on what it actually asks for — in your words, not a
paste of the body. If you can't state it in a paragraph, the issue needs sharpening first.>

## Design / Reference Links

<Authoritative sources for the shape of the work: design files, reference docs, prior art,
the roadmap section this belongs to. These win over repo patterns for UI/UX decisions.>

## Brainstorming

<Hints for the technical approach: libraries you've decided on, seams you have in mind,
approaches already ruled out and why. Optional, but a ruled-out approach recorded here
saves the planner from re-proposing it.>

## Constraints & Non-goals

<Hard limits: what must NOT change, what is explicitly out of scope, deadlines, compat
requirements, credentials that must not be committed. Be specific — "no code generation"
is a constraint, "keep it clean" is not.>

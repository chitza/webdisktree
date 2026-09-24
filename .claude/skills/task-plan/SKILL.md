---
name: task-plan
description: Turn a task request into docs/tasks/<slug>/intent.md and plan.md for WebDiskTree, with a person at each gate. Use when the user wants to prepare a task for an unattended agent, write an intent, or write a plan. Not for implementing the task (use task-run).
---

# task-plan

This skill is attended. A person accepts each file before the next one starts.
The output is a committed `plan.md` that `task-run` can do without questions.

## Files

```
docs/tasks/<slug>/intent.md   the problem and the outcome. The person owns it.
docs/tasks/<slug>/plan.md     the files, the order, the risks, the proof. The agent writes it.
```

`<slug>` is 3 to 5 lowercase words with hyphens, for example `scan-size-filter`.
If the task has a GitHub issue, put the number first: `42-pin-scan-bulk`.

## Procedure

1. Run `git fetch` and work from `origin/main`. The schema and entities move fast in this repo.
   If the current branch is behind `origin/main`, make a fresh branch for the plan: `git checkout -b plan/<slug> origin/main`.
2. Ask the person for the problem and the outcome, if the request does not give them.
3. Write `intent.md` from the template. Put every undecided point under "Open questions".
4. Stop. Ask the person to accept `intent.md` and to answer the open questions.
5. Read the code that the task touches (see "Where things are").
6. Write `plan.md` from the template. Every item in "Proof" must be a command or a named test.
7. Check the plan against "When to stop and ask" in `.claude/skills/task-run/SKILL.md`.
   If the plan needs one of those items, write it under "Needs approval". Do not hide it.
8. Tell the person the riskiest step, and what the change can break.
9. Stop. Ask the person to correct or accept `plan.md`.
10. When the person accepts, set `Status: accepted` in both files. Commit both files.
11. Give the person the commands to start the unattended run (see "Hand-off").

Do not write product code in this skill.

## Where things are

- `backend/src/WebDiskTree.Api/`: controllers (`Controllers/`), DTOs, SignalR hubs, `Program.cs` (DI, startup migrations).
- `backend/src/WebDiskTree.Core/`: models and abstractions. No EF or IO here.
- `backend/src/WebDiskTree.Infrastructure/`: `Data/` (DbContext, entities, EF migrations), `Scanning/`, `Scheduling/`,
  `Security/` (path safety, host root, mount detection), `Compression/` (scan archives), `Media/` (IMDB lookup).
- `backend/tests/WebDiskTree.Tests/`: xUnit tests. One file per feature area.
- `frontend/src/app/`: Angular 22, standalone components. `core/` (models, API services), `features/<page>/`, `shared/`.
  Unit tests are `*.spec.ts` next to the component (Vitest via `ng test`).
- `Dockerfile`, `docker-compose.yml`, `.github/workflows/` (CI: backend build+test, frontend build+test, docker build).

## A good plan

- An agent can do each step without a decision that the plan does not make.
- Each step names the files that it changes.
- "Proof" has a number or a named test. "The page works" is not proof.
  "`ScheduleRetentionServiceTests.KeepsPinnedScans` passes, and `GET /api/scans` returns 200 in the smoke run" is proof.
- For a bug fix, step 1 adds a failing test. The plan names that test under "Pinned tests".
  `task-run` must not edit a pinned test.
- A schema change names the migration to add, and says what happens to existing rows.
  Existing user databases are migrated on startup, so a migration must work on real data, not only on an empty DB.
- A smoke run mounts only test directories that `task-run` makes, never the real `/` or another real host path.
- For a small fix, the plan can be 15 lines. Do not pad it.

## intent.md template

```markdown
# Intent: <short title>
Author: <name>. Issue: <#number or none>. Status: draft.

## Problem
<what a person cannot do today, and who that person is>

## Proposed outcome
<what is true when the task is done>

## Affected parts
<backend API, backend core/infrastructure, EF schema/migration, frontend, Docker/compose, CI>

## Constraints
<security (host root, read-write mounts, delete safety), data (existing DB, scan archives), compatibility, what is out of scope>

## Open questions
<every point that is not decided>
```

## plan.md template

```markdown
# Plan: <short title>
Intent: [intent.md](intent.md). Status: draft.
Branch: <feature/<slug> or fix/<slug>>

## Files that change
- `path/to/file` (new | modified): <why>

## Order of work
1. <step>. Files: <paths>.
2. <step>. Files: <paths>.

## Pinned tests
<tests that task-run must not edit. "None" if there are none>

## Risks
<what this can break. Mark the riskiest step>

## Needs approval
<items from "When to stop and ask" in .claude/skills/task-run/SKILL.md, with the person's answer. "None" if there are none>

## Proof
- Checks: <backend, frontend, or both>
- Migration check: <needed or "not needed" with the reason>
- Smoke run: <needed, with the requests to make; or "not needed" with the reason>
- New tests: <file and what each asserts>
- Target: <a measurable result>
```

## Hand-off

Push the commit first. Then give the person these commands. Replace `<slug>` and `<branch>`.

```bash
git fetch && git worktree add ../webdisktree-worktrees/<slug> -b <branch> origin/main
```

```bash
cd ../webdisktree-worktrees/<slug> && claude --permission-mode auto "/task-run docs/tasks/<slug>/plan.md"
```

The plan commit must be on `origin/main` (merged) before the worktree is made, or the worktree will not have it.
If it is only on a plan branch, make the worktree from that branch instead of `origin/main`.
Each task gets its own worktree, so each task gets its own `bin/`, `obj/`, Docker image tag and container.

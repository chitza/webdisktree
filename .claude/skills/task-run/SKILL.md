---
name: task-run
description: Implement an accepted docs/tasks/<slug>/plan.md in WebDiskTree without a person, prove it with the backend/frontend checks and a Docker smoke run, and open a pull request. Use when the user runs /task-run with a plan path, or asks an agent to do an accepted plan unattended. Not for writing the plan (use task-plan).
---

# task-run

You work alone. Nobody reads the transcript. A person reads only the pull request.
Your job ends when the pull request is open and green, or when you stop at an escalation.

## Inputs

- The argument is the path to `plan.md`.
- Read `plan.md` and `intent.md` next to it.
- If `plan.md` does not say `Status: accepted`, stop. Report that the plan is not accepted.

## Checks

Run every command from the worktree root. Use `set -o pipefail` when you pipe output, so a failure is not hidden.

**Backend.** Run `dotnet` only in Docker, never with the host SDK. The host glibc is too old for the
Sqlite native library, and mixing host and container restores breaks `obj/`.
The container runs as your user, so `bin/` and `obj/` stay writable.

```bash
docker run --rm -u "$(id -u):$(id -g)" -e HOME=/tmp -e NUGET_PACKAGES=/nuget \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
  -v "$HOME/.nuget/packages:/nuget" -v "$PWD/backend:/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 sh -c \
  'dotnet restore WebDiskTree.slnx && dotnet build --no-restore WebDiskTree.slnx && dotnet test --no-build WebDiskTree.slnx'
```

**Migration check.** Run it when the branch changes anything in `backend/src/WebDiskTree.Infrastructure/Data/`.
It must print "No changes have been made to the model since the last migration".
Use the same `docker run` prefix, with this command:

```bash
dotnet tool install --tool-path /tmp/tools dotnet-ef --version "10.0.*" >/dev/null &&
/tmp/tools/dotnet-ef migrations has-pending-model-changes \
  --project src/WebDiskTree.Infrastructure --startup-project src/WebDiskTree.Api
```

To add a migration, use the same prefix with
`/tmp/tools/dotnet-ef migrations add <Name> --project src/WebDiskTree.Infrastructure --startup-project src/WebDiskTree.Api`.
Do not write a migration by hand.

**Frontend.**

```bash
cd frontend && npm ci --no-audit --no-fund && npx ng build && npx ng test --watch=false
```

**Smoke run.** Builds the production image, the same way as CI and the release. `<slug>` is the plan's slug.

```bash
docker build -t webdisktree:<slug> .
docker rm -f wdt-<slug> 2>/dev/null
docker run -d --name wdt-<slug> -p 127.0.0.1::8080 webdisktree:<slug>
P=$(docker port wdt-<slug> 8080/tcp | head -1)
for i in $(seq 30); do curl -fs "http://$P/api/roots" >/dev/null && break; sleep 1; done
curl -s -o /dev/null -w "%{http_code}\n" "http://$P/"          # expect 200
curl -s -o /dev/null -w "%{http_code}\n" "http://$P/api/scans" # expect 200
```

Then make the requests that the plan lists under "Smoke run". If a request fails, read `docker logs wdt-<slug>`.
The container has no `AllowedRoots` and no host mounts. If the plan needs a scan, it must say which
`-e AllowedRoots__Roots__0__Path=...` and `-v` mounts to add. Mount only a directory that you made for the test, read-only.

Remove the smoke run: `docker rm -f wdt-<slug>; docker rmi webdisktree:<slug>`.

## When to stop and ask

The plan must approve each of these under "Needs approval". If it does not, go to "Escalate".

- A new EF migration, or any change to the entities or `WebDiskTreeDbContext`.
- A migration that deletes, rewrites or moves existing rows.
- A change to `Security/` (path safety, `AllowedRoots`), to the delete endpoint, or to what a scan can reach.
- A change to the scan export or IMDB cache archive format, or to which archive versions import.
- A new NuGet or npm package, or a version change of one.
- A change to `Dockerfile`, `docker-compose.yml`, `.github/`, or the default config in `appsettings*.json`.
- An API route or DTO that is removed or renamed.

## Protected paths

Never edit these, even with approval:

- Existing migrations in `backend/src/WebDiskTree.Infrastructure/Data/Migrations/`
  (adding a new migration with `dotnet-ef` is fine when approved; the model snapshot updates with it).
- `docs/tasks/<slug>/intent.md` and `plan.md`.
- `.claude/`, `.superdesign/`, `.vscode/`, `backend/.idea/`.

## Procedure

### 1. Prepare

1. Make sure that you are in a git worktree, not in the main checkout: `git rev-parse --git-dir` must contain `/worktrees/`.
   If you are in the main checkout, stop and report it.
2. Run `git fetch`. If the worktree is not on the branch named in the plan, make it from `origin/main`.
   If `origin/main` has commits that the branch does not have, and the branch has none of your commits yet, reset to `origin/main` first.
3. Run `docker rm -f wdt-<slug> 2>/dev/null` to remove a smoke run left from an earlier attempt.
4. Run the checks for the parts that the plan changes, before you edit anything. If they fail on a clean `origin/main`, go to "Escalate".

### 2. Implement

Do the steps in "Order of work", in order. After each step:

1. Run the checks for the parts that the step changed (backend, migration check, frontend).
2. Fix every failure before the next step.
3. Commit the step. Use one commit per step. The message says what changed.

Rules:

- Do not edit a file in "Pinned tests".
- Do not edit a protected path.
- Do not delete or skip a test to make a check pass.
- If a step needs an item from "When to stop and ask", and "Needs approval" in the plan does not
  approve it, go to "Escalate".
- If the code shows that a plan step is wrong, go to "Escalate". Do not invent a new plan.
- For every other choice, take the simplest option that matches the code around it. Write it in your notes for "Decisions".

### 3. Prove

1. List the changed parts: `git diff --name-only origin/main...HEAD`.
2. Run the backend checks if `backend/` changed, the migration check if `Data/` changed, and the frontend checks if `frontend/` changed.
3. If the plan asks for the smoke run, do it now, from a fresh build. An image from an earlier run is not proof.
4. If a check fails, fix the code. Go to step 1.
5. After 3 failed attempts on the same failure, go to "Escalate".

### 4. Independent check

Start the `verifier` subagent. Give it the plan path and the slug.
Do not give it your notes or your opinion.

- If the verifier reports `FAIL`, fix each finding and go to "Prove".
- After 2 `FAIL` reports, go to "Escalate".

### 5. Open the pull request

1. Remove the smoke run.
2. Push the branch: `git push -u origin HEAD`.
3. Open a draft pull request with `gh pr create --draft --base main`. Use the body template below.
4. Watch the CI checks with `gh pr checks --watch`. CI runs "CI BackEnd" and "CI FrontEnd" only when those
   folders change, and "Docker build" always. If a check fails, read the log (`gh run view <id> --log-failed`),
   fix the code, and push. Stop after 3 rounds, then go to "Escalate".
5. When all checks pass, mark the pull request ready: `gh pr ready`.

Do not merge. Do not enable auto-merge.

## Escalate

1. Commit the work that you have. Push the branch.
2. Open a draft pull request. Put `BLOCKED:` at the start of the title.
3. Under "Not done", write:
   - the step where you stopped,
   - the rule from this skill that stopped you, or the failure,
   - the exact question that the person must answer,
   - the options that you see, with your recommendation.
4. Remove the smoke run.
5. Stop.

## Pull request body

```markdown
## Summary
<2 to 4 sentences: what changed, and why>

## Plan
[plan.md](docs/tasks/<slug>/plan.md). <"All steps done" or the list of steps not done>

## Decisions
- <each choice the plan did not make, and the reason>

## Verification
<for each command: the command, the exit code, the last 20 lines of output>

Verifier: PASS. <one line from its report>

## Not done
<what is left, or "Nothing">

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

## Always

- Remove your smoke-run container and image before you stop, on success and on failure.
- Touch only Docker containers and images named `wdt-<slug>` / `webdisktree:<slug>`. Never prune, never stop other containers.
- Never run the app against the real `/hostfs` or the production data volume.
- Never force-push. Never use `--no-verify`.
- Never report a check as passed that you did not run in this session.

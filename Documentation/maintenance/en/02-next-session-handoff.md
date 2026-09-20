# Unattended Handoff Guide

This file is not a history log. After taking over, complete the safety checks, then continue in the established priority order. Do not wait for the user to split the task.

## Startup safety checks

1. Confirm the current directory is the repository root. The root entry is [`../../../Microsoft.Dotnet.Wpf.slnx`](../../../Microsoft.Dotnet.Wpf.slnx).
2. Confirm `origin/` exists and is non-empty. If it is empty, stop migration immediately. Do not copy, clean, or repair builds.
3. Inspect Git changes. Identify and protect existing work. Do not overwrite unexplained modifications.
4. Remember that `origin/src` is excluded by `origin/.gitignore` and is not protected by the outer Git status.
5. Do not run `git clean -xdf`. Clean only confirmed regenerable output that is directly related to the current verification.

## Required reading

1. [`../README.md`](../README.md): language-neutral documentation entry.
2. [README.md](README.md): document roles, fact-maintenance rules, and safety rules.
3. [00-overview.md](00-overview.md): the single source of current status.
4. [01-phase-plan.md](01-phase-plan.md): later phases and completion criteria.
5. `.github/copilot-instructions.md`: repository-wide implementation rules.

Other topic documents are implementation references only. If they conflict with `00-overview.md`, `00-overview.md` wins.

## First command

Run the safety check at the repository root. Do not clean first:

```powershell
if (-not (Test-Path origin) -or -not (Get-ChildItem origin -Force | Select-Object -First 1)) { throw 'origin is empty; stop migration' }; git status --short
```

After confirming `origin/` is non-empty and reviewing Git changes, continue with builds or edits.

## Execution entry

Current build results, the first real blocker, and verification bounds are maintained only in [00-overview.md](00-overview.md). Do not copy them here. After the safety checks:

1. Read the "Current build status" and "Current open items" sections in `00-overview.md`.
2. Start from the first unfinished phase in [01-phase-plan.md](01-phase-plan.md).
3. Use the current entry, command, and error recorded in `00-overview.md` as the starting point. If the error has changed, the first real error from a fresh run wins.
4. Keep reproducible logs. After status changes, update only `00-overview.md`. Update `01-phase-plan.md` only when phase boundaries change.

## Continuous-progress rules

- Follow the phase order in [01-phase-plan.md](01-phase-plan.md). When a phase meets its completion criteria, start the next phase immediately.
- When a new error appears inside a phase, first update the current facts in [00-overview.md](00-overview.md), then continue with the first real blocker of that same phase.
- Do not skip current verification because of historical clues kept in topic documents. Do not generalize a local success to a full solution success.
- After a local success, continue to the next item. Do not replace implementation with document cleanup.

## Stop conditions

Stop continuous progress only when:

- `origin/` is missing or empty.
- The next step would overwrite existing Git changes that cannot be attributed, backed up, or isolated.
- Reproducible evidence shows that a required toolchain, permission, or external dependency is unavailable in the current environment, and there is no safe local alternative.
- Continued fixes would expand into an unauthorized repository scope and would violate the current task's file or module boundary.

When stopping, record the exact command, the first error, excluded causes, and recovery conditions. Do not replace evidence with unverified speculation.

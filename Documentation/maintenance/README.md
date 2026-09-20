# Repository Maintenance Documentation

This folder holds process documentation for maintaining this repository: current facts, later phases, unattended handoff, and engineering notes. It is not the product contribution guide. Public contributor and developer docs remain in [`../`](../).

The root solution entry is [`../../Microsoft.Dotnet.Wpf.slnx`](../../Microsoft.Dotnet.Wpf.slnx).

## Languages

Localized copies live in locale folders. Chinese is the current source language. English and other translations can be added later as sibling folders such as `en/`.

| Locale | Path | Status |
|---|---|---|
| Chinese | [`zh-CN/`](zh-CN/) | Current source |
| English | `en/` | Not added yet |

Until another locale exists, use [`zh-CN/README.md`](zh-CN/README.md) as the detailed index, then follow the Chinese core documents.

## How documents relate

```text
.github/copilot-instructions.md
        └── Documentation/maintenance/README.md          language-neutral entry
                    └── zh-CN/README.md                  detailed Chinese index
                            ├── 00-overview.md           current facts
                            ├── 01-phase-plan.md         later phases
                            ├── 02-next-session-handoff.md
                            ├── topic documents
                            └── archive/
```

- [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md) only states repository-wide agent rules. It should point here instead of a locale-specific file.
- This file only describes the folder layout and reading order. It does not copy current build status.
- [`zh-CN/README.md`](zh-CN/README.md) is the detailed Chinese index for document roles, reading order, fact-maintenance rules, and safety constraints.
- [`zh-CN/00-overview.md`](zh-CN/00-overview.md) is the single source of current workspace facts.
- [`zh-CN/01-phase-plan.md`](zh-CN/01-phase-plan.md) records later phases only.
- [`zh-CN/02-next-session-handoff.md`](zh-CN/02-next-session-handoff.md) records the unattended handoff procedure.
- Topic documents under `zh-CN/` cover one mechanism each. If they conflict with `00-overview.md`, `00-overview.md` wins.
- [`zh-CN/archive/`](zh-CN/archive/) is historical material. Do not use it as current status.

## Reading order

1. Read this file to learn where maintenance docs live.
2. Read [`zh-CN/README.md`](zh-CN/README.md) for document roles and safety constraints.
3. Read [`zh-CN/00-overview.md`](zh-CN/00-overview.md) for current workspace facts.
4. Read [`zh-CN/01-phase-plan.md`](zh-CN/01-phase-plan.md) to choose the next phase.
5. Read [`zh-CN/02-next-session-handoff.md`](zh-CN/02-next-session-handoff.md) before making changes.
6. Open a topic document only for that topic.
7. Open `zh-CN/archive/` only to trace historical design decisions.

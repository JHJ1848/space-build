# oh-my-codex Desktop Compatibility

This project uses a Desktop-compatible subset of `oh-my-codex`.

## What is enabled

- `oh-my-codex-compat` skill under `~/.codex/skills/`
- OMX-style workflow discipline:
  - clarify when needed
  - plan before non-trivial implementation
  - execute persistently to completion
  - verify before closing
- specialist role framing for analysis, planning, execution, verification, and review
- project memory remains in the repository root `MEMORY.md`

## What is intentionally not enabled

- `omx team`
- `tmux` panes or worker orchestration
- HUD or reply-to-pane flows
- `.omx/` runtime state
- native hook ownership through `.codex/hooks.json`

These surfaces are CLI-only and should not be assumed in Codex Desktop.

## Precedence in this repository

Use the following instruction order:

1. This repository's existing `AGENTS.md`
2. The global `oh-my-codex-compat` skill
3. External CLI fallback only when a request explicitly depends on OMX runtime features

If there is a conflict, the project `AGENTS.md` wins.

## Desktop operating model

- Default to one main Codex session.
- For complex tasks, follow the repository's existing sub-agent policy instead of OMX pane semantics.
- Do not reference `omx` commands unless the user specifically asks for the external CLI path.
- When using the compat workflow, prefer the semantic stages below over literal OMX commands:
  - interview: clarify scope, constraints, and non-goals
  - plan: lock the implementation path and tradeoffs
  - execute: implement and verify
  - review: inspect for regressions, missing tests, and risky assumptions

## Practical prompt examples

- "Use `oh-my-codex-compat` and clarify the scope before planning this feature."
- "Use the compat workflow and do a strict review pass before finalizing."
- "Treat this as plan then execute, but stay Desktop-only and avoid OMX runtime assumptions."

## External CLI fallback

If a task truly requires the full OMX runtime, use a separate CLI session for that path. Keep Desktop as the primary interface for normal project work.

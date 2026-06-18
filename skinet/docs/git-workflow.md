# Git Workflow

How code flows from a task to a sprint release. Pairs with the deploy gate in `testing.md`.

## Branches

| Branch | Holds | Merge style in |
|---|---|---|
| `main` | Sprint releases only — **exactly one squashed commit per sprint** (descriptive body). Push = Azure deploy. | **squash** |
| `development` | Integration — one **squashed commit per task**. | squash |
| `feature/<n>-<slug>` | One per task, from `development`. | — |
| `fix/task-<n>-<slug>` | Forgotten changes on a done task, from `development`. | squash |

## Task flow (during a sprint)

```bash
git switch development && git pull
git switch -c feature/<n>-<slug>
# work; commit freely — messy WIP commits are fine, they get squashed
git push -u origin feature/<n>-<slug>
gh pr create --base development --title "feat(task-<n>): <desc>" --body "..."
gh pr merge <pr> --squash --delete-branch     # development = 1 clean commit per task
```

## Sprint flow (sprint end)

```bash
# 1. all sprint tasks squash-merged into development
# 2. run the deploy gate in testing.md (local verify)
# 3. PR body = the sprint's items as `feat(task-N): …` / `fix(bug-N): …` lines (becomes the commit body)
gh pr create --base main --head development --title "Sprint N — <name>" --body "<task + bug lines>"
gh pr merge <pr> --squash                        # SQUASH — exactly one commit per sprint. NEVER --merge
# 4. push to main triggers Azure deploy → run the Azure half of the checklist
```

## Why squash to main (STRICT)

- **Squash → development:** collapses WIP noise into one readable commit per task.
- **Squash → main:** **exactly one commit per sprint.** `main` is the recruiter-/interviewer-facing
  history — it must read as a tidy list of sprints, never 20+ task commits. The granular work
  stays on `development` and in PRs. Put the sprint's items in the commit **body** as
  `feat(task-N): …` and `fix(bug-N): …` lines so each sprint maps to `Execution plan.md`.

`main` log must read like this — nothing else:

```
* Sprint 2 — Testing + Data Integrity
* Sprint 1 — Security + CI/CD
```

> **Never** release to `main` with `--merge` / `--no-ff`. A merge commit drags every development
> commit onto `main` (the exact mess this rule exists to prevent). Squash only — if `main` ever
> shows more than one commit per sprint, squash it back (see the Sprint 2 rewrite, Jun 18 2026).

## Rules

- Never commit directly to `main` or `development`.
- `main` = **one squashed commit per sprint**, with a `feat(task-N): …` / `fix(bug-N): …` body. Squash only, never `--merge`.
- Every Sprint / Task / bug fix tracked in `Execution plan.md` is squash-merged — each maps to a `task-N` / `bug-N` reference.
- `main` push = production deploy — only at a **verified** sprint end (checklist green first).
- Local → development → main. Verify with your own eyes before the `main` merge.

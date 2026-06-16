# Git Workflow

How code flows from a task to a sprint release. Pairs with `sprint-end-checklist.md`.

## Branches

| Branch | Holds | Merge style in |
|---|---|---|
| `main` | Sprint releases only — one **merge commit per sprint**. Push = Azure deploy. | merge commit (`--no-ff`) |
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
# 2. run sprint-end-checklist.md (local verify)
gh pr create --base main --head development --title "Sprint N — <name>"
gh pr merge <pr> --merge                        # MERGE COMMIT, not squash
# 3. push to main triggers Azure deploy → run the Azure half of the checklist
```

## Why these two merge methods

- **Squash → development:** collapses WIP noise into one readable commit per task.
- **Merge commit → main:** keeps each task visible, grouped under a `Sprint N` merge commit.
  `main` reads as a list of sprints; expand a merge to see the tasks underneath.
  No force-pushes, no branch divergence.

`main` log ends up like:

```
*   Merge: Sprint 3 — Admin Panel
|\
| * feat(task-8): FluentValidation
| * feat(task-24): Bogus seed
|/
* Sprint 1 — Security + CI/CD
```

## Rules

- Never commit directly to `main` or `development`.
- `main` push = production deploy — only at a **verified** sprint end (checklist green first).
- Local → development → main. Verify with your own eyes before the `main` merge.

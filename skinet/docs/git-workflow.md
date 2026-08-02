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

🔴 **No PR here — `main` is an orphan branch.** It shares no commit with `development`, so GitHub
cannot diff them and `gh pr create --base main --head development` 404s (`no history in common`).
That is by design: an orphan `main` is what keeps its log to one commit per sprint. The release is a
**tree replacement**, not a merge.

```bash
# 1. all sprint tasks squash-merged into development
# 2. run the deploy gate in testing.md (local verify) — a Release build, not just Debug
git switch main && git pull
git read-tree --reset -u development   # take development's tree wholesale — no 3-way merge, no conflicts
git diff development --stat            # MUST be empty — proves main now matches development exactly
git commit -m "Sprint N — <name>" -m "feat(task-N): …
fix(bug-N): …"                         # body = the sprint's items, one line each
git push origin main                   # 3. triggers Azure deploy → run the Azure half of the checklist
```

`read-tree --reset -u` overwrites the index and working tree from another branch without trying to
merge — the reason it can't conflict. `git merge --squash --allow-unrelated-histories` is the obvious
alternative and it **fails**: it still runs a 3-way diff, so files added independently on both branches
come back as add/add conflicts.

## Why squash to main (STRICT)

- **Squash → development:** collapses WIP noise into one readable commit per task.
- **Tree replacement → main:** **exactly one commit per sprint.** `main` is the recruiter-/interviewer-facing
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

- Never commit directly to `development` — always a branch + PR.
- Never commit directly to `main` **except the sprint release itself**, which has no other mechanic
  (orphan branch, see Sprint flow). One such commit per sprint, at a verified sprint end. Nothing else.
- `main` = **one squashed commit per sprint**, with a `feat(task-N): …` / `fix(bug-N): …` body. Squash only, never `--merge`.
- Every Sprint / Task / bug fix tracked in `Execution plan.md` is squash-merged — each maps to a `task-N` / `bug-N` reference.
- `main` push = production deploy — only at a **verified** sprint end (checklist green first).
- Local → development → main. Verify with your own eyes before the `main` merge.

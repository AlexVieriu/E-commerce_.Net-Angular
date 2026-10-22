# Git Workflow

How code flows from a task to a sprint release. Pairs with the deploy gate in `testing.md`.

## Branches

| Branch | Holds | Merge style in |
|---|---|---|
| `main` | Sprint releases only — **exactly one squashed commit per sprint** (descriptive body). Push = Azure deploy. | **squash** |
| `development` | Integration — one **squashed commit per task**. | squash |
| `feature/<n>-<slug>` | One per task, from `development`. | — |
| `fix/bug-<n>-<slug>` | One per bug in `docs/archive/execution-plan.md`, from `development`. | squash |
| `fix/task-<n>-<slug>` | Forgotten changes on an already-merged task, from `development`. | squash |

## Task flow (during a sprint)

One branch, one PR, one squashed commit. The PR is opened **as a draft** once the implementation is
done — a draft cannot be merged, which is the guardrail. It is also the branch's before/after
(`git diff development...HEAD`), and the gate and the doc review are both read against it.

```bash
git switch development && git pull
git switch -c fix/bug-<n>-<slug>              # or feature/<n>-<slug>

# 1. Work. Commit as often as you like — messy WIP commits are fine, they all squash at merge.

# 2. Implementation done -> run "After each task" in testing.md:
#    dotnet test · dotnet build -c Release · lint + format · smoke -> smoke-tests/sprint-<n>/

# 3. Push and open the PR as a DRAFT. Body #1: what changed and why, in bullets.
git push -u origin fix/bug-<n>-<slug>
gh pr create --base development --draft --title "fix(bug-<n>): <desc>" --body-file <file>

# 4. The 2-sentence test at merge -> pass, or the concept joins the queue in mindset/the-gate.md.
#    The queue blocks the sprint, not this PR; the gate day at sprint end clears it.

# 5. Gate cleared -> edit the PR body against the final diff, then the bookkeeping
#    (the task-wrapup skill): progress.md · mindset/daily-log.md · docs/ · mindset/question-tally.md

# 6. 🔴 Your sign-off — never Claude's call.
gh pr ready <pr>
gh pr merge <pr> --squash --delete-branch     # 1 clean commit on development; deletes local + remote

# 7. Scaffold the next spike in mindset/spikes/sprint-<n>/
```

The PR keeps the individual commits and the review browsable forever, so deleting the branch loses
nothing — `development` gets the one clean commit.

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
  `feat(task-N): …` and `fix(bug-N): …` lines so each sprint maps to `docs/archive/execution-plan.md`.

`main` log must read like this — nothing else:

```
* Sprint 2 — Testing + Data Integrity
* Sprint 1 — Security + CI/CD
```

A release between sprints (a freeze or a hotfix) uses the same mechanic, titled `vX.Y.Z — <name>`.

> **Never** release to `main` with `--merge` / `--no-ff`. A merge commit drags every development
> commit onto `main` (the exact mess this rule exists to prevent). Squash only — if `main` ever
> shows more than one commit per sprint, squash it back (see the Sprint 2 rewrite, Jun 18 2026).

## Rules

- Never commit directly to `development` — always a branch + PR.
- Never commit directly to `main` except the release commit itself (orphan branch, see Sprint flow).
- Every Sprint / Task / bug fix tracked in `docs/archive/execution-plan.md` is squash-merged — each maps to a `task-N` / `bug-N` reference.
- `main` push = production deploy — only at a **verified** sprint end (checklist green first).
- Local → development → main. Verify with your own eyes before the `main` merge.

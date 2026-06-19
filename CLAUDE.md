# CLAUDE.md
# YOUR ROLE
You are a senior .NET + Angular developer. Be concise. Call out bad ideas immediately.
Comments: one sentence per line, simple and to the point; skip the obvious.

# THE MAIN FLOW
Create the following to-do list immediately.
- Analyze user request.
- Read the doc(s) matching the task — start at `skinet/docs/INDEX.md` (routes in DOCS).
- Analyze examples from the docs.
- Revise execution plan and present it to the user with todo items.
- Once the user accepts, create revised todo items.
- Start work on the task.
- Keep the docs as short as possible.
- After the task is done, run the AFTER EACH TASK checklist.

# TASK KICKOFF
Before starting any task, list:
- Required knowledge (critical only)
- Knowledge gaps to address first

 # QUESTIONS & EXERCISES
  After each completed task (non-trivial concepts only):
  1. Create `skinet/questions/sprint-N/task-N-concept-name.md` with scaffold and fill it in fully:
     - # Task N — Concept Name
     - ## Why this approach
     - ## What confused me
     - ## Explanation
     - ## Exercise
     - ## Solution
     - ## Tests
     - ## Interview Q&A — answers as bullet points, not prose
  2. Append one line to `skinet/questions/interview-prep.md`:
     `- [Concept](sprint-N/task-N-concept-name.md) — one-line answer`
  When merging a `fix/task-N-*` branch: append `## Fix — <date>` to the Solution section.
   
# GIT
  - Branch per task: `feature/<task-number>-<short-description>` from `development`
  - Task done → open PR to `development` as a **draft**; squash-merge (one commit per task) only after
    the gate + doc review, so review tweaks fold in.
  - No standalone or per-tweak docs PRs — docs/process changes ride in the related task or `fix` PR
    (interviewers care about task & bug-fix PRs, not docs noise). One PR open at a time; close it once.
  - PR to `main` at sprint end only → **merge commit (`--no-ff`)**, titled `Sprint N — <name>`
    (groups tasks under the sprint; pushing `main` deploys to Azure — verify first)
  - Never commit directly to `main` or `development`
  - For forgotten changes on a completed task: `fix/task-N-description` from `development` (squash-merge)
  - Full flow + commands: `skinet/docs/git-workflow.md`

 # PR
  When asked to open/create a PR, always output a suggested title and description before the URL.
  - Title: `<type>(task-N): <short description>`
  - Description: bullet-point summary of what changed and why.

# AFTER EACH TASK
Single checklist for post-task bookkeeping. Run in order:
  1. Tick the task checkbox + add a Daily Log row in `skinet/progress.md`.
  2. Add a bullet under today's section in `mindset/daily-log.md` (see END OF DAY).
  3. Update any affected docs in `skinet/docs/` (keep them short).
  4. Scaffold the NEXT task/bug spike at the TOP of `mindset/spikes.md` (see MINDSET).
  5. Scaffold a journal stub for the finished task in `mindset/journal/sprint-N.md` — journal.md's
     template with task-specific EXPLAIN/REBUILD/BREAK *prompts* filled in, answers left BLANK. Then
     REMIND the user to write the entry before starting the next task; don't start the next until it's
     written. (Prompts only — never write the answers.)
  6. Questions & Exercises for non-trivial concepts (see above).

# END OF DAY / SESSION
Before wrapping up, ensure `mindset/daily-log.md` has today's dated section
(`## YYYY-MM-DD (Day)`, newest on top) covering what was touched — even non-task work (bugs, docs).
The user's personal notes ("tomorrow, first thing…", how it felt) stay theirs.

# PROTECTED FILES
Never modify these files unless explicitly instructed:
- `skinet/questions/interview-gen-prompt.md`
- `skinet/questions/anki/md_to_anki.py`
- `skinet/questions/anki-prompt.md`

# DOCS
Doc index: `skinet/docs/INDEX.md` — read the doc matching the task before starting.
Routes: Angular component/service/route/model → docs/frontend/actions/; interceptors/guards/state
→ docs/frontend/; specs/EF/repo → docs/backend/data-layer.md, specifications.md; Stripe →
docs/backend/payment.md; git/PR → docs/git-workflow.md; verification + deploy gate → docs/testing.md.

# MINDSET (scaffold only — never write the user's thinking)
- Each task/bug → a top entry in `mindset/spikes.md` (Question/Options/Decision/Why + checklist);
  the top entry is the active to-do. Leave Decision + Why blank for the user.
- New sprint → scaffold empty `mindset/journal/sprint-N.md` from the template in journal.md.
- Never write spike Decisions or journal entries — that's the user's job (skill practice).

# SUPERVISOR MINDSET
Act as a senior dev / supervisor, not just an executor. From time to time —
especially before big, risky, or cosmetic work — stop and surface:
- Is this the highest-value thing right now? Call out over-investment & scope creep.
- What's the best next step? Recommend it plainly.
- Separate what I *want* from what's *good for the project* (shipping > polishing).
Don't wait to be asked — raise the flag when priorities drift.

DO NOT EDIT THIS FILE.

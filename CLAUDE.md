# CLAUDE.md
# YOUR ROLE
- Senior .NET + Angular developer. Be concise. Call out bad ideas immediately.
- Comments: one sentence per line, simple and to the point — skip the obvious.
- LEARN tasks: verify against official docs before presenting — cite the source, don't trust recall alone.
- English: user is a non-native speaker improving it — one-line correction on mistakes, then answer normally. Don't nag.

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
After each completed task. **`## The Questions` + `## Hints` are written BEFORE the cold test** (they
give nothing away); **`## Answers` is written AFTER it**, so the gaps you surfaced drive the content
(non-trivial concepts only). Verify every technical claim against the official docs *before* writing —
cite what you used in `## Sources`; don't trust recall alone.
1. Create `skinet/questions/sprint-N/task-N-concept-name.md` with scaffold and fill it in fully:
   Default every section to bullet points over paragraph prose — bold-label bullets for connected
   reasoning (e.g. **Concrete trace:** / **The rule:** / **Why it matters:**) instead of solid
   paragraphs. Exception: `What confused me` stays a table.
   - # Task N — Concept Name
   - ## The Questions — the gate's 3 EXPLAIN + 2 BREAK prompts, verbatim. Nothing else.
   - ## Hints — one line per question: `file · what to notice`. A location anchor, NEVER the answer.
     The rung between "I know it" and "just tell me" — without it, every gap becomes a chat dump.
   - ## Answers — written at LEARN, after the sit. **One chain, in order** — each answer sets up the
     next. Five disconnected islands is what leaves nothing in your head.
   - ## Why this approach — one bullet = one decision or trade-off
   - ## What confused me — the actual gaps from the cold test / LEARN session. Tag each gap with a
     location anchor (`file · where in it`) and a focus tier so future-me drills only what pays off:
     **OWN** (interview-likely — explain cold) · **RECOGNIZE** (re-derive on the spot, don't memorize)
     · **LET GO** (trivia — one-line summary is enough). Lead the section with a small triage table.
   - ## Exercise
   - ## Solution
   - ## Tests
   - ## Interview Q&A
   - ## Sources — official docs backing the claims above (link + what it confirms)
2. Append one line to `skinet/questions/interview-prep.md`:
   `- [Concept](sprint-N/task-N-concept-name.md) — one-line answer`
When merging a `fix/task-N-*` branch: append `## Fix — <date>` to the Solution section.

# GIT
- **One branch + one PR per task/bug** (`feature/<task-number>-…` or `fix/…` from `development`).
  Commit to it as often as you like — commits squash into one at merge; never open another PR for
  the same work.
- Task done → open PR to `development` as a **draft**; squash-merge (one commit per task) only after
  the gate + doc review, so review tweaks fold in.
- No standalone or per-tweak docs PRs — docs/process changes ride in the related task or `fix` PR
  (interviewers care about task & bug-fix PRs, not docs noise). One PR open at a time; close it once.
- PR to `main` at sprint end only → **squash** (exactly one commit per sprint), titled `Sprint N — <name>`
  (task/bug lines go in the commit body; pushing `main` deploys to Azure — verify first)
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
4. Scaffold a journal stub for the finished task in `mindset/journal/sprint-N.md` — journal.md's
   template, prompts only, answers BLANK. Scaffold REBUILD `*2` scratch files too (procedure in
   `mindset/journal.md`). Verify build green. Remind the user to write the entry before starting
   the next task; don't start it until they have.
5. Questions & Exercises for non-trivial concepts (see above).
6. Append one line to `mindset/question-tally.md` — junior vs mid-level % for this session's
   technical questions (see that file for the rubric).
7. After the PR is merged — right before starting the next task — scaffold the NEXT task/bug spike
   at the TOP of `mindset/spikes.md` (see MINDSET), so Decision+Why are filled while context is fresh.

# START OF DAY / SESSION
- At the start of the day's first work session, ask ONE line:
  "Energy today — 1–5? (5 🔵 peak · 4 🟢 high · 3 🟡 ok · 2 🟠 low · 1 🔴 depleted)"
- Read `mindset/energy-protocol.md` and recommend the day's focus for that level
  (4–5 → deep LEARN/REBUILD; 3 → mechanical + light study; 2 → chores + light mechanical, no new learning;
  1 → recovery/rest).
- One line, skippable, don't nag. Ask once per day, not every chat.

# END OF DAY / SESSION
- Before wrapping up, ensure `mindset/daily-log.md` has today's dated section
  (`## YYYY-MM-DD (Day)`, newest on top) covering what was touched — even non-task work (bugs, docs).
- The user's personal notes ("tomorrow, first thing…", how it felt) stay theirs.

# PROTECTED FILES
Never modify these files unless explicitly instructed:
- `skinet/questions/interview-gen-prompt.md`
- `skinet/questions/anki/md_to_anki.py`
- `skinet/questions/anki-prompt.md`

# DOCS
- Doc index: `skinet/docs/INDEX.md` — read the doc matching the task before starting.
- Routes: Angular component/service/route/model → docs/frontend/actions/; interceptors/guards/state
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

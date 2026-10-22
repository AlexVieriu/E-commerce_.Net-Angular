---
name: task-wrapup
description: The AFTER EACH TASK checklist — post-task bookkeeping in order (gate, progress.md tick, daily log, docs, question tally, next spike). Use when a task or bug is done and its draft PR is open, or before merging a task/bug PR.
---

# AFTER EACH TASK
Single checklist for post-task bookkeeping. **Push + open the draft PR before step 0** (see GIT in CLAUDE.md) —
the gate and the doc review are both read against its diff. Run in order:
0. **THE GATE — read `mindset/the-gate.md` and follow it.**
   Run the gate for a new interview-explainable concept only (mechanical tasks skip it); all in **one file**, the
   questions doc (`skinet/questions/sprint-N/<top-concept>.md`; sections in the `learning-workflow` skill). The loop:
   solve → learn → gap → log → merge. Attempt → stuck → Claude answers **now** → move on. No ping-pong.
   **A sprint never closes with a concept you can't explain.**
1. Tick the task checkbox in `skinet/progress.md` (inside the sprint's `<details>`).
2. Add a bullet under today's section in `mindset/daily-log.md` (see END OF DAY in CLAUDE.md).
3. Update any affected docs in `skinet/docs/` (keep them short).
4. Append one line to `mindset/question-tally.md` — junior vs mid-level % for this session's
   technical questions (see that file for the rubric).
5. After the PR is merged — right before starting the next task — scaffold the NEXT task/bug spike
   in `mindset/spikes/sprint-N/` (see the `learning-workflow` skill), so Decision+Why are filled while context is fresh.

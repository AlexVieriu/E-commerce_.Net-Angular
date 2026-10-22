---
name: learning-workflow
description: Gate-day rules and the questions doc format, the emoji rules for learning docs, and the mindset rules (spikes, sprint map). Use when running a gate day, writing a questions/*.md gate file, writing a spike or sprint-map entry, or choosing emoji headings in a learning doc.
---

# Learning workflow

## QUESTIONS & EXERCISES
On a gate day — the full flow is in `mindset/the-gate.md` (**one file**). A merge that fails the
2-sentence test leaves its concept for the next gate day; mechanical work skips it (build, verify, tick).
`## Read First` + `## Change Map` + `## Hints` are scaffolded first (study material — give nothing away);
**you** write `## 🔁 REBUILD / 🧠 EXPLAIN / 💥 BREAK` inline; Claude fills the rest from the gaps you hit.
Verify every technical claim against official docs *before* writing — cite in `## Sources`; don't trust recall.
1. Create `skinet/questions/sprint-N/<top-concept-name>.md` — one file per gate day, named for its
   top-ranked concept, top to bottom, in this order:
   Default every section to bullet points over prose — bold-label bullets (e.g. **Concrete trace:** /
   **The rule:** / **Why it matters:**). Exception: `What confused me` stays a table.
   - `# Sprint N gate — <top concept>`, then a `## 🎯 Full — <concept>` heading per full concept (max 2)
   - `## 📂 Read First` — scaffolded first, **read BEFORE the Change Map.** The changed/created code worth
     reading, tiered by depth: **read fully** (the 1–2 files that *are* the task) · **read the key lines** ·
     **just locate it**. List only files worth opening — mechanical noise is named in the Change Map, don't repeat it.
   - `## 🗺️ The Change Map` — scaffolded first. Opens with **the Ideas** — 3–5 load-bearing ideas (the spine
     every prompt hangs off). Then **LOAD-BEARING** (the files that *are* the task, tagged with their Idea) +
     **MECHANICAL** (reformat / codegen noise — named so it's skipped). A 157-file diff → ~8 files, ~5 ideas.
   - `## 🧭 Hints` — one line per attempt: `file · what to notice`. A location anchor, **never the answer.**
   - `## 🔁 REBUILD` — *you write.* The key lines, cold.
   - `## 🧠 EXPLAIN` — *you write.* **3–4 clear, self-contained** "why does it work?" questions (Claude scaffolds), answered 1–2 lines each.
   - `## 💥 BREAK` — *you write.* **1–2 clear** "what fails in prod?" questions (Claude scaffolds), each naming a concrete failure.
   - `## ⚖️ Why this approach` — a table: one row = one decision or trade-off (shape in `the-gate.md`).
   - `## 🌫️ What confused me` — your real gaps. Lead with a triage table, tag each: **OWN** (explain cold) ·
     **RECOGNIZE** (re-derive, don't memorize) · **LET GO** (trivia) + a location anchor.
   - `## 💡 Explanation` — **one chain, in order** — each idea sets up the next, built from the gaps you hit.
   - `## 🎤 Interview Q&A` — numbered **Q1:** / **Q2:** then a **bulleted** answer (never prose), so you can say "stuck on Q4".
   - `## 📚 Sources` — official docs backing the claims (link + what it confirms).
   - *(optional — skip for a task you built: ## ✏️ Exercise · ## ✅ Solution · ## 🧪 Tests)*
2. Append one line to `skinet/questions/interview-prep.md`:
   `- [Concept](sprint-N/top-concept-name.md) — one-line answer` — one line per concept, so a gate day
   with four of them appends four, all pointing at that day's single file
When merging a `fix/task-N-*` branch: append `## Fix — <date>` to the Solution section.

## EMOJI
Learning docs should be pleasant to open — but a landmark only works if it's rare.
- **Emoji mark fixed, recurring section headings.** If a heading appears in *every* instance of a doc
  type, it earns one. A sentence, a table cell, or a one-off heading does not.
- **Where they go:** `questions/…/<top-concept>.md` (🎯🪶📂🗺️🧭🔁🧠💥⚖️🌫️💡✏️✅🧪🎤📚) · `mindset/the-gate.md`
  (🎯 the gate) · `mindset/spikes/**` (❓ Question · ⚖️ Options · ✅ Decision · 💬 Why · 📋 Checklist).
- **Where they don't:** this file · `progress.md` rows · `daily-log.md` · `skinet/docs/**` — contracts,
  prose, and technical reference someone else may read.
- **Status markers are always fine, anywhere:** ✅ ⚠️ ❌ 🔴 🟡 🟢 ⏳ — they carry meaning, not decoration.
- The failure mode is marking *everything*: if every heading is a landmark, none of them are.

## MINDSET (scaffold only — never write the user's thinking)
The `mindset/` folder is the learning-process system — six entries, one per workflow moment. **This
section is the map; the step-by-step trigger lives in the section named in the last column.**

| File | What it's for | Fires (→ section) | Who writes |
|---|---|---|---|
| `energy-protocol.md` | energy level → task type | START OF DAY | Claude routes |
| `sprint-map.md` | the sprint drawn (routes + wireframes) | sprint start & end (MINDSET) | Claude draws |
| `spikes/` | decide the approach before coding | before a task (AFTER EACH TASK #5) | you: Decision+Why · Claude scaffolds |
| `the-gate.md` | prove you learned it | gate day — sprint end (AFTER EACH TASK #0 feeds it) | you attempt · Claude fills |
| `daily-log.md` | manager-facing day record | AFTER EACH TASK #2 · END OF DAY | Claude logs |
| `question-tally.md` | junior/mid interview calibration | AFTER EACH TASK #4 | Claude appends |

- Each task/bug → its own file in `mindset/spikes/sprint-N/` (Question/Options/Decision/Why + checklist).
  The current sprint gets a folder, one file per item; past sprints collapse to one file each.
  Leave Decision + Why blank for the user.
- Sprint map (`mindset/sprint-map.md`, newest sprint on top) — **written exactly twice per sprint,
  never in between:**
  - **Sprint start:** the route tree + one ASCII wireframe per task — the sprint made visible before
    it's built (~15 min).
  - **Sprint end:** append an **as-built diff** — what shipped differently from the picture, redrawing
    only the boxes that moved (~10 min).
  A snapshot, not a spec. A wireframe that goes stale mid-sprint **stays** stale — that's the point.
- Never write spike Decisions or the user's REBUILD/EXPLAIN/BREAK answers — that's the user's job (skill practice).

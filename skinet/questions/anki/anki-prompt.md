# Anki Export Prompt

## Context

Python script `md_to_anki.py` (`skinet/questions/anki/`) converts structured markdown Q&A files into tab-separated Anki-importable `.txt` files.

---

## What the Script Does

- Parses questions formatted as **B1.**, **A3.**, etc.
- Extracts three collapsible blocks per question:
  - `The 5% Answer` → answer content (blue callout)
  - `Red Flag` → shallow-answer warning (red callout)
  - `Green Flag` → pro insight (green callout)
- Auto-detects sprint from parent folder (`sprint-2-interview` → Sprint 2)
- Auto-detects section from filename (`section-b-*` → Section B)
- Maps `### BEHAVIORAL` heading to Medium difficulty
- Strips bare URL lines and repeated `**The 5% Answer...:**` headers inside detail blocks

---

## Color Theme — One Dark Pro

| Role | Hex |
|---|---|
| Background | `#282c34` |
| Surface | `#3e4451` |
| Muted | `#5c6370` |
| Text | `#abb2bf` |
| Cyan | `#56b6c2` |
| Blue | `#61afef` |
| Green | `#98c379` |
| Orange | `#d19a66` |
| Red | `#e06c75` |

---

## Card Format

### Front

Two-row layout:

- **Row 1** — muted monospace: `S01.01 · [difficulty badge] · [frequency badge]` (`color:#5c6370`, `font-size:0.68em`)
- **Row 2** — question text (`font-size:1.08em`, `line-height:1.55`, `system-ui`)

**Badges:**

| Type | Value | Color |
|---|---|---|
| Difficulty | Easy | `#98c379` |
| Difficulty | Medium | `#d19a66` |
| Difficulty | Hard | `#e06c75` |
| Frequency | `★ HIGH` | `#4078f2` |
| Frequency | `◆ MED` | `#986801` |
| Frequency | `○ LOW` | `#5c6370` |

Frequency badge is extracted from `[HIGH]`/`[MEDIUM]`/`[LOW]` at the start of the question, then stripped before rendering.

**Inline code:** `<code style="background:#3e4451;color:#56b6c2;...">`
**Fenced code blocks:** `<pre style="background:#282c34;border-left:3px solid #61afef;..."><code>...</code></pre>`
Front uses inline styles throughout.

### Back

Starts with a `<style>` block defining:

| Class | Purpose |
|---|---|
| `.c` | Inline code — cyan `#56b6c2` on surface `#3e4451` |
| `.p` | Code block — dark bg, blue `#61afef` left border, `<br>` line breaks |
| `.ac` | Answer wrapper — `system-ui`, `line-height:1.65`, proper list margins |
| `.ac b` | Bold key terms — cyan `#56b6c2` |
| `.ac li::marker` | Bullets/numbers — blue `#61afef` |
| `.af` / `.af-label` | **The 5% Answer** — blue left border, dark blue bg |
| `.rf` / `.rf-label` | **Red Flag** — red left border, dark red bg |
| `.gf` / `.gf-label` | **Green Flag** — green left border, dark green bg |

**Back structure:**

```html
<style>...</style>
<div class="af"><p class="af-label">★ The 5% Answer</p><div class="ac">...</div></div>
<div class="rf"><p class="rf-label">🚩 Red Flag — shallow answer</p><div class="ac">...</div></div>
<div class="gf"><p class="gf-label">✓ Green Flag — pro insight</p><div class="ac">...</div></div>
```

Lists use `<ul><li>` / `<ol><li>`. Generic types inside code tags must be HTML-escaped.
The `<style>` in Back applies to the whole Anki answer page (`{{FrontSide}}<hr>{{Back}}`).

---

## Output & Deck Structure

Files are saved in an `anki/` subfolder inside the sprint interview folder:

```
sprint-1-interview/anki/section-a-csharp-dotnet_anki.txt
sprint-2-interview/anki/section-b-efcore-concurrency_anki.txt
```

Deck tag format: `#deck:E-Commerce - Sprints::Sprint N::Section X`

```
E-Commerce - Sprints
  Sprint 1
    Section A
    Section B
  Sprint 2
    Section A
    ...
```

---

## Your Task

1. Find all `section-X-*.md` files in the target sprint folder:
   - `skinet/questions/sprint-2/sprint-2-interview/`
2. Run: `py skinet/questions/anki/md_to_anki.py <filepath>` for each file.
3. List output files and confirm card count per file.

**Filename mapping:**
```
section-a-cors-csp-security.md        → anki/section-a-cors-csp-security_anki.txt
section-b-optimistic-concurrency.md   → anki/section-b-optimistic-concurrency_anki.txt
```

---

## Validation Checklist

After generating, verify each `_anki.txt`:

- [ ] First line is `#separator:tab`
- [ ] Fourth line is `#deck:E-Commerce - Sprints::Sprint N::Section X`
- [ ] Front: two-div structure — metadata row + question row
- [ ] Back: `<style>` block contains `.c`, `.p`, `.ac`, `.af`, `.rf`, `.gf`
- [ ] Back: `.af` box with `★ The 5% Answer` label
- [ ] Back: `.rf` box with `🚩 Red Flag` label (if present in source)
- [ ] Back: `.gf` box with `✓ Green Flag` label (if present in source)
- [ ] Inline code uses `class="c"`, code blocks use `class="p"`
- [ ] Code blocks use `<br>` for line breaks — not raw newlines
- [ ] No card has an empty back field
- [ ] No raw `https://` URLs in any field
- [ ] No raw triple backticks (` ``` `) in any field

Report any issues found.

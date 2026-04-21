# Sprint Interview Question Generator

**.NET / Angular E-commerce** — Clean Architecture · EF Core · Redis · Stripe · SignalR · Azure

---

## Candidate Profile

| Axis | Level | Implication |
|---|---|---|
| **.NET / C#** | **Mid-level** | Full depth on async/await, DI lifetimes, EF Core, Clean Architecture, security, testing. Hard questions are fair. |
| **Angular** | **Junior** | Easy–Medium only. Skip advanced zoneless internals, `httpResource()`, `linkedSignal()` unless tagged `[OUT-OF-SCOPE]`. |
| **.NET target** | **10–11** | Use current APIs: `ValidateOnStart()`, `IExceptionHandler`, `ExecuteUpdateAsync`, `TimeProvider`. |
| **Angular target** | **21–22** | Use current syntax: `@if`/`@for`/`@defer`, `signal()`, `input()`, `inject()`, standalone. No `ngIf`, no `NgModule`. |

**Goal:** Top 5% candidate calibrated to the levels above.

---

## Pre-Work

Read every task file for the target sprint (`skinet/questions/sprint-N/task-N-*.md`) before writing a single question. Extract the actual decision made, the bug hit, the pattern used, and the trade-off chosen — this is the source for the 70% in-scope questions.

---

## Structure

**Sections:** group sprint tasks into labeled sections (A, B, C…). Scale question count to concept depth — 5 questions for a focused task, 10+ for multi-bug/multi-trade-off sections. Every question must earn its place. No padding, no repetition.

**Difficulty split (approximate):**

| Tier | Weight | What it tests |
|---|---|---|
| 🟢 Easy (~20%) | Recall + direct application | Answerable in under 60 sec. Tied to a real sprint decision or a fundamental no mid-level should miss. |
| 🟡 Medium (~40%) | Diagnosis + trade-offs | *"Works locally, breaks in prod."* · *"Write the correct version."* · *"X vs Y — when does each break?"* |
| 🔴 Hard (~40%) | Production consequences + architecture | Real bug with a fixed version. At least 1 multi-bug hunt per section. Security/payments: at least 1 attack scenario with exact HTTP request shown. |

**Out-of-scope ratio:** ~30% of questions per section tagged `[OUT-OF-SCOPE]` — high-frequency industry topics a senior is expected to know cold.

---

## Frequency Tags

`[HIGH]` — eliminated if missed · `[MEDIUM]` — expected at mid-level · `[LOW]` — **not allowed**, replace with a `[HIGH]`/`[MEDIUM]` on a related concept.

**HIGH examples:** N+1 · CORS · async/await deadlocks · DI captive dependency · EF `IQueryable` vs `IEnumerable` · concurrency tokens · Angular signals + change detection · RxJS cancellation · payment idempotency · config provider chain.

---

## Question Format

```
**X1.** [HIGH] Question text
**X2.** [HIGH] [OUT-OF-SCOPE] Out-of-scope question

<details><summary>The 5% Answer</summary>

Bullet points. **Bold key terms.** Fenced code with language tags.
Show WHY — not just what was built. Trade-offs + what breaks under load.
Hard answers end with: **Production consequence:** *what fails if wrong in prod*

</details>
<details><summary>Red Flag</summary>

**Red Flag:** The shallow/textbook answer — one concrete example of what it sounds like.

</details>
<details><summary>Green Flag</summary>

**Green Flag:** The nuance only someone who built and operated this knows.

</details>
```

---

## Section-Ending Quick-Fire (every section)

End each section with **2–3 quick-fire syntax questions**. Same full format — `[HIGH]`, `The 5% Answer`, `Red Flag`, `Green Flag` — but answers are tight: a few bullets or one short code block max.

**.NET / C# targets:** `async`/`await` (`ConfigureAwait`, `async void`, `ValueTask`), `throw` vs `throw ex`, `DateTime` vs `DateTimeOffset`, `IDisposable`/`using`, LINQ pitfalls, records vs classes.

**Angular targets (junior-calibrated):** `signal()`/`computed()`, `inject()`, `@if`/`@for`/`@defer`, `toSignal()` vs `AsyncPipe`, `takeUntilDestroyed()`, `switchMap` vs `mergeMap`.

Match language to the section. Mixed sections: one from each.

---

## Answer Rules

- No filler. No *"Great question."*
- **Bold** key terms. Language tag on every code block (`csharp`, `typescript`, `yaml`, `sql`, `bash`).
- Show **why** — the reasoning behind the decision, not just the correct API.
- Hard answers must include a **Production consequence** line.

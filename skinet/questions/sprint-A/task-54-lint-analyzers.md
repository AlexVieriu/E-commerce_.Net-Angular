# Task 54 — ESLint, Prettier & Roslyn Analyzers (the quality gate)

`claude --resume e6d59f3a-3397-41a2-bb04-30084b7e971e`

> **How to use this doc** (the gate → [`mindset/the-gate.md`](../../../mindset/the-gate.md)):
> 1. **STUDY** — read `## The Change Map`. ~10 min. *This is the textbook, not the answer key.*
> 2. **SIT** — the questions live in the **journal** (`mindset/journal/sprint-A.md`). Closed book, 25 min.
> 3. Stuck? → `## Hints` — a location anchor, not an answer. Connect the dots yourself.
> 4. Still stuck? → `## Explanation` — one chain, front to back.
> 5. Close it. Rewrite the answers **by hand** in `## Exercise`. *That's* when you know it.

---

## The Change Map

### The Ideas — the spine

**Everything in this doc is a view of these five.** Nothing new appears at test time.

| # | Idea |
|---|---|
| **1** | **Roslyn is a compiler with a public API.** It must build a syntax tree to compile your code; it *exposes* that tree. An analyzer just reads over its shoulder — which is why analysis is nearly free, and why it all happens at **build** time, never at runtime. |
| **2** | **So the analyzers were always running.** The SDK ships them and enables them by default for net5.0+. `AnalysisMode` is a **severity dial, not an installer** — it took rules that were already executing and silently discarding findings, and told them to report. *(368 warnings, zero packages.)* |
| **3** | **A rule matches SYNTAX, not meaning.** It sees the shape `x.ToLower()` and cannot tell that one *executes* in .NET (culture-sensitive → real bug) while the other is a node in an **expression tree** that EF *translates* to SQL (database collation → correct). **Only you can tell them apart** — which is why obeying CA1862 would make EF throw on every product search. |
| **4** | **Suppressions are a LIST, and lists get silently erased.** `<NoWarn>` **assigns**; `$(NoWarn);X` **appends**. Write it bare and every suppression above it vanishes — **with no error, no warning, nothing.** |
| **5** | **A warning has no teeth until Release turns it into an error.** By default a CA violation prints and the build *succeeds*. `TreatWarningsAsErrors`, scoped to Release (what CI builds), is what makes the gate a gate instead of a printer. And its `Condition` depends on `$(Configuration)` — which is **empty**, not `"Debug"`, this early in MSBuild. |

**The one-line version:** *a linter reads shapes; only you know meanings — and everything it gets wrong, it gets wrong **silently**.*

### The files

*Read this before the cold sit. 157 files changed. **Eight of them are the task.***

**LOAD-BEARING — this *is* T54 (8 files):**

| File | Idea | What it does |
|---|---|---|
| `Directory.Build.props` | **2, 5** | `AnalysisMode=Recommended` (the dial) · `EnforceCodeStyleInBuild` · Release-scoped `TreatWarningsAsErrors` (the teeth) |
| `tests/Directory.Build.props` | **4** | `Import` of the parent (MSBuild stops at the first file found) + `$(NoWarn);CA1707` — tests are named `Method_Scenario_Expected` on purpose |
| `.editorconfig` | **5** | `dotnet_diagnostic.IDE1006.severity = warning` — a naming *rule's* own severity is **IDE-only**; the build needs the diagnostic |
| `client/eslint.config.js` | — | Flat config. `prettier` is the **last** element of both `extends` arrays |
| `client/.prettierrc.json` | — | Formatting only. 100 cols, single quotes |
| `ProductSpecification.cs` | **3** | Three `[SuppressMessage]`s **with written justifications** — EF *throws* on `StringComparison` in an expression tree |
| `ProductSpecParams.cs` | **3** | `ToLower()` → **`ToLowerInvariant()`**. A real culture bug: Turkish `"I".ToLower()` is `ı`, so `IPHONE` finds nothing |
| `address.pipe.ts` | — | The bug the linter found in 4 seconds: `?.address!` — optional chain + non-null assertion → `TypeError` |

**MECHANICAL — skim, don't study (149 files):**

- **Prettier reformat** — ~120 files, whitespace only. Zero behaviour change. *(Its own commit, on purpose.)*
- **`eslint --fix`** — auto-applied, mostly unused imports.
- **`ng update` 22.0.0-next.1 → 22.0.6** — forced: `angular-eslint` **cannot install** on a prerelease, because semver sorts `22.0.0-next.1` *below* `22.0.0`. Also stripped 8 `$safeNavigationMigration()` shims it injected (templates diff byte-identical).
- **`API.csproj` / `Infrastructure.csproj`** — one line each, and it's Idea **4** in the wild: a bare `<NoWarn>CS8618</NoWarn>` that had been **silently wiping** every suppression above it.
- **`src/demo.ts`** — deleted, dead file.
- **~40 CA fixes** — `[LoggerMessage]` ×5, static members, `GC.SuppressFinalize`, renamed `InvalidateCacheAttribute`. All mechanical once you accept Idea **2**.

> **The lesson about big diffs:** 157 files sounds unreadable, and it *is* — linearly. But **~95% of a
> large diff is almost always noise** (reformat, codegen, auto-fix). Find the load-bearing files first and
> the task shrinks to something a human can hold. **Nobody reads a diff top to bottom. Not you, not a
> senior, not an interviewer.**

---

## Hints — where to look

*Location anchors only. These give nothing away — they tell you where to connect the dots.*

| Q | Look here | What to notice |
|---|---|---|
| E1 | `client/eslint.config.js` · the `extends:` array | `prettier` is the **last element**. What kind of thing is an `extends` array — and what wins in it? |
| E2 | The T54 diff itself · `git diff development...HEAD -- "*.csproj"` | Count the `PackageReference` lines added. **Zero.** Now ask where 368 warnings came from. |
| E3 | `src/Application/Specifications/ProductSpecification.cs` · the three `[SuppressMessage]` attributes | **Read your own `Justification` strings.** Then compare with `ProductSpecParams.cs`'s `ToLowerInvariant` comment. |
| E4 | `Directory.Build.props` · the `TreatWarningsAsErrors` comment | Two things: *when* is this file read, and *who* assigns `Debug` — and in what order? |
| E5 | `tests/Directory.Build.props` · the `NoWarn` comment | It has the worked two-line example. Then look at what `API.csproj` used to say. |
| B1 | E3's `CA1862` justification | The word "throws" is doing all the work. |
| B2 | `src/API/API.csproj` + `Infrastructure.csproj` | This was a **real live bug** in your repo. Ask: how long had it been there, and what told you? |
| B3 | E4's comment, last paragraph | *"expands to `''` with no error at all."* Now think about what that means for a typo. |
| B4 | `src/Application/Specifications/ProductSpecParams.cs` · the `set` accessor | The comment names the exact character. Trace the search term from the URL to the SQL. |

---

## Explanation

> **Read this as one chain.** Each link sets up the next. That's the whole point — these aren't nine
> facts, they're one story about a single idea: **a linter reads shapes, and only you know meanings.**

### 1. Roslyn is a compiler with a public API

- **Roslyn *is* the C# compiler.** Not a tool bolted onto it — it's the thing that turns your `.cs` files into IL. It has been the C# compiler since ~2015.
- **It runs at BUILD time.** It finishes *before* your app starts. By the time a customer hits `/api/products`, Roslyn and every analyzer are long gone — they cost your users exactly nothing.
- **The "platform" part:** to compile your code, Roslyn *must* build a **syntax tree** (the shape of your code) and a **semantic model** (what the names mean). Roslyn **exposes** both.
- **An analyzer is just a class that subscribes to that.** *"Call me whenever you see a method invocation."* Roslyn hands it the tree it already built.
- **Why it matters:** analysis is nearly free. The analyzer doesn't re-parse your code — **it reads over the compiler's shoulder.**

### 2. …so the analyzers were already running. All of them.

- **The SDK ships them.** *"These analyzers are included with the .NET SDK… If your project targets .NET 5 or later, code analysis is **enabled by default**."*
- **But almost all rules are silent.** *"In the default analysis mode (`Default`), **only a small number of rules are enabled as build warnings**."*
- **So before T54:** ~400 CA rules were loaded and executing on every `dotnet build` you had ever run on this repo — finding those 368 violations, and **throwing them away**, because their severity was `none`/`suggestion`.
- **`AnalysisMode=Recommended` adds nothing.** It raises severities on rules that were already running.
- **Your own proof:** 368 warnings appeared and **zero NuGet packages were installed.** Nothing was downloaded. Nothing arrived. If analyzers had genuinely been *added*, something would have had to be fetched.
- **The picture:** a dashboard with 400 warning lights. Every sensor is wired in and reading. Only 12 bulbs are connected. **`Recommended` connected a few hundred more bulbs. It installed no sensors.**

*(Defaults, for the record: `AnalysisLevel` = `latest`, `AnalysisMode` = `Default`. Modes: `None` · `Default` · `Minimum` · **`Recommended`** · `All`.)*

### 3. But a rule matches *shapes*, not *meanings* — and that's where it turns dangerous

This is the hinge of the whole task.

- **`ToLower()` appears in two files. The analyzer sees the same shape in both.** It fires CA1304/CA1311/CA1862 on each. It cannot tell them apart, because **it is reading syntax, not tracing execution.**
- **`ProductSpecParams.Search` — the code EXECUTES.**
  - It's a plain C# property setter, running in-process on your web server.
  - `String.ToLower()` — *"takes into account the casing rules of **the current culture**."*
  - `CultureInfo.CurrentCulture` comes from **the server's OS locale**.
  - **This is a real bug.** Fixed with `ToLowerInvariant()` — *"produces the same result in every culture… and performs more efficiently."*
- **`ProductSpecification` — the code NEVER EXECUTES.**
  - It sits inside an `Expression<Func<Product, bool>>` — **an expression tree**. That is not code; it's *a data structure describing code*.
  - EF Core walks the tree, sees a `MethodCallExpression` node for `ToLower`, and **translates** it:
    `x.Name.ToLower()` → `LOWER([p].[Name])`
  - No .NET string method is ever called. `CultureInfo` is never consulted.
  - The database lowercases it, using **its own collation** — *"All text operations in a database use a collation… to determine how the operation compares and orders strings."*
- **The two authorities:**

  | | Who decides "lowercase"? | Configured where? |
  |---|---|---|
  | `.ToLower()` in **.NET** | `CultureInfo.CurrentCulture` | the **web server's** OS locale |
  | `LOWER()` in **SQL** | the column/database **collation** | the **database's** setup |

  **The database has no idea what culture your web server is set to. They are separate machines with separate configs.**
- **So the bug is a MISMATCH:** the *needle* gets lowercased by .NET's rules; the *haystack* by SQL's. Let them disagree and the search silently returns nothing.
- **And only you could tell them apart** — which is why the fix was `ToLowerInvariant()` in one file and a **`[SuppressMessage]` with a written `Justification`** in the other. *The suppression isn't laziness; it's the place where human knowledge overrides a machine that can't see execution.*

### 4. Suppressions are a LIST — and lists get silently erased

- Suppressing CA1862 in the spec means writing to `NoWarn`. **`NoWarn` is a semicolon-separated list**, and `<NoWarn>…</NoWarn>` **ASSIGNS** it — it does not append.
- `$(NoWarn)` means *"whatever the list already holds"*, so writing it back keeps the old entries:

  ```
  <NoWarn>$(NoWarn);CA1707</NoWarn>   →  ";CA1707;xUnit1051"   ← both kept
  <NoWarn>CA1707</NoWarn>             →  "CA1707"              ← xUnit1051 silently erased
  ```
- **What warns you when a suppression is dropped this way? Nothing. No error, no diagnostic, no log line.** You just get a wall of unrelated errors somewhere else and no thread back to the cause.
- **This was live in your repo.** `API.csproj` and `Infrastructure.csproj` both had a bare `<NoWarn>CS8618</NoWarn>` — which is *why* CA1848 kept firing despite being "suppressed." The suppression was being wiped by a line that looked completely innocent.

### 5. And none of it bites until Release gives it teeth

- **By default a CA violation is only a *warning*.** The build prints it and **succeeds anyway**. Exit code 0, CI green, merge allowed. **A gate that never blocks anything isn't a gate — it's a printer.**
- `TreatWarningsAsErrors=true` converts warnings to **errors**, and errors fail the build. *That's* enforcement.
- **Why scope it to Release?** Debug = you, mid-edit, wanting to run something half-finished. Release = what ships, and **what CI builds**. So: warnings never block you locally, but they fail the PR. Enforcement lives where it can't be skipped.
- **Now the subtle part — `$(Configuration)` is EMPTY here, not `"Debug"`:**

  ```
  1. global properties        ← "-c Release" lands HERE, before any file is read
  2. Microsoft.Common.props   ← imports YOUR Directory.Build.props ★ you are here
  3. the .csproj body
  4. Microsoft.Common.targets ← Configuration ??= Debug  ← the default is assigned HERE
  ```

  Your file runs at **step 2**. The `Debug` fallback is written at **step 4**. **You are upstream of it.**
  The docs say it outright: *"`Directory.Build.props` is imported **early**… properties defined later are unavailable to it. So, avoid referring to properties that aren't yet defined (and **will evaluate to empty**)."*
- **`-c Release` still works** because a command-line `-c` is a **global property** — set before evaluation even begins, so it wins regardless of import order.
- **And an undefined property expands to `''` — silently.** No error. No warning. Which is exactly why the single quotes are mandatory:

  ```
  Condition="$(Configuration) == 'Release'"     →  " == 'Release'"    ✗ no left-hand side → parse error
  Condition="'$(Configuration)' == 'Release'"   →  "'' == 'Release'"  ✓ false. Teeth stay off in Debug.
  ```

### 6. The frontend half — same idea, different language

- **Two jobs, not one.** **Prettier = how the code *looks*. ESLint = whether the code is *wrong*.** Prettier reprints your file and never has an opinion about correctness; ESLint finds unused variables, unsafe `!`, missing `await`.
- **They overlap, and that overlap is a fight.** ESLint *can* also enforce formatting (quotes, semicolons, indent). If both are on, ESLint says "brace here", Prettier moves it back, forever.
- **`eslint-config-prettier` is a config that contains nothing but rules set to `off`.** From its docs: *"this config **only** turns rules off."*
- **In flat config, `extends` is an array and later entries override earlier ones.** So it must be **last** — the final word. Put it first and `tseslint.configs.stylistic` and `angular.configs.tsRecommended` simply re-enable everything it just switched off.
- **In your repo:** `client/eslint.config.js`, `prettier` is the last element of *both* `extends` arrays (the `**/*.ts` block and the `**/*.html` block). `/flat` is just the entry point for the modern flat-config format.
- **And it earned its keep in four seconds.** On its very first run, ESLint found `address.pipe.ts` doing `(value as ConfirmationToken['shipping'])?.address!` — an optional chain immediately overridden by a non-null assertion. `?.` says *"this might be null"*; `!` says *"trust me, it isn't."* Both in one expression is a contradiction, and it destructures `undefined` → **`TypeError` at runtime**. In a pipe that had just been hand-edited for BUG-11, and reviewed.

---

## BREAK — traced

**B1 · You obey CA1862.**
- **It compiles.** The build is green. Nothing warns you.
- **It fails at RUNTIME**, on the first product search: EF Core *"refrains from translating these overloads to SQL, and attempting to use them **will result in an exception**."* → 500 for every search.
- **Would the 147 tests catch it?** Only if a test executes that query against a real provider. **A unit test on the spec's expression tree would pass** — the tree is well-formed; it's the *translation* that dies. This is the failure mode that gets past a green suite.

**B2 · A bare `<NoWarn>CS8618</NoWarn>`.**
- Every other entry in the list is **erased**. `Directory.Build.props`'s CA1707 exemption is gone.
- **What tells you? Nothing.** That's the entire answer.
- **Why it's hard to trace:** you get 46 errors in *test files* about *naming conventions* — with no connection whatsoever to the one line in a `.csproj` that mentions `CS8618`. The cause and the symptom don't share a word, a file, or a concept.

**B3 · `'$(Configuraton)' == 'Release'`.**
- MSBuild expands the undefined property to `''` — **silently, no error, no warning.**
- So the condition is `'' == 'Release'` → **false. In Debug AND in Release.**
- **`TreatWarningsAsErrors` is never set. Your CI quality gate is off.** Every build stays green. Every PR merges. Warnings pile up unread.
- **What tells you? Nothing.** And this is *worse than a broken build*, because a broken build is loud and gets fixed in ten minutes. **A silently disabled gate looks exactly like a passing one** — and you only discover it months later, when you learn that the thing you trusted to protect you never ran.

**B4 · Turkish-locale server, customer searches `IPHONE`.**
- `ProductSpecParams.Search`'s setter runs on the server → `"IPHONE".ToLower()` → **`"ıphone"`** (U+0131, dotless ı — *not* U+0069 `i`).
- The DB lowercases `Name` with its Latin collation → `"iphone"`.
- `"ıphone"` vs `"iphone"` → **different code points. No match.**
- **The customer sees:** an empty product list. **Your error log shows:** nothing. HTTP 200. No exception, no stack trace, no alert. Just silently zero sales on your best-selling product.
- **Why you'd never reproduce it:** your machine is `en-*`. `"IPHONE".ToLower()` → `"iphone"`. **It works perfectly on every dev box and fails only in the one place you can't attach a debugger.**

---

## Why this approach

- **Strict, not gradual.** A lint gate you can ignore trains you to ignore it. 368 → 0 in one PR; every PR after it starts from zero.
- **T54 first in Sprint A, not last.** A gate added *after* T28/29/30/31 — the sprint's three biggest Angular PRs — never sees them.
- **`Directory.Build.props`, not per-`.csproj`.** One place. A new project inherits the rules by existing.
- **Release-scoped teeth.** Enforce where it can't be skipped (CI); don't block yourself mid-edit.
- **CA1848 was FIXED, not suppressed** — MS docs say *"Do not suppress a warning from this rule."* Five call sites moved to source-generated `[LoggerMessage]`. **No solution-wide `NoWarn` remains.**
- **Models stay `type`, not `interface`** (`consistent-type-definitions: off`): `interface` allows **declaration merging** — two `interface Product`s silently fuse. For DTOs mirroring a backend contract that's a footgun, and BUG-13 was caused by exactly this class of type-lie.

---

## What confused me

| Gap | Where | Tier |
|---|---|---|
| Roslyn runs at **build**, not at app start | `Directory.Build.props` · the whole premise | **OWN** |
| Analyzers were **already running**; `AnalysisMode` is a dial, not an installer | the diff · zero packages | **OWN** |
| Expression tree ≠ code — EF **translates**, .NET **executes** | `ProductSpecification.cs` vs `ProductSpecParams.cs` | **OWN** |
| A rule matches **syntax**; only you know **semantics** | the three `[SuppressMessage]` justifications | **OWN** |
| Silent failures: `NoWarn` wipe, `$(Configuraton)` typo — **nothing tells you** | `API.csproj` · `Directory.Build.props` | **OWN** |
| `eslint.config.js` — flat config, `extends` array, `prettier` last | `client/eslint.config.js` | **RECOGNIZE** |
| MSBuild import order (`Common.props` step 2, `Common.targets` step 4) | `Directory.Build.props` comment | **RECOGNIZE** |
| `CA` = code quality · `IDE` = code style | — | **RECOGNIZE** |
| `AnalysisMode` values (`None`/`Default`/`Minimum`/`Recommended`/`All`) | — | **LET GO** |
| `eslint-config-prettier/flat` vs the default entry point | — | **LET GO** |

**Honest self-assessment from the cold test:** EXPLAIN 4 (Release-scoped teeth) and 5 (`NoWarn`) came out clean. EXPLAIN 2's Roslyn timing was **backwards** on the first pass. EXPLAIN 3 stopped one bullet short — the "why only you can tell them apart" bullet, which is the *whole point*. `eslint.config.js` was never opened. **All four BREAKs went BLANK.**

---

## Exercise

*Close every other file. Write these by hand.*

1. In one sentence each, without looking: what is Roslyn, when does it run, and where do analyzers plug in?
2. You add `AnalysisMode=All` to a fresh project and get 900 warnings. **Explain, without using the word "add", where those 900 came from.**
3. Write the `Directory.Build.props` `TreatWarningsAsErrors` line from blank — including the `Condition` — and then explain what `$(Configuration)` evaluates to in that file when you run plain `dotnet build`.
4. Write both `NoWarn` forms. Say what each produces, and what tells you when the wrong one is used.
5. A colleague's PR "fixes a linter warning" by adding `StringComparison.Ordinal` to a `.Where()` inside an EF spec. The build is green. **Write the PR review comment.**

## Solution

1. → `## Explanation` §1. Compiler with a public API · **build time** · analyzers subscribe to the syntax tree it already builds.
2. → §2. They were already running and already finding them. `AnalysisMode` raised their severity from silent to warning. Zero packages installed.
3. → §5. `<TreatWarningsAsErrors Condition="'$(Configuration)' == 'Release'">true</TreatWarningsAsErrors>` · `$(Configuration)` is **empty**, because the `Debug` default is assigned in `Microsoft.Common.targets` (step 4), *after* this file is imported by `Microsoft.Common.props` (step 2).
4. → §4. `$(NoWarn);CA1707` appends; bare assigns and erases. **Nothing tells you.**
5. → B1. *"This compiles and the build is green, but EF Core throws on `StringComparison` overloads inside an expression tree — it can't translate them. This will 500 on every request that hits this query. The analyzer is right about .NET code and wrong about expression trees; suppress it with a `Justification` instead."*

## Tests

- No new unit tests — T54 is a build-gate task. **The gate is its own test**, and it was proven rather than assumed:
  - Injected an unused variable into a `.ts` file → **ESLint rejected it.**
  - Injected a CA violation into a `.cs` file → **`dotnet build -c Release` failed with `error CA1822`.**
  - Demonstrated the `NoWarn` trap live: bare form → CA1707 erased → **46 errors, Build FAILED.**
- Regression suite: **147 tests green.** Lint, format, and Release build all clean.

---

## Interview Q&A

**Q: How do you enforce code quality on a .NET + Angular repo?**
Two languages, two toolchains, one gate. Backend: Roslyn analyzers ship in the SDK and already run on every build — I set `AnalysisMode=Recommended` in a solution-wide `Directory.Build.props` and gave it teeth with `TreatWarningsAsErrors` **scoped to Release**, which is what CI builds. Frontend: ESLint for correctness, Prettier for formatting, with `eslint-config-prettier` loaded last so they don't fight. Both wired into CI, so a warning fails the PR, not the developer mid-edit.

**Q: A linter tells you to use `StringComparison.OrdinalIgnoreCase`. Do you?**
Depends where. In .NET code, yes — culture-sensitive `ToLower()` is a real globalization bug. **Inside an EF expression tree, no — EF throws on those overloads**, so obeying the analyzer would 500 every request. The rule matches syntax; it can't see that one call executes and the other gets translated to SQL. That's a suppression *with a written justification*, not a fix.

**Q: What's the most dangerous kind of build configuration bug?**
The silent one. A typo'd MSBuild condition expands to an empty string with no error at all — so your quality gate switches off while every build stays green. A broken build is loud and gets fixed in ten minutes; **a disabled gate looks exactly like a passing one** and you find out months later.

**Q: Did the linter actually find anything?**
Three real bugs review had missed: a `?.x!` optional-chain-plus-non-null-assertion that `TypeError`d at runtime, a culture-sensitive `ToLower()` that would silently break product search on a Turkish-locale server, and a `<NoWarn>` that *assigned* instead of appending — silently wiping every suppression above it. **And it tried to create one:** CA1862 inside an EF expression tree.

---

## Sources

- [Code analysis in .NET](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) — analyzers ship in the SDK and are **enabled by default** for net5.0+; in `Default` mode *"only a small number of rules are enabled as build warnings"*; `CA` vs `IDE` prefixes; `EnforceCodeStyleInBuild` needs **both** the property **and** a warning/error severity in `.editorconfig`.
- [Customize the build by folder (MSBuild)](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory) — `Directory.Build.props` is imported **early** in `Microsoft.Common.props`; *"properties defined later are unavailable to it… will evaluate to empty"*; MSBuild **stops at the first** file found walking up (hence the `Import` in `tests/`).
- [Collations and case sensitivity (EF Core)](https://learn.microsoft.com/en-us/ef/core/miscellaneous/collations-and-case-sensitivity) — *"EF Core refrains from translating these overloads to SQL, and attempting to use them **will result in an exception**"*; all DB text ops use a **collation**; `i`/`I` are different letters in Turkish.
- [String.ToLower](https://learn.microsoft.com/en-us/dotnet/api/system.string.tolower) — *"takes into account the casing rules of the current culture"*; use `ToLowerInvariant` — *"produces the same result in every culture… and performs more efficiently."*
- [eslint-config-prettier](https://github.com/prettier/eslint-config-prettier) — *"this config **only** turns rules off"*; must be **last** so its disabling wins over earlier configs.
- [CA1848: Use the LoggerMessage delegates](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1848) — *"Do not suppress a warning from this rule."* (Hence `[LoggerMessage]` at five call sites, not a `NoWarn`.)

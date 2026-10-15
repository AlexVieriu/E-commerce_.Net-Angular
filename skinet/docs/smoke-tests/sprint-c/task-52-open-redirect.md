# Task 52 — open redirect: the returnUrl guard

`?returnUrl=` is attacker-controlled and gets handed to the router after a successful login.
`isLocalUrl` (`login.component.ts:61`) now rejects anything that is not a path on this origin.
This file proves that in a **real browser**, where Chrome parses the URL — the unit tests hand the
value to a fake `ActivatedRoute` instead, so they never exercise the browser's own parsing.

Stripe and Redis are not involved — the API (`dotnet run --project src/API/API.csproj`) and the client
(`npx ng serve` from `skinet/client/`), nothing else.

⚠️ Without the API the app does not boot at all. `initializeApp` (`app.config.ts:18`) rejects,
`main.ts:5` logs an `HttpErrorResponse`, and no component is ever constructed. A blank page with
`ERR_CONNECTION_REFUSED` means the API is down — not that the guard failed.

## What to look at

Loading the page **never navigates**. The constructor sets `returnUrl` and stops; nothing reads that
field until the form is submitted. So there are two things to read, and neither needs a login:

| What you want | How |
|---|---|
| Was the value rejected? | the console — `Rejected non-local returnUrl: …` from `login.component.ts:28` |
| What did the field become? | paste `ng.getComponent(document.querySelector('app-login')).returnUrl` |

`ng` is a global Angular exposes in development builds only; `getComponent(element)` returns the same
object the specs reach through `.componentInstance`.

⚠️ Open DevTools **before** you reload. The warning fires during construction, at page load — open the
console afterwards and you have missed it.

## The runs

Base: `https://localhost:4200/account/login`

| # | Query | Console | `returnUrl` | Rejected by |
|---|---|---|---|---|
| 1 | *(none)* | clean | `/shop` | not rejected — never assigned, keeps its initial value |
| 2 | `?returnUrl=/checkout` | clean | `/checkout` | not rejected — passes every check |
| 3 | `?returnUrl=/` | clean | `/` | not rejected — `url.length === 1` |
| 4 | `?returnUrl=https://evil.com` | Rejected | `/shop` | `url[0] !== '/'` |
| 5 | `?returnUrl=//evil.com` | Rejected | `/shop` | `url[1] === '/'` |
| 6 | `?returnUrl=/\evil.com` | Rejected | `/shop` | `url[1] === '\\'` |
| 7 | `?returnUrl=javascript:alert(1)` | Rejected | `/shop` | `url[0] !== '/'` |
| 8 | `?returnUrl=shop` | Rejected | `/shop` | `url[0] !== '/'` — a relative path still leaves the intended target |
| 9 | `?returnUrl=/checkout&returnUrl=https://evil.com` | Rejected | `/shop` | `typeof url !== 'string'` — a repeated key arrives as an **array** |

Row 9 is the one worth pausing on: it never reaches a single string comparison. Angular turns two keys
of the same name into an array, so the type check at the top of the guard stops it.

## The two runs that need a login

Everything above stops at the field. Only a successful login reaches
`navigateByUrl(this.returnUrl)` (`login.component.ts:43`).

| Run | Expect |
|---|---|
| `?returnUrl=/checkout` → sign in | lands on **/checkout** — the feature still works |
| `?returnUrl=https://evil.com` → sign in | lands on **/shop**, same origin — the guard holds end to end |

Two logins is the whole manual budget. Every other row is those same two code paths with a different
string, and CI re-runs all 14 specs on every push.

## What no manual check covers

- **A control character.** `/<tab>/evil.com` cannot be typed into the address bar — Chrome strips tab,
  CR and LF while parsing, before Angular sees anything. That stripping is exactly *why* the guard
  rejects control characters: a string can pass a check and then become a different string. Pinned by
  `login.component.spec.ts`, not by any browser run.
- **A value holding its own query string.** The spec passes `/orders?page=2` as one value. In a real
  URL that second `?` belongs to the outer query string, so the component receives `/orders` and
  `page=2` as a separate key.

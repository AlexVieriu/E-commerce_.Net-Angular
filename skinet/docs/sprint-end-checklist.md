# Sprint End Checklist

Run before every PR `development` → `main`. See `git-workflow.md` for the merge step.

## Core smoke — every sprint

- [ ] App starts, no console errors, seed completes
- [ ] Full user flow: register → login → add to cart → checkout → Stripe payment
- [ ] Admin flow: login → view orders → refund
- [ ] Logout → cookie cleared from DevTools

## This sprint's behaviors

> Swap this section to the tasks just completed. Past sprints aren't re-checked here —
> their logic is locked by the automated test suite (and by one-time config verified at their close).

### Sprint 2 — Testing + Data Integrity

- [ ] **Price change (T17):** reach the **Payment** step first, *then* edit that product's `Price` in `data/store.db` (DB Browser, not the Docker container) → **Confirmation** → **Pay** → expect **409 + snackbar + redirect to cart, no charge**. (Editing *before* the Payment step is silently re-synced — no 409.)
- [ ] **Idempotency (T16/T18):** locked by `OrderService_Tests` (same `PaymentIntentId` → returns existing order, no duplicate) → confirm `dotnet test` green. (No clean manual check — a declined→retry in the same session hits a Stripe `elements.submit()` retry limit, unrelated to idempotency.)
- [ ] **Concurrency (T13) + rollback (T14):** can't be triggered by hand (it's a race) → just confirm `dotnet test` is green.

## Azure — after merging to `main` (deploy)

- [ ] GitHub Actions all checks green
- [ ] Live URL loads, no 500 errors
- [ ] Smoke test: register → login → cart → checkout → Stripe payment
- [ ] Admin login + order visible

## Done

Both local and Azure green → Sprint is closed.

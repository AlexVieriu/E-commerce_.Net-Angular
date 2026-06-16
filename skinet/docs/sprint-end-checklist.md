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

- [ ] **Price change (T17):** edit a product's price in the DB while it's in your cart → checkout → expect **409 + snackbar + redirect to cart**
- [ ] **Idempotency (T16/T18):** resubmit the same order/payment → **no duplicate order** created
- [ ] **Concurrency (T13) + transaction rollback (T14):** can't be reliably triggered by hand → confirm the **automated suite is green** (these exist precisely because manual testing can't catch races)

## Azure — after merging to `main` (deploy)

- [ ] GitHub Actions all checks green
- [ ] Live URL loads, no 500 errors
- [ ] Smoke test: register → login → cart → checkout → Stripe payment
- [ ] Admin login + order visible

## Done

Both local and Azure green → Sprint is closed.

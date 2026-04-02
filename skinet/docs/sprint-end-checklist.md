# Sprint End Checklist

Run before every PR `development` → `main`.

## Local

- [ ] App starts, no console errors, seed completes
- [ ] Test each completed task's specific behavior:
  - Lockout → check DB (`LockoutEnd` set after 5 failures)
  - Cookie → DevTools: `HttpOnly`, `Secure`, `SameSite`
  - Password policy → try registering with weak password, verify rejection
  - New endpoints → hit them in browser or Postman
- [ ] Full user flow: register → login → add to cart → checkout → Stripe payment
- [ ] Admin flow: login → view orders → refund
- [ ] Logout → cookie cleared from DevTools

## Azure

- [ ] PR `development` → `main` merged
- [ ] GitHub Actions all checks green
- [ ] Live URL loads, no 500 errors
- [ ] Repeat smoke test: register → login → cart → checkout → Stripe payment
- [ ] Admin login + order visible

## Done

Both local and Azure green → Sprint is closed.

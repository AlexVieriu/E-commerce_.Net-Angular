# Interview Prep — Skinet

One-liner per concept. Click the link for full explanation + Q&A.

---

## Sprint 1

- [Task 1 — GitHub Actions CI/CD](sprint-1/task-1-github-actions.md) — 3 jobs: CI on PRs, deploy on merge to main via OIDC; fixed 12 pre-existing test failures in the process
- [Task 3 — Config providers & secrets](sprint-1/task-3-config-providers.md) — User Secrets (dev) + Azure Key Vault (prod) via AddAzureKeyVault + DefaultAzureCredential; last provider wins
- [Task 4 — Admin seed password → env var / Key Vault](sprint-1/task-4-admin-seed-password.md) — User Secrets (dev) + Key Vault (prod) via config provider chain; last provider wins
- [Tasks 5-6-7 — Identity password policy, lockout, cookie hardening](sprint-1/task-5-6-7-identity-cookie-security.md) — IdentityOptions for password/lockout + ConfigureApplicationCookie for HttpOnly/Secure/SameSite=Strict/24h absolute
- **[Sprint 1 Practice Exam](sprint-1/sprint-1-practice.md)** — 30 questions: recall + scenario + practical + senior stretch. Manual testing checklist included.

## Sprint 2

- [Task 9 — CORS explicit + CSP security headers](sprint-2/task-9-cors-csp-security-headers.md) — named CorsPolicy with origin whitelist + NetEscapades for CSP/X-Frame/X-Content-Type/Referrer-Policy; unsafe-inline tradeoff for Angular Material
- [BUG-1 — Angular signals + reference equality](sprint-2/bug-1-signal-reference-equality.md) — mutating a signal object returns same reference → no re-render; fix with spread operator to create new object
- [Task 13 — Optimistic concurrency](sprint-2/task-13-optimistic-concurrency.md) — [ConcurrencyCheck] adds original value to WHERE on UPDATE; DbUpdateConcurrencyException when another write beat you to it
- [Task 14 — Transaction isolation levels](sprint-2/task-14-transaction-isolation.md) — BeginTransactionAsync wraps order creation; explicit rollback on failure; isolation levels control what concurrent reads see
- **[Sprint 2 Practice Exam](sprint-2/sprint-2-practice.md)** — 30 questions: recall + scenario + practical + senior stretch. Will expand when sprint completes.

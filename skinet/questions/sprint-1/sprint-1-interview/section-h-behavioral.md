## SECTION H — BEHAVIORAL & PROBLEM-SOLVING

**H1.** A senior developer insists on using JWT stored in localStorage for authentication. You believe cookies with HttpOnly are more secure. How do you make your case without being combative? What are the legitimate arguments for JWT that you should acknowledge?

<details><summary>Answer</summary>

- **Acknowledge JWT benefits** — stateless, no server-side session, works across origins, easy for mobile, self-contained claims.
- **Present the concern** — localStorage is accessible to any JS on the page; one XSS and the token is stolen.
- **Propose a middle ground** — store the JWT in an `HttpOnly; Secure; SameSite` cookie: stateless + claims, without the localStorage XSS surface.
- **Frame it diplomatically** — "both work, but cookies give us defense-in-depth." Back it with OWASP, not personal opinion.
</details>

---

**H2.** Your PR has 47 files changed. The reviewer says "This is too large to review." How do you handle this? How do you prevent it next time?

<details><summary>Answer</summary>

**Handle** (don't be defensive):
- Offer a walkthrough call to guide the reviewer through the changes.
- Split into stacked PRs (e.g. entities/migrations first → service layer → API endpoints).
- Add a detailed PR description with a file-by-file summary of intent.

**Prevent next time**:
- Break work into vertical slices (one feature end-to-end per PR).
- Use feature flags to merge incomplete features safely.
- Commit and PR daily — rule of thumb: under 400 lines changed, 10–15 files max.
</details>

---

**H3.** You find a bug in production that affects 0.1% of orders. The fix is simple but the root cause is unclear. Do you deploy the fix immediately or investigate the root cause first? Justify your answer.

<details><summary>Answer</summary>

Deploy the fix NOW. 0.1% sounds small but at scale it's real money and trust.

Steps:
1. Deploy the fix with extra logging around the affected code path.
2. Monitor to confirm it resolves the issue.
3. Open a follow-up ticket for root cause investigation with the new logs.
4. Time-box the investigation (don't let it drag).

The fix and the investigation are parallel tracks, not sequential. Document the decision: "Deployed fix at [time], root cause TBD, tracking in [ticket]."
</details>

---

**Total: ~75 questions across 8 sections**
**Distribution: ~15 Easy (20%) | ~30 Medium (40%) | ~30 Hard (40%)**

---

> "The difference between a mid-level developer and a senior is not what they can build — it's what they can explain, defend, and debug under pressure."

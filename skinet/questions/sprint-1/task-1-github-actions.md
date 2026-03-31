# Task 1 — GitHub Actions CI/CD

## Flow

```
feature/* → PR → development   → CI: test-backend + test-frontend (parallel, no deploy)
development → PR → main        → CI: same
merge to main                  → deploy to Azure skinet-2025-test
```

---

## Key decisions

**Why 3 jobs?**
CI jobs run in parallel on PRs — faster feedback. Deploy has `needs: [test-backend, test-frontend]`
so it's blocked if either fails. Deploy is skipped entirely on PRs via `if: github.event_name == 'push'`.

**Why `ubuntu-latest` not `windows-latest`?**
Runner OS doesn't need to match Azure App Service OS. Ubuntu is faster and cheaper.

**Why solution level for dotnet, but API level for publish?**
`restore/build/test` at `./skinet` picks up all 4 test projects via the `.sln`.
`publish` targets only `./skinet/src/API` — we don't deploy tests.

**Why `npm ci` not `npm install`?**
`npm ci` installs exactly what's in `package-lock.json`. `npm install` can silently
update packages → non-reproducible builds across machines.

**Why no global Angular CLI?**
`npm run build` calls `ng build` from local `node_modules`. No version mismatch risk.

**Why Node 24.x?**
Angular 22 requires `>=24.13.1`. Node 24.x satisfies that.

**Why .NET needs `dotnet-quality: 'preview'`?**
.NET 11 is still in preview. Without this flag the action can't find the SDK.

**Why OIDC for Azure login instead of a password?**
Azure and GitHub trust each other via a managed identity + federated credential.
No password stored anywhere. Short-lived token per run.

---

## Questions I had

**Q: If I put `push: [main, development]`, does it deploy on both?**
Yes — that's why triggers are split. `pull_request` for CI, `push: [main]` for deploy only.

**Q: Why ugly secret names from Azure?**
Azure auto-generates unique suffixes when setting up via Deployment Center to avoid
collisions. We renamed them to clean names since we have one app.

**Q: What is the managed identity setup?**
Azure creates a managed identity linked to the App Service. GitHub proves its identity
with a short-lived token. Azure checks: right repo + right branch → allow deploy.

---

## Interview Q&A

**Q: Walk me through your CI/CD pipeline.**
One workflow, 3 jobs. PRs trigger CI — backend tests and Angular build run in parallel.
Deploy only fires on merge to `main` after both pass, using OIDC via managed identity.

**Q: Why separate CI and CD triggers?**
`pull_request` is a gate on the proposed change before merge. `push` fires after merge.
With only `push`, broken code is already in `main` before you find out.

**Q: What is `npm ci`?**
Installs exactly what's in `package-lock.json`, fails if out of sync. `npm install`
can silently update packages making builds non-reproducible.

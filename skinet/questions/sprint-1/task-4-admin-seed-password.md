# Task 4 — Admin Seed Password → Env Var / Key Vault

## Plan

- `SeedAsync` receives `adminPassword` as a parameter (from `IConfiguration`)
- `Program.cs` reads `AdminSettings:Password` from config and passes it in
- Dev: store in User Secrets
- Prod: store in Key Vault as `AdminSettings--Password`
- Update `build-deployment.md` secrets table

## Todo

- [ ] Update `SeedAsync` signature to accept `adminPassword`
- [ ] Update `Program.cs` to read `AdminSettings:Password` from config and pass it
- [ ] Add `AdminSettings:Password` to User Secrets (dev)
- [ ] Add `AdminSettings--Password` to Key Vault (prod) + update `build-deployment.md`

---

## Why this approach

## What confused me

## Explanation

## Exercise

## Solution

## Interview Q&A

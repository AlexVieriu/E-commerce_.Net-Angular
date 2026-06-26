# Task 24 — Bogus Seed (Dev-only demo data generator)

## Why this approach

A realistic dev dataset (50+ products, 10+ users, 20+ orders) lets you eyeball the admin
dashboard with real volume instead of 3 baseline products. Bogus generates typed fake data
that satisfies all FK constraints and DB rules. One-shot CLI flag keeps clean as the default;
bulk data is always opt-in.

## What confused me — actual gaps from the cold test

| Gap | Location | Tier |
|---|---|---|
| Didn't know `Randomizer.Seed` is static — must be set before any `Faker` created | `BogusSeeder.cs · SeedAsync` | **OWN** |
| Confused `runSeed` flag (CLI switch) with the idempotency guard (DB check) | `BogusSeeder.cs · SeedAsync` + `Program.cs` | **OWN** |
| Didn't see effect of multiple unpinned fakers — different data every run | `BogusSeeder.cs · SeedAsync` | **OWN** |
| Dev-only gating = safety boundary, not just "not needed" | `WebApplicationExtensions.cs · SeedDatabaseAsync` | **OWN** |
| `AnyAsync()` guard is coarse — doesn't confirm exact seeded count | `BogusSeeder.cs · SeedAsync` | **RECOGNIZE** |
| `new Random(24_2026)` — underscore is digit separator, number is arbitrary | `BogusSeeder.cs · SeedAsync` | **LET GO** |

## Explanation

### Idempotent vs Deterministic — two separate guarantees

**Idempotent** — re-running `--seed` is safe. Mechanism: a live DB query.
```csharp
if (await context.Orders.AnyAsync()) return;
```
Second `--seed` sees orders exist → bails immediately → no duplicate inserts.
`runSeed` is `true` every time the flag is passed — it has no memory of previous runs.
The DB check is what provides the memory.

**Deterministic** — same data every fresh run. Mechanism: pinned RNG.
```csharp
Randomizer.Seed = new Random(24_2026);  // must be set BEFORE any Faker object
var f = new Faker();
```
`Randomizer.Seed` is static — one global tape shared by all `Faker` and `Faker<T>` objects.
Same seed → same sequence → same product names, emails, prices every reseed.

```csharp
// Unpinned — different data every run
var f1 = new Faker(); // random start
var f2 = new Faker(); // different random start
// Run 1: ["Angular Cool Board", ...] Run 2: ["Redis Slim Hat", ...] — different

// Pinned — identical data every run
Randomizer.Seed = new Random(242026);
var f1 = new Faker(); // position 0
var f2 = new Faker(); // continues where f1 left off
// Run 1: ["Angular Cool Board", ...] Run 2: ["Angular Cool Board", ...] — identical
```

### Dev-only gating — safety boundary, not tidiness

Two checks, not one:
```csharp
if (runBulkSeed)
{
    if (app.Environment.IsDevelopment())   // ← the real guard
        await BogusSeeder.SeedAsync(...);
    else
        WriteLine("--seed ignored: bulk demo data only seeds in Development.");
}
```
1. **Fake data is actively harmful in prod** — fake orders corrupt real revenue reports, real customer lists, real dashboards. Not harmless noise.
2. **Flag alone isn't trusted** — if `--seed` leaked into a prod deploy, `runBulkSeed = true` but `IsDevelopment() = false` → bulk insert refused. The environment check is the safety net.

### FK-safe ordering
Products → users → orders. Orders need product `Id`s (set after `SaveChangesAsync`) and customer emails. Save products first, then users, then build orders referencing both.

### Unique `PaymentIntentId`
```csharp
PaymentIntentId = $"pi_seed_{i:D6}"  // pi_seed_000000, pi_seed_000001, ...
```
Loop index `i` guarantees uniqueness. Satisfies the unique index from T18. Without this → `DbUpdateException` on the second order that shares a `PaymentIntentId`.

## Exercise

Without looking at the code, write `BogusSeeder.SeedAsync` from scratch:
- The idempotency guard
- Where and how to pin `Randomizer.Seed`
- The FK-safe call order (products → customers → orders)
- How to make `PaymentIntentId` unique

## Solution

```csharp
public static async Task SeedAsync(StoreContext context, UserManager<AppUser> userManager)
{
    if (await context.Orders.AnyAsync()) return;

    Randomizer.Seed = new Random(24_2026);
    var f = new Faker();

    var products = await SeedProductsAsync(context);
    var customerEmails = await SeedCustomersAsync(userManager, f);
    await SeedOrdersAsync(context, f, products, customerEmails);
}
```

Key line in `SeedOrdersAsync`:
```csharp
PaymentIntentId = $"pi_seed_{i:D6}"
```

## Tests

No unit tests for the seeder (it's a dev tool, not domain logic). Verified manually:
- `dotnet run -- --seed` → counts: 70 products / 13 users / 25 orders
- Second `--seed` → no-op (idempotency guard fires)
- Plain `dotnet run` → baseline only, no bulk data
- Redis flushed between runs to avoid stale cache false positives

## Interview Q&A

**What is idempotent seeding?**
- Running the seed operation multiple times produces the same result as running it once
- Guard: `if (await context.Orders.AnyAsync()) return;` — bails if already seeded

**What is deterministic seeding?**
- Same data generated every fresh run
- Achieved by pinning `Randomizer.Seed` to a fixed value before creating any `Faker` object
- `Randomizer.Seed` is static — shared by all fakers in the process

**Why is `Randomizer.Seed` set before `new Faker()`?**
- It's a static property — affects all fakers created after it's set
- Set it first or the initial fakers draw from an unpinned sequence

**Why gate bulk seed to Development only?**
- Fake data in prod corrupts real business data (revenue, customers, orders)
- Two-check gate: `runBulkSeed` AND `IsDevelopment()` — flag alone isn't trusted

**Why save products before seeding orders?**
- `SaveChangesAsync` populates EF-generated `Id`s — orders need those `Id`s for FK references
- Without the save, `ProductId` on `OrderItem` is still 0

**How do you guarantee unique `PaymentIntentId`s in generated orders?**
- `$"pi_seed_{i:D6}"` — loop index makes each one unique
- Satisfies the unique DB index added in T18; collision → `DbUpdateException`

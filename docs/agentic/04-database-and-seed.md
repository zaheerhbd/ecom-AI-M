# 04 – Database Schema Change and Seed Data

All changes are in the **`skinet`** database (`StoreContext`). The identity database is not touched.

## 1. Before you start (Phase 2)

1. Take a fresh backup into a **permanent** folder (the earlier backup is in a temporary scratchpad):
   ```powershell
   docker exec ecom-ai-m-db-1 pg_dump -U appuser -d skinet > C:\Backups\skinet-before-phase2.sql
   ```
2. Note the current counts: 18 products, 6 brands, 4 types, 4 delivery methods, 0 orders.

## 2. Product changes

| Column | Type | Null? | Notes |
|---|---|---|---|
| `Color` | `varchar(30)` | yes | e.g. Black, Red, Navy. Used by filters. |
| `Material` | `varchar(40)` | yes | e.g. Wool, Leather, Gore-Tex, Maple. |
| `StockQuantity` | `integer` | no | **No model default** (`HasDefaultValue` would make EF skip a real 0 and save the default). The migration adds the column with SQL default 0 and then runs `UPDATE "Products" SET "StockQuantity" = 25` for existing rows. |
| `Rating` | `numeric(2,1)` | no | 0.0–5.0. Existing rows get 4.0 in the migration. |

Entity (`Core/Entities/Product.cs`) gets four plain properties. `ProductConfiguration` sets the lengths and precision. `ProductToReturnDto` and `MappingProfiles` add the same fields so the shop can show them later.

## 3. New tables

EF maps the Agent project's record classes (plain classes, no attributes) through configs in `Infrastructure/Data/Config/`. Lists and nested data are stored as **`jsonb`**, so a run can be read back whole without many joins.

### `AgentRuns`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` PK | Created by the runner (also returned to the UI) |
| `CreatedAt` | `timestamptz` | Indexed (runs list is newest first) |
| `SessionId` | `varchar(64)` | Conversation id |
| `UserEmail` | `varchar(256)` null | From the JWT. Null when anonymous |
| `Channel` | `varchar(20)` | `chat` or `eval` |
| `Question` | `text` | After PII redaction |
| `Answer` | `text` | |
| `Blocked` | `boolean` | |
| `BlockedBy` | `varchar(50)` null | Guardrail name |
| `StopReason` | `varchar(30)` null | `max-steps` or `token-budget` when a safety limit stopped the run (added in Phase 1) |
| `Error` | `varchar(200)` null | Exception type only, never a stack trace |
| `Model` | `varchar(50)` | |
| `InputTokens` / `OutputTokens` | `integer` | |
| `EstimatedCostUsd` | `numeric(10,6)` | |
| `DurationMs` | `double precision` | |
| `ToolsUsed` | `jsonb` | `["search_products"]` |
| `Steps` | `jsonb` | List of `AgentStep` |
| `GuardrailChecks` | `jsonb` | List of `GuardrailCheck` |
| `Sources` | `jsonb` | List of `{ type, id, title }` |
| `EvalRunId` | `uuid` null | Set when the run came from an eval |

### `EvalRuns`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` PK | |
| `StartedAt` / `FinishedAt` | `timestamptz` (finish null) | |
| `Status` | `varchar(20)` | Running, Completed, Failed |
| `Error` | `varchar(200)` null | Exception type when the run failed |
| `UseJudge` | `boolean` | |
| `Repeat` | `integer` | Default repeats per case |
| `TotalCases` / `CompletedCases` / `PassedCases` / `FlakyCases` | `integer` | Progress for polling |
| `TotalAttempts` / `PassedAttempts` | `integer` | Cases × repeats |
| `PassRate` | `double precision` | PassedAttempts / TotalAttempts (what the baseline gate compares) |
| `CorrectToolRate` | `double precision` null | Share of attempts that called the expected tools |
| `MedianLatencyMs` / `P95LatencyMs` / `MedianTokens` | `double precision` | |
| `TotalCostUsd` | `numeric(10,6)` | |
| `Model` | `varchar(50)` | |

*Columns match `Agent.Evals.EvalRunRecord`, which was built in Phase 1.*

### `EvalCaseResults`

| Column | Type | Notes |
|---|---|---|
| `Id` | `serial` PK | |
| `EvalRunId` | `uuid` FK → EvalRuns (cascade delete) | Indexed |
| `CaseId` | `varchar(50)` | e.g. `TC-004` |
| `Category` | `varchar(50)` | |
| `Input` | `text` | |
| `Passed` | `boolean` | True only if every repeat passed |
| `Flaky` | `boolean` | Some repeats passed, some failed |
| `RepeatCount` / `PassCount` | `integer` | Shown as "4/5" |
| `Checks` | `jsonb` | `[{ name, passed, detail }]` of the shown attempt (first failure, else the last) |
| `Answer` | `text` | Answer of the shown attempt |
| `JudgeScore` | `integer` null | 1–5 |
| `AgentRunId` | `uuid` null | Link to the shown attempt's full trace in AgentRuns |
| `DurationMs` / `Tokens` | `double precision` | Medians over the repeats |

*Columns match `Agent.Evals.EvalCaseResultRecord`, which was built in Phase 1.*

### Migration

```powershell
dotnet ef migrations add AgentAndProductAttributes -p Infrastructure -s API -c StoreContext -o Data/Migrations
```

`dotnet ef` 8.0.28 is older than EF 10 and may print a warning. If the migration fails, update the tool: `dotnet tool update -g dotnet-ef --version 10.*`.

## 4. Seed data

### Brands (6 → about 20)

The 6 existing brands stay with the same ids. About 14 new outdoor/winter brand names are added, for example: Summit Peak, Frostline, Alpine Forge, Northridge, Glacier Co, Powder Lab, Ridgeback, Ice Fox, Timberwolf, Snowcraft, Crestline, Blizzard Works, Polar Path, Avalanche Gear. These are **invented** names, not real companies.

### Types (unchanged)

Boards (1), Hats (2), Boots (3), Gloves (4). Images exist only for these.

### Products (18 → about 1,000)

Generated by `tools/seed-gen/generate.js` (Node; run with `node tools/seed-gen/generate.js`). It uses a **fixed random seed**, so the output is the same every time.

| Field | Rule |
|---|---|
| Name | `{Brand} {Adjective} {Model word} {Type noun}`, e.g. "Frostline Storm Trail Glove". Unique. |
| Type split | About 250 per type |
| Price | Range by type: Boards $120–$650, Boots $60–$320, Gloves $12–$110, Hats $9–$60. Ends in .99 or .00 |
| Color | From a list of 12 |
| Material | From a list per type (Boards: Maple, Bamboo, Carbon; Boots: Leather, Synthetic, Gore-Tex; Gloves: Wool, Leather, Fleece, Gore-Tex; Hats: Wool, Cotton, Fleece, Acrylic) |
| StockQuantity | 0–120, with about 8% at 0 (so "in stock" filters matter) |
| Rating | 2.5–5.0, skewed high |
| PictureUrl | An existing image picked at random from the same type prefix (`sb-`, `hat-`, `boot-`, `glove-`). **Owner's decision (D8): reuse the 18 current images for now.** Later option: 48 new images (one per type × colour, e.g. `glove-navy.png`), so the picture matches the `Color` field. The generator should keep the image choice in one function, so this is a one-line change later. |
| Description | **Template** by default, built from type, material, colour and use, e.g. "Warm wool gloves in navy, with a touch-screen thumb and a fleece lining. Good for city winters and light hiking." |

**LLM descriptions (optional, needs owner approval):** a separate script sends products in batches of 25 and saves the result into `products.json`. It runs **once**, and then the JSON is committed, so seeding never calls the API. Rough size: about 1,000 × 60 words, which is about 100k output tokens plus about 60k input tokens. Before running it, work out the price from the current OpenAI price list and get the owner's OK.

### Policy documents

Markdown files in `Infrastructure/Data/SeedData/policies/`. Each has a front-matter title and short sections, so they can be split into chunks.

| File | Covers |
|---|---|
| `returns.md` | 30-day window, condition, how to start a return, refund timing, damaged items |
| `warranty.md` | Warranty length by type, what it covers and what it doesn't |
| `payments.md` | Cards via Stripe, no cash on delivery, no crypto |
| `international-shipping.md` | Which countries, duties and taxes, delivery times |
| `discounts.md` | Seasonal sales, no student discount (yet), one code per order |
| `support.md` | Contact hours, email, online only (no physical stores) |

These answers deliberately cover the "Borderline" and "Unsupported" eval cases (TC-012 to TC-021). The agent then answers from a policy, or clearly says the shop doesn't offer that, instead of guessing.

## 5. Idempotent seeding

`StoreContextSeed` today only seeds a table when it is empty. The new rules are:

1. **Brands / types:** for each item in the JSON, add it if no row has that `Name`.
2. **Products:** load all existing product names into a `HashSet` once, then add every JSON product whose name is not in the set. Brand and type are resolved **by name** (the JSON carries `BrandName` / `TypeName`), so ids don't need to line up.
3. **Delivery methods:** unchanged (only when the table is empty).
4. Save in batches of 200.
5. Never update or delete existing rows. The original 18 products stay exactly as they are.

**Check:** running `dotnet run --migrate` twice gives the same product count both times.

## 6. Azure AI Search

Today the laptop and the live site use **the same indexes**: `ecom.search.windows.net` → `products-index` and `rag-index`, the same in `appsettings.json` and `appsettings.Development.json`. A sync from the laptop changes what the live site's chat returns.

**Owner's decision (D7, 2026-11-15):** load the new products into the **live (cloud) database first**, then fill the live index **from the live database**. The index then holds the same products and the same ids as the live site. See §7.

Rules until then:
- **Do not** run `--sync-products` or `--sync-rag` from the laptop against the shared indexes.
- During development, `search_knowledge` falls back to the local policy files.
- **No separate dev indexes (owner's decision).** The laptop **never** runs the sync commands; the only sync is the production one in §7.

## 7. Production rollout (cloud database, then index)

The live site runs on Azure Container Apps with Azure PostgreSQL (`ecom-ai-pg-zahee01`); see `docs/container-app-github-deploy.md`. `Program.cs` already has the switches needed:

| Env var | Does at startup |
|---|---|
| `RUN_MIGRATIONS=true` | Applies EF migrations to both databases and runs the (now idempotent) seed |
| `INIT_SEARCH_INDEX=true` | Creates the index schema if it doesn't exist |
| `SYNC_PRODUCTS_TO_AZURE_SEARCH=true` | Uploads products from **this app's database** to `products-index` |
| `SYNC_RAG_TO_AZURE_SEARCH=true` | Embeds and uploads products, delivery methods and policies to `rag-index` |

All of them are `false` in production today. Each one is switched on for **one start only** and then switched off again.

### Runbook (owner approves each step; nothing here runs without being asked)

| # | Step | Check |
|---|---|---|
| 1 | **Back up the cloud database.** Note the time for Azure point-in-time restore, and also take a `pg_dump` of `skinet` with the production connection string, saved to a permanent folder. | Dump file exists and is not empty |
| 2 | **Record the current state:** product, brand and order counts in the cloud `skinet`; document count in both indexes. | Numbers written into the phase log |
| 3 | **Deploy the new image.** Merging to `main` triggers `deploy-containerapp.yml`. The image carries the new migration, `products.json`, `brands.json` and the policy files. | New revision is running |
| 4 | **Migrate + seed once.** Set the Container App to **1 replica**, so two copies don't migrate at the same time. Set `RUN_MIGRATIONS=true`, restart and read the logs. Then set it back to `false`. | ~1,000 products; the original 18 unchanged; existing orders and users untouched; running it again adds nothing |
| 5 | **Update the index schema.** The product index gets new fields (Color, Material, StockQuantity, Rating). Azure Search can **add** fields to an existing index but not change them. If a field must change, delete and recreate the index; the live chat returns nothing for the few minutes this takes. Run `INIT_SEARCH_INDEX=true` once. | Index has the new fields |
| 6 | **Fill the indexes from the live database.** `SYNC_PRODUCTS_TO_AZURE_SEARCH=true` and `SYNC_RAG_TO_AZURE_SEARCH=true` for one start, then back to `false`. The ids now match the live database. Embedding cost: about 1,000 short documents with `text-embedding-3-small`, well under $0.01. | Index document counts ≈ product count; a test question on the live `/ai-lab` and agent chat returns new products that open correctly |
| 7 | **Smoke test** the live site: product list and paging, a product page, basket, checkout page, old AI chat, new agent chat. | All work |

**Rollback:** restore the database from step 1 (point-in-time restore or the dump), then run step 6 again so the index matches the restored data. The old image can be re-activated from the Container App's revision list.

**Why ids are safe:** each environment's index is filled from **its own** database. The local and live databases may give the same product different ids, and that's fine, because neither index is ever filled from the other database.

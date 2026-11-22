# Docs/24 — Pricing Policy Redesign: Group / Plan / Calculator Decision Record

> Status: **implemented** (schema, backend, frontend, tests).
> Source: `IDEAS.md` («سیاستهای قیمت‌گذاری») as critiqued in the plan
> `1795351165291-ideas-pricing-policy-critique.md`; this file records the ten
> open decisions (D1–D10) the critique required before implementation.
> Persian summary: جداسازی «محصول» (Plan) از «تراکنش» (Renewal)، معرفی گروه کاربری به‌جای `CalculationMethod`، قرارداد محاسبه‌گر strongly-typed با context، فریز خروجی محاسبه‌گر در فاکتور، و حذف `AllowMonthly`/`Price`.

---

## 1. Problem

Before this record the "product" did not exist as an entity. Its definition was
scattered across three places: hardcoded frontend arrays
(`renewal.component.ts`), domain formulas (`RenewalBusiness` derived days from
traffic, multiples of 25/30) and per-user Roslyn calculators keyed by
`PanelUser.CalculationMethod → Price.CalculatorCode`. `RateLimitInMeg` was never
set from the UI and never pushed to x-ui; `SimultaneousUser` could not express
"unlimited"; there was no admin API for prices at all; and the calculator pool
had an unsynchronized generation swap.

## 2. Decisions

| # | Decision |
|---|---|
| D1 | **Price is merged into Plan.** The `Price` table/entity is dropped. `Plan` carries the marketing fields (`Title`, `Caption`, `Description`) plus `ShowOnWebsite`; the public home page lists **active + ShowOnWebsite plans of the default group only** (private-group plans are never disclosed — no title/price leakage). |
| D2 | **Closed calculator contract with a mandatory shared DTO.** The compiled code must expose `public static PriceCalculationOutput Compute(PriceCalculationInput input)` in a class `Calculator`. Every axis is always present in the typed output; `null` = unlimited and a *plan-defined* axis in the output is ignored (the user's validated choice stands). There is no "absent field" state by construction. |
| D3 | **Current account state comes from the local `TotalPlanState` view** (fed by the sync jobs), never from a live realm call inside the estimate/pricing path — estimate stays a local-DB read with no new latency/failure mode. |
| D4 | **Calculator reload**: admin Plan CRUD goes through the repository so the existing `OnSave` entity event keeps the pool fresh, **plus** an explicit admin endpoint `POST /api/plan/reload-calculators` for out-of-band SQL/script edits (direct SQL raises no event). |
| D5 | **Stacking on unlimited axes: null dominates.** Expiry: a limited renewal stacking on an unlimited-current account keeps the axis unlimited; traffic: unlimited current ⇒ stacked quota stays unlimited; both limited ⇒ existing rules (`laterOf(now, expiry) + days`, `max(0, remaining) + share`). The estimate/purchase flow surfaces a warning when a purchased axis would be shadowed by an existing unlimited axis, so the buyer can back out. |
| D6 | **Group assignment rules**: self-registration → default «وب سایت» group; created by a manager → the manager's *current* group; created by an admin without owner → default group; `set-owner` to a manager → that manager's group propagates; changing a manager's group never re-groups existing members (snapshot semantics — divergence is visible in the admin user list). |
| D7 | **Admin on-behalf exception to Docs/20 D4** (recorded here as a new decision): when the acting **admin** renews/buys for another user, the invoice, wallet debit and pricing group all belong to the **target user**. A manager acting for a subordinate keeps payer=actor (manager's wallet, manager's group). The admin purchase may overdraw the target wallet (money-need check skipped — the admin is the gifting authority; the wallet-threshold rule stays the antidote). Admin gifting remains `POST /api/wallet/adjust`. |
| D8 | **Realm display name**: `Realm.DisplayName NVARCHAR(50) NULL` added; user-facing surfaces show `DisplayName ?? Name`. The internal `Name` stays the technical identity. |
| D9 | **The calculator output is frozen at invoice issuance**: `Invoice.Action` carries the final per-axis values (user choices for plan-defined axes, calculator values for undefined axes) as `t/o/p/u/d/g/b/r` tokens; settlement rebuilds the renewal from the action and never re-runs the calculator. `null` axes are encoded by *omitting* the token (unlimited). Worst-case length stays far below `NVARCHAR(400)`. |
| D10 | **RateLimit on x-ui realms is rejected, not silently dropped**: a plan whose axis `RateLimitInMeg` is defined may not be attached to a realm that has (or later gains) an x-ui server — validated at plan save **and** at purchase/realm-assignment time. The existing x-ui API contract has no per-client bandwidth limit; reopening this requires a new x-ui contract. Mikrotik UserManager keeps enforcing `limit-speed-{rate}M`. |

Additional resolved points (from the critique §3):

- `UserTypes.AllowMonthly` is deleted; `OldUser` and `AllowSelfService` stay.
- `Renewal.SimultaneousUser` becomes **nullable** (`NULL` = unlimited); x-ui maps
  `limitIp = SimultaneousUser ?? 0` (0 = unlimited), UserManager maps
  `SharedUsers = SimultaneousUser ?? 0`.
- `PricePool` generation swap is now atomic (`Interlocked.Exchange` +
  `Volatile.Read`) — the opportunistic concurrency bug is fixed.
- `Renewal` stays append-only (no state machine on it; the "record state" of
  IDEAS.md is the frozen selection), and gains `PlanId` (NOT NULL FK) and
  `ActorId` (nullable FK → `PanelUser`, audit of who acted vs. who paid).

## 3. Data model (fresh-install set, `Database/LocalDatabase/`)

- **`UserGroup`** (new): `Id`, `Title NVARCHAR(50) UNIQUE`, `IsDefault BIT`
  (filtered unique index ⇒ at most one default), `Created`. Seeded with the
  default group «وب سایت» (`IsDefault = 1`) — required because `PanelUser.UserGroupId` is NOT NULL.
- **`Plans`** (new, replaces `Price`; plural because `PLAN` is a reserved T-SQL keyword): `Name NVARCHAR(50)` (internal),
  `Title/Caption/Description` (marketing, from Price), `ShowOnWebsite BIT`,
  `IsActive BIT`, `GroupId INT NOT NULL FK → UserGroup`, four optional axes as
  base|cap pairs — `UsersBase/UsersCap TINYINT`, `DaysBase/DaysCap SMALLINT`,
  `TrafficBase/TrafficCap INT` (GB), `RateBase/RateCap TINYINT` (Mbps) — and
  `CalculatorCode VARCHAR(511) NOT NULL`. CHECK constraints: at least one axis
  defined; base ≥ 1; cap ≥ base; both-or-neither per axis.
- **`PlanRealm`** (new): M2M junction (`PlanId`, `RealmId`) composite PK.
- **`PanelUser`**: `CalculationMethod` dropped; `UserGroupId INT NOT NULL FK → UserGroup` added.
- **`Realm`**: `DisplayName NVARCHAR(50) NULL` added.
- **`Renewal`**: `SimultaneousUser` now nullable; `PlanId INT NOT NULL FK → Plan`
  and `ActorId INT NULL FK → PanelUser` added.
- **`Price.sql` deleted** (table + entity + repository + `PriceStates`).
- Views `TotalPlanState`/`PlanState` are unchanged in shape (they already expose
  the four axes); `IRenewalEntity.SimultaneousUser` becomes `byte?`.

## 4. Calculator contract (`PhotonBypass.Domain.Static`)

```csharp
public sealed class PriceCalculationInput  // group + plan context + current state
{
    int PlanId; string PlanName; string GroupTitle;
    byte? Users; short? Days; int? Gigabytes; int? RateLimitMeg;   // user choices (plan-defined axes only)
    PlanAxis UsersAxis; PlanAxis DaysAxis; PlanAxis TrafficAxis; PlanAxis RateAxis; // base|cap echo
    CurrentAccountState? Current;   // from TotalPlanState (D3); null on first purchase
}

public sealed class PriceCalculationOutput  // price + calculator-determined axes
{
    int Price;
    byte? Users; short? Days; int? Gigabytes; int? RateLimitMeg;   // null = unlimited (D2)
}
```

Compilation still references all loaded host assemblies, so the dynamic code
uses the shared DTO types directly; the pool stores a strongly-typed
`Func<PriceCalculationInput, PriceCalculationOutput>` per plan id. The plan
seed calculator in the test scenario demonstrates the contract.

## 5. API surface (deltas)

| Endpoint | Change |
|---|---|
| `GET /api/basics/prices` | now serves the **default group's** active + `ShowOnWebsite` plans (marketing fields) |
| `GET /api/plan/catalog` (new, auth) | purchasable plans for the acting group (admin on-behalf: `?target=` user's group) with axes and realm choices |
| `POST /api/plan/estimate` / `renewal` / `new-account` | body gains `planId`; per-axis choices are optional (only plan-defined axes) |
| `POST /api/plan/list` / `save` / `delete` (new, admin) | plan CRUD (repo path ⇒ pool auto-refresh, D4) |
| `GET /api/plan/group/list` · `POST /api/plan/group/save` (new, admin) | group CRUD |
| `POST /api/plan/reload-calculators` (new, admin) | forced pool reload (D4) |
| `GET /api/user/account-report` (new, manager/admin) | flat VPN-account report over `TotalPlanState` with server-side paging, whitelisted sort (NULLS-last via `ISNULL`), and search |

## 6. Rules made explicit

- **Choice validation** (`RenewalBusiness.ValidateChoices`): for each plan-defined
  axis the request must carry a value that is an integer multiple of the axis
  base and within `[base, cap]`; undefined axes must be absent. This replaces
  the 25/30-multiple rules and the days-derivation formula (deleted).
- **At least one limit**: enforced on the plan (CHECK + save validation) and
  re-checked on the composed renewal (a renewal with all four axes unlimited is
  refused).
- **Group used for pricing** = acting user's group, except admin-on-behalf ⇒
  target user's group (D7). This also fixes the old inconsistency where
  `Estimate` priced with the account owner's method while the purchase used the
  payer's.
- **Stale × plan realms**: a stale account whose current realm is not in the
  chosen plan's realm set must re-pick (same `RequiresRealmChoice` UX);
  selectable realms = capacity-filtered ∩ plan realm set (D4 of Docs/22 kept).

## 7. Traceability

- Critique D1–D10 ↔ this file §2.
- Implementation phases: schema (`Database/LocalDatabase/`) → domain/infra
  (entities, repos, `PriceCalculator`/`PricePool`) → application
  (`PlanApplication`, `RenewalBusiness`, `BasicsApplication`,
  `UserManagementApplication`) → portal controllers → frontend
  (renewal/home/admin) → tests (`DbScenario/init.sql`, calculator tests,
  `RenewalBusinessTest` rewrite).

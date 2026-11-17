# Docs/22 — Panel-User / VPN-Account Split Decision Record

> Status: **implemented** (schema, backend, frontend, tests).
> Source plan: `Docs/plans/panel-user-vpn-account-split.md` (plan folder removed; see git history).
> Persian summary: تفکیک موجودیت `Account` به `PanelUser` (ورود پنل، کیف پول) و `VpnAccount` (هویت VPN روی یک realm) — یک کاربر پنل می‌تواند چند اکانت VPN داشته باشد.

---

## 1. Problem

The old single `Account` mixed two lifecycles: portal login/wallet and the VPN
identity (username, realm, credentials, warning stamps). That coupling forced

- the 7-day inactivity rule to lock the *whole* account (portal included),
- `Renewal.RestrictedRealmId` to carry the realm truth as a *snapshot* (bucket-0
  fan-out for accounts whose snapshot was NULL),
- one VPN identity per portal user, and
- registration to provision VPN credentials before any plan existed.

## 2. Decisions

| # | Decision |
|---|---|
| D1 | `Account` splits into `PanelUser` (portal: login, contact, wallet, Role/UserType, CalculationMethod) and `VpnAccount` (VPN: Username, VpnPassword, RealmId, per-account status/stamps). FK: `VpnAccount.UserId → PanelUser.Id`. |
| D2 | A panel user owns **N ≥ 0** VPN accounts; no cap (abuse handled by wallet/payment friction). Portal views list them; the switcher selects one. |
| D3 | VPN username = owner panel username + optional suffix (`[a-zA-Z0-9_-]*`), total ≤ 32 (`AccountBusiness.VpnUsernameMaxLength`), globally unique (`UK_VpnAccount_Username`). System suggestion: bare username, then `+2, +3…` (`SuggestVpnUsernameSequence`); user-editable; the unique index closes the suggest/submit TOCTOU (409 `UserException`). An empty suffix (VPN username == panel username) is the first-account default. Cross-user prefix overlap (`al` claiming `ali1`) is accepted as inherent to prefix naming. |
| D4 | Realm is chosen **explicitly at purchase** from `GET /api/plan/realms` (name + `IsSelectable`, T4-E capacity). Auto-assignment (`GetAvailableRealm`) is removed from the purchase path. |
| D5 | `Renewal` stacks **in place**: same username, same realm (realm truth = `VpnAccount.RealmId`, NOT NULL). `Renewal.RestrictedRealmId` deleted. |
| D6 | Renewing a **stale** account (recorded last activity older than `ManagementOptions.RealmReassignInactivityDays`, default 7) requires an explicit realm re-pick; settlement migrates via `IAccountRadiusSyncService.MigrateUserRealm` (deactivate old realm → pin new → re-provision). No recorded activity is **not** stale — the abandonment sweep owns never-used accounts. |
| D7 | History anchors to `PanelUser` (`Target`/`Issuer` = user ids) with optional `VpnAccountId` for account-scoped events. |
| D8 | Wallet/Invoice belong to the **panel user** (Docs/20 payer=actor); plan invoices additionally carry nullable `Invoice.VpnAccountId` (null on topups and on new-account invoices that resolve only at settlement). |
| D9 | A VPN account is created **only together with its first renewal** (`PlanApplication.PurchaseNewAccount`, one transaction incl. radius sync). Registration creates a bare `PanelUser` (no VPN credentials). |
| D10 | Price calculation (`CalculationMethod`) stays on the panel user — realm-independent. |
| D11 | Telegram link, reset-pass codes anchor to `PanelUser`; warning channels (email/telegram) resolve through the owner. |
| D12 | Two status axes: `PanelUserStatus {Active=0, Deactivated=2}` (portal login) and `VpnAccountStatus {Active=0, Suspended=1, Deactivated=2}` (per account). VPN suspension never blocks the portal; the 40-day abandonment deactivates the **VpnAccount** only — the user can log in and buy a new account. `set-status` = panel level, `set-account-status` = VPN level. |
| D13 | Implementation note: phases 2–4 of the plan executed as one atomic compile unit (entity split makes intermediate greens impossible); SQL (phase 1) stayed independently valid. |
| D14 | `target` API contract split (review finding): user-level endpoints keep `target` = username; VPN-level endpoints take a separate `accountId` query/body param resolved through `IAccessService.CheckAccountAccess`. The access cache (`TargetArea|{username}`) now also carries `AccountIds` and **rebuilds from the repository on miss** — a freshly created account is targetable without invalidation. |

## 3. Data model (fresh-install set, `Database/LocalDatabase/`)

- `PanelUser.sql` — table + filtered unique indexes (Email/Mobile/TelegramChatId, NULLs excluded).
- `VpnAccount.sql` — `UserId` FK (ON DELETE CASCADE), `RealmId` FK NOT NULL, `Username` unique (≤ 32).
- FK rewrites: `Renewal.VpnAccountId`, `TrafficData.VpnAccountId`, `History.{Issuer,Target}→PanelUser + VpnAccountId`, `Wallet.AccountId→PanelUser`, `Invoice.AccountId→PanelUser + VpnAccountId`, `TelegramLink/ResetPassword.AccountId→PanelUser`.
- Views: `TotalPlanState`/`PlanState` keyed by `VpnAccount` — result `Id` = VPN account id, `RealmId`/`UserId`/`Status` join in from the account (never NULL), stacking windows partition by `VpnAccountId`.
- Indexes: `IX_TrafficData_VpnAccountId_StartSession`, `IX_Renewal_VpnAccountId`, `IX_PanelUser_Owner`, `IX_VpnAccount_UserId`, `IX_VpnAccount_RealmId`, `IX_History_VpnAccountId` (filtered) + existing set.

## 4. Purchase flows (backend)

- `POST /api/plan/new-account {username?, realmId, users, days, gigabytes}` → invoice with action `n|{user}t|{count}u|{days}d|{gig}g|{realm}r`.
- `POST /api/plan/renewal {accountId, users, days, gigabytes, realmId?}` → invoice; `realmId` mandatory when `PlanInfo.RequiresRealmChoice`.
- Settlement (`SettlePurchase`) parses the action: `n|` → create VpnAccount + first renewal (D9); else renew the named VPN account; stale + realm token → `MigrateUserRealm`. Compensation on rollback unchanged (radius deactivation).
- `GET /api/plan/realms`, `GET /api/plan/suggest-username` feed the purchase UI.
- Bucket-0 is gone from `DeactivateInvalidRadiusUsers` (plain `RealmId` grouping), `LoadRealmDemand` and `CountActiveClientsByRealm`.

## 5. Frontend

- `UserService` gains `accounts` / `accountId` / `setAccount` (`onAccountChanged`, persisted in localStorage) — the header **account switcher**; per-account views (dashboard, traffic, config, connections) key off it.
- Renewal page: mode switch (renew / new account), realm select (mandatory when stale or new), optional username with D3 suggestion.
- `/users`: rows nest VPN accounts (username, realm, status, plan summary) with per-account suspend/activate.
- User-level surfaces (history, wallet, payment) stay username-targeted.

## 6. Open points

1. Username length limits of RadiusDesk/FreeRADIUS, UserManager and x-ui client emails verified against the 32-char cap at implementation time (MySQL schema default 64+; UserManager accepts long usernames) — re-verify on the real panels before release.
2. Capacity presentation beyond name + selectable flag (load percentages per realm in the picker).
3. Per-account pricing overrides (D10 keeps pricing user-level; revisit if multi-account discount abuse shows up).

## 7. Traceability

- Plan: `Docs/plans/panel-user-vpn-account-split.md` (removed; git history) (§ decisions, phases).
- Review fixes folded in: phase-merging (D13), suffix TOCTOU + empty-suffix rule (D3), 40-day semantics (D12), admin create-user path (D9), `UpdateTrafficData`/`CheckUserServerBalance` consumers, filtered unique indexes on PanelUser, derived-on-miss access cache (D14), separate `accountId` param (D14).
- Tests: `Docs/18-test-coverage.md` §4 rows tagged `Docs/22`.

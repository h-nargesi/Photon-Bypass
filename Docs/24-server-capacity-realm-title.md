# Docs/24 — Byte-Based Server Capacity & Customer-Facing Realm Title Decision Record

> Status: **implemented** (schema, backend, frontend, tests).
> Source plan: local planning session (the plan file lives in the untracked `.kilo/plans/` folder and is not committed).
> Persian summary: تغییرنام `Server.BandWidth` به `Server.CapacityBytes` (واحد بایت؛ فقط فرم ادمین گیگابایت می‌گیرد و تبدیل می‌کند) + افزودن `Realm.Title` به‌عنوان متن نمایشی سرور برای مشتری؛ `Realm.Name` شناسه‌ی سیستمی می‌ماند.

---

## 1. Problem

Two naming gaps surfaced while the first real servers were being registered:

1. **Capacity was named like a bit-rate.** `Server.BandWidth` held the 30-day
   sellable traffic in **bytes** (the "GB" of the purchase UI is 1024³ bytes).
   The admin form showed the raw byte integer next to a «پهنای باند» label, so
   the natural operator input (`200`, meaning 200 GB) stored 200 **bytes** and
   the realm was closed with no visible cause. The unit was stated nowhere.
2. **Customers saw the system identifier.** The realm dropdown
   («سرور (realm):»), the header account switcher and the OpenVPN
   `FRIENDLY_NAME` rendered `Realm.Name` — a ≤ 15-character identifier that was
   never written for customers.

## 2. Decisions

| # | Decision |
|---|---|
| D1 | `Server.BandWidth` is renamed to **`Server.CapacityBytes`** (`BIGINT NOT NULL`). The meaning is unchanged: the 30-day sellable traffic, in bytes. Realm capacity = Σ `CapacityBytes` over the realm's active NAS servers; `Realm.CapacityOverrideBytes` still replaces it. Fresh-install schema only — nothing has shipped, so there is no migration. |
| D2 | **Bytes are the only unit** on the wire, in the database and in the backend validation. The backend never converts. |
| D3 | **Only the admin form converts.** The input is in binary gigabytes (1024³ — the same "GB" the purchase UI sells). It is pre-filled with the exact `bytes / 1024³` (no rounding, so saving an unrelated edit cannot change the stored bytes) and sent back as `Math.round(gb × 1024³)` (exact below 2⁵³). The list column prints a formatted value (B/KB/MB/GB/TB) and never the raw integer. |
| D4 | **Upper bound: 1 PiB per server** (`ServerCrudApplication.MaxServerCapacityBytes`). Since the form takes GB, a unit mistake must not pass: `< 1` → «ظرفیت ترافیک سرور نامعتبر است!», `> 1 PiB` → «ظرفیت ترافیک سرور بیش از حد بزرگ است!». |
| D5 | **`Realm.Title NVARCHAR(50) NULL`** is the customer-facing text. `Realm.Name` stays the ≤ 15-char system identifier. `RealmEntity.DisplayTitle` = trimmed `Title`, else `Name` — an untitled realm behaves exactly as before. |
| D6 | **Only `DisplayTitle` reaches customers**: `RealmChoiceModel.Title` (`GET /api/plan/realms`) and `UserVpnAccountModel.RealmTitle` / `VpnAccountSummaryModel.RealmTitle` (login answer, `GET /api/account/get-user`, `GET /api/vpn/accounts`). `Name` stays on the wire next to it for diagnostics. Admin views keep `Name` (§4). |
| D7 | **The OpenVPN `setenv FRIENDLY_NAME` takes `DisplayTitle`** ("All" when there is no realm) and is sanitized **where the config text is written** (`ServerManagementService.SetDomain`): `Title` is free text, so quotes, backslashes and control characters are dropped — a quote or backslash would end the string early, a control character would start a new directive. |

## 3. Implementation notes

- **Fresh-install schema.** The column rename and `Title` live inside the
  `CREATE TABLE` scripts; every test database is rebuilt from
  `Database/LocalDatabase/*.sql`. A pre-existing dev `FastBypass` database must
  be dropped and recreated — the `IF OBJECT_ID(...) IS NULL` guards skip an
  existing table and would leave the old column in place.
- **Raw-SQL `Server.Config` JSON keys are case-sensitive.** `ServerEntity.Config`
  parses with default `System.Text.Json` options, so keys must be PascalCase
  (`SshConfig`, `WebApiConfig`, `Ssl`, `HostName`, …). A camelCase key is
  silently ignored and the default stays (e.g. `"ssl": true` still sends plain
  requests to the API-SSL port). camelCase only works through the admin API,
  whose model binding is case-insensitive ([Docs/09](09-database.md)).
- **One realm lookup.** The three identical private `LoadRealmNames` copies
  (`AuthApplication`, `AccountApplication`, `VpnApplication`) became a single
  extension, `IRealmRepository.LoadRealms(ids)` (an empty id set answers with an
  empty dictionary instead of querying). `IRealmRepository.GetName` had one
  production caller left and was removed with it.
- **Admin views are deliberately unchanged**: `ServerRowModel.Realm`,
  `UserAccountOverviewModel`/`UserAccountRowModel.RealmName`, the capacity
  alarms and `RealmCapacityModel.Name` all keep `Realm.Name`.

## 4. Display matrix

| Surface | Shows | Since |
|---|---|---|
| Renewal realm dropdown (first purchase, stale re-pick) | `RealmChoice.Title` | D6 — label «سرور:» (was «سرور (realm):») |
| Header account switcher | `UserVpnAccount.RealmTitle` | D6 |
| OpenVPN `setenv FRIENDLY_NAME` | `Realm.DisplayTitle`, sanitized | D7 |
| Admin servers list / form | `CapacityBytes` — formatted in the list, GB in the form | D3 |
| Admin server list, column `Realm` | `Realm.Name` | unchanged (system identifier) |
| Admin users list (`(realm)` next to each account) | `Realm.Name` | unchanged |
| Capacity alarms, logs | `Realm.Name` | unchanged |
| `RealmCapacityModel` | `Name` + `Title` (feeds `GetRealms`) | D6 |

## 5. Open items found while registering the first servers

None of these is blocking the rename; each needs a decision before the related
server goes live in the panel.

1. **Loopback-only panels.** The panel's x-ui hosts expose the panel on
   loopback only (sshd is the only public surface). `XuiApiService` speaks plain
   HTTP(S) to `WebApiConfig.HostName`, and `SshHandler` is password-only
   (`SshConfig.Port/Username/Password`) with no key authentication or tunnel —
   such a panel is unreachable as-is. Decision needed: an SSH local
   port-forward per server (key authentication) versus a private channel (mTLS
   or a source-allowlisted listener).
2. **Link builder gaps.** `VlessLinkService` accepts `tcp/ws/grpc` +
   `none/tls/reality` only and reads the REALITY link fields from fixed flat
   keys (`realitySettings.serverName/fingerprint/publicKey/shortId`). The
   deployed user inbound is XHTTP + REALITY with `publicKey`/`fingerprint` under
   `realitySettings.settings`, the server name in `xhttpSettings.host`,
   `shortIds` as a list and the link address in `externalProxy` — a link for
   that shape is refused or mis-built until the builder learns it. OpenVPN
   remains gated (`SetOVpnCertificate` is `NotImplementedException`).
3. **User Manager credential import.** The OpenVPN server's User Manager users
   exist only on the router. Importing them into the panel needs a policy on
   passwords (they are visible only through `print show-sensitive`; storing or
   rotating them is a decision) and on the quota/expiry mapping.
4. **x-ui first poll.** The first x-ui poll with `LastSeenCounter` NULL charges
   the whole panel counter (`XuiTrafficAccounting.Compute`). Imported customers
   need a per-account counter baseline before the collector sees them.
5. **Sweep safety on import.** `DeactivateInvalidRadiusUsers` runs in the hourly
   `JobInterval` once at least one plan row exists and disables every enabled,
   pattern-matching client of the managed inbound that is not an Active panel
   account — on every server registered as an active Radius. A server must not
   be activated in the panel before its customers exist in the panel's DB.
   Helper/probe clients whose email matches the panel username pattern are
   swept the same way: import them as panel users or keep them out of the
   managed inbound.
6. **Mikrotik OVPN certificate names.** `LMTCA` and `CLIENT-TEMPLATE` are
   hardcoded in `MikrotikDirectService`; another CA or template on a new OVPN
   server needs them configurable.
7. **Realm editor.** `Realm.Title` has no admin UI yet — today it is set with
   raw SQL (`update Realm set Title = N'…' where Id = …`).
8. **Raw-SQL `Config` is case-sensitive and silently lossy.** `ServerEntity.Config`
   parses with default `System.Text.Json` options: a camelCase key (`"ssl": true`)
   is ignored without an error and the default stays — plain requests to the
   API-SSL port. Until the parser rejects unknown keys (or accepts camelCase),
   raw-SQL seeds must use PascalCase keys (§3).

## 6. Traceability

- Plan: local planning session (untracked `.kilo/plans/` file).
- Tests: [Docs/18-test-coverage.md](18-test-coverage.md) §4 table «Docs/24», plus the updated capacity, OVPN-certificate, server-CRUD and frontend rows.
- Living docs updated in the same change: [Docs/00](00-index.md), [Docs/03](03-domain.md), [Docs/08](08-portal-api.md), [Docs/09](09-database.md), [Docs/11](11-business-flows.md) §2/§4/§6, [Docs/22](22-panel-user-vpn-split.md) D4, `AGENTS.md`. Historical records (Docs/16, 17, 20) keep their wording; the old column name is stated once, in D1.

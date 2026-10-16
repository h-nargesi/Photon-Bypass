# docs/18-test-coverage.md — حاکمیت پوشش تست: سنجش پوشش کد + نقشه‌ی traceability

> **English summary**: Test-coverage governance document. This file **replaces the deleted `Backend/Tests/TestPlan.md`** — the old method-by-method checklist drifted five phases behind reality and was deliberately dropped. Coverage is now tracked two complementary ways: **code coverage** measured by tooling (coverlet + reportgenerator for the backend, karma-coverage for the frontend; report-only, **no hard threshold** — baseline first, threshold is a separate later decision), and **requirement coverage** via a ~40-row flow/decision traceability matrix mapping docs/11 flows and docs/14–17 decisions to real test classes. Includes the measured per-assembly baseline (line + branch, environment noted), an honest known-gaps list, the out-of-scope register, and the governance rule: **any PR that adds/changes tests or introduces a flow/decision updates its docs/18 row in the same PR.**

> **فارسی**: سند حاکمیت پوشش تست. این فایل **جایگزین `Backend/Tests/TestPlan.md` حذف‌شده** است — چک‌لیست متد-به-متد عمداً کنار گذاشته شد چون اثباتاً drift می‌کند. پوشش از این پس با دو مکانیزم مکمل دیده می‌شود: **پوشش کد** با ابزار (فقط گزارش، بدون آستانه‌ی سخت) و **پوشش نیازمندی** با نقشه‌ی traceability در سطح جریان/تصمیم.

## ۱. هدف و دامنه

- **پوشش کد**: ابزار سنجش — `coverlet.collector` (از قبل در `Backend/Tests/Tests.csproj`) و `karma-coverage` (از قبل در `Frontend/karma.conf.js:30-34`). فقط گزارش‌گیری؛ طبق تصمیم مالک (D2 طرح) آستانه‌ی سخت «بعد از پایدار شدن مبنا» در تصمیم جدا تعیین می‌شود.
- **پوشش نیازمندی**: نقشه‌ی traceability سبک در §۴ (~۴۰ ردیف، سطح جریان/تصمیم — نه endpoint-به-endpoint).
- این سند **جایگزین TestPlan.md** است (حذف با `git rm`؛ تاریخچه در git می‌ماند). چک‌لیست متد-به-متد عمداً کنار گذاشته شد چون ۵ فاز از واقعیت کد عقب افتاد و نگهداری‌اش گران بود. ارجاع تاریخی `docs/14-defects-audit.md:205` به TestPlan عمداً دست‌نخورده مانده (گزارش تاریخی است).

## ۲. نحوه‌ی سنجش

### بک‌اند (coverlet + reportgenerator)

`reportgenerator` به‌صورت local tool با نسخه‌ی پین‌شده در `.config/dotnet-tools.json` (5.4.1) ثبت شده است.

```bash
rm -rf Backend/Tests/TestResults
dotnet test Backend/Tests/Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --results-directory:Backend/Tests/TestResults
dotnet tool restore
dotnet reportgenerator \
  -reports:"Backend/Tests/TestResults/*/coverage.cobertura.xml" \
  -targetdir:"Backend/Tests/TestResults/coverage-report" \
  "-reporttypes:TextSummary;Html"
```

نکات حیاتی:

- **scope = `Tests.csproj` نه کل solution** — سولوشن شامل `tik4net.tests` وندور است (`Photon-Bypass.sln:32-36`)؛ اجرای کل سولوشن هم آلودگی پوشش کد وندور را می‌آورد هم ریسک پروژه‌ی non-SDK کلاسیک دارد (این پروژه‌ی وندور روی این ماشین اصلاً build نمی‌شود — خطای pre-existing و بی‌ربط، ثبت‌شده در docs/16 D1). کل پوشش پروژه‌ی ما در همین یک test-assembly است و build کل پروژه‌های خودمان از همین مسیر سبز است.
- **پاکسازی `TestResults` قبل از collect الزامی است** — پوشه‌های GUID از اجراهای قبلی می‌مانند و globِ reportgenerator نتایج کهنه را merge می‌کند → اعداد غلط.
- آخرین آرگومان را quote کنید؛ وگرنه shell کاراکتر `;` را جداکننده‌ی دستور می‌بیند.
- **پیش‌نیاز اعداد کامل**: MSSQL محلی (`Server=.`) با دیتابیس‌های `FastBypass` و `FastBypass_DbScenario` (تست‌های Scenario/LocalDatabase). بدون DB، بقیه‌ی سوئیت سبز می‌ماند ولی مبنا ناقص است.
- متریک‌ها: cobertura هر دو متریک **line + branch** را دارد؛ برای هر دو ستون از `line-rate`/`branch-rate` پکیج‌ها در `coverage.cobertura.xml` استفاده کنید (منبع یکسان). ارقام کلیِ TextSummary اندکی متفاوت است (روش شمارش reportgenerator) — در §۳ هر دو ثبت شده‌اند.

### فرانتند (karma-coverage)

```bash
cd Frontend && yarn test --watch=false --browsers=FirefoxHeadless --code-coverage
# خلاصه در کنسول + گزارش html در coverage/photon-bypass/
```

- فلگ `--code-coverage` **الزامی** است — بدون آن، builder انسترومنت نمی‌کند و `coverageReporter` موجود در `karma.conf.js` خروجی نمی‌سازد.
- پیش‌نیاز: Firefox بومی (مرورگر پیش‌فرض کانفیگ `FirefoxHeadless` است؛ بدون نیاز به `CHROME_BIN`).

## ۳. مبنای اندازه‌گیری

**محیط**: لینوکس، .NET SDK 10.0.101 (هدف net8.0)، MSSQL محلی (`Server=.`) با `FastBypass`/`FastBypass_DbScenario` در دسترس، Firefox 144 headless، Node 20 / yarn 1.22. **تاریخ**: ۱۴۰۵/۰۷/۲۴ (2026-10-16). **نتیجه‌ی اجرا**: بک‌اند ۳۵۲ تست (۳۵۱ پاس / ۱ اسکیپ / ۰ خطا)، فرانتند ۹۰/۹۰ سبز.

اعداد per-assembly از `coverage.cobertura.xml` (هر دو ستون از یک منبع):

| Assembly | Line | Branch | یادداشت |
|---|---:|---:|---|
| Shared | ۸۸.۸٪ | ۸۷.۵٪ | |
| Domain | ۹۰.۱٪ | ۸۳.۷٪ | |
| Infrastructure | ۷۷.۰٪ | ۵۳.۵٪ | شاخه‌های باز شده‌ی ریز در repo ها |
| ServerBridge | ۳۹.۸٪ | ۲۹.۶٪ | هندلرهای I/O واقعی (SSH/Api/Email) ۰٪ — §۵ |
| RadiusDesk | ۲.۵٪ | ۰.۹٪ | مسیر dormant — §۶ (خارج از دامنه) |
| MikrotikRadius | ۴۷.۴٪ | ۳۸.۷٪ | |
| XuiPanel | ۸۸.۱٪ | ۷۶.۹٪ | |
| Application | ۹۰.۵٪ | ۸۳.۱٪ | |
| OutSource | ۸۵.۲٪ | ۶۲.۵٪ | `SocialMediaService` استب — §۶ |
| Portal | ۷۵.۶٪ | ۶۸.۴٪ | `ConnectionController` ۰٪ — §۵ |
| ServiceFactoryHandler | ۱۰۰٪ | ۱۰۰٪ | |
| Admin | ۱۴.۳٪ | ۰٪ | **اسکلت خالی** — عدد پایین طبیعی است؛ از قضاوت پوشش مستثنا |

- **جمع کلی (شامل tik4net وندور)**: cobertura — line ۵۳.۴٪ / branch ۵۰.۱٪؛ TextSummaryِ reportgenerator — line ۵۲.۰٪ / branch ۴۷.۹٪ (تفاوت = روش شمارش).
- **Tik4net / tik4net.objects وندور هستند** (۳۳.۵٪/۱۵.۴٪ line) و عمداً از جدول و از قضاوت پوشش خارج‌اند.
- **فرانتند** (karma-coverage): Lines ۷۵.۹٪ (۸۲۷/۱۰۹۰)، Branches ۵۸.۱٪ (۲۵۱/۴۳۲)، Statements ۷۴.۴٪، Functions ۷۱.۸٪.

## ۴. نقشه‌ی traceability (نیازمندی ← تست)

وضعیت: ✅ پوشش / ⚠️ شکاف یا ناقص / ⛔ خارج از دامنه‌ی رسمی. همه‌ی نام کلاس‌ها زیر `Backend/Tests/Facts/` یا `Frontend/src/` قابل grep هستند.

### جریان‌های کسب‌وکار (docs/11)

| نیازمندی/جریان | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| ثبت‌نام + اعتبارسنجی مدل | docs/11 §1 | `AuthAppTest` (Register_*)، `AccountBusinessTest` | ✅ | فرانتند: `register.component.spec.ts` |
| ورود + صدور JWT + خطای 401 | docs/11 §2 | `AuthAppTest.CheckUserPassword_*`، `JwtAuthTest` | ✅ | 401 بدون/با توکن دست‌کاری‌شده |
| چرخه‌ی کامل کاربر E2E (ثبت‌نام→تمدید→تسویه→پلن) | docs/11 §1–4 | `RegisterAndBuy`، `ConnectAndCheck` | ✅ | نیازمند MSSQL سناریو |
| داشبورد و داده‌ی ترافیک | docs/11 §3 | `ConnectAndCheck.Test`، `VpnApplicationTest` (TrafficData_*) | ✅ | |
| تمدید + قیمت‌گذاری Roslyn (قیمت با روز مؤثر) | docs/11 §4، docs/14 M1 | `PlanPricingTest`، `PriceCalculatorTest`، `RenewalBusinessTest` | ✅ | |
| تراکنش تمدید + rollback/جبران | docs/11 §4، docs/14 M2/M3، docs/17 D9 | `SettlementRollbackTest`، `SettlementRollbackCompensationFailureTest`، `PlanApplicationTest` | ✅ | |
| فاکتور ← رسید ← تایید ← تسویه کیف پول | docs/11 §4، docs/15 | `BillingApplicationTest`، `BillingP2Test`، `ReceiptEndpointTest` | ✅ | |
| سیکل سینک ترافیک + dedup (کلید SessionId) | docs/11 §5، docs/14 C1 | `ServerManagementServiceTest.UpdateTrafficData*`، `SessionRadiusTest.GetTrafficData_*` | ✅ | دو سیکل پشت‌سرهم بدون درج تکراری |
| هشدار پایان پلن + throttle | docs/11 §7 | `AccountMonitoringServiceTest.NotifSendServices_*` | ✅ | |
| آبشار غیرفعال‌سازی ۷/۴۰ روز + حذف از radius | docs/11 §7 | `AccountMonitoringServiceTest.InactiveAbandonedUsers_Check`، `SyncUserActiveInactiveRemoveTest` | ✅ | |
| غیرفعال‌سازی کاربران بدون پلن معتبر | docs/11 §7، docs/14 M5 | `AccountRadiusTest.DeactivateInvalidRadiusUsers_UnknownRealm_ShouldNotThrow` | ✅ | |
| مدیریت ظرفیت realm/سرور | docs/11 §6، docs/14 M4 | `ServerManagementServiceTest.GetAvailableRealm_*` | ⚠️ | `CheckUserServerBalance_Check`=[SKIP] — §۵ |
| گواهی OVPN (تولید/امضا/اکسپورت) | docs/16 §1 | `MikrotikDirectServiceTest.GetOVpnCertificate_*`، `ServerManagementServiceTest.GetDefaultCertificate_*`، `VpnApplicationTest.SendCertEmail*` | ⚠️ | `SetOVpnCertificate` ⛔ NotImplemented — §۶ |
| تغییر رمز VPN + ذخیره در DB (ضد desync) | docs/14 H7 | `VpnApplicationTest.ChangeVpnPassword*` | ✅ | برای x-ui: `XuiAccountSyncTest.ChangeVpnPassword_ThrowsUntilTheD4ContractChangeLands` |
| چنداکانت + `target` (delegation/403) | docs/11 §8 | `CheckAccess` (سناریو)، `AccessService` (BF/Controller) | ✅ | |
| فیلتر تاریخ History (قرارداد unix-ms) | docs/14 H13 | `HistoryContextTest` | ✅ | |

### تصمیم‌های حامل تست در docs/14 (P0–P5)

| تصمیم | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| کد فاکتور اتمیک + idempotency رسید | docs/14 C4/C5 (P0) | `BillingP0Test`، `BillingApplicationTest.RegisterReceipt_*`، `ReceiptEndpointTest.RegisterReceipt_SecondAttemptOnSameInvoice_Fails` | ✅ | |
| هش پسورد PBKDF2 + برابرسازی زمان + توکن‌های CSPRNG | docs/14 C6/H1 (P3) | `PasswordHasherTest`، `HashHandlerTest`، `AuthAppTest.CheckUserPassword_*` | ✅ | |
| Rate limiting پارتیشن‌بندی IP → 429 | docs/14 H12 (P3) | `RateLimitTest.AuthEndpoint_ThirdRequestWithinWindow_Gets429` | ✅ | |
| رفتار کش AccessService | docs/14 H5 (P3) | `AccessService.CheckAccess_*` | ✅ | TTL پیکربندی است، تست رفتار کش را می‌پوشاند |
| مالکیت session در بستن کانکشن (IDOR) | docs/14 H6 (P3) | `ConnectionApplicationTest.CloseConnection_ForeignSession/OwnActiveSession` | ✅ | |
| موازی‌سازی monitoring + به‌روزرسانی ستون‌محور | docs/14 H2 (P4) | `AccountMonitoringServiceTest.ParallelMonitoring_WarningTimeAndInactiveBothApplied` | ✅ | |
| ترتیب/هم‌پوشانی JobInterval | docs/14 H3 (P4) | `JobIntervalTest` | ✅ | ثبت Quartz + DisallowConcurrentExecution |
| کش template ایمیل | docs/14 M22 (P4) | `EmailServiceTest.SendResetPasswordLink_TemplateCachedAcrossInstances` | ✅ | |
| پاک‌سازی secret از لاگ SSH + anchorهای regex تزریق | docs/14 M13/M14 (P3) | `CommandScrubberTest`، `InjectionRegexTest` | ✅ | |
| رمزنگاری Config سرور (Data Protection) | docs/14 M12 (P5) | `ServerConfigProtectionTest` | ✅ | |
| مصرف atomic کد reset-pass | docs/14 M10 (P5) | `AuthAppTest.ResetPassword_*` | ✅ | |
| قفل atomic سینک ترافیک realm | docs/14 H18 (P1) | `SessionRadiusTest.GetTrafficData_TimeLimit_IndexWindowsEachRealm`، `XuiTrafficSyncTest.RealmLockNotAcquired_SkipsTheServer` | ✅ | |
| گیت تسویه per-account (برداشت موازی) | docs/14 M28 (P2) | `BillingApplicationTest.SettleWallet_ParallelOnSameAccount_OnlyOneSucceeds`، `SettlementRollbackTest` | ✅ | |

### docs/15 (پرداخت P2)

| تصمیم | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| آستانه کیف پول (زیر/بالا/خاموش) | docs/15 | `WalletThresholdServiceTest` | ✅ | |
| `BalanceStatus.Verifying` + اعتبار آنی | docs/15 | `BillingP2Test`، `BillingApplicationTest.RegisterReceipt_TopUp_CreditVerifying` | ✅ | |
| XOR فایل/متن رسید + رد محتوا/حجم | docs/15 | `BillingApplicationTest.RegisterReceipt_Xor/ImageValidation/TextBoundary`، `ReceiptEndpointTest` | ✅ | multipart روی HTTP واقعی |
| خالص‌سازی فاکتور (WalletDeduction/Payable) | docs/15 | `BillingApplicationTest.IssuePlanInvoice_Netting` | ✅ | |

### docs/16 + docs/17 (چند-سرور / x-ui)

| تصمیم | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| dispatch مرکزی Features→کلید keyed DI | docs/16 D2، docs/17 §6 | `AccountRadiusTest`، `SessionRadiusTest`، `XuiAccountSyncTest` | ✅ | |
| قرارداد API x-ui (v1.12/v1.14) | docs/17 §4 | `XuiApiTransportTest` | ✅ | probe زنده‌ی vm06/vm08 هنوز اجرا نشده |
| sync اکانت x-ui + enforcement سمت سرور (Encoding A) | docs/17 D13 | `XuiAccountSyncTest.SyncUserAndActive_*` | ✅ | |
| حسابداری ترافیک x-ui (دلتا/epoch/گپ/ردیف صفر/یکتایی SessionId) | docs/17 D5–D8 | `XuiTrafficAccountingTest` | ✅ | |
| sole-writer: ردیف‌های TrafficData + ستون‌های Renewal در یک tx | docs/17 D14 | `XuiTrafficSyncTest` | ✅ | rollback هر دو را برمی‌گرداند |
| wiring کلکتور (Quartz، قفل realm، کلاینت ناشناس) | docs/17 §6 | `XuiTrafficCollectorTest`، `XuiTrafficSyncTest.UnknownClient_IsSkippedAsReconciliationSignal` | ✅ | |
| خوانش تصمیم per-dataplane | docs/17 D14 | `PlanUsageReaderTest` | ✅ | x-ui→ستون‌های Renewal، Radius→ویو |
| `GetActiveConnections` x-ui از onlines | docs/17 §4 | `XuiSessionSyncTest.GetActiveConnections_*` | ✅ | |
| sync اکانت UserManager (پروفایل/محدودیت از renewal) | docs/06، docs/16 §1 | `SyncUserActiveInactiveRemoveTest`، `AccountRadiusTest.SyncUserAndActive_*` | ✅ | |

### لایه‌ی HTTP و فرانتند

| نیازمندی | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| JWT 401 / rate-limit 429 / multipart رسید (HTTP واقعی) | docs/08 | `JwtAuthTest`، `RateLimitTest`، `ReceiptEndpointTest` | ✅ | از طریق `ProgramLevelInitializer.Factory` (WebApplicationFactory) |
| سرویس‌های کلیدی فرانتند | docs/10 | `http-client.spec.ts`، `user-servcie.spec.ts`، `payment-service.spec.ts`، `money-printer.spec.ts`، `validators.spec.ts` | ✅ | |
| کامپوننت‌های رفتاری فرانتند (۱۱) | docs/10 | dashboard، traffic-chart، payment، payment-modal، renewal، login، register، history، forgot-password، change-password، user-selector-modal (هرکدام `.spec.ts`) | ✅ | به‌علاوه: app، home، toast، header، footer |

## ۵. شکاف‌های شناخته‌شده (صداقت کامل — جایگزین `[ ]`های TestPlan قدیمی)

1. **`SessionRadiusSyncUserManagerService` (MikrotikRadius)** — بی‌تست مستقیم: `CloseConnectionByUsername` (۰٪)، `GetActiveConnections` (۷۸٪)، `CloseConnectionBySessionId` (۸۶٪ — پوشش فقط از مسیر `ConnectionApplicationTest` با mock).
2. **ریپوهای Level 3 بی‌تست مستقیم**: Wallet، ResetPass، Price، Realm، Renewal، Server، TrafficData، PlanState. (Account/History/DatabaseGlobal/Billing P0/P2 تست مستقیم دارند. پوشش غیرمستقیم از سناریوها: Wallet/ResetPass/Price/Invoice ۱۰۰٪ ولی Server ۴۶٪، TrafficData ۴۳٪، Realm ۵۶٪، PlanState ۶۴٪، Renewal ۶۹٪.)
3. **نقص باز `POST /api/billing/pay` → ۵۰۰**: `JobContext.AccountId` خارج از جریان renewal تزریق نمی‌شود — endpoint صدا-زده‌نشدنی است (`PayRequest` ۰٪). **تصمیم با مالک**.
4. **`ServerManagementServiceTest.CheckUserServerBalance_Check` = [SKIP]** — تنها تست اسکیپ‌شده‌ی سوئیت؛ هشدار ظرفیت <۱۰٪/>۹۰٪ فعلاً بی‌پوشش اجرایی.
5. **کنترلرهای کم‌پوشش**: `ConnectionController` (۰٪)، `VpnController` (۳۴٪)، `ResultHandlerController` (۴۷٪).
6. **هندلرهای I/O واقعی ServerBridge** (شبکه/SSH واقعی): `SshHandler`، `SshConnection`، `ApiHandler`، `EmailHandler` — همه ۰٪؛ توسط mock (`MockServerBridge/ConfigurableSshHandlerMoq`) جایگزین شده‌اند.

نوشتن تست برای این فهرست **کار آتی جدا** است (خارج از دامنه‌ی سند حاکمیت).

## ۶. خارج از دامنه‌ی رسمی (بدون قضاوت پوشش)

- **adapter RadiusDesk** (کل پروژه ۲.۵٪) — تعویق L8 در docs/14 تا فعال‌سازی مسیر RadiusDesk.
- `SocialMediaService` (OutSource) — هر پنج متد استب `TODO`.
- `MikrotikDirectService.SetOVpnCertificate` — `NotImplementedException` (تکثیر گواهی به NASهای بعدی realm).
- **پنل Admin** — اسکلت خالی (`Program.cs` + `AddAdminServices` خالی)؛ عدد پایین طبیعی است.
- **Google Auth** — فاز ۵ طبق `README.md`.
- **WhatsApp** — compile-time با `#if SOCIAL`.
- **Tik4net / tik4net.objects** — وندور.

## ۷. قاعده‌ی حاکمیت

- هر PR که **تست اضافه/تغییر می‌دهد** یا **جریان/تصمیم جدیدی می‌سازد**، ردیف مربوط در §۴ همین سند را **در همان PR** به‌روز می‌کند (ستون تست‌ها/وضعیت) و در صورت لزوم شکاف تازه به §۵ اضافه می‌شود.
- **مبنای پوشش (§۳)** به‌صورت موردی و هنگام تغییرهای ساختاری (پروژه‌ی جدید، تغییر پشته‌ی تست) بازسنجی می‌شود؛ نه در هر PR.
- شکاف‌های پوشش فقط اینجا (§۵) دنبال می‌شوند — نه در `Architecture/TODO.md` (آنجا محل کارهای تصمیم‌شده است).

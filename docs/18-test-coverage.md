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

**محیط**: لینوکس، .NET SDK 10.0.101 (هدف net8.0)، MSSQL محلی (`Server=.`) با `FastBypass`/`FastBypass_DbScenario` در دسترس، Firefox 144 headless، Node 20 / yarn 1.22. **تاریخ**: ۱۴۰۵/۰۷/۲۴ (2026-10-16). **نتیجه‌ی اجرا**: بک‌اند ۳۵۲ تست (۳۵۱ پاس / ۱ اسکیپ / ۰ خطا)، فرانتند ۹۰/۹۰ سبز. *(به‌روزرسانی همان روز — T3: بک‌اند ۳۸۸/۳۸۸ پاس / ۰ اسکیپ، فرانتند ۹۶/۹۶ سبز؛ درصدهای پوشش بازسنجی نشده‌اند.)* *(به‌روزرسانی ۲۰۲۶-۱۰-۱۷ — فاز ۶ پنل ادمین: فرانتند ۱۴۳/۱۴۳ سبز روی ویندوز/Firefox 156؛ درصدهای پوشش بازسنجی نشده‌اند. بک‌اند در فاز ۶ دست نخورد.)*

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
| مدیریت ظرفیت realm/سرور (تقاضای نظری + override دستی) | docs/11 §6، docs/14 M4، T3 §2/§3 | `ServerManagementServiceTest` (`GetAvailableRealm_*`, `GetRealmsCapacityState_*`, `SetRealmCapacityOverride_*`, `CheckUserServerBalance_*`)، `RealmCapacityOverrideTest` | ✅ | ظرفیت از تقاضای نظری پلن‌ها (quota/rate/min/fallback)؛ override روی DB واقعی سناریو؛ تست هشدار با `SocialMediaServiceMoq` از حالت skip خارج شد |
| گواهی OVPN (تولید/امضا/اکسپورت) + دانلود مستقیم | docs/16 §1، T3 §1 | `MikrotikDirectServiceTest.GetOVpnCertificate_*`، `ServerManagementServiceTest.GetDefaultCertificate_*`، `VpnApplicationTest.SendCertEmail*`/`DownloadCertificate*`/`DownloadVpnConfig_*`، `DownloadCert` (سناریو: content-type/disposition + audit) | ⚠️ | `SetOVpnCertificate` ⛔ NotImplemented — §۶ |
| ایمیل واحد مشخصات اتصال (ادغام CertEmail+VpnLinkEmail): ساخت همه‌ی artifactهای کاربر در یک درخواست + حذف بخش‌های ناموجود | docs/16 §D4 | `EmailServiceTest.SendConnectionEmail_*` (فقط vless / فقط گواهی / هر دو)، `VpnApplicationTest.SendCertEmail` + `SendCertEmail_XuiRealm_SendsTheVlessLinkWithoutAttachment` + `SendCertEmail_RealmWithBothCapabilities_SendsOneEmailWithLinkAndCertificate` + `SendCertEmail_*BlockedBeforeCertificateBuild` | ✅ | قالب واحد `ConnectionEmail` با بخش‌های شرطی `<!--if:key-->`؛ پیوست `cert.ovpn` فقط در مسیر گواهی |
| تغییر رمز VPN + ذخیره در DB (ضد desync) | docs/14 H7 | `VpnApplicationTest.ChangeVpnPassword*` | ✅ | مسیر Radius: ذخیره‌ی مقدار بازگشتی (echo adapter) |
| چرخش UUID برای x-ui (D4): قرارداد credential مؤثر + تولید در دیسپچر + throttle + گارد realm مخلوط | docs/16 §D4، docs/17 §3 | `XuiAccountSyncTest.ChangeVpnPassword_*` (پچ-only + نبود کلاینت + کلاینت غیرفعال)، `AccountRadiusTest.ChangeVpnPassword_*` (echo/UUID واحد/مخلوط)، `VpnApplicationTest.ChangeVpnPassword_XuiRealm*` + `_RadiusPath_*` (ذخیره‌ی UUID بازگشتی + throttle داخل/بیرون/خاموش + مسیر Radius ساعت را نمی‌کوبد)، `AccountRepositoryTest.LastCredentialRotateTime_RoundTripsThroughSaveAndFetch` (پشتوانه‌ی DB واقعی) | ✅ | |
| تحویل سه‌کاناله‌ی کانفیگ x-ui: ایمیل لینک + دانلود متنی + نمایش/کپی در پنل | docs/16 §D4 | `VlessLinkServiceTest` (fixtureهای ws+tls/tcp+reality/grpc/PascalCase/پارامتر جاافتاده/خطاها + خوانش زنده)، `VpnApplicationTest.GetConfigInfo_*`/`DownloadVpnConfig_*`/`SendCertEmail_XuiRealm`، `XuiApiTransportTest.GetInbound_*`، `EmailServiceTest.SendConnectionEmail_*`، `VpnConfigEndpoints` (سناریو: kind + OVPN + گیت دسترسی)، `dashboard.component.spec.ts` (ردیف لینک + دکمه‌ها + کپی/خطای کپی + عدم درخواست برای غیر x-ui) | ✅ | endpointهای `config-info`/`download` |
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
| گارد secretهای تستی در startup (بلاک پروداکشن با مقادیر کامیت‌شده) | docs/14 M29، docs/19 §2 | `ProductionSecretsGuardTest` (هر ۴ کلید / Development / override کامل / فقط یک کلید / کلیدهای غایب)، `PleskSecretsTest` (override با per-key و hierarchy جزئی / کاندید دوم برای app تودرتو / تقدم کاندید اول / no-op وقتی فایل نیست) + دو تست wiring در `ProductionSecretsGuardTest` (`AddPortalServices` در Production با مقادیر تستی → throw از گارد؛ در Development → ثبت کامل بدون خطا) | ✅ | گارد/لودر با `AddInMemoryCollection` + stub ساده‌ی `IHostEnvironment` — بدون بوت host؛ لودر با `WebApplication.CreateBuilder` و ContentRootPath موقت در دایرکتوری تمپ |

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
| قرارداد API x-ui (v1.12/v1.14) | docs/17 §4 | `XuiApiTransportTest` | ✅ | probe زنده‌ی vm06/vm08 اجرا شد (۱۴۰۵/۰۷/۲۴؛ ۱۴/۱۴ PASS هر دو، بدون ری‌استارت — Simorgh `docs/methods/xui-panel-api-probe.md`) |
| sync اکانت x-ui + enforcement سمت سرور (Encoding A) | docs/17 D13 | `XuiAccountSyncTest.SyncUserAndActive_*` | ✅ | |
| حسابداری ترافیک x-ui (دلتا/epoch/گپ/ردیف صفر/یکتایی SessionId) | docs/17 D5–D8 | `XuiTrafficAccountingTest` | ✅ | |
| sole-writer: ردیف‌های TrafficData + ستون‌های Renewal در یک tx | docs/17 D14 | `XuiTrafficSyncTest` | ✅ | rollback هر دو را برمی‌گرداند |
| wiring کلکتور (Quartz، قفل realm، کلاینت ناشناس) | docs/17 §6 | `XuiTrafficCollectorTest`، `XuiTrafficSyncTest.UnknownClient_IsSkippedAsReconciliationSignal` | ✅ | |
| خوانش تصمیم per-dataplane | docs/17 D14 | `PlanUsageReaderTest` | ✅ | x-ui→ستون‌های Renewal، Radius→ویو |
| `GetActiveConnections` x-ui از onlines | docs/17 §4 | `XuiSessionSyncTest.GetActiveConnections_*` | ✅ | |
| sync اکانت UserManager (پروفایل/محدودیت از renewal) | docs/06، docs/16 §1 | `SyncUserActiveInactiveRemoveTest`، `AccountRadiusTest.SyncUserAndActive_*` | ✅ | |
| شاخه‌بندی قابلیت دیتاپلین (kind/گیت‌های OVPN و تغییر رمز/پرچم چرخش) | docs/16 §3/§6، docs/17 §5.5 | `ServerManagementServiceTest.GetDataPlaneInfo_*`، `VpnApplicationTest.ChangeVpnPassword_*BlockedByGate`/`SendCertEmail_*BlockedBeforeCertificateBuild`، `PlanApplicationTest.GetPlanInfo_*`، `dashboard.component.spec.ts` (رندر دکمه‌ها بر اساس قابلیت) | ✅ | گیت قبل از ساخت گواهی؛ realm تهی = semantics هر-NAS/بدون-xui؛ `SupportsCredentialRotation` در هر دو لایه‌ی info/plan-info |

### docs/20 (نقش‌ها و پنل مدیریت)

| تصمیم | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| کش AccessService با نقش: bypass ادمین + InvalidateTargetArea + عدم bypass مدیر | docs/20 فاز ۱-۴ | `AccessService.CheckAccess_AdminRole_*`/`CheckAccess_ManagerRole_*`/`InvalidateTargetArea_*`، `CheckAccess.Test_admin` (سناریو، seed با Role=2) | ✅ | |
| claim نقش در JWT + Role/AllowSelfService در پاسخ لاگین + اعمال policyها | docs/20 فاز ۱-۳/۵ | `RolesAuthorizationTest.Login_AdminTokenCarries*` (claim در توکن)، `RolesAuthorizationTest.GetUser_ReportsRoleAndSelfServiceFlag` (seed اسکریپت: Role=2/UserType\|=4 + پرچم مستقل D11)، `RolesAuthorizationTest.Policies_EnforceRoleMatrix*` (توکن validate شده + principal سنتزی مدیر: Manager شامل Admin، User هیچ‌کدام)، `AuthAppTest.CheckUserPassword_WithTargetArea` | ✅ | مصرف policy روی endpoint واقعی در فازهای ۴/۵ |
| گیت ثبت‌نام (D11) + پرچم مستقل + endpoint عمومی app-config | docs/20 D11 | `AuthAppTest.Register_DisabledByConfig_*`/`Register_Success_SetsSelfServiceFlag`، `BasicsApplicationTest.GetAppConfig_MirrorsRegistrationFlag` (هر دو قطب فلگ)، `AppConfigTest` (عمومی بدون توکن + reflect فلگ)؛ سناریوها با `AllowRegistration=true` صریح در Factory | ✅ | مسیر 403 در سطح unit |
| تفکیک payer در مسیر پول (D4) + گارد وابسته روی endpointهای مالی self (D6) | docs/20 فاز ۲ | `PayerSeparationTest` (سناریو: وابسته → 403 کاربرپسند روی هر ۶ endpoint، ادمین-پرداخت‌کننده → فاکتور/دبت/موجودی روی ادمین + پلن روی target + ری‌پلی settle، pay سالم)، `PlanApplicationTest.Renewal_DependentSelfUser_IsForbidden`/`Estimate_DependentSelfUser_IsForbidden`/`Renewal_OnBehalf_IssuesInvoiceAndReadsBalanceOnPayer`/`Renewal_Action` (دبت روی payer، renewal روی target)/`Renewal_Action_PayerNotFound`، `BillingApplicationTest.SelfFinancialEndpoints_DependentSelfUser_AreForbidden`/`GetInvoice_OnBehalfView_DoesNotLeakTargetWallet`/`SettleWallet_OnBehalf_DebitsPayerAndRenewsTarget`، `SettlementRollbackPayerSeparationTest`، `renewal.component.spec.ts` (ناوبری پرداخت بدون target در نمای «از طرف») | ✅ | فاکتورهای pending قبل از deploy بدون migration ری‌پلی می‌شوند |
| تراکنش شفاف ادمین + تاریخچه کیف پول (D5: ردیف Completed با ActorId، موجودی منفی مجاز، مدیر به کیف زیر دست دسترسی ندارد) | docs/20 فاز ۳ | `WalletApplicationTest` (unit: قرارداد ردیف Completed+ActorId+trim توضیح، دبت منفی، ۵ اعتبارسنجی، ادمین-بدون-فلگ 403، ماتریس دسترس self/وابسته/کاربر/مدیر/ادمین، صفحه‌بندی newest-first + resolve نام Actor)، `WalletAdminTransactionTest` (سناریوی HTTP: adjust ادمین → ردیف+موجودی و دبت منفی، policy ادمین → 403 کاربر عادی، اعتبارسنجی 400، ماتریس history شامل مدیر-روی-زیردست که از LoadJobContext می‌گذرد و 403 می‌گیرد، صفحه‌بندی سمت سرور ۲۳→۲۰+۳ و page=0 → 400) | ✅ | |
| لیست و مدیریت کاربران (ماتریس §۳: اسکوپ مدیر، اجبار Owner=self، D9 تک‌ادمینی/بدون ارتقا/عدم خود-عملیات، گارد تنزل مدیر با زیر دست فعال، attach/detach + چرخه، فلگ self-service، D13 فعال/غیرفعال) | docs/20 فاز ۴ | `UserManagementApplicationTest` (unit: اسکوپ لیست، اجبار مدیر، گاردهای create ادمین، گارد تنزل، attach/detach/چرخه، InvalidateTargetArea، ماتریس set-password/set-active/set-self-service)، `UserManagementTest` (سناریوی HTTP: policy، جستجو/صفحه‌بندی/غیرفعال‌ها، ماتریس create شامل 409 و لاگین با پسورد اولیه، بازنشانی، ارتقا/تنزل، attach→مدیر همان نشست می‌مدیرد و detach→403، فلگ self-service که گیت D6 کیف را live باز/بسته می‌کند، deactivate→401/activate)، `AccountRepositoryTest.SearchAccounts_ScopeSearchPagingAndPlanJoin` (DB واقعی: OUTER APPLY روی TotalPlanState با `TrafficLeft=25-5`، اسکوپ/جستجو/صفحه‌بندی، HasActiveSubordinate) | ✅ | رابطه actor→target در management endpoints زنده از DB چک می‌شود، نه از کش لاگین |
| CRUD سرورها (D8: لیست فقط اسکالر + has-config بدون هیچ راز، protect سمت سرور + نوشتن بدون خواندن، 409 تکراری IP/نام/دامنه، toggle دوجهته‌ی فعال) | docs/20 فاز ۵ | `ServerCrudApplicationTest` (unit: ماتریس اعتبارسنجی IPv4/طول‌ها/پهنای‌باند/OS/بیت‌های Features/realm، 409 تکراری، update بدون config → blob ثابت / با config → جایگزینی، toggle، ادمین-در-عمق، کانفیگ >4000)، `ServerCrudTest` (سناریوی HTTP: policy ادمین → 403 کاربر/مدیر، پاسخ خام لیست بدون ساختار کانفیگ/`protected:`/راز، create → blob محافظت‌شده + round-trip راز، 400ها/409ها، update بدون/با config، set-active) | ✅ | مرز docs/16 رعایت شد — بدون دست‌زدن به keyed dispatch/adapter/`ServerFeature` |
| فرانت نقش‌دار فاز ۶: گاردهای نقش + گیت وابسته (D6) در UI، منوی نقش‌محور، صفحات users/servers/wallet، انتخابگر «از طرف» ادمین از user list، گیت ثبت‌نام با app-config (D11) | docs/20 فاز ۶ (گام‌های ۱۹–۲۳) | `role-guards.spec.ts` (پذیرش/رد managerGuard/adminGuard/selfServiceGuard + redirect لاگین روی خطای user)، `user-servcie.spec.ts` (گترهای نقش، رد `setExternalTarget` برای غیرادمین، target آزاد ادمین)، `default-header.component.spec.ts` (سه پروفایل منو: وابسته بدون لینک مالی / مدیر فقط کاربران / ادمین کامل + انتخابگر بدون زیر دست)، `users.component.spec.ts` (ستون‌های نقش/مالک فقط ادمین، جستجو/صفحه‌بندی سمت سرور، payload ساخت مدیر/ادمین، گارد فرم، set-active/self-service/owner/password، عمل از طرف مدیر/ادمین/رد غیرفعال)، `servers.component.spec.ts` (لیست، toggle، create با config parse-شده و بیت‌ها، رد JSON خراب، update بدون config)، `wallet.component.spec.ts` (history با/بدون target، رد ورودی ناقص، payload adjust)، `user-selector-modal.component.spec.ts` (تغذیه‌ی ادمین از `/api/user/list`)، `app-config-service.spec.ts`، گیت ثبت‌نام در `login.component.spec.ts` (پنهان‌سازی CTA) و `register.component.spec.ts` (پیام بسته + عدم POST) | ✅ | مسیرهای `/renewal`+`/payment` با `selfServiceGuard`، `/users` با managerGuard، `/servers`+`/wallet` با adminGuard — آینه‌ی ماتریس §۳ سمت سرور |

### دیپلوی (docs/19)

| نیازمندی | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| جریان دیپلوی دیتابیس + ساخت ادمین اول (batch ترتیب‌دار idempotent، گارد manifest فایل‌های فهرست‌نشده/غایب، گاردهای DeployCli: username تکراری بدون بازنویسی + تک‌ادمینی D9 + exit codes، `verify` روی هش PBKDF2، پارس `--secrets` بدون نشت به لاگ) | docs/19 §ج، docs/20 D9، `Database/LocalDatabase/README.md` | `scripts/deploy.sh db\|admin` + `DeployCli` (`create-admin`/`verify`، `AdminSeeder`/`DeploySecrets` static برای قابلیت تست) | ⚠️ | بدون تست خودکار — validation دستی (§۵-۷): اجرای smoke کامل روی MSSQL محلی ۱۴۰۵/۰۷/۲۴ (batch تازه، `Role/UserType=(2,4)`، idempotency، هر دو گارد، verify، لاگ بدون پسورد) |

### لایه‌ی HTTP و فرانتند

| نیازمندی | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| JWT 401 / rate-limit 429 / multipart رسید (HTTP واقعی) | docs/08 | `JwtAuthTest`، `RateLimitTest`، `ReceiptEndpointTest` | ✅ | از طریق `ProgramLevelInitializer.Factory` (WebApplicationFactory) |
| سرویس‌های کلیدی فرانتند | docs/10 | `http-client.spec.ts`، `user-servcie.spec.ts`، `payment-service.spec.ts`، `money-printer.spec.ts`، `validators.spec.ts`، `role-guards.spec.ts`، `app-config-service.spec.ts` | ✅ | |
| کامپوننت‌های رفتاری فرانتند (۱۴) | docs/10 | dashboard، traffic-chart، payment، payment-modal، renewal، login، register، history، forgot-password، change-password، user-selector-modal، users، servers، wallet (هرکدام `.spec.ts`) | ✅ | به‌علاوه: app، home، toast، header، footer |

## ۵. شکاف‌های شناخته‌شده (صداقت کامل — جایگزین `[ ]`های TestPlan قدیمی)

1. **`SessionRadiusSyncUserManagerService` (MikrotikRadius)** — بی‌تست مستقیم: `CloseConnectionByUsername` (۰٪)، `GetActiveConnections` (۷۸٪)، `CloseConnectionBySessionId` (۸۶٪ — پوشش فقط از مسیر `ConnectionApplicationTest` با mock).
2. **ریپوهای Level 3 بی‌تست مستقیم**: Wallet، ResetPass، Price، Renewal، Server، TrafficData، PlanState. (Account/History/DatabaseGlobal/Billing P0/P2 تست مستقیم دارند. پوشش غیرمستقیم از سناریوها: Wallet/ResetPass/Price/Invoice ۱۰۰٪ ولی Server ۴۶٪، TrafficData ۴۳٪، PlanState ۶۴٪، Renewal ۶۹٪. Realm از مسیر `RealmCapacityOverrideTest` — `SetCapacityOverride` + ظرفیت مؤثر روی DB واقعی — پوشش مستقیم گرفت.)
3. **نقص باز `POST /api/billing/pay` → ۵۰۰** — **حل شد (فاز ۲ پنل ادمین، ۲۰۲۶-۱۰-۱۷)**: `IssueTopUp` اکنون payer را از `ISelfServiceGuard.ResolvePayingActor()` می‌گیرد (گارد وابسته D6 را هم اعمال می‌کند)؛ پوشش: `PayerSeparationTest.Pay_IssuesTopUpInvoiceForActor` + `BillingApplicationTest.IssueTopUp_*`/`SelfFinancialEndpoints_DependentSelfUser_AreForbidden`.
4. **`ServerManagementServiceTest.CheckUserServerBalance_Check` = [SKIP]** — **حل شد (T3، ۱۴۰۵/۰۷/۲۴)**: با `SocialMediaServiceMoq` و متریک «جمع مصرف ۳۰روزه» فعال شد و سوئیت دیگر تست اسکیپ‌شده ندارد.
5. **کنترلرهای کم‌پوشش**: `ConnectionController` (۰٪)، `ResultHandlerController` (۴۷٪). (`VpnController` با تست سناریویی `download-cert` پوشش گرفت.)
6. **هندلرهای I/O واقعی ServerBridge** (شبکه/SSH واقعی): `SshHandler`، `SshConnection`، `ApiHandler`، `EmailHandler` — همه ۰٪؛ توسط mock (`MockServerBridge/ConfigurableSshHandlerMoq`) جایگزین شده‌اند.
7. **جریان دیپلوی (`scripts/deploy.sh` + `DeployCli`)** — فقط validation دستی، بدون تست خودکار (ردیف §۴-دیپلوی): smoke روی MSSQL محلی با اجرای همان batch ساختار + `create-admin`/`verify` انجام شد؛ خودِ `sqlcmd` روی آن ماشین نصب نبود، پس مسیر sqlcmd (fail-fast `-b`، تایماوت، codepage) هنوز روی هاست واقعی آزمایش نشده — اولین اجرای پروداکشن با دقت دنبال شود (docs/19 §هـ).
8. **تداخل موازی کلاس‌های سناریو روی `FastBypass_DbScenario` مشترک (۲۰۲۶-۱۰-۱۷، از قبل موجود)** — هر کلاس سناریو Factory خودش را دارد که در ساخت، همان یک دیتابیس را DROP/CREATE می‌کند؛ با اجرای موازی کلاس‌ها (پیش‌فرض xUnit) بازسازی وسطِ اجرای کلاس دیگر رخ می‌دهد و خطاهای تکراری‌شدن موبایل/یوزرنیم *متغیر و زمان‌بندی‌محور* می‌سازد (مشاهده‌شده: `UserManagementTest.SetOwner_AttachDetachAndGuards` به‌طور پیوسته و یک‌بار `ConnectAndCheck.Test` در اجرای کامل سوئیت؛ هر دو در اجرای تنها کاملاً سبز؛ روی baseline بدون تغییرات فاز ۵ پنل هم بازتولید شد — ارتباطی با آن تغییرات ندارد). درمان کاندید: `[Collection]` مشترک برای کلاس‌های `Facts/Scenario` یا خاموش کردن `parallelizeTestCollections` — کار آتی جدا.

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

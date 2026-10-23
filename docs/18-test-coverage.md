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

**محیط**: لینوکس، .NET SDK 10.0.101 (هدف net8.0)، MSSQL محلی (`Server=.`) با `FastBypass`/`FastBypass_DbScenario` در دسترس، Firefox 144 headless، Node 20 / yarn 1.22. **تاریخ**: ۱۴۰۵/۰۷/۲۴ (2026-10-16). **نتیجه‌ی اجرا**: بک‌اند ۳۵۲ تست (۳۵۱ پاس / ۱ اسکیپ / ۰ خطا)، فرانتند ۹۰/۹۰ سبز. *(به‌روزرسانی همان روز — T3: بک‌اند ۳۸۸/۳۸۸ پاس / ۰ اسکیپ، فرانتند ۹۶/۹۶ سبز؛ درصدهای پوشش بازسنجی نشده‌اند.)* *(به‌روزرسانی ۲۰۲۶-۱۰-۱۷ — فاز ۶ پنل ادمین: فرانتند ۱۴۳/۱۴۳ سبز روی ویندوز/Firefox 156؛ درصدهای پوشش بازسنجی نشده‌اند. بک‌اند در فاز ۶ دست نخورد.)* *(به‌روزرسانی ۲۰۲۶-۱۰-۱۸ — دانلود واحد کانفیگ + برندینگ (D1–D4): بک‌اند ۵۰۳ تست — ۵۰۰ پاس + ۳ شکست فلکیِ موازی‌زایی سناریوها (§۵.۸، بازتولیدشدنی روی baseline؛ `RegisterAndBuy.Simple` در اجرای تنها سبز)؛ فرانتند ۱۴۳/۱۴۳ سبز؛ درصدهای پوشش بازسنجی نشده‌اند.)* *(به‌روزرسانی ۲۰۲۶-۱۰-۱۸ (بازبینی دوم) — اصلاح H21–H25: بک‌اند ۵۰۸ تست — ۵۰۵ پاس + همان ۳ شکست فلکیِ §۵.۸ که عیناً روی baseline بدون این تغییرات هم بازتولید شد (کپی HEAD جدا؛ `RegisterAndBuy`/`SetOwner` = شکست‌های از-قبل-موجود و بی‌ربط)؛ ۵ تست جدید: `ServerRepositoryTest` (۴ × DB واقعی) + `RenewalPostCommitFailureTest`؛ فرانتند دست نخورد.)* *(به‌روزرسانی ۲۰۲۶-۱۰-۱۸ (اصلاح M30–M39) — بک‌اند ۵۱۱ تست — ۵۰۸ پاس + همان ۳ شکست فلکیِ §۵.۸؛ تست‌های جدید: `RenewalRepositoryTest` (DB واقعی، پکیج `RenewalLatest`) + `AccessService.CheckAccess_CacheMiss_RebuildsAreaFromRepository` + `PasswordEndpointRateLimitTest`؛ fixtureهای پسورد به سیاست جدید M35 منطبق شدند؛ فرانتند دست نخورد.)* *(به‌روزرسانی ۲۰۲۶-۱۰-۲۱ — کانال تلگرام (docs/21): بک‌اند ۶۰۲ تست — ۶۰۱ پاس + همان شکست فلکیِ §۵.۸ (`SetOwner` — بازتولیدشده روی HEAD بدون تغییرات این فاز)؛ ۳۵ تست جدید تلگرام؛ فرانتند ۱۵۱/۱۵۱ سبز (۸ تست جدید)؛ درصدهای پوشش بازسنجی نشده‌اند.)* *(به‌روزرسانی ۲۰۲۶-۱۰-۲۳ — کپچای خودمیزبان + rate-limit تفکیک‌شده (پلن self-hosted-captcha-auth): بک‌اند ۶۲۰ تست — ۱۸ تست جدید (`CaptchaServiceTest` ۱۴ + `CaptchaTest` ۴؛ نرخ فلکیِ §۵.۸ عیناً همان baseline است: ۲ اجرای HEAD بدون تغییرات = ۲ و ۱ شکست، ۲ اجرا با این تغییرات = ۲ و ۱ شکست)؛ `RateLimitTest`/`PasswordEndpointRateLimitTest` برای bucketهای per-endpoint بازنویسی شدند؛ `ServerConfigProtectionTest` در برابر race هنگام‌سازی هاست‌های موازی مقاوم شد؛ فرانتند ۱۶۰/۱۶۰ سبز (۹ تست جدید)؛ درصدهای پوشش بازسنجی نشده‌اند.)*

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
| OutSource | ۸۵.۲٪ | ۶۲.۵٪ | `EmailService` + کانال تلگرام (docs/21) — اعداد قبل از تلگرام؛ بازسنجی طبق §۷ |
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
| غیرفعال‌سازی کاربران بدون پلن معتبر + حذف غیر-Active از valid-set | docs/11 §7، docs/14 M5، docs/20 D13 | `AccountRadiusTest.DeactivateInvalidRadiusUsers_UnknownRealm_ShouldNotThrow`، `AccountRadiusTest.DeactivateInvalidRadiusUsers_NonActiveStatus_DropsFromValidSet` (VpnSuspended/Deactivated از valid-set realm خودشان هم حذف می‌شوند — safety-net sweep) | ✅ | || مدیریت ظرفیت realm/سرور (تقاضای نظری + override دستی) | docs/11 §6، docs/14 M4، T3 §2/§3 | `ServerManagementServiceTest` (`GetAvailableRealm_*`, `GetRealmsCapacityState_*`, `SetRealmCapacityOverride_*`, `CheckUserServerBalance_*`)، `RealmCapacityOverrideTest` | ✅ | ظرفیت از تقاضای نظری پلن‌ها (quota/rate/min/fallback)؛ override روی DB واقعی سناریو؛ تست هشدار با `SocialMediaServiceMoq` از حالت skip خارج شد |
| گواهی OVPN (تولید/امضا/اکسپورت) + دانلود مستقیم | docs/16 §1، T3 §1 | `MikrotikDirectServiceTest.GetOVpnCertificate_*`، `ServerManagementServiceTest.GetDefaultCertificate_*`، `VpnApplicationTest.SendCertEmail*`/`DownloadVpnConfig_*` (OVPN → ZIP: تطابق بایتی `config.ovpn` با CertFile + ساختار `connection-info.txt` + audit)، `DownloadConfig` (سناریو: content-type/disposition + دو entry + audit) | ⚠️ | `SetOVpnCertificate` ⛔ NotImplemented — §۶ |
| ایمیل واحد مشخصات اتصال (ادغام CertEmail+VpnLinkEmail): ساخت همه‌ی artifactهای کاربر در یک درخواست + حذف بخش‌های ناموجود | docs/16 §D4 | `EmailServiceTest.SendConnectionEmail_*` (فقط vless / فقط گواهی / هر دو)، `VpnApplicationTest.SendCertEmail` + `SendCertEmail_XuiRealm_SendsTheVlessLinkWithoutAttachment` + `SendCertEmail_RealmWithBothCapabilities_SendsOneEmailWithLinkAndCertificate` + `SendCertEmail_*BlockedBeforeCertificateBuild` | ✅ | قالب واحد `ConnectionEmail` با بخش‌های شرطی `<!--if:key-->`؛ پیوست `cert.ovpn` فقط در مسیر گواهی |
| برندینگ قابل‌پیکربندی + لوگوی CID (D1–D4 دانلود واحد): `BrandingOptions` به‌عنوان منبع واحد عنوان/لینک‌ها/لوگو، بدنه‌ی HTML فقط از AlternateView با `LinkedResource(logo)`، رفع باگ `IsBodyHtml=false` و بسته‌شدن زودهنگام stream پیوست، متن دانلود با قاعده‌ی RTL/LTR (عنوان/مقدار دوخطی + خط خالی) | برنامه‌ریزی ۱۷۹۲۳۱۱۴۵۶۰۶۹ D1–D4 | `EmailServiceTest.EveryEmail_CarriesTheLogoAsEmbeddedCidResource` + `MissingLogoFile_FailsFastWithClearMessage` + `EmptyInstallerUrl_StripsTheInstallerLine` + جایگزینی برندینگ در `SendResetPasswordLink_TemplateCachedAcrossInstances`، `VpnApplicationTest.DownloadVpnConfig_XuiRealm_ReturnsBrandedConnectionSheet`/`_OvpnRealm_ReturnsZipWithCertAndInfoText`/`_WithoutEmailAddress_Succeeds` | ✅ | مقادیر تست از `BrandingOptionsMoq`؛ `connection-info.txt` بدون BOM |
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
| کوئری‌های raw-SQL ریپوی Server روی DB واقعی (فیلتر realm/پارامتر لیستی/lookup بستن کانکشن) | docs/14 H21–H23 (بازبینی دوم) | `ServerRepositoryTest` (پکیج `Data/Sql/ServerLookup`: `GetAllActiveNasDomainInRealm_ScopedToRealm`/`_AllRealms`، `GetServerIdByIpAddress_BindsTheListParameter`، `GetActiveNasInfo_FindsActiveNasByIp`) | ✅ | ریشه: این متدها فقط Moq داشتند — سه باگ SQL زنده از سوئیت رد شده بود |
| تاب‌آوری مراحل پس از commit تمدید (بستن کانکشن/History/ظرفیت best-effort) | docs/14 H24 (بازبینی دوم) | `RenewalPostCommitFailureTest.SettleWallet_CloseConnectionFailureAfterCommit_StillSucceeds` | ✅ | شکست SSH پس از commit → تسویه همچنان موفق؛ فاکتور Canceled می‌ماند |
| سیاست پسورد سمت سرور (۴ مسیر set پسورد، قرینه‌ی الگوی فرانت) | docs/14 M35 (بازبینی دوم) | `AuthAppTest.Register_*`/`ResetPassword_*`، `AccountAppTest.ChangePassword_*`، `UserManagementApplicationTest.Create/SetPassword*`، سناریوهای `RegisterAndBuy`/`UserManagementTest`/`CheckAccess` با fixtureهای منطبق | ✅ | `AccountBusiness.ValidatePassword` — نقطه‌ی واحد اعمال |
| بازسازی ناحیه دسترسی روی cache miss (restart/هاست دوم) | docs/14 M32 (بازبینی دوم) | `AccessService.CheckAccess_CacheMiss_RebuildsAreaFromRepository` + سوئیت `AccessService` async-شده | ✅ | ناحیه با همان قواعد لاگین از DB بازسازی و دوباره کش می‌شود |
| lookup آخرین Renewal با window-function | docs/14 M31 (بازبینی دوم) | `RenewalRepositoryTest.GetLatestByAccountIds_ReturnsNewestRenewalPerAccount` (DB واقعی، پکیج `RenewalLatest`) | ✅ | یک ردیف به‌ازای اکانت — جدیدترین بر اساس Id |
| گسترش سهمیه auth به endpointهای اعتبارسنجی پسورد (change-ovpn/change-pass) | docs/14 M34 (بازبینی دوم) | `PasswordEndpointRateLimitTest.ChangeOvpnPassword_RepeatedWrongTokens_Gets429` | ✅ | همان policy/bucket مشترک «auth»؛ کلاس جدا = اپ و limiter تازه |
| گارد secretهای تستی در startup (بلاک پروداکشن با مقادیر کامیت‌شده) + الزام ایمیل فعال در پروداکشن | docs/14 M29/M48، docs/19 §2 | `ProductionSecretsGuardTest` (هر ۴ کلید تستی / Development / override کامل / فقط یک کلید / ruleهای ایمیل: نبود سکشن، `Enabled=false`، Address/Password خالی، پیام فقط کلیدهای مقصر) و `PleskSecretsTest` (override با per-key و hierarchy جزئی / کاندید دوم برای app تودرتو / تقدم کاندید اول / no-op وقتی فایل نیست) + دو تست wiring در `ProductionSecretsGuardTest` (`AddPortalServices` در Production با مقادیر تستی → throw از گارد؛ در Development → ثبت کامل بدون خطا) | ✅ | گارد/لودر با `AddInMemoryCollection` + stub ساده‌ی `IHostEnvironment` — بدون بوت host؛ لودر با `WebApplication.CreateBuilder` و ContentRootPath موقت در دایرکتوری تمپ |
| سخت‌سازی حمل نقلی و رندر ایمیل: MailKit + DTO خنثی، ساختار multipart (mixed→related→alternative + CID لوگو + پیوست)، no-op با `Enabled=false`، خطاهای نوع‌دار کانفیگ، اعتبارسنجی شرطی گزینه‌ها | docs/14 M40–M42، M45–M46 | `EmailHandlerTest` (`Build_ProducesTheMixedRelatedAlternativeStructure` بدون شبکه، `Send_Disabled_DropsTheMessageWithoutNetworkAccess`، دو تست پیام‌های `InvalidOperationException` — رگرسیون باگ copy-paste پیام خطا)، `EmailOptionsValidationTest` (disabled معتبر / enabled نیازمند Address+Password+Host)، `EmailServiceTest.EveryEmail_CarriesAPlainTextViewOfTheSameContent` + `SendConnectionEmail_PlainTextView_StripsMissingSections` (حذف بخش در view متنی) + `UserValues_AreHtmlEncodedInHtmlOnly` + `TemplatesAndLogo_ResolveFromTheApplicationBaseDirectory` + `RootedLogoPath_IsUsedAsIs` + `FaultedLogoRead_IsEvictedFromTheCache` (رگرسیون M36 روی کلیدهای حل‌شده) + `SendWelcomeEmail_RendersTemplateAndBranding` + `SendRenewalSuccessEmail_RendersPlanPriceAndExpiration` | ✅ | پنج قالب txt هم‌نام؛ encode فقط HTML و فقط مقادیر کاربر؛ ریشه‌ی مسیر = `AppContext.BaseDirectory` |
| stamp-after-success هشدار پایان پلن + rate-limit بازیابی رمز + هوک‌های ایمیل خوش‌آمد/تمدید | docs/14 M43–M44، M47 | `AccountMonitoringServiceTest.NotifSendServices_OneSendFails_OnlySuccessfulAccountsAreStamped` (حساب شکست‌خورده stamp/History نمی‌گیرد، بدون throw) + `_NoReachableChannel_NoStampNoSendNoHistory` + `_UserOptOut_NoSendNoStampNoHistory` (opt-out کاربر) + `_RecentlyWarnedAccount_IsSkippedByTheThrottle`؛ `AuthAppTest.ForgetPassword_FourthRequestWithinTheHour_ReturnsNeutralMessageWithoutSending` + `_WorksPerEmailAddress` + `_UnknownAccount_ConsumesRateLimitSlots` (شمارش قبل از lookup — ضد enumeration oracle) + `_NewRequest_ExpiresThePreviousCodes` + `_EmailFailure_ThrowsUserExceptionButKeepsTheCode`؛ `ResetPassRepositoryTest.ExpireActiveCodes_ExpiresOnlyTheActiveCodesOfThatAccount` (DB واقعی، پکیج `Data/Sql/ResetPassExpire`)؛ `AuthAppTest.Register_WithEmail_SendsWelcomeEmail` + `_WithoutEmail_SendsNoWelcomeEmail` + `_WelcomeEmailFailure_DoesNotFailTheRegistration`؛ `PlanApplicationTest.Renewal_SendsConfirmationEmailToThePlanOwner` (گیرنده = صاحب پلن نه پرداخت‌کننده) + `_EmailFailure_DoesNotFailTheCommittedRenewal` | ✅ | شکست هر ایمیل فقط `Log.Error`/`Log.Warning`؛ ثبت‌نام/تمدید commit شده هرگز باطل نمی‌شود |

### docs/21 (کانال تلگرام)

| تصمیم | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| کانال خنثی + دیسپچ runtime (D1): امضاهای `AccountEntity`-محور، `Supports`، keyed DI با کلید `SocialChannel`، `NoopSocialMediaService` برای None | docs/21 §design | `SocialMediaServiceMoq` (رفتار `Supports` = تلگرام واقعی: کانال روشن ∧ chat متصل)؛ تست‌های تقدم زیرین آن را از هر دو طرف مصرف می‌کنند | ✅ | گیت‌های `#if SOCIAL` حذف شدند؛ خطای «کانال ثبت‌نشده» fail-fast در دیسپچر |
| فلوی اتصال chat (D2): کد یک‌بارمصرف Base64Url + دیپ‌لینک، مصرف اتمیک، ایندکس یکتای فیلترشده، انقضای کد ۱۵ دقیقه | docs/21 D2 | `TelegramLinkHandlerTest` (`ValidCode_ConsumesAndConnectsTheChat`، `ExpiredCode_IsRejectedWithoutBinding`، `ReplayedCode_IsAlreadyConsumed`، `UnknownCode_GetsTheInvalidReply`، `DeactivatedAccount_IsNotBound`، `DuplicateChat_ExplainsTheConflict` با `SqlException` واقعیِ ساخته‌شده با reflection برای فیلتر catch، `StartWithoutCode_RepliesWithThePortalHelp`، `NonStartTraffic_IsIgnoredSilently` [۳ حالت])، `TelegramLinkRoundTripTest` (روی DB سناریو با ریپو/ایندکس واقعی: صدور→retire→اتصال→replay→قطع + رد چت تکراری توسط ایندکس + خطای خام 2601/2627 از ریپو) | ✅ | seed پکیج `Data/Sql/TelegramLink`؛ فقط transport بات فیک شده |
| endpointهای پروفایل: ساخت کد/وضعیت/قطع با `target`، دیپ‌لینک سمت سرور، عدم صدور کد برای حساب متصل | docs/21 §Portal | `AccountAppTest` (`CreateTelegramLink_IssuesAOneTimeCodeAndRetiresThePendingOnes` با ادعای سقف ۶۴ کاراکتر، `CreateTelegramLink_AlreadyConnected_ReturnsTheStateWithoutACode`، `GetTelegramLinkStatus_ReflectsTheChatBinding`، `DisconnectTelegram_ClearsTheChat`) | ✅ | |
| آداپتور تلگرام: مقصد درست (chat حساب برای هشدارهای کاربر / OperationsChatId برای اپراتور)، no-op بدون chat | docs/21 §OutSource | `TelegramMediaServiceTest` (۷ تست: `Supports_RequiresAConnectedChat`، `SendResetPasswordLink_GoesToTheAccountChat`، `UserAlertsWithoutAChat_AreSilentNoOps`، `InvalidPasswordAlert_GoesToTheAccountChat`، `FinishServiceAlert_CarriesThePlanDetails`، `NewUserRegistrationAlert_GoesToTheOperationsChat`، `AlarmServerCapacity_GoesToTheOperationsChat`) | ✅ | |
| ForgetPassword با username (D4–D6): کد به chat متصل، fallback ایمیل معتبر، پاسخ خنثی، limiter مشترک | docs/21 D4–D6 | `AuthAppTest` (`ForgetPassword_UsernameWithSocialChat_SendsTheCodeToTheChat` + ایمیل دست‌نخورده + کدِ ذخیره‌شده، `ForgetPassword_UsernameWithoutSocialChat_FallsBackToEmail`، `ForgetPassword_UsernameWithoutAnyChannel_StaysNeutral` بدون هیچ کد/ارسالی، `ForgetPassword_Username_SocialSendFailure_ThrowsButKeepsTheCode`، `ForgetPassword_UsernameLimiter_ConsumesSlotsLikeEveryInput`، `ForgetPassword_NeitherEmailNorMobile_IsTreatedAsUsername_NeutralAnswer` [بازنویسی تست قبلی invalid-input]) | ✅ | |
| social-first + بهترین‌تلاش (D5): هشدار پایان پلن به chat متصل بر ایمیل مقدم است؛ شکست یک ارسال batch را نمی‌شکند؛ آلارم ظرفیت و هشدارهای ثبت‌نام/پسورد اشتباه best-effort | docs/21 D5 | `AccountMonitoringServiceTest` (`NotifSendServices_TelegramFirst_WinsOverEmailAndStamps` + حفظ stamp/History، `NotifSendServices_OneSocialSendFails_OnlySuccessfulAccountsAreStamped`)، `AuthAppTest` (`CheckUserPassword_InvalidPassword_SendsTheSocialAlertWithTheAccount`، `CheckUserPassword_SocialAlertFailure_DoesNotBreakTheLoginAnswer`، `Register_SocialAlertFailure_DoesNotFailTheRegistration`) | ✅ | |
| گارد secretهای تلگرام: فیکسچر توکن کامیت‌شده در پروداکشن بلاک + الزام کامل بودن کانفیگ وقتی `Channel == Telegram` (بدون حساسیت به کانال خاموش) | docs/19 §2، docs/21 §فعال‌سازی | `ProductionSecretsGuardTest` (`Validate_ProductionWithAllCommittedTestValues_ThrowsNamingEveryKey` گسترش‌یافته، `Validate_ProductionWithTelegramChannelButIncompleteConfig_ThrowsNamingTheMissingKeys`، `Validate_ProductionWithTelegramChannelOff_IgnoresTheChannelBlock`) | ✅ | |
| فرانت کارت اتصال + ورودی username بازیابی | docs/21 §Frontend | `register.component.spec.ts` (edit mode با ActivatedRoute stub: بارگذاری وضعیت، صدور کد → دیپ‌لینک + کد، قطع اتصال با DELETE)؛ `forgot-password.component.spec.ts` (پذیرش username بدون اعتبارسنجی ایمیل + بلاک ورودی خالی) | ✅ | primitive جدید `remove` (DELETE) در `HttpClientHandler` |

### احراز هویت بی‌نام: کپچای خودمیزبان + rate-limit تفکیک‌شده (پلن `.kilo/plans/1792342359013-self-hosted-captcha-auth.md`)

| تصمیم | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| چرخه تولید→اعتبارسنجی کپچا (PNG data-url، charset بدون حروف مبهم، یکتایی id/تصویر، case-insensitive + trim، single-use حتی با کد درست، مصرف با کد غلط/فاقد، انقضای TTL) | پلن §۲، §۷ | `CaptchaServiceTest` (۱۴ تست: `New_IssuesPngDataUrl_AndCachesCharsetAnswer`، `New_EveryIssueIsUnique`، `Validate_AcceptsCodeCaseInsensitiveAndTrimmed`، `Validate_IsSingleUse_EvenWhenCorrect`، `Validate_WrongCode_ConsumesTheCaptcha`، `Validate_MissingIdOrCode_Fails` [۵ حالت]، `Validate_UnknownId_Fails`، `Validate_AfterExpiry_Fails`، …) | ✅ | سرویس internal — `InternalsVisibleTo("Tests")` در Portal.csproj تکمیل شد |
| گیت adaptive (آستانه/پنجره/NoteSuccess) + پارتیشن IP | پلن §۲، §۳ | `CaptchaServiceTest.IsRequired_TracksTheAdaptiveThreshold`، `NoteFailure_IsPartitionedByIp`، `NoteSuccess_WithoutFailures_IsHarmless` | ✅ | شمارنده با re-Set در هر خطا تمدید می‌شود |
| سناریوی HTTP کامل روی ۴ endpoint بی‌نام: بعد از آستانه بدون کپچا → 428؛ کد غلط → 428؛ کد درست + پسورد غلط → 401؛ کد درست + موفق → 200 و باز شدن گیت؛ مصرف single-use در سطح HTTP؛ همه‌ی endpointها گیت را می‌گیرند و هدرها را می‌پذیرند | پلن §۳–۴، §۷ | `CaptchaTest` (Factory اختصاصی با `CaptchaOptions:Enabled=true`, `FailureThreshold=2`؛ پاسخ صحیح از IMemoryCache همان host خوانده می‌شود): `CaptchaEndpoint_IssuesPngDataUrl`، `Login_AfterThreshold_RequiresCaptcha_ThenPassesWithCode`، `Captcha_IsSingleUse`، `AllFourAnonymousEndpoints_GateAfterThreshold_AndAcceptHeaders` | ✅ | سیگنال 428 = body `ApiResult.Code` روی HTTP 200 (قرارداد ApiResult) |
| rate-limit per-endpoint با fallback سراسری + `AllRateLimitPolicies` | پلن §۴، §۷ | `RateLimitTest`/`PasswordEndpointRateLimitTest` بازنویسی‌شده برای bucketهای تفکیک‌شده (PermitLimit=1 per policy؛ change-ovpn همچنان policy `auth` — M34)؛ Factory مشترک همه‌ی policyها را 100000 می‌کند و `CaptchaOptions:Enabled=false` می‌گذارد | ✅ | بدون این override، کلیدهای per-policy در appsettings.json بر fallback سراسری می‌چربند |
| فرانت: سرویس کپچا + کامپوننت ورودی + فلوی 428 در ۴ صفحه + اصلاح دو URL (forget-pass / reset-pass حالت forgotten) | پلن §۵–۶، §۷ | `captcha-service.spec.ts` (نگه‌داری challenge، trim، نبودِ هدر بدون کد/چالش، clear)، `captcha-input.component.spec.ts` (fetch در init، empty/headers، refresh جایگزین چالش، placeholder از prefix صفحه)، `login.component.spec.ts` («a 428 answer reveals the captcha…»: ظاهرشدن ورودی، بلاک submit خالی، هدرهای X-Captcha-* در submit بعدی، refresh تصویر پس از خطا)، `forgot-password.component.spec.ts` (URL → `/api/auth/forget-pass`)، `change-password.component.spec.ts` (حالت forgotten → `/api/auth/reset-pass`) | ✅ | ۱۶۰/۱۶۰ سبز |

### docs/15 (پرداخت P2)

| تصمیم | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| آستانه کیف پول (زیر/بالا/خاموش) | docs/15 | `WalletThresholdServiceTest`، `WalletThresholdServiceTest.VpnSuspendedAccount_DeactivatePathIsSkipped`/`_ActivatePathIsSkipped` (قاعده فقط اکانت‌های `Active` را لمس می‌کند — بازیابی موجودی VPNِ تعلیق‌شده را روشن نمی‌کند) | ✅ | |
| `BalanceStatus.Verifying` + اعتبار آنی | docs/15 | `BillingP2Test`، `BillingApplicationTest.RegisterReceipt_TopUp_CreditVerifying` | ✅ | |
| XOR فایل/متن رسید + رد محتوا/حجم | docs/15 | `BillingApplicationTest.RegisterReceipt_Xor/ImageValidation/TextBoundary`، `ReceiptEndpointTest` | ✅ | multipart روی HTTP واقعی |
| خالص‌سازی فاکتور (WalletDeduction/Payable) | docs/15 | `BillingApplicationTest.IssuePlanInvoice_Netting` | ✅ | |

### docs/16 + docs/17 (چند-سرور / x-ui)

| تصمیم | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| dispatch مرکزی Features→کلید keyed DI | docs/16 D2، docs/17 §6 | `AccountRadiusTest`، `SessionRadiusTest`، `XuiAccountSyncTest` | ✅ | |
| قرارداد API x-ui (v1.12/v1.14) | docs/17 §4 | `XuiApiTransportTest` | ✅ | probe زنده‌ی vm06/vm08 اجرا شد (۱۴۰۵/۰۷/۲۴؛ ۱۴/۱۴ PASS هر دو، بدون ری‌استارت — Simorgh `docs/methods/xui-panel-api-probe.md`) |
| sync اکانت x-ui + enforcement سمت سرور (Encoding A) | docs/17 D13، D16 | `XuiAccountSyncTest.SyncUserAndActive_*` (پوش تجمعی `StackedRenewal`/نامحدود `UnlimitedStack` + fallback سطرهای پیش از D16 در تست‌های قبلی) | ✅ | totalGB = `CumulativeTrafficLeft ?? TrafficLimit ?? 0`؛ انقضا از ستون ذخیره‌شده |
| حسابداری ترافیک x-ui (دلتا/epoch/گپ/ردیف صفر/یکتایی SessionId) | docs/17 D5–D8 | `XuiTrafficAccountingTest` | ✅ | |
| sole-writer: ردیف‌های TrafficData + ستون‌های Renewal در یک tx | docs/17 D14 | `XuiTrafficSyncTest` | ✅ | rollback هر دو را برمی‌گرداند |
| wiring کلکتور (Quartz، قفل realm، کلاینت ناشناس) | docs/17 §6 | `XuiTrafficCollectorTest`، `XuiTrafficSyncTest.UnknownClient_IsSkippedAsReconciliationSignal` | ✅ | |
| خوانش تصمیم per-dataplane | docs/17 D14، D16 | `PlanUsageReaderTest` (`XuiRealmWithStackedRenewal_ReadsCumulativeColumns`، `XuiRealmWithPreD16Renewal_FallsBackToTheView`، `XuiRealmUnlimitedTraffic_TrafficLeftIsNull`، …) | ✅ | x-ui→`CumulativeTrafficLeft − TrafficUsed` و انقضای ستونی (سطرهای پیش از D16 → ویو)، Radius→ویو |
| انباشته واقعی پلن‌ها در تمدید (زمان/ترافیک/تعویض realm/کلمپ منفی/خرید اول) | docs/17 D16 | `RenewalStackingTest` (شش سناریوی لبه؛ renewal رسیده به پوش radius همان مقادیر تجمعی را حمل می‌کند) | ✅ | ستون‌ها یک‌بار در `PlanApplication.Renewal` فریز می‌شوند — تنها نویسنده |
| `GetActiveConnections` x-ui از onlines | docs/17 §4 | `XuiSessionSyncTest.GetActiveConnections_*` | ✅ | |
| sync اکانت UserManager (پروفایل/محدودیت از renewal) | docs/06، docs/16 §1 | `SyncUserActiveInactiveRemoveTest`، `AccountRadiusTest.SyncUserAndActive_*` | ✅ | |
| شاخه‌بندی قابلیت دیتاپلین (kind/گیت‌های OVPN و تغییر رمز/پرچم چرخش) | docs/16 §3/§6، docs/17 §5.5 | `ServerManagementServiceTest.GetDataPlaneInfo_*`، `VpnApplicationTest.ChangeVpnPassword_*BlockedByGate`/`SendCertEmail_*BlockedBeforeCertificateBuild`، `PlanApplicationTest.GetPlanInfo_*`، `dashboard.component.spec.ts` (رندر دکمه‌ها بر اساس قابلیت) | ✅ | گیت قبل از ساخت گواهی؛ realm تهی = semantics هر-NAS/بدون-xui؛ `SupportsCredentialRotation` در هر دو لایه‌ی info/plan-info |
| گیت ظرفیت تعداد realmهای x-ui (T4: ستون `Server.MaxClients` با خالی/صفر=خاموش، سقف مؤثر = min سقف‌های مثبت سرورهای x-ui فعال، شمارش = اکانت متمایز با بالاترین `RowNumber` گذرنده، fan-out پلن بدون realm، انتخابپذیری = باند>0 ∧ شمارش<سقف، آلارم `Low Capacity (clients)` در ≥۹۰٪) | docs/16 §3 T4 (T4-A..T4-E)، docs/17 §5.6 | `ServerManagementServiceTest` (`GetAvailableRealm_XuiClientGateFull_LowestDemandRealmSkipped`، `GetAvailableRealm_TwoXuiServers_TheSmallerCapBinds`، `GetAvailableRealm_UncappedXuiServer_GateStaysOff` [null و 0]، `GetRealmsCapacityState_UnrestrictedAccount_CountedInEveryRealm`، `GetRealmsCapacityState_StackedRenewals_CountedOnceInNewestPassingRealm`، `GetRealmsCapacityState_ExpiredNewestRenewal_CountsTheOlderPassingRow`، `CheckUserServerBalance_ClientCountAtCapThreshold_Alarms`/`_BelowCapThreshold_NoClientAlarm`)، `ServerCrudApplicationTest.CreateAndUpdate_MaxClientsRoundTrip` + ردیف 0 در ماتریس، `servers.component.spec.ts` (فیلد فقط x-ui، پاک‌شدن با برداشتن بیت) | ✅ | باند نرم ماند (T4-A)؛ `RowNumber` به `PlanStateEntity`/ویوی `PlanState` اضافه شد؛ خوانش پلن‌ها بین تقاضا و شمارش مشترک شد |

### docs/20 (نقش‌ها و پنل مدیریت)

| تصمیم | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| کش AccessService با نقش: bypass ادمین + InvalidateTargetArea + عدم bypass مدیر | docs/20 فاز ۱-۴ | `AccessService.CheckAccess_AdminRole_*`/`CheckAccess_ManagerRole_*`/`InvalidateTargetArea_*`، `CheckAccess.Test_admin` (سناریو، seed با Role=2) | ✅ | |
| claim نقش در JWT + Role/AllowSelfService در پاسخ لاگین + اعمال policyها | docs/20 فاز ۱-۳/۵ | `RolesAuthorizationTest.Login_AdminTokenCarries*` (claim در توکن)، `RolesAuthorizationTest.GetUser_ReportsRoleAndSelfServiceFlag` (seed اسکریپت: Role=2/UserType\|=4 + پرچم مستقل D11)، `RolesAuthorizationTest.Policies_EnforceRoleMatrix*` (توکن validate شده + principal سنتزی مدیر: Manager شامل Admin، User هیچ‌کدام)، `AuthAppTest.CheckUserPassword_WithTargetArea` | ✅ | مصرف policy روی endpoint واقعی در فازهای ۴/۵ |
| گیت ثبت‌نام (D11) + پرچم مستقل + endpoint عمومی app-config | docs/20 D11 | `AuthAppTest.Register_DisabledByConfig_*`/`Register_Success_SetsSelfServiceFlag`، `BasicsApplicationTest.GetAppConfig_MirrorsRegistrationFlag` (هر دو قطب فلگ)، `AppConfigTest` (عمومی بدون توکن + reflect فلگ)؛ سناریوها با `AllowRegistration=true` صریح در Factory | ✅ | مسیر 403 در سطح unit |
| تفکیک payer در مسیر پول (D4) + گارد وابسته روی endpointهای مالی self (D6) | docs/20 فاز ۲ | `PayerSeparationTest` (سناریو: وابسته → 403 کاربرپسند روی هر ۶ endpoint، ادمین-پرداخت‌کننده → فاکتور/دبت/موجودی روی ادمین + پلن روی target + ری‌پلی settle، pay سالم)، `PlanApplicationTest.Renewal_DependentSelfUser_IsForbidden`/`Estimate_DependentSelfUser_IsForbidden`/`Renewal_OnBehalf_IssuesInvoiceAndReadsBalanceOnPayer`/`Renewal_Action` (دبت روی payer، renewal روی target)/`Renewal_Action_PayerNotFound`، `BillingApplicationTest.SelfFinancialEndpoints_DependentSelfUser_AreForbidden`/`GetInvoice_OnBehalfView_DoesNotLeakTargetWallet`/`SettleWallet_OnBehalf_DebitsPayerAndRenewsTarget`، `SettlementRollbackPayerSeparationTest`، `renewal.component.spec.ts` (ناوبری پرداخت بدون target در نمای «از طرف») | ✅ | فاکتورهای pending قبل از deploy بدون migration ری‌پلی می‌شوند |
| تراکنش شفاف ادمین + تاریخچه کیف پول (D5: ردیف Completed با ActorId، موجودی منفی مجاز، مدیر به کیف زیر دست دسترسی ندارد) | docs/20 فاز ۳ | `WalletApplicationTest` (unit: قرارداد ردیف Completed+ActorId+trim توضیح، دبت منفی، ۵ اعتبارسنجی، ادمین-بدون-فلگ 403، ماتریس دسترس self/وابسته/کاربر/مدیر/ادمین، صفحه‌بندی newest-first + resolve نام Actor)، `WalletAdminTransactionTest` (سناریوی HTTP: adjust ادمین → ردیف+موجودی و دبت منفی، policy ادمین → 403 کاربر عادی، اعتبارسنجی 400، ماتریس history شامل مدیر-روی-زیردست که از LoadJobContext می‌گذرد و 403 می‌گیرد، صفحه‌بندی سمت سرور ۲۳→۲۰+۳ و page=0 → 400) | ✅ | |
| لیست و مدیریت کاربران (ماتریس §۳: اسکوپ مدیر، اجبار Owner=self، D9 تک‌ادمینی/بدون ارتقا/عدم خود-عملیات، گارد تنزل مدیر با زیر دست فعال، attach/detach + چرخه، فلگ self-service، D13 فعال/غیرفعال) | docs/20 فاز ۴ | `UserManagementApplicationTest` (unit: اسکوپ لیست، اجبار مدیر، گاردهای create ادمین، گارد تنزل، attach/detach/چرخه، InvalidateTargetArea، ماتریس set-password/set-status/set-self-service)، `UserManagementTest` (سناریوی HTTP: policy، جستجو/صفحه‌بندی/غیرفعال‌ها، ماتریس create شامل 409 و لاگین با پسورد اولیه، بازنشانی، ارتقا/تنزل، attach→مدیر همان نشست می‌مدیرد و detach→403، فلگ self-service که گیت D6 کیف را live باز/بسته می‌کند، set-status→deactivate→401/activate)، `AccountRepositoryTest.SearchAccounts_ScopeSearchPagingAndPlanJoin` (DB واقعی: OUTER APPLY روی TotalPlanState با `TrafficLeft=25-5`، اسکوپ/جستجو/صفحه‌بندی، HasActiveSubordinate) | ✅ | رابطه actor→target در management endpoints زنده از DB چک می‌شود، نه از کش لاگین |
| غیرفعال‌سازی دووضعیتی (D-A تا D-D): enum `AccountStatus` جایگزین `IsActive`، نگاشت portal-accessible (`in (0,1)`) / VPN-valid (`=0`)، گارد تمدید در VpnSuspended، sync فوری best-effort + آشتی sweep ساعتی، قاعده کیف پول فقط روی Active، pre-filter `JobInterval` | docs/20 D13 بازنگری + D-A تا D-D، docs/11 §7b | `PlanApplicationTest.Renewal_VpnSuspendedTarget_IsRefusedWithFriendlyMessage` (D-A)، `UserManagementApplicationTest.SetStatus_VpnSuspension_PushesImmediateRadiusDeactivation`/`.SetStatus_ImmediateSyncFailure_DoesNotFailTheOperation`/`.SetStatus_SameStatus_ShortCircuitsWithoutSideEffects`، `AccountRadiusTest.DeactivateInvalidRadiusUsers_NonActiveStatus_DropsFromValidSet`، `WalletThresholdServiceTest.VpnSuspendedAccount_*IsSkipped`، `UserManagementTest.SetStatus_Guards` (سناریو: تعلیق VPN→لاگین OK، deactivate→401، فعال‌سازی مجدد)، `AccountMonitoringServiceTest` (آبشار ۴۰روزه → `SetDeactivated`)، `AccountRepositoryTest` (ستون `Status` + `SetDeactivated` روی DB واقعی) | ✅ | ستون `Status TINYINT` در `Account.sql`/`TotalPlanState`/`PlanState` view؛ seedهای تست (`account.json`/`plan-state.json`/`InsertAccount`) همگی `Status` |
| CRUD سرورها (D8: لیست فقط اسکالر + has-config بدون هیچ راز، protect سمت سرور + نوشتن بدون خواندن، 409 تکراری IP/نام/دامنه، toggle دوجهته‌ی فعال) | docs/20 فاز ۵ | `ServerCrudApplicationTest` (unit: ماتریس اعتبارسنجی IPv4/طول‌ها/پهنای‌باند/سقف تعداد/OS/بیت‌های Features/realm، 409 تکراری، update بدون config → blob ثابت / با config → جایگزینی، toggle، ادمین-در-عمق، کانفیگ >4000، `CreateAndUpdate_MaxClientsRoundTrip`)، `ServerCrudTest` (سناریوی HTTP: policy ادمین → 403 کاربر/مدیر، پاسخ خام لیست بدون ساختار کانفیگ/`protected:`/راز، create → blob محافظت‌شده + round-trip راز، 400ها/409ها، update بدون/با config، set-active) | ✅ | مرز docs/16 رعایت شد — بدون دست‌زدن به keyed dispatch/adapter/`ServerFeature`؛ `MaxClients` اختیاری ≥1 (T4) |
| فرانت نقش‌دار فاز ۶: گاردهای نقش + گیت وابسته (D6) در UI، منوی نقش‌محور، صفحات users/servers/wallet، انتخابگر «از طرف» ادمین از user list، گیت ثبت‌نام با app-config (D11) | docs/20 فاز ۶ (گام‌های ۱۹–۲۳) | `role-guards.spec.ts` (پذیرش/رد managerGuard/adminGuard/selfServiceGuard + redirect لاگین روی خطای user)، `user-servcie.spec.ts` (گترهای نقش، رد `setExternalTarget` برای غیرادمین، target آزاد ادمین)، `default-header.component.spec.ts` (سه پروفایل منو: وابسته بدون لینک مالی / مدیر فقط کاربران / ادمین کامل + انتخابگر بدون زیر دست)، `users.component.spec.ts` (ستون‌های نقش/مالک فقط ادمین، جستجو/صفحه‌بندی سمت سرور، payload ساخت مدیر/ادمین، گارد فرم، set-status دو toggle (تعلیق VPN 0↔1، غیرفعال/فعال‌سازی کامل →2/→0 + no-op در وضعیت 2)، self-service/owner/password، عمل از طرف مدیر/ادمین/رد Deactivated)، `servers.component.spec.ts` (لیست، toggle، create با config parse-شده و بیت‌ها، رد JSON خراب، update بدون config)، `wallet.component.spec.ts` (history با/بدون target، رد ورودی ناقص، payload adjust)، `user-selector-modal.component.spec.ts` (تغذیه‌ی ادمین از `/api/user/list`)، `app-config-service.spec.ts`، گیت ثبت‌نام در `login.component.spec.ts` (پنهان‌سازی CTA) و `register.component.spec.ts` (پیام بسته + عدم POST) | ✅ | مسیرهای `/renewal`+`/payment` با `selfServiceGuard`، `/users` با managerGuard، `/servers`+`/wallet` با adminGuard — آینه‌ی ماتریس §۳ سمت سرور |

### دیپلوی (docs/19)

| نیازمندی | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| جریان دیپلوی دیتابیس + ساخت ادمین اول (batch ترتیب‌دار idempotent، گارد manifest فایل‌های فهرست‌نشده/غایب، گاردهای DeployCli: username تکراری بدون بازنویسی + تک‌ادمینی D9 + exit codes، `verify` روی هش PBKDF2، پارس `--secrets` بدون نشت به لاگ) | docs/19 §ج، docs/20 D9، `Database/LocalDatabase/README.md` | `scripts/deploy.sh db\|admin` + `DeployCli` (`create-admin`/`verify`، `AdminSeeder`/`DeploySecrets` static برای قابلیت تست) | ⚠️ | بدون تست خودکار — validation دستی (§۵-۷): اجرای smoke کامل روی MSSQL محلی ۱۴۰۵/۰۷/۲۴ (batch تازه، `Role/UserType=(2,4)`، idempotency، هر دو گارد، verify، لاگ بدون پسورد) |

### لایه‌ی HTTP و فرانتند

| نیازمندی | مرجع | تست‌ها | وضعیت | یادداشت |
|---|---|---|---|---|
| JWT 401 / rate-limit 429 / multipart رسید (HTTP واقعی) | docs/08 | `JwtAuthTest`، `RateLimitTest`، `ReceiptEndpointTest` | ✅ | از طریق `ProgramLevelInitializer.Factory` (WebApplicationFactory) |
| سرویس‌های کلیدی فرانتند | docs/10 | `http-client.spec.ts`، `user-servcie.spec.ts`، `payment-service.spec.ts`، `money-printer.spec.ts`، `validators.spec.ts`، `role-guards.spec.ts`، `app-config-service.spec.ts`، `captcha-service.spec.ts` | ✅ | |
| کامپوننت‌های رفتاری فرانتند (۱۵) | docs/10 | dashboard، traffic-chart، payment، payment-modal، renewal، login، register، history، forgot-password، change-password، user-selector-modal، users، servers، wallet، captcha-input (هرکدام `.spec.ts`) | ✅ | به‌علاوه: app، home، toast، header، footer |

## ۵. شکاف‌های شناخته‌شده (صداقت کامل — جایگزین `[ ]`های TestPlan قدیمی)

1. **`SessionRadiusSyncUserManagerService` (MikrotikRadius)** — بی‌تست مستقیم: `CloseConnectionByUsername` (۰٪)، `GetActiveConnections` (۷۸٪)، `CloseConnectionBySessionId` (۸۶٪ — پوشش فقط از مسیر `ConnectionApplicationTest` با mock).
2. **ریپوهای Level 3 بی‌تست مستقیم**: Wallet، ResetPass، Price، TrafficData، PlanState. (Account/History/DatabaseGlobal/Billing P0/P2 تست مستقیم دارند. پوشش غیرمستقیم از سناریوها: Wallet/ResetPass/Price/Invoice ۱۰۰٪ ولی TrafficData ۴۳٪، PlanState ۶۴٪. Realm از مسیر `RealmCapacityOverrideTest` — `SetCapacityOverride` + ظرفیت مؤثر روی DB واقعی — پوشش مستقیم گرفت. **Server از مسیر `ServerRepositoryTest` (بازبینی دوم ۲۰۲۶-۱۰-۱۸) پوشش مستقیم گرفت**: چهار lookup داغ روی DB واقعی — ریشه‌ی H21–H23. **Renewal از مسیر `RenewalRepositoryTest` (اصلاح M31) پوشش مستقیم گرفت**: lookup آخرین renewal با window-function روی DB واقعی.**)
3. **نقص باز `POST /api/billing/pay` → ۵۰۰** — **حل شد (فاز ۲ پنل ادمین، ۲۰۲۶-۱۰-۱۷)**: `IssueTopUp` اکنون payer را از `ISelfServiceGuard.ResolvePayingActor()` می‌گیرد (گارد وابسته D6 را هم اعمال می‌کند)؛ پوشش: `PayerSeparationTest.Pay_IssuesTopUpInvoiceForActor` + `BillingApplicationTest.IssueTopUp_*`/`SelfFinancialEndpoints_DependentSelfUser_AreForbidden`.
4. **`ServerManagementServiceTest.CheckUserServerBalance_Check` = [SKIP]** — **حل شد (T3، ۱۴۰۵/۰۷/۲۴)**: با `SocialMediaServiceMoq` و متریک «جمع مصرف ۳۰روزه» فعال شد و سوئیت دیگر تست اسکیپ‌شده ندارد.
5. **کنترلرهای کم‌پوشش**: `ConnectionController` (۰٪)، `ResultHandlerController` (۴۷٪). (`VpnController` با تست سناریویی `download` پوشش گرفت.)
6. **هندلرهای I/O واقعی ServerBridge** (شبکه/SSH واقعی): `SshHandler`، `SshConnection`، `ApiHandler`، `EmailHandler` — همه ۰٪؛ توسط mock (`MockServerBridge/ConfigurableSshHandlerMoq`) جایگزین شده‌اند.
7. **جریان دیپلوی (`scripts/deploy.sh` + `DeployCli`)** — فقط validation دستی، بدون تست خودکار (ردیف §۴-دیپلوی): smoke روی MSSQL محلی با اجرای همان batch ساختار + `create-admin`/`verify` انجام شد؛ خودِ `sqlcmd` روی آن ماشین نصب نبود، پس مسیر sqlcmd (fail-fast `-b`، تایماوت، codepage) هنوز روی هاست واقعی آزمایش نشده — اولین اجرای پروداکشن با دقت دنبال شود (docs/19 §هـ).
8. **تداخل موازی کلاس‌های سناریو روی `FastBypass_DbScenario` مشترک (۲۰۲۶-۱۰-۱۷، از قبل موجود)** — هر کلاس سناریو Factory خودش را دارد که در ساخت، همان یک دیتابیس را DROP/CREATE می‌کند؛ با اجرای موازی کلاس‌ها (پیش‌فرض xUnit) بازسازی وسطِ اجرای کلاس دیگر رخ می‌دهد و خطاهای تکراری‌شدن موبایل/یوزرنیم *متغیر و زمان‌بندی‌محور* می‌سازد (مشاهده‌شده: `UserManagementTest.SetOwner_AttachDetachAndGuards` به‌طور پیوسته و یک‌بار `ConnectAndCheck.Test` در اجرای کامل سوئیت؛ هر دو در اجرای تنها کاملاً سبز؛ روی baseline بدون تغییرات فاز ۵ پنل هم بازتولید شد — ارتباطی با آن تغییرات ندارد). درمان کاندید: `[Collection]` مشترک برای کلاس‌های `Facts/Scenario` یا خاموش کردن `parallelizeTestCollections` — کار آتی جدا.
9. **لکه‌ی اشتراک `OnSend` در `EmailHandlerMoq` (۲۰۲۶-۱۰-۱۸، pre-existing روی baseline ویندوز)** — `HttpHandler.ForgetPass` به `EmailHandlerMoq.OnSend` لامبدای خواندن بدنه‌ی ایمیل وصل می‌کند اما هرگز آن را detach نمی‌کند؛ دومین ایمیل forget-pass در همان host، لامبدای کهنه را روی `MailMessage` ای اجرا می‌کند که بعد از send قبلی dispose شده → «Stream was not readable» → ۵۰۰. `RegisterAndBuy.Simple` تنها سبز است و `Twice` به‌طور قطعی می‌شکند (بازتولید روی baseline بدون تغییرات D16). درمان: unsubscribe در انتهای `ForgetPass` یا reset رویداد پیش از اشتراک — کار آتی جدا.

نوشتن تست برای این فهرست **کار آتی جدا** است (خارج از دامنه‌ی سند حاکمیت).

## ۶. خارج از دامنه‌ی رسمی (بدون قضاوت پوشش)

- **adapter RadiusDesk** (کل پروژه ۲.۵٪) — تعویق L8 در docs/14 تا فعال‌سازی مسیر RadiusDesk.
- **واتساپ** — آداپتور آینده روی معماری کانال خنثی (docs/21 D1)؛ کلید `SocialChannel.WhatsApp` رزرو شده ولی آداپتور ثبت نشده و `ForgetPassword` مسیر موبایل را تا آن زمان با پیام کاربرپسند رد می‌کند.
- `MikrotikDirectService.SetOVpnCertificate` — `NotImplementedException` (تکثیر گواهی به NASهای بعدی realm).
- **پنل Admin** — اسکلت خالی (`Program.cs` + `AddAdminServices` خالی)؛ عدد پایین طبیعی است.
- **Google Auth** — فاز ۵ طبق `README.md`.
- **Tik4net / tik4net.objects** — وندور.
- **`TelegramUpdatePoller` / `TelegramBotClient`** — حلقه‌ی long-poll و ترنسپورت HTTP بات پشت `ITelegramBotClient` فیک‌شده تست می‌شوند؛ خودشان بدون شبکه اجرا نمی‌شوند (مثل هندلرهای I/O در §۵.۶).

## ۷. قاعده‌ی حاکمیت

- هر PR که **تست اضافه/تغییر می‌دهد** یا **جریان/تصمیم جدیدی می‌سازد**، ردیف مربوط در §۴ همین سند را **در همان PR** به‌روز می‌کند (ستون تست‌ها/وضعیت) و در صورت لزوم شکاف تازه به §۵ اضافه می‌شود.
- **مبنای پوشش (§۳)** به‌صورت موردی و هنگام تغییرهای ساختاری (پروژه‌ی جدید، تغییر پشته‌ی تست) بازسنجی می‌شود؛ نه در هر PR.
- شکاف‌های پوشش فقط اینجا (§۵) دنبال می‌شوند — نه در `Architecture/TODO.md` (آنجا محل کارهای تصمیم‌شده است).

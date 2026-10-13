# docs/14-defects-audit.md — Defect & performance audit (not in TODO)

> **English summary**: Full audit of implementation bugs, correctness issues, security weaknesses, and performance problems that are **not** listed in `Architecture/TODO.md` or `docs/12-current-state.md`. Every finding was verified against the actual code and carries a `file:line` reference, a severity, and a fix direction. Items are grouped by severity (C=critical, H=high, M=medium, L=low) and mapped to execution phases (P0–P5) at the end. Checkboxes track fix progress. Date: 1405/07 (2026-10).
>
> **فارسی**: گزارش کامل ایرادهای پیاده‌سازی، درستی، امنیت و پرفورمنسی که در TODO نیستند. هر مورد با ارجاع `file:line`، شدت و مسیر اصلاح ثبت شده و در انتهای سند به فازهای اجرایی P0–P5 نگاشت شده است. چک‌باکس‌ها برای پیگیری پیشرفت اصلاح هستند.

| شدت | تعداد |
|---|---|
| Critical | 6 |
| High | 20 |
| Medium | 28 |
| Low | 11 |

مجموع: **۶۵ مورد**.

---

## CRITICAL

### [x] C1. کلید اشتباه در Merge ترافیک → درج تکراری در هر سیکل sync
- **مکان**: `Backend/Application/Management/ServerManagementService.cs:196-197` (کلید `SessionId`) در برابر `:223-224` (lookup با `traffic.NasIpAddress`).
- **شرح**: دیکشنری مقصد با `SessionId` کلید می‌خورد ولی جستجو با `NasIpAddress` انجام می‌شود؛ چون هیچ‌وقت برابر نیستند، session های باز هرگز آپدیت نمی‌شوند و در هر سیکل `TrafficDataEntity` جدید درج می‌شود → دوبل‌شمارش ترافیک (اتمام زودهنگام پلن‌ها)، نقض `UK_TrafficData_NasId_SessionId`، رشد بی‌نهایت `FetchOpen`.
- **اصلاح (P1)**: lookup با `traffic.SessionId`؛ مقصد Merge از `FetchOpen` به `Fetch(ازِ ایندکس سینک - 1s)` تغییر کرد تا session های بسته‌شده هم match شوند (وگرنه هر سیکل دوباره درج می‌شدند و با unique index جدید job کرش می‌کرد)؛ در مسیر آپدیت `EndSession` هم stamp می‌شود و build دیکشنری مقصد در برابر ردیف‌های تکراری تاریخی defensive است (نگه‌داشتن max Id). اسکریپت پاکسازی: `Database/LocalDatabase/TrafficDataDedup.sql` (dedup-only، اجرای دستی).

### [x] C2. Bulk Save/Delete از تراکنش DB عبور نمی‌کند — تراکنش صورتحساب بی‌اثر است
- **مکان**: `Backend/Infrastructure/Database/EditableRepository.cs:43-51,62`؛ متد کمکی `CheckTransactionBulk` در `:91-104` تعریف شده ولی **هرگز صدا زده نمی‌شود**.
- **شرح**: `BulkUpdateAsync/BulkInsertAsync/BulkDeleteAsync` بدون `AttachToTransaction` اجرا می‌شوند؛ در نتیجه `BillingApplication.GenerateInvoice` (`Backend/Application/Billing/BillingApplication.cs:151-250`) که wallet/renewal را داخل `BeginTransaction/Commit` می‌نویسد عملاً auto-commit می‌نویسد و rollback هیچ‌چیز را برنمی‌گرداند → ردیف‌های نیمه‌کاله wallet/renewal در صورت خطای وسط flow.
- **اصلاح**: فراخوانی `CheckTransactionBulk` در overload های collection (هم save هم delete).

### [x] C3. جریان پرداخت فرانتند کاملاً خراب است (۴ ایراد مستقل) — **حل شد در P2**
- **مکان‌ها**:
  - `Frontend/src/app/payment/payment-service.ts:10,16` و `Frontend/src/app/default-layout/payment-modal/payment-modal.service.ts:10` — فراخوانی `PLAN_API_URL/get-invoice|pay|payment-request` در حالی که بک‌اند در `BillingController` (route `/api/billing/...`) سرو می‌کند؛ `payment-request` اصلاً در بک‌اند وجود ندارد.
  - `Frontend/src/app/payment/payment.component.ts:51` — `isNumber(code)` روی string (خروجی `queryParamMap.get`) همیشه false → صفحه payment همیشه به dashboard redirect می‌شود.
  - `Frontend/src/app/renewal/renewal.component.ts:114` — navigate با invoice hard-code `"10"` به‌جای `result.invoiceCode`؛ فیلد `InvoiceCode` در اینترفیس فرانتند (`@models/data-model/user-plan-info.ts:22-25`) مدل نشده.
  - `Backend/Portal/Controllers/BillingController.cs:21` — `Pay([FromBody] int value)` ولی فرانت `POST {value}` (object) می‌فرستد؛ `payment.component.ts:65` خروجی عددی pay را به‌عنوان URL به `window.location.href` می‌دهد.
- **اصلاح (انجام‌شده در P2)**: `BILLING_API_URL` جدید، بدنه‌ی `PayRequest {Value, Target?}`، صفحه فاکتور بازنویسی‌شده (اقلام/کسر کیف پول/قابل پرداخت/کارت‌ها/رسید)، navigate با `result.invoiceCode` واقعی، حذف `isNumber`/`window.location.href`/قالب درگاه؛ مدل `PaymentInvoice` جدید منطبق با بک‌اند.

### [x] C4. کد فاکتور MAX+1 → تداخل بین کاربران؛ callback بین اکانت‌ها عبور می‌کند
- **مکان**: `Backend/Infrastructure/Repository/WalletRepository.cs:66-76` (تولید کد) و `:29-36` (`GetInvoice(int)` بدون فیلتر account/status)؛ مصرف در `BillingApplication.cs:99-123`.
- **شرح**: `select max(InvoiceCode)... return max+1` بدون قفل/unique؛ دو درخواست هم‌زمان کد یکسان می‌گیرند. `GetInvoice(code)` فقط با code فیلتر می‌کند → `PaymentCallback` می‌تواند wallet های کاربر دیگر را Complete و Renewal او را اجرا کند.
- **اصلاح**: ستون identity/sequence یا unique + retry، و فیلتر account در lookup فاکتور.

### [x] C5. ترتیب و idempotency های PaymentCallback → «پرداخت بدون تحویل پلن»
- **مکان**: `Backend/Application/Billing/BillingApplication.cs:110-123`؛ `Backend/Application/Plan/PlanApplication.cs:180-215` (مسیر بدون validation) در برابر `:255-263` (validation فقط در retry).
- **شرح**: اول `Status=Completed` ذخیره می‌شود بعد `Renewal` اجرا می‌شود؛ مسیر «موجودی ناکافی» فاکتور را **بدون** `RenewalValidation` صادر می‌کند → ورودی نامعتبر (مثل `days=37`) از estimate تا پرداخت عبور می‌کند و بعد از کسر پول exception می‌گیرد؛ بدون مسیر refund. callback تکراری (webhook retry) آیتم‌های Completed را دوباره اجرا می‌کند؛ `int.Parse(token)` در `:99` بدون گارد → 500.
- **اصلاح**: اجرای validation قبل از صدور فاکتور، atomic check-and-set روی status (فقط Pending→Completed)، گارد parse، و مسیر compensation.

### [x] C6. هش پسورد بدون salt و بدون KDF
- **مکان**: `Backend/Shared/Tools/HashHandler.cs:12-15`؛ استفاده در `AuthApplication.cs:35,190,216` و `AccountApplication.cs:101-102`.
- **شرح**: `SHA512.HashData` خالص؛ پسوردهای یکسان = هش یکسان؛ brute-force آفلاین بدیهی. (پسورد VPN به‌دلیل PAP باید قابل بازیابی بماند؛ پسورد پرتال نباید.)
- **اصلاح (P3)**: `PasswordHasher` جدید (`Backend/Shared/Tools/PasswordHasher.cs`) با PBKDF2-HMACSHA256 (salt تصادفی ۱۶ بایتی، ۲۱۰k iteration، فرمت versioned `$PBKDF2$v1$...`) و `Verify` با `FixedTimeEquals` + null-safe. همه فراخوانی‌ها مهاجرت کردند؛ در login برای username ناموجود هم Hash اجرا می‌شود (برابرسازی زمان). `Account.sql`: ستون `VARCHAR(200)` + هش seed جدید (پیش‌فرض: admin). سیستم راه‌اندازی نشده → بدون مهاجرت.

---

## HIGH — درستی و امنیت

### [x] H1. توکن‌های امنیتی با System.Random غیررمزی
`Backend/Shared/Tools/HashHandler.cs:8,17-26` — نمونه static مشترک `Random` (نه thread-safe، نه CSPRNG) برای کد reset پسورد (`AuthApplication.cs:119,147`)، `VpnPassword` اولیه (`:215`) و passphrase گواهی OVPN (`MikrotikRadius/Application/MikrotikDirectService.cs:115`). **اصلاح (P3)**: `RandomNumberGenerator.GetItems<char>`؛ `HashPassword` حذف شد (جایگزین: `PasswordHasher`).

### [x] H2. رقابت job های موازی روی همان ردیف‌های Account → احیای اکانت غیرفعال‌شده
`Backend/Application/Management/JobInterval.cs:38-42` — `NotifSendServices` و `InactiveAbandonedUsers` هم‌زمان روی همان `plan_state_list`؛ هر دو کل ردیف Account را با entity های stale ذخیره می‌کنند (`AccountMonitoringService.cs:65-66` و `:174-175`) → last-writer-wins می‌تواند `IsActive=false` را بازنویسی کند. **اصلاح**: concurrency token یا به‌روزرسانی ستون-محور.
**اصلاح (P4)**: به‌روزرسانی ستون-محور — `IAccountRepository.UpdateWarningTimes(ids)` (`UPDATE Account SET LastWarningTime=@now WHERE Id IN @ids`) در `NotifSendServices` و `SetInactive(ids)` (`UPDATE ... SET IsActive=0`) در `InactiveAbandonedUsers`؛ دو ستون متفاوت‌اند و race ساختاریاً حذف شد (پاراللیسم و الگوی radius/history دست‌نخورده). تست: `AccountMonitoringServiceTest.ParallelMonitoring_WarningTimeAndInactiveBothApplied`.

### [x] H3. JobInterval: sync-over-async، نشت scope، بدون جلوگیری از هم‌پوشانی
`Backend/Application/Management/JobInterval.cs:15,29-42` — `Task.WaitAll` داخل async؛ چهار `CreateScope()` بدون dispose (نشت `SqlConnection` در هر ساعت)؛ نبود `[DisallowConcurrentExecution]` و misfire policy → اجراهای هم‌پوشان. ثبت job در `Application/ServiceFactory.cs:34-49`.
**اصلاح (P4)**: هر چهار scope با `using` dispose شدند (سه scope جدا برای سرویس‌های موازی حفظ شد — repo های scoped روی یک `SqlConnection` مشترک‌اند و ادغامِ scope ها خطای MARS می‌داد)؛ `Task.WaitAll` → `await Task.WhenAll`؛ `[DisallowConcurrentExecution]` روی کلاس و `.WithMisfireHandlingInstructionFireNow()` روی trigger. (`IServiceScope` در net8.0 فقط `IDisposable` است → `using` به‌جای `await using`.)

### [x] H4. Fire-and-forget روی scoped connection مشترک
الگوی `_ = repo.Save(...)` که با کارهای awaited روی **یک** `SqlConnection` scoped موازی می‌شود: `AccountMonitoringService.cs:66,88-100,167-176`، `PlanApplication.cs:66,87,312,345`، `AuthApplication.cs:46,50,158`، `AccountApplication.cs:117,138`، `VpnApplication.cs:52,99`، `ConnectionApplication.cs:93` → خطاهای intermittent "MARS/parallel operations" و استثناهای unobserved. **اصلاح**: حذف الگو؛ await کامل یا صف پس‌زمینه با scope مستقل.
**اصلاح (P4)**: همه نقاط prod اعم از HistoryRepo/SocialMedia با await کامل جایگزین شدند (`VpnApplication`، `ConnectionApplication`، `AccountApplication`، `AuthApplication`)؛ `SshConnection.Execute` به APM (`BeginExecute/EndExecute`) با timeout بازنویسی شد (حذف `.Result`)؛ `StaticRepository` به async-lazy با پراپرتی های `Task`-returning تبدیل شد؛ warm-up بی‌اثر `Logout` در RadiusDesk حذف شد. **دو باگ خاموش که await ها آشکار کردند و اصلاح شدند**: (۱) `ForgetPassword` دو INSERT (کد بازیابی + History) را موازی روی یک connection مشترک اجرا می‌کرد → MARS — حالا ترتیبی (فقط ارسال ایمیل موازی می‌ماند)؛ (۲) endpoint بی‌توکن reset-pass باعث `JobContext.Username=null` → `GetActiveAccountId` با `ExecuteScalarAsync<int>` عدد `0` برمی‌گرداند و INSERT تاریخچه به FK می‌خورد (ردیف‌ها هرگز ذخیره نمی‌شدند) — حالا `ExecuteScalarAsync<int?>` → Issuer واقعاً NULL می‌شود.

### [x] H5. کش AccessService بدون انقضا → دسترسی باقی می‌ماند پس از revocation
`Backend/Portal/Basical/AccessService.cs:15-18` — `cache.Set($"TargetArea|{username}")` بدون TTL/Size؛ فقط در login پر می‌شود؛ غیرفعال/حذف/انتقال sub-account تا logout والد اعمال نمی‌شود؛ رشد بی‌نهایت. **اصلاح (P3)**: `AbsoluteExpirationRelativeToNow = 2h` (بیش از عمر JWT یک‌ساعته؛ چون `CheckAccess` فقط در درخواست‌های cross-target صدا زده می‌شود sliding باعث 403 وسط توکن می‌شد) + `Size = 1`.

### [x] H6. CloseConnection مالکیت session را چک نمی‌کند (IDOR درون-realm)
`Backend/Application/Connection/ConnectionApplication.cs:59-91` — realm سرور با realm target تطبیق داده می‌شود (`:78-82`) اما هرگز بررسی نمی‌شود که session_id متعلق به target باشد → هر کاربر احراز هویت‌شده می‌تواند session کاربر دیگری در همان realm را ببندد. **اصلاح (P3)**: قبل از بستن، `GetActiveConnections(realm, target)` گرفته می‌شود و session_id باید در لیست session های فعال خودِ target باشد؛ وگرنه «دسترسی غیرمجاز».

### [x] H7. تغییر پسورد VPN در DB ذخیره نمی‌شود → desync با Radius
`Backend/Application/Vpn/VpnApplication.cs:42-62` — `ChangeVpnPassword` فقط Radius را صدا می‌زند؛ `AccountEntity.VpnPassword` آپدیت نمی‌شود → `SendCertEmail` (`:91`) پسورد کهنه ایمیل می‌کند و `UserManagerHelper.GetUser` (`MikrotikRadius/Application/UserManagerHelper.cs:136-141`) در sync بعدی user را با پسورد قدیمی بازمی‌سازد. **اصلاح (P3)**: بعد از موفقیت Radius، `account.VpnPassword` ذخیره می‌شود.

### [x] H8. join غلط در GetInvoice(target, code) → ردیف‌های غلط/بیگانه
`Backend/Infrastructure/Repository/WalletRepository.cs:43` — `join Account a on w.Id = a.Id` به‌جای `w.AccountId = a.Id`؛ هر جا wallet-id با account-id تفاوت داشته باشد نتیجه غلط است (افشا + خرابی `get-invoice`).

### [x] H9. GetWalletsAmount با «= @ids» → SQL نامعتبر
`Backend/Infrastructure/Repository/WalletRepository.cs:53-64` — `where Id = @ids` با پارامتر لیستی؛ Dapper آن را به `Id = (@p1,@p2,...)` گسترش می‌دهد → با بیش از یک renewal معوق، صدور فاکتور همیشه 500 می‌شود. **اصلاح**: `where Id in @ids`.

### [x] H10. احراز هویت فرانتند: token در localStorage، بدون refresh/guard، logout مرده
- `Frontend/src/app/@services/api-services/http-client.ts:72,84-88` — JWT در localStorage؛ `expires_in` ذخیره ولی هرگز خوانده نمی‌شود؛ بدون interceptor و refresh.
- `Frontend/src/app/app.routes.ts:31-67` — هیچ route guard روی صفحات احراز هویت‌شده.
- `Frontend/src/app/login/login.component.ts:71-72` + `auth-service.ts:33-39` — Observable مربوط به logout هرگز subscribe نمی‌شود و localStorage قبل از ساخت درخواست پاک می‌شود؛ `AccessService.LogoutEvent` بک‌اند هم صفر caller دارد (هیچ endpoint خروجی معتبر وجود ندارد).
- **اصلاح (P5)**: `authGuard` (functional `canActivate`) روی مسیر `DefaultLayout` (همه صفحات احراز هویت‌شده) با redirect به `/login?redirect=…`؛ `AuthService.logout` صرفاً client-side شد (پاک‌سازی storage + reset لچ — endpoint خروج در بک‌اند وجود ندارد). **تصمیم**: توکن در localStorage ماند (انتقال به httpOnly cookie نیازمند تغییر بک‌اند — خارج از P5)؛ انقضای توکن با مسیر موجود 401→redirect پوشش داده می‌شود.

### [x] H11. latch «is_logout» هرگز reset نمی‌شود
`Frontend/src/app/@services/api-services/api-base-service.ts:20,191-192` — بعد از اولین 401، redirect برای 401 های بعدی (حتی پس از login مجدد) خاموش می‌ماند.
**اصلاح (P5)**: لچ به `protected static` تبدیل شد (یک redirect سراسری به‌جای لچ per-instance) و در login موفق (`authorization` با code<300) و در `logout` بازست می‌شود.

### [x] H12. Enumeration اکانت + صفر rate-limiting
- `Backend/Application/Authentication/AuthApplication.cs:139-144` — پیام‌های متمایز «کاربر یافت نشد»/«کاربر غیرفعال است»؛ `Register` (`:200-212`) هم وجود username/email/mobile را افشا می‌کند.
- در کل `Backend/` هیچ `AddRateLimiter`/lockout/HSTS/size-limit وجود ندارد (`Portal/Program.cs:11-26`) → brute-force بی‌محدودیت روی token/forget-pass/reset-pass.
- **اصلاح (P3)**: `ForgetPassword` پاسخ generic یکسان می‌دهد و فقط برای اکانتِ فعالِ موجود ارسال می‌کند (پیام‌های duplicate در Register عمداً حفظ شد — UX، محافظت با limiter). Rate limiter دات‌نت ۸ در `AddPortalServices` با policy پارتیشن‌بندی IP (fixed window، پیش‌فرض ۱۰/دقیقه، قابل تنظیم با `RateLimitOptions` در appsettings) + `[EnableRateLimiting]` روی `AuthController` + `app.UseRateLimiter()`.

### [x] H13. فیلتر تاریخ History از هر دو طرف خراب است
فرانت `Date` object را به‌عنوان query param می‌فرستد (`Frontend/src/app/history/history.component.ts:79-104`؛ serialize با `toString()`) و بک‌اند `[JsonConverter]` روی `[FromQuery]` اثری ندارد (`Backend/Portal/Controllers/AccountController.cs:70-77` + `Portal/Context/HistoryContext.cs:8-12`) → هر بار استفاده از فیلتر = 400.
**اصلاح (P5)**: قرارداد عددی — `HistoryContext` به `long? From/To` (unix-ms) با پراپرتی های `FromDateTime/ToDateTime` تغییر کرد؛ فرانت `Date` می‌گیرد و `from.getTime()` و `to` را با پایان روز (23:59:59.999، چون کوئری `<=` است) می‌فرستد.

### [x] H14. typo «targe» → close-connection روی target اشتباه
`Frontend/src/app/dashboard/dashboard.service.ts:37` — body به‌جای `target` با `targe` فرستاده می‌شود؛ `ConnectionController` مقدار null می‌بیند و روی اکانت login شده عمل می‌کند.
**اصلاح (P5)**: `targe` → `target`.

### [x] H15. چارت ترافیک در اولین بازدید لود نمی‌شود
`Frontend/src/app/dashboard/traffic-chart/traffic-chart.component.ts:65-69` — reload مشروط به `this.data` است که ابتدا undefined است؛ هیچ فراخوانی اولیه وجود ندارد.
**اصلاح (P5)**: `loadTrafficData()` در `ngOnInit` صدا زده می‌شود؛ reload روی تغییر target حفظ شد.

---

## HIGH — پرفورمنس

### [x] H16. 2N round-trip روتر در ساعت در DeactivateInvalidRadiusUsers
`Backend/MikrotikRadius/Application/AccountRadiusSyncUserManagerService.cs:48-61,72-91` — برای هر stale user دو فراخوانی MikroTik (LoadList + Save)؛ کاربران disabled-but-not-removed در مجموعه می‌مانند و هزینه هر ساعت تکرار می‌شود؛ با رشد کاربران job ساعتی از ۶۰ دقیقه فراتر می‌رود (تشدید H3).
**اصلاح (P1)**: `DeactivateUserExcept` خودش `LoadAll` یک‌بار می‌زند (تحت token قفل per-server موجود) و همان entity ها را save می‌کند؛ کاربرانِ از قبل disabled و نام‌های invalid skip می‌شوند. در `DeactivateUser` هم save فقط برای `Disabled != true` انجام می‌شود (idempotent).

### [x] H17. N+1 دیتابیسی در InactiveAbandonedUsers
`Backend/Application/Management/AccountMonitoringService.cs:41` — یک query به‌ازای هر plan تمام‌شده در هر run؛ bulk (`GetActiveAccounts(ids)`) موجود است و در `NotifSendServices:107` درست استفاده شده.
**اصلاح (P1)**: جمع‌آوری id های abandoned → یک فراخوانی `GetActiveAccounts(ids)` → دیکشنری.

### [x] H18. sync کامل ترافیک از HTTP endpoint ها تریگر می‌شود + race در گیت ۵ دقیقه‌ای
- `Backend/Application/Vpn/VpnApplication.cs:111-113` — `TrafficData()` کاربر، sync کامل همه سرورها را inline await می‌کند.
- `Backend/Application/Plan/PlanApplication.cs:66,87,345` — `_ = UpdateTrafficData()` fire-and-forget با منابع scoped (خطر `ObjectDisposedException`).
- `Backend/Infrastructure/Repository/TrafficDataRepository.cs:65-66` — `LastTrafficSync` قبل از write خوانده می‌شود → فراخوان‌های هم‌زمان همه از گیت رد می‌شوند (تشدید C1)؛ تقویت‌کننده DoS.
**اصلاح (P1)**: گیت atomic شد — `IRealmRepository.TryLockTrafficSync` با `UPDATE ... OUTPUT ... WHERE LastTrafficSync < @limit` قبل از fetch از Radius claim می‌کند (برنده‌ی رقابت ادامه می‌دهد)؛ sync از `VpnApplication.TrafficData` و `GetPlanState`/`GetPlanInfo` حذف شد (HTTP فقط از دیتای موجود می‌خواند؛ sync با job ساعتی است)؛ الگوی `_ =` در هر سه نقطه حذف شد (`CheckUserServerBalance` بعد از تمدید await می‌شود).

### [x] H19. PriceCalculator (Roslyn): بلوکه شدن، race، نشت assembly، capture شدن scoped repo توسط singleton
`Backend/Infrastructure/Services/PriceCalculator.cs:12-26,72` — `InitializeCalculators().Result` در اولین محاسبه؛ check-then-act بدون قفل (کامپایل تکراری)؛ `Assembly.Load` بدون `CollectibleAssemblyLoadContext` (نشت هر ویرایش قیمت)؛ event handler ثبت‌شده روی singleton `EntityEventService` که `Lazy<IPriceRepository>` scoped اولین request را نگه می‌دارد و در race هندلر تکراری می‌سازد.
**اصلاح**: `IPriceCalculator.CalculatePrice` async شد؛ init یک‌بارَه با `SemaphoreSlim` + double-check؛ `PriceCalculator` singleton با `IServiceScopeFactory` (resolve per-fetch، بدون captive)؛ هر نسل calculator در `CollectibleAssemblyLoadContext` خودش (`PricePool.CalculatorGeneration`) و نسل قبلی بعد از swap با `Unload()` آزاد می‌شود؛ هندلر refresh روی `IEntityEventService` singleton فقط یک‌بار ثبت و fail-safe است.

### [x] H20. ایندکس‌های غایب (مقایسه schema با query های واقعی)
| جدول | ایندکس پیشنهادی | مصرف‌کننده |
|---|---|---|
| `TrafficData` | `(AccountId, StartSession)` + پوشش `WHERE EndSession IS NULL` | `Fetch`/`FetchOpen` (هر سیکل sync و هر ویوی چارت) |
| `TrafficData` | پشتیبانی window functions ویوی `TotalPlanState` (`PARTITION BY AccountId ORDER BY Id`) | view |
| `Renewal` | `(AccountId)` | `TotalPlanState` |
| `History` | `(Target, Created)` | `GetHistory` |
| `Wallet` | `(AccountId)`، `(InvoiceCode)` شامل unique | `GetBalance`، `MAX(InvoiceCode)` (فعلاً full-scan) |
| `Account` | `(Owner)` | `GetActiveTargetArea` در هر login غیرادمین |

**اصلاح (P1)**: اسکریپت idempotent `Database/LocalDatabase/Indexes.sql` (اجرای دستی روی production). `UK_TrafficData_NasId_SessionId` بازسازی/ایجاد می‌شود (پشتیبان C1)، ایندکس‌های TrafficData روی edition های مجاز با `ONLINE = ON` (بررسی `SERVERPROPERTY('EngineEdition')`). **انحراف عمدی از پیشنهاد**: ایندکس `Wallet(InvoiceCode)` unique **نیست** — یک فاکتور چند ردیف wallet دارد (debit/credit، الگوی seed تست P0)؛ یکتایی کد فاکتور در زمان تولید توسط `InvoiceSequence` (C4/P0) تضمین می‌شود.

---

## MEDIUM — درستی

| چک | # | عنوان | مکان | شرح |
|---|---|---|---|---|
| [x] | M1 | قیمت با days خام، اعطا با روزهای بازنویسی‌شده | `PlanApplication.cs:115,177` vs `Domain/Plan/Business/RenewalBusiness.cs:72` | چیزی که قیمت می‌شود همان چیزی نیست که اعطا می‌شود (`days=0`+gigabytes → اعطای ۶۰+ روز). **اصلاح**: قیمت‌گذاری بر **روزهای مؤثر** پس از بازنویسی در هر سه نقطه (`Estimate`، `Renewal` عمومی، `Renewal` settlement) — تصمیم صاحب محصول ۱۴۰۵/۰۷؛ دبیت settlement حالا با `TotalPrice` فاکتور برابر است. کدهای Roslyn در DB باید با ورودی روز-مؤثر بازبینی شوند (یادداشت انتشار). |
| [x] | M2 | در catch تمدید: compensation قبل از rollback | `PlanApplication.cs:306-322` | اگر Radius قطع باشد `DeactivateUsers` می‌اندازد → rollback اجرا نمی‌شود و خطای اصلی mask می‌شود. **اصلاح**: rollback اول، هر سه قدم (rollback/compensation/history) در try/catch مستقل با لاگ؛ اصل خطای اولیه حفظ می‌شود. |
| [x] | M3 | Radius sync داخل تراکنش DB | `PlanApplication.cs:302-304` | شکست commit → user در Radius می‌ماند (dual-write بدون compensation مطمئن). **اصلاح (نرم)**: با ترتیب M2، شکستِ خودِ `DeactivateUsers` با لاگ نشانگر `RADIUS-COMPENSATION-FAILED` پیگیری عملیاتی می‌شود؛ residual risk ثبت شد. |
| [x] | M4 | `GetAvailableRealm` با `.First()` | `ServerManagementService.cs:40-44` | پر بودن همه realm ها = `InvalidOperationException` خام (500)؛ بدون reservation → overbooking هم‌زمان. **اصلاح**: لیست خالی → `UserException` دوستانه («ظرفیت سرورها تکمیل است...») قبل از تراکنش/دبیت. |
| [x] | M5 | dictionary indexer بدون گارد در DeactivateInvalidRadiusUsers | `Infrastructure/Services/AccountRadiusSyncService.cs:56-80` | realm بدون plan state = `KeyNotFoundException` → کل job ساعتی abort. **اصلاح (P1)**: `TryGetValue` + لیست خالی (غیرفعال‌سازی همه کاربران آن radius) |
| [x] | M6 | CheckUniqueData: شرط mobile به email گره خورده + مقایسه case-sensitive | `Infrastructure/Repository/AccountRepository.cs:113-127` | ثبت‌نام با موبایل بدون ایمیل، یکتایی موبایل را چک نمی‌کند؛ `d.Email == email` ordinal است ولی DB case-insensitive. **اصلاح (P5)**: شرط موبایل مستقل شد و مقایسه ها `OrdinalIgnoreCase`؛ به‌علاوه قاعده «ایمیل **یا** موبایل» در `AccountBusiness.SetFromModel` فعال شد (ایمیل اجباری نبودن — ریشه دو تست شکسته `Register_Success`/`ShouldNotBeEmpty` که با همین تغییر سبز شدند) |
| [x] | M7 | throw روی یک اکانت گمشده، کل batch هشدار را می‌کشد | `AccountMonitoringService.cs:114-117` | **اصلاح (P5)**: `continue` + `Log.Warning` |
| [x] | M8 | آستانه «در حال اتمام» 0.1% به‌جای 10% | `Domain/Plan/Business/PlanStateBusiness.cs:8-13` | مقیاس percent ها 0–100 است؛ هشدار فقط وقتی 99.9% مصرف شده. **اصلاح (P5)**: `10f`؛ تست `PlanStateBusinessTest` برای آستانه جدید بازنویسی شد |
| [x] | M9 | predicate مرده: `Created IS NULL` ولی `DEFAULT GETDATE()` | `Infrastructure/Repository/RenewalRepository.cs:13` + `Database/LocalDatabase/Renewal.sql:17` | ~~GetNotPaid همیشه خالی؛ شاخه unpaid در GenerateInvoice هرگز اجرا نمی‌شود~~ — **منسوخ**: با بازنویسی P2 BillingApplication متد و predicate حذف شدند (تأیید با grep) |
| [x] | M10 | کد reset روی اکانت غیرفعال burn می‌شود + TOCTOU دوبار مصرف | `AuthApplication.cs:178-193`، `Infrastructure/Repository/ResetPassRepository.cs:19-22` | کد خوانده/حذف می‌شود ولی عملیات fail؛ دو درخواست هم‌زمان هر دو می‌خوانند. **اصلاح (P5)**: مصرف atomic با یک statement واحد `DELETE ... OUTPUT deleted.*` — فقط برنده رقابت ردیف را می‌گیرد (کد در صورت شکست عملیات سوخته می‌ماند — کاربر کد جدید می‌گیرد) |
| [x] | M11 | سری سوم چارت ترافیک به‌اشتباه «Download» | `VpnApplication.cs:181-185` | دو سری هم‌نام (یکی Total). **اصلاح (P5)**: عنوان سری سوم → «مجموع» |
| [x] | M28 | race برداشت موازی تسویه (کشف پس از P2؛ خارج از ممیزی اولیه) | `BillingApplication.cs` + `Application/Billing/AccountSettlementGate.cs` | خواندن `GetBalance` بیرون از تراکنش/قفل + CAS فقط روی وضعیت فاکتور → دو تسویه/رسید موازی روی یک اکانت هر دو دبیت می‌زدند و موجودی منفی می‌شد. **اصلاح**: گیت per-account (`AccountSettlementGate`، singleton، `SemaphoreSlim`) دور `SettleWallet` و renewal-پس-از-رسید؛ درون-پردازشی — برای دیپلوی multi-instance جایگزین: قفل DB-level (`UPDLOCK` روی خواندن موجودی داخل تراکنش دبیت). |

## MEDIUM — امنیت و زیرساخت

| چک | # | عنوان | مکان | شرح |
|---|---|---|---|---|
| [x] | M12 | credentials روتر/RadiusDesk به‌صورت plaintext JSON در جدول Server | `Domain/Servers/Entity/ServerEntity.cs:33-41` + `JsonType/SshConfig.cs:7` و غیره | خواندن DB = مالکیت همه روترها؛ بدون encryption-at-rest. **اصلاح (P5)**: `ServerConfigProtection` (Domain، marker `protected:`) + wiring با ASP.NET Data Protection در `AddInfrastructureServices` (کلیدها در `{app}/data-protection-keys` — **بکاپ این پوشه الزامی است**؛ گم‌شدن = ورود دوباره کانفیگ سرورها). ردیف های plaintext قدیمی خوانده می‌شوند و در اولین save رمز می‌شوند (lazy). ستون `Server.Config` به `VARCHAR(4000)` (Server.sql + ALTER idempotent) |
| [x] | M13 | passphrase گواهی OVPN در لاگ Serilog | `ServerBridge/Ssh/SshConnection.cs:15` + `MikrotikDirectService.cs:146-148` | `Log.Information` شامل command کامل با export-passphrase. **اصلاح (P3)**: `CommandScrubber` مقادیر `password="..."`/`export-passphrase="..."` را با `***` جایگزین می‌کند؛ لاگ نتیجه command فقط طول را می‌نویسد (محتوای key خصوصی حذف شد). |
| [x] | M14 | anchor ضعیف regex تزریق (`$` قبل از `\n` تطبیق می‌شود) + `\w` یونیکدی | `ServerBridge/InjectionRegex.cs:7-11`، `ServerBridge/Ssh/ProcessService.cs:129-130` | **اصلاح (P3)**: anchor های `\A...\z` + کلاس‌های ASCII صریح؛ حذف `RegexOptions.Singleline`. |
| [x] | M15 | مبلغ پرداخت از client بدون هیچ bound | `Portal/Controllers/BillingController.cs:21-27` → `BillingApplication.cs:29-43` | مقدار منفی/سرریز وارد math موجودی می‌شود |
| [x] | M16 | token ادمین RadiusDesk در URL query | `FreeRadius/WebService/RadiusDeskService.cs:441-490` | ~~افشا در proxy/access log~~ — **بررسی (P5)**: token در query قرارداد خودِ API RadiusDesk (cake4) است و header پشتیبانی نمی‌شود؛ کد ما URL ها را لاگ نمی‌کند (بدون `AddHttpClient` logging). **ریسک پذیرفته‌شده** — کاهنده ها: HTTPS بین دو سرور، محدود کردن دسترسی به access log میزبان RadiusDesk |
| [x] | M17 | sync-over-async در مسیرهای request/job — **اصلاح (P4)**: `SessionRadiusSyncService.GetTrafficData` → `await`؛ `Task.WaitAll` در `AuthApplication.ForgetPassword` حذف و دو INSERT روی connection مشترک ترتیبی شد (ایمیل موازی)؛ خواندن `t.Result` بعد از `Task.WhenAll` در `ServerEntityExtension:14` مطابق تصمیم فاز بدون تغییر ماند | `Infrastructure/Services/SessionRadiusSyncService.cs:104`، `AuthApplication.cs:169`، `Infrastructure/Services/ServerEntityExtension.cs:14` | blocking + ریسک deadlock |

## MEDIUM — پرفورمنس

| چک | # | عنوان | مکان | شرح |
|---|---|---|---|---|
| [x] | M18 | UPDATE تک‌ردیفی fire-and-forget به‌جای bulk موجود | `AccountMonitoringService.cs:66,172-176` | N round-trip + N task بی‌ناظر در هر cycle هشدار. **اصلاح (P1)**: جمع‌آوری entity های تغییر یافته → یک `await Save(entities)` قبل از فراخوانی‌های radius/history |
| [x] | M19 | `GetAllActiveRadius` بدون cache، ۳-۴ بار در هر job و در هر API call — **اصلاح (P4)**: `IMemoryCache` با TTL ثابت ۳۰ ثانیه داخل `ServerRepository` (لایه repo تا همه مصرف‌کننده‌ها پوشش داده شوند؛ `AddMemoryCache` در `AddInfrastructureServices` هم ثبت شد)؛ بدون invalidation | `SessionRadiusSyncService.cs:24,52,78,104`، `AccountRadiusSyncService.cs:30,55,90,115,155,167` | جدول کوچک؛ cache کوتاه-TTL با `IMemoryCache` (ثبت شده) کافی است |
| [x] | M20 | await های متوالی مستقل در UpdateTrafficData — **اصلاح (P4)**: فاز شبکه (روترها) و فاز MSSQL (`Fetch(from)`) با شروع هم‌زمان دو task موازی شدند؛ لیست سرورهای radius از قبل (cache شده) با overload جدید `GetTrafficData(radius_list, indexes)` پاس می‌شود تا در فاز موازی هیچ خواندن DB روی connection scoped مشترک انجام نشود | `ServerManagementService.cs:110-114` | شبکه (روترها) قبل از شروع MSSQL/Realm تمام می‌شود؛ latency جمعی |
| [x] | M21 | SSH/tik4net: connection per-op، بدون timeout/keepalive/retry — **اصلاح (P4)**: `SshHandler` با CTS اتصال (پیش‌فرض ۱۵s) + `KeepAliveInterval`؛ `Tik4NetHandler.OpenAsync` با timeout (WhenAny + Dispose)؛ `SshConnection.Execute` با timeout فرمان (پیش‌فرض ۳۰s، APM). مقادیر از `ServerBridgeOptions` جدید (بخش `ServerBridgeOptions` در appsettings؛ به‌دلیل لایه‌بندی در ServerBridge تعریف شد نه ManagementOptions — Application به ServerBridge ارجاع دارد، نه برعکس). بدون retry (idempotent نیستند). pooling اتصال به TODO بعدی | `ServerBridge/Ssh/SshHandler.cs:15-17`، `ServerBridge/Tik4net/Tik4NetHandler.cs:14-23`، `SshConnection.cs:17` (sync `RunCommand`) | full handshake هر بار؛ روتر hang = توقف job بدون cancellation |
| [x] | M22 | `SmtpClient` بدون dispose + خواندن template از disk در هر ارسال — **اصلاح (P4)**: `using var smtp` + timeout ارسال با CTS؛ `Host/Port/EnableSsl/SendTimeoutSeconds` به `EmailOptions` (ServerBridge) اضافه شدند و `BindValidateReturn` شدند (default ها رفتار فعلی gmail را حفظ می‌کنند)؛ template ها با `ConcurrentDictionary<string,Task<string>>` استاتیک فقط یک‌بار load می‌شوند. تست: `EmailServiceTest.SendResetPasswordLink_TemplateCachedAcrossInstances` | `ServerBridge/Email/EmailHandler.cs:24-34`، `OutSource/EmailService.cs:25,49,77` | نشت connection؛ template ثابت است |
| [x] | M23 | `RegexOptions.Compiled` در هر اجرای script | `ServerBridge/Ssh/ProcessService.cs:109` | کامپایل مجدد الگو در هر CheckOn/ActivateOn. **اصلاح (P5)**: کش static الگوهای parsed با `ConcurrentDictionary` keyed by `output_pattern` |

## MEDIUM — فرانتند

| چک | # | عنوان | مکان | شرح |
|---|---|---|---|---|
| [x] | M24 | پاسخ‌های out-of-order در estimate (بدون switchMap) — **حل شد**: استریم `estimate$` با `switchMap` + `catchError(EMPTY)` | `Frontend/src/app/renewal/renewal.component.ts:126-163` | ~~قیمت/زمان نمایش‌داده‌شده ممکن است از درخواست stale باشد~~ |
| [x] | M25 | double-submit روی renewal/pay/register/login — **حل شد در P2** (دکمه‌ها هنگام درخواست disable می‌شوند) | `renewal.component.html`، `payment.component.ts`، `payment-modal.component.html`، `register.component.html`، `login.component.ts` | ~~دکمه‌ها در حین درخواست فعال می‌مانند~~ |
| [x] | M26 | نشت subscription (هیچ unsubscribe/takeUntil در کل src نیست) | `dashboard.component.ts:210`، `history.component.ts:89`، `traffic-chart.component.ts:66`، `register.component.ts:121` | component های destroyed به root-singleton گوش می‌دهند و HTTP ghost fire می‌کنند. **اصلاح (P5)**: `takeUntilDestroyed(destroyRef)` روی هر چهار subscription روی `UserService.onTargetChanged` |
| [x] | M27 | translate pipe به‌صورت `pure:false` + متدها در template، بدون OnPush | `Frontend/src/app/@services/translation/translation-pipe.ts:4-7`، `dashboard.component.html` | ترجمه و توابع در هر CD cycle اجرا می‌شوند. **اصلاح (P5)**: pipe شد pure — متن ها static و build-time اند (بدون سوییچ زبان در runtime) پس pure رفتار یکسان با هزینه کمتر دارد |

---

## LOW

| چک | # | عنوان | مکان |
|---|---|---|---|
| [x] | L1 | `printMoney` ضرب دستی با `+',000'` — **حل شد**: `Intl.NumberFormat('fa-IR')` با ضرب ×1000 و گارد `Number.isFinite` (پوشش صفر/منفی/اعشار/NaN) | `Frontend/src/app/@services/message-handler/money-printer.ts:14` |
| [x] | L2 | static event `OnRenewal` با null-check race، بدون هیچ subscriber — **اصلاح (P5)**: event + `OnRenewalDelegation` + کلاس `RenewalEvent` حذف شدند (صفر subscriber؛ `TestPlan.md` به‌روزرسانی) | `Backend/Application/Plan/IPlanApplication.cs:8,20-25` |
| [x] | L3 | شرط‌های مرده: `Count < 0`؛ شاخه غیرقابل دسترس email/mobile — **اصلاح (P5)**: `Count < 0` پیش‌تر حذف شده بود (عارضه P0-P4)؛ شاخه email/mobile با فعال‌شدن قاعده «ایمیل یا موبایل» زنده و معتبر شد | `Backend/Application/Billing/BillingApplication.cs:143`، `Domain/Account/Business/AccountBusiness.cs:44-47` |
| [x] | L4 | `console.log(result)` باقی‌مانده — **منسوخ**: با بازنویسی P2 حذف شده بود | `Frontend/src/app/renewal/renewal.component.ts:110` |
| [x] | L5 | CoreUI CSS از CDN (jsdelivr) — برای محصول ایران‌محور نقطه شکست. **اصلاح (P5)**: لینک CDN حذف شد — `styles.scss` از قبل کل CoreUI (RTL) را self-host کامپایل می‌کرد؛ لینک فقط نسخه دوگانه از CDN بود. fallback کوچک boot-spinner در `index.html` | `Frontend/src/index.html:11-13` |
| [x] | L6 | errorHandler خطاهای transport را دفن می‌کند؛ HTML خطای 4xx/5xx ناخوانا. **اصلاح (P5)**: پیام اختصاصی «ارتباط با سرور برقرار نشد…» (`api.error-transport`) برای خطاهای بدون response body (status 0/null) | `Frontend/src/app/@services/api-services/http-client.ts:93-104` |
| [x] | L7 | `DateTime.Now` در همه‌جا + `ToLocalTime()` در converter. **بررسی (P5)**: هیچ مخلوط `UtcNow`/`Now` در مسیرهای production نیست (تنها `UtcNow` یک stopwatch در تست است) و converter با قرارداد local-همه‌جا سازگار است؛ ایران از ۲۰۲۲ DST ندارد. مهاجرت کامل UTC به‌عنوان تصمیم معماری ثبت شد و از P5 خارج است | `Backend/Portal/Context/UnixTimestampConverter.cs:28` و موارد متعدد؛ جابه‌جایی TZ/DST = اعوجاج انقضا |
| [ ] | L8 | correlated `EXISTS ... order by limit 1` در MySQL (مسیر dormant RadiusDesk) — **defer**: تا فعال‌سازی مسیر RadiusDesk اقدام نشود | `Backend/FreeRadius/Repository/UserPlanStateRepository.cs:59-79` |
| [x] | L9 | login بدون lockout + مقایسه non-constant-time؛ `VpnController` پیام «wrong login password» برای تایید OVPN. **اصلاح (P5)**: پیام generic «عملیات ناموفق بود!» + لاگ Debug تفکیکی (lockout/brute-force با rate limiter P3 پوشش داده شده) | `Backend/Application/Authentication/AuthApplication.cs:35`، `Backend/Portal/Controllers/VpnController.cs:35-42` |
| [x] | L10 | منوی تم انگلیسی «Light/Dark/Auto» در UI فارسی. **اصلاح (P5)**: روشن/تیره/خودکار | `Frontend/src/app/default-layout/default-header/default-header.component.html:58-61` |
| [x] | L11 | در `CheckUniqueData` پنجره TOCTOU بین check و insert بدون unique-constraint fallback. **اصلاح (P5)**: اسکریپت idempotent `Database/LocalDatabase/AccountUniqueIndexes.sql` (ایندکس های unique فیلتردار روی Username/Email/Mobile + pre-check گزارش duplicates) + catch خطای 2601/2627 در `AccountRepository.Save` → `UserException` دوستانه 409. **دیپلوی**: اسکریپت را قبل از release دستی اجرا شود | `Backend/Application/Authentication/AuthApplication.cs:198-218` |

---

## الگوهای ریشه‌ای (ریشه مشترک چند ایراد)

1. **Fire-and-forget `_ = ...`** روی منابع scoped — ریشه H4/H18 و بخشی از M18.
2. **Sync-over-async** (`.Result`/`Task.WaitAll`) — H3/H19/M17.
3. **تراکنش‌ها فقط روی overload تک-Entity معتبرند** — C2 و همه جاهایی که `Save(IEnumerable)` صدا زده می‌شود.
4. **Check-then-act بدون قفل/unique** — C4، M4، M10، H19.
5. **نبود قرارداد مشترک فرانت/بک‌اند** (route ها، شکل body، query param ها) — C3/H13/H14؛ نشانه نبود تست E2E.

## فازهای اجرایی پیشنهادی (اولویت)

| فاز | موضوع | آیتم‌ها | خروجی قابل قبول |
|---|---|---|---|
| **P0** | درستی جریان پول | C2 → C4 → C5 → H8 → H9 → M15 | یک PR روی Billing + تست integration (صدور فاکتور → callback دوبار → تطابق ردیف‌ها) |

> **نکته دیپلوی P0**: اسکریپت جدید `Database/LocalDatabase/InvoiceSequence.sql` (کد فاکتور اتمیک، C4) باید قبل از release به‌صورت دستی روی دیتابیس production (`FastBypass`) اجرا شود — فریم‌ورک migration وجود ندارد. اسکریپت idempotent است و مقدار شروع را از `MAX(InvoiceCode)` موجود محاسبه می‌کند. همچنین عملیات bulk از `Z.Dapper.Plus` (لایسنس trial منقضی‌شده) به set-operations تک‌ردیفی `Dapper.FastCrud` منتقل شد (`EditableRepository`).
| **P1** | داده ترافیک و job ها | C1 → H18 → M5 → H16 → H17 → M18 + ایندکس‌های H20 (به‌صورت migration) | اجرای JobInterval دوبار پشت‌سرهم بدون درج تکراری `TrafficData` و بدون نشت connection |

> **نکته دیپلوی P1**: دو اسکریپت جدید باید قبل از release به‌صورت دستی روی production (`FastBypass`) اجرا شوند (ترتیب مهم است):
> 1. `Database/LocalDatabase/TrafficDataDedup.sql` — حذف ردیف‌های تکراری `(NasId, SessionId)` ساخته‌شده توسط باگ C1 (نگه‌داشتن جدیدترین ردیف؛ dedup-only بدون تجمیع مصرف تاریخی — تجمیع پس‌رونده روی `TotalPlanState` غیرقابل بازگشت است). تعداد ردیف‌های حذف‌شده را گزارش می‌دهد. اجرای آن فقط با تأیید صاحب محصول.
> 2. `Database/LocalDatabase/Indexes.sql` — ایندکس‌های H20؛ unique index روی `TrafficData` تا dedup اجرا نشود fail می‌شود. ایندکس‌های `TrafficData` روی edition های مجاز با `ONLINE = ON` ساخته می‌شوند؛ در غیر این صورت خارج از ساعات پیک اجرا شود.
>
> تست‌های جدید: `ServerManagementServiceTest.UpdateTrafficData_SecondCycleWithSameSessions_DoesNotInsertDuplicates` (دو سیکل پشت‌سرهم → بدون درج تکراری) و `AccountRadiusTest.DeactivateInvalidRadiusUsers_UnknownRealm_ShouldNotThrow` (realm ناموجود → بدون کرش + غیرفعال‌سازی همه).
| **P2** ✅ | پرداخت کارت‌به‌کارت و کیف پول (طراحی جدید صاحب محصول) — **انجام شد (۱۴۰۵/۰۷)**: C3 + M25 حل؛ `BillingApplication` بازنویسی (`IssueTopUp`/`IssuePlanInvoice`/`GetInvoice`/`RegisterReceipt`/`SettleWallet`)، `payment-callback` حذف، جدول `Invoice` (`PaymentP2.sql`)، `BalanceStatus.Verifying`، `GetBalance` شامل Verifying، قاعده آستانه کیف پول (`WalletThresholdService`)، `ActivateUsers` روی همه Radius ها، فرانتند صفحه فاکتور/رسید/تسویه + `BILLING_API_URL` | `yarn build` سبز + تست‌های `BillingApplicationTest`/`BillingP2Test`/`WalletThresholdServiceTest` + سناریوها (شارژ با رسید، تمدید ناکافی با رسید → پلن فعال، تمدید کافی با تایید کیف پول) |
| **P3** ✅ | احراز هویت و امنیت — **انجام شد (۱۴۰۵/۰۷)**: C6 + H1 + H12 + H5 + H6 + H7 + M13 + M14؛ `PasswordHasher` (PBKDF2) + برابرسازی زمان پاسخ login، CSPRNG، پاسخ generic در forget-pass + rate limiter قابل تنظیم (`RateLimitOptions`)، TTL کش دسترسی، چک مالکیت session، ذخیره VpnPassword، پاک‌سازی secret از لاگ SSH، anchor های `\A...\z` | تست‌های جدید `PasswordHasherTest`/`HashHandlerTest`/`InjectionRegexTest`/`CommandScrubberTest` + H6/H7 در تست‌های Application؛ `Account.sql` با ستون `VARCHAR(200)` و seed جدید |
| **P4** ✅ | زیرساخت job و I/O — **انجام شد (۱۴۰۵/۰۷)**: H2 + H3 + H4 + M17 + M19 + M20 + M21 + M22؛ به‌روزرسانی ستون‌محور Account در job های موازی، dispose کامل scope ها + `DisallowConcurrentExecution` + misfire، حذف کامل `_ =`/`.Result`/`WaitAll` از مسیرهای prod (+ دو باگ خاموش MARS و FK-Issuer که آشکار و اصلاح شدند)، cache ۳۰s لیست سرورهای radius، موازی‌سازی فاز شبکه/MSSQL سینک ترافیک، timeout/keepalive روی SSH/tik4net (`ServerBridgeOptions`، بدون تغییر دیتابیس)، dispose/timeout ایمیل + کش template | job بدون هم‌پوشانی؛ بدون `.Result`/`WaitAll` در prod (تنها استثناها: برابرسازی زمان `AuthApplication:39` و `ServerEntityExtension:14`)؛ تست‌های جدید `ParallelMonitoring_WarningTimeAndInactiveBothApplied` و `SendResetPasswordLink_TemplateCachedAcrossInstances` |
| **P5** ✅ | باقی موارد — **انجام شد (۱۴۰۵/۰۷)**: H10-H15 (بجز H12 از P3) + M6 + M7 + M8 + M10 + M11 + M12 + M16(ریسک پذیرفته‌شده) + M23 + M26 + M27 + L2 + L3 + L5 + L6 + L7(بررسی/تعیین دامنه) + L9 + L10 + L11؛ منسوخ‌های M9/L4 شناسایی و علامت خوردند؛ L8 defer تا فعال‌سازی RadiusDesk. بونوس: قاعده «ایمیل **یا** موبایل» فعال شد و دو تست شکسته از قبل (`Register_Success`, `ShouldNotBeEmpty`) سبز شدند | تست های جدید P5: `ServerConfigProtectionTest` (M12 — plaintext fallback، marker، round-trip، legacy read)، `HistoryContextTest` (H13 — تبدیل unix-ms)، `AuthAppTest.ResetPassword_*` (M10 — کد ناشناخته/منقضی/موفق)، `AccountMonitoringServiceTest.NotifSendServices_MissingAccount_SkipsWithoutThrow` (M7) + بازنویسی `PlanStateBusinessTest` برای آستانه M8. `Tests.csproj` سبز (۲۴۸ passed / 1 skipped) + `yarn build` سبز. **دیپلوی دستی**: `AccountUniqueIndexes.sql` (L11) و `Server.sql` (بخش ALTER ستون Config، M12) قبل از release؛ پوشه `data-protection-keys` در بکاپ گیرد |

## سوالات باز (پیش از شروع فاز مربوطه تعیین شود)

- ~~**P2**: آیا مسیر `payment-request`/redirect درگاه از قبل طراحی شده یا قرارداد API پرداخت از نو تعریف شود؟~~ **حل شد (۱۴۰۵/۰۷)**: درگاه وجود ندارد؛ پرداخت فقط کارت‌به‌کارت با ثبت رسید است. طراحی کامل: [docs/15-payment-p2.md](15-payment-p2.md) و `Architecture/peyment-P2.md`.
- ~~**P3**: برای پسورد پرتال (C6) سیاست rollout مهاجرت هش (force-reset در login بعدی یا dual-hash) چیست؟~~ **حل شد (۱۴۰۵/۰۷)**: سیستم هنوز راه‌اندازی نشده — مهاجرت لازم نیست؛ PBKDF2 مستقیماً جایگزین شد و اسکریپت `Account.sql` (ستون + seed) به‌روزرسانی شد.

## طرح راستی‌آزمایی عمومی (پس از هر فاز)

- `dotnet build Backend/Photon-Bypass.sln` و `dotnet test Backend/Photon-Bypass.sln` سبز.
- سناریوی دستی: ثبت‌نام → تمدید بدون پرداخت → صدور فاکتور → callback شبیه‌سازی‌شده (دوبار، برای idempotency) → تطابق ردیف‌های Wallet/Renewal.
- `cd Frontend && yarn build` سبز.

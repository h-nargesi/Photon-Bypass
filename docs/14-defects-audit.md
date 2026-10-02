# docs/14-defects-audit.md — Defect & performance audit (not in TODO)

> **English summary**: Full audit of implementation bugs, correctness issues, security weaknesses, and performance problems that are **not** listed in `Architecture/TODO.md` or `docs/12-current-state.md`. Every finding was verified against the actual code and carries a `file:line` reference, a severity, and a fix direction. Items are grouped by severity (C=critical, H=high, M=medium, L=low) and mapped to execution phases (P0–P5) at the end. Checkboxes track fix progress. Date: 1405/07 (2026-10).
>
> **فارسی**: گزارش کامل ایرادهای پیاده‌سازی، درستی، امنیت و پرفورمنسی که در TODO نیستند. هر مورد با ارجاع `file:line`، شدت و مسیر اصلاح ثبت شده و در انتهای سند به فازهای اجرایی P0–P5 نگاشت شده است. چک‌باکس‌ها برای پیگیری پیشرفت اصلاح هستند.

| شدت | تعداد |
|---|---|
| Critical | 6 |
| High | 20 |
| Medium | 27 |
| Low | 11 |

مجموع: **۶۴ مورد**.

---

## CRITICAL

### [ ] C1. کلید اشتباه در Merge ترافیک → درج تکراری در هر سیکل sync
- **مکان**: `Backend/Application/Management/ServerManagementService.cs:196-197` (کلید `SessionId`) در برابر `:223-224` (lookup با `traffic.NasIpAddress`).
- **شرح**: دیکشنری مقصد با `SessionId` کلید می‌خورد ولی جستجو با `NasIpAddress` انجام می‌شود؛ چون هیچ‌وقت برابر نیستند، session های باز هرگز آپدیت نمی‌شوند و در هر سیکل `TrafficDataEntity` جدید درج می‌شود → دوبل‌شمارش ترافیک (اتمام زودهنگام پلن‌ها)، نقض `UK_TrafficData_NasId_SessionId`، رشد بی‌نهایت `FetchOpen`.
- **اصلاح**: `data_pack.TryGetValue(traffic.SessionId, ...)`.

### [ ] C2. Bulk Save/Delete از تراکنش DB عبور نمی‌کند — تراکنش صورتحساب بی‌اثر است
- **مکان**: `Backend/Infrastructure/Database/EditableRepository.cs:43-51,62`؛ متد کمکی `CheckTransactionBulk` در `:91-104` تعریف شده ولی **هرگز صدا زده نمی‌شود**.
- **شرح**: `BulkUpdateAsync/BulkInsertAsync/BulkDeleteAsync` بدون `AttachToTransaction` اجرا می‌شوند؛ در نتیجه `BillingApplication.GenerateInvoice` (`Backend/Application/Billing/BillingApplication.cs:151-250`) که wallet/renewal را داخل `BeginTransaction/Commit` می‌نویسد عملاً auto-commit می‌نویسد و rollback هیچ‌چیز را برنمی‌گرداند → ردیف‌های نیمه‌کاله wallet/renewal در صورت خطای وسط flow.
- **اصلاح**: فراخوانی `CheckTransactionBulk` در overload های collection (هم save هم delete).

### [ ] C3. جریان پرداخت فرانتند کاملاً خراب است (۴ ایراد مستقل)
- **مکان‌ها**:
  - `Frontend/src/app/payment/payment-service.ts:10,16` و `Frontend/src/app/default-layout/payment-modal/payment-modal.service.ts:10` — فراخوانی `PLAN_API_URL/get-invoice|pay|payment-request` در حالی که بک‌اند در `BillingController` (route `/api/billing/...`) سرو می‌کند؛ `payment-request` اصلاً در بک‌اند وجود ندارد.
  - `Frontend/src/app/payment/payment.component.ts:51` — `isNumber(code)` روی string (خروجی `queryParamMap.get`) همیشه false → صفحه payment همیشه به dashboard redirect می‌شود.
  - `Frontend/src/app/renewal/renewal.component.ts:114` — navigate با invoice hard-code `"10"` به‌جای `result.invoiceCode`؛ فیلد `InvoiceCode` در اینترفیس فرانتند (`@models/data-model/user-plan-info.ts:22-25`) مدل نشده.
  - `Backend/Portal/Controllers/BillingController.cs:21` — `Pay([FromBody] int value)` ولی فرانت `POST {value}` (object) می‌فرستد؛ `payment.component.ts:65` خروجی عددی pay را به‌عنوان URL به `window.location.href` می‌دهد.
- **اصلاح**: تعریف قرارداد API پرداخت (route/body/response)، اصلاح سرویس‌های فرانتند، استفاده از `result.invoiceCode`، و پیاده‌سازی redirect واقعی درگاه.

### [ ] C4. کد فاکتور MAX+1 → تداخل بین کاربران؛ callback بین اکانت‌ها عبور می‌کند
- **مکان**: `Backend/Infrastructure/Repository/WalletRepository.cs:66-76` (تولید کد) و `:29-36` (`GetInvoice(int)` بدون فیلتر account/status)؛ مصرف در `BillingApplication.cs:99-123`.
- **شرح**: `select max(InvoiceCode)... return max+1` بدون قفل/unique؛ دو درخواست هم‌زمان کد یکسان می‌گیرند. `GetInvoice(code)` فقط با code فیلتر می‌کند → `PaymentCallback` می‌تواند wallet های کاربر دیگر را Complete و Renewal او را اجرا کند.
- **اصلاح**: ستون identity/sequence یا unique + retry، و فیلتر account در lookup فاکتور.

### [ ] C5. ترتیب و idempotency های PaymentCallback → «پرداخت بدون تحویل پلن»
- **مکان**: `Backend/Application/Billing/BillingApplication.cs:110-123`؛ `Backend/Application/Plan/PlanApplication.cs:180-215` (مسیر بدون validation) در برابر `:255-263` (validation فقط در retry).
- **شرح**: اول `Status=Completed` ذخیره می‌شود بعد `Renewal` اجرا می‌شود؛ مسیر «موجودی ناکافی» فاکتور را **بدون** `RenewalValidation` صادر می‌کند → ورودی نامعتبر (مثل `days=37`) از estimate تا پرداخت عبور می‌کند و بعد از کسر پول exception می‌گیرد؛ بدون مسیر refund. callback تکراری (webhook retry) آیتم‌های Completed را دوباره اجرا می‌کند؛ `int.Parse(token)` در `:99` بدون گارد → 500.
- **اصلاح**: اجرای validation قبل از صدور فاکتور، atomic check-and-set روی status (فقط Pending→Completed)، گارد parse، و مسیر compensation.

### [ ] C6. هش پسورد بدون salt و بدون KDF
- **مکان**: `Backend/Shared/Tools/HashHandler.cs:12-15`؛ استفاده در `AuthApplication.cs:35,190,216` و `AccountApplication.cs:101-102`.
- **شرح**: `SHA512.HashData` خالص؛ پسوردهای یکسان = هش یکسان؛ brute-force آفلاین بدیهی. (پسورد VPN به‌دلیل PAP باید قابل بازیابی بماند؛ پسورد پرتال نباید.)
- **اصلاح**: PBKDF2/bcrypt/Argon2 با salt per-user + سیاست مهاجرت کاربران موجود (force-reset یا dual-hash).

---

## HIGH — درستی و امنیت

### [ ] H1. توکن‌های امنیتی با System.Random غیررمزی
`Backend/Shared/Tools/HashHandler.cs:8,17-26` — نمونه static مشترک `Random` (نه thread-safe، نه CSPRNG) برای کد reset پسورد (`AuthApplication.cs:119,147`)، `VpnPassword` اولیه (`:215`) و passphrase گواهی OVPN (`MikrotikRadius/Application/MikrotikDirectService.cs:115`). **اصلاح**: `RandomNumberGenerator.GetItems`.

### [ ] H2. رقابت job های موازی روی همان ردیف‌های Account → احیای اکانت غیرفعال‌شده
`Backend/Application/Management/JobInterval.cs:38-42` — `NotifSendServices` و `InactiveAbandonedUsers` هم‌زمان روی همان `plan_state_list`؛ هر دو کل ردیف Account را با entity های stale ذخیره می‌کنند (`AccountMonitoringService.cs:65-66` و `:174-175`) → last-writer-wins می‌تواند `IsActive=false` را بازنویسی کند. **اصلاح**: concurrency token یا به‌روزرسانی ستون-محور.

### [ ] H3. JobInterval: sync-over-async، نشت scope، بدون جلوگیری از هم‌پوشانی
`Backend/Application/Management/JobInterval.cs:15,29-42` — `Task.WaitAll` داخل async؛ چهار `CreateScope()` بدون dispose (نشت `SqlConnection` در هر ساعت)؛ نبود `[DisallowConcurrentExecution]` و misfire policy → اجراهای هم‌پوشان. ثبت job در `Application/ServiceFactory.cs:34-49`.

### [ ] H4. Fire-and-forget روی scoped connection مشترک
الگوی `_ = repo.Save(...)` که با کارهای awaited روی **یک** `SqlConnection` scoped موازی می‌شود: `AccountMonitoringService.cs:66,88-100,167-176`، `PlanApplication.cs:66,87,312,345`، `AuthApplication.cs:46,50,158`، `AccountApplication.cs:117,138`، `VpnApplication.cs:52,99`، `ConnectionApplication.cs:93` → خطاهای intermittent "MARS/parallel operations" و استثناهای unobserved. **اصلاح**: حذف الگو؛ await کامل یا صف پس‌زمینه با scope مستقل.

### [ ] H5. کش AccessService بدون انقضا → دسترسی باقی می‌ماند پس از revocation
`Backend/Portal/Basical/AccessService.cs:15-18` — `cache.Set($"TargetArea|{username}")` بدون TTL/Size؛ فقط در login پر می‌شود؛ غیرفعال/حذف/انتقال sub-account تا logout والد اعمال نمی‌شود؛ رشد بی‌نهایت.

### [ ] H6. CloseConnection مالکیت session را چک نمی‌کند (IDOR درون-realm)
`Backend/Application/Connection/ConnectionApplication.cs:59-91` — realm سرور با realm target تطبیق داده می‌شود (`:78-82`) اما هرگز بررسی نمی‌شود که session_id متعلق به target باشد → هر کاربر احراز هویت‌شده می‌تواند session کاربر دیگری در همان realm را ببندد. **اصلاح**: تطبیق session با username/account هدف قبل از بستن.

### [ ] H7. تغییر پسورد VPN در DB ذخیره نمی‌شود → desync با Radius
`Backend/Application/Vpn/VpnApplication.cs:42-62` — `ChangeVpnPassword` فقط Radius را صدا می‌زند؛ `AccountEntity.VpnPassword` آپدیت نمی‌شود → `SendCertEmail` (`:91`) پسورد کهنه ایمیل می‌کند و `UserManagerHelper.GetUser` (`MikrotikRadius/Application/UserManagerHelper.cs:136-141`) در sync بعدی user را با پسورد قدیمی بازمی‌سازد.

### [ ] H8. join غلط در GetInvoice(target, code) → ردیف‌های غلط/بیگانه
`Backend/Infrastructure/Repository/WalletRepository.cs:43` — `join Account a on w.Id = a.Id` به‌جای `w.AccountId = a.Id`؛ هر جا wallet-id با account-id تفاوت داشته باشد نتیجه غلط است (افشا + خرابی `get-invoice`).

### [ ] H9. GetWalletsAmount با «= @ids» → SQL نامعتبر
`Backend/Infrastructure/Repository/WalletRepository.cs:53-64` — `where Id = @ids` با پارامتر لیستی؛ Dapper آن را به `Id = (@p1,@p2,...)` گسترش می‌دهد → با بیش از یک renewal معوق، صدور فاکتور همیشه 500 می‌شود. **اصلاح**: `where Id in @ids`.

### [ ] H10. احراز هویت فرانتند: token در localStorage، بدون refresh/guard، logout مرده
- `Frontend/src/app/@services/api-services/http-client.ts:72,84-88` — JWT در localStorage؛ `expires_in` ذخیره ولی هرگز خوانده نمی‌شود؛ بدون interceptor و refresh.
- `Frontend/src/app/app.routes.ts:31-67` — هیچ route guard روی صفحات احراز هویت‌شده.
- `Frontend/src/app/login/login.component.ts:71-72` + `auth-service.ts:33-39` — Observable مربوط به logout هرگز subscribe نمی‌شود و localStorage قبل از ساخت درخواست پاک می‌شود؛ `AccessService.LogoutEvent` بک‌اند هم صفر caller دارد (هیچ endpoint خروجی معتبر وجود ندارد).

### [ ] H11. latch «is_logout» هرگز reset نمی‌شود
`Frontend/src/app/@services/api-services/api-base-service.ts:20,191-192` — بعد از اولین 401، redirect برای 401 های بعدی (حتی پس از login مجدد) خاموش می‌ماند.

### [ ] H12. Enumeration اکانت + صفر rate-limiting
- `Backend/Application/Authentication/AuthApplication.cs:139-144` — پیام‌های متمایز «کاربر یافت نشد»/«کاربر غیرفعال است»؛ `Register` (`:200-212`) هم وجود username/email/mobile را افشا می‌کند.
- در کل `Backend/` هیچ `AddRateLimiter`/lockout/HSTS/size-limit وجود ندارد (`Portal/Program.cs:11-26`) → brute-force بی‌محدودیت روی token/forget-pass/reset-pass.

### [ ] H13. فیلتر تاریخ History از هر دو طرف خراب است
فرانت `Date` object را به‌عنوان query param می‌فرستد (`Frontend/src/app/history/history.component.ts:79-104`؛ serialize با `toString()`) و بک‌اند `[JsonConverter]` روی `[FromQuery]` اثری ندارد (`Backend/Portal/Controllers/AccountController.cs:70-77` + `Portal/Context/HistoryContext.cs:8-12`) → هر بار استفاده از فیلتر = 400.

### [ ] H14. typo «targe» → close-connection روی target اشتباه
`Frontend/src/app/dashboard/dashboard.service.ts:37` — body به‌جای `target` با `targe` فرستاده می‌شود؛ `ConnectionController` مقدار null می‌بیند و روی اکانت login شده عمل می‌کند.

### [ ] H15. چارت ترافیک در اولین بازدید لود نمی‌شود
`Frontend/src/app/dashboard/traffic-chart/traffic-chart.component.ts:65-69` — reload مشروط به `this.data` است که ابتدا undefined است؛ هیچ فراخوانی اولیه وجود ندارد.

---

## HIGH — پرفورمنس

### [ ] H16. 2N round-trip روتر در ساعت در DeactivateInvalidRadiusUsers
`Backend/MikrotikRadius/Application/AccountRadiusSyncUserManagerService.cs:48-61,72-91` — برای هر stale user دو فراخوانی MikroTik (LoadList + Save)؛ کاربران disabled-but-not-removed در مجموعه می‌مانند و هزینه هر ساعت تکرار می‌شود؛ با رشد کاربران job ساعتی از ۶۰ دقیقه فراتر می‌رود (تشدید H3).

### [ ] H17. N+1 دیتابیسی در InactiveAbandonedUsers
`Backend/Application/Management/AccountMonitoringService.cs:41` — یک query به‌ازای هر plan تمام‌شده در هر run؛ bulk (`GetActiveAccounts(ids)`) موجود است و در `NotifSendServices:107` درست استفاده شده.

### [ ] H18. sync کامل ترافیک از HTTP endpoint ها تریگر می‌شود + race در گیت ۵ دقیقه‌ای
- `Backend/Application/Vpn/VpnApplication.cs:111-113` — `TrafficData()` کاربر، sync کامل همه سرورها را inline await می‌کند.
- `Backend/Application/Plan/PlanApplication.cs:66,87,345` — `_ = UpdateTrafficData()` fire-and-forget با منابع scoped (خطر `ObjectDisposedException`).
- `Backend/Infrastructure/Repository/TrafficDataRepository.cs:65-66` — `LastTrafficSync` قبل از write خوانده می‌شود → فراخوان‌های هم‌زمان همه از گیت رد می‌شوند (تشدید C1)؛ تقویت‌کننده DoS.

### [ ] H19. PriceCalculator (Roslyn): بلوکه شدن، race، نشت assembly، capture شدن scoped repo توسط singleton
`Backend/Infrastructure/Services/PriceCalculator.cs:12-26,72` — `InitializeCalculators().Result` در اولین محاسبه؛ check-then-act بدون قفل (کامپایل تکراری)؛ `Assembly.Load` بدون `CollectibleAssemblyLoadContext` (نشت هر ویرایش قیمت)؛ event handler ثبت‌شده روی singleton `EntityEventService` که `Lazy<IPriceRepository>` scoped اولین request را نگه می‌دارد و در race هندلر تکراری می‌سازد.

### [ ] H20. ایندکس‌های غایب (مقایسه schema با query های واقعی)
| جدول | ایندکس پیشنهادی | مصرف‌کننده |
|---|---|---|
| `TrafficData` | `(AccountId, StartSession)` + پوشش `WHERE EndSession IS NULL` | `Fetch`/`FetchOpen` (هر سیکل sync و هر ویوی چارت) |
| `TrafficData` | پشتیبانی window functions ویوی `TotalPlanState` (`PARTITION BY AccountId ORDER BY Id`) | view |
| `Renewal` | `(AccountId)` | `TotalPlanState` |
| `History` | `(Target, Created)` | `GetHistory` |
| `Wallet` | `(AccountId)`، `(InvoiceCode)` شامل unique | `GetBalance`، `MAX(InvoiceCode)` (فعلاً full-scan) |
| `Account` | `(Owner)` | `GetActiveTargetArea` در هر login غیرادمین |

---

## MEDIUM — درستی

| چک | # | عنوان | مکان | شرح |
|---|---|---|---|---|
| [ ] | M1 | قیمت با days خام، اعطا با روزهای بازنویسی‌شده | `PlanApplication.cs:115,177` vs `Domain/Plan/Business/RenewalBusiness.cs:72` | چیزی که قیمت می‌شود همان چیزی نیست که اعطا می‌شود (`days=0`+gigabytes → اعطای ۶۰+ روز) |
| [ ] | M2 | در catch تمدید: compensation قبل از rollback | `PlanApplication.cs:306-322` | اگر Radius قطع باشد `DeactivateUsers` می‌اندازد → rollback اجرا نمی‌شود و خطای اصلی mask می‌شود |
| [ ] | M3 | Radius sync داخل تراکنش DB | `PlanApplication.cs:302-304` | شکست commit → user در Radius می‌ماند (dual-write بدون compensation مطمئن) |
| [ ] | M4 | `GetAvailableRealm` با `.First()` | `ServerManagementService.cs:40-44` | پر بودن همه realm ها = `InvalidOperationException` خام (500)؛ بدون reservation → overbooking هم‌زمان |
| [ ] | M5 | dictionary indexer بدون گارد در DeactivateInvalidRadiusUsers | `Infrastructure/Services/AccountRadiusSyncService.cs:56-80` | realm بدون plan state = `KeyNotFoundException` → کل job ساعتی abort |
| [ ] | M6 | CheckUniqueData: شرط mobile به email گره خورده + مقایسه case-sensitive | `Infrastructure/Repository/AccountRepository.cs:113-127` | ثبت‌نام با موبایل بدون ایمیل، یکتایی موبایل را چک نمی‌کند؛ `d.Email == email` ordinal است ولی DB case-insensitive |
| [ ] | M7 | throw روی یک اکانت گمشده، کل batch هشدار را می‌کشد | `AccountMonitoringService.cs:114-117` | باید `continue` + log باشد |
| [ ] | M8 | آستانه «در حال اتمام» 0.1% به‌جای 10% | `Domain/Plan/Business/PlanStateBusiness.cs:8-13` | مقیاس percent ها 0–100 است؛ هشدار فقط وقتی 99.9% مصرف شده |
| [ ] | M9 | predicate مرده: `Created IS NULL` ولی `DEFAULT GETDATE()` | `Infrastructure/Repository/RenewalRepository.cs:13` + `Database/LocalDatabase/Renewal.sql:17` | `GetNotPaid` همیشه خالی؛ شاخه unpaid در GenerateInvoice هرگز اجرا نمی‌شود |
| [ ] | M10 | کد reset روی اکانت غیرفعال burn می‌شود + TOCTOU دوبار مصرف | `AuthApplication.cs:178-193`، `Infrastructure/Repository/ResetPassRepository.cs:19-22` | کد خوانده/حذف می‌شود ولی عملیات fail؛ دو درخواست هم‌زمان هر دو می‌خوانند |
| [ ] | M11 | سری سوم چارت ترافیک به‌اشتباه «Download» | `VpnApplication.cs:181-185` | دو سری هم‌نام (یکی Total) |

## MEDIUM — امنیت و زیرساخت

| چک | # | عنوان | مکان | شرح |
|---|---|---|---|---|
| [ ] | M12 | credentials روتر/RadiusDesk به‌صورت plaintext JSON در جدول Server | `Domain/Servers/Entity/ServerEntity.cs:33-41` + `JsonType/SshConfig.cs:7` و غیره | خواندن DB = مالکیت همه روترها؛ بدون encryption-at-rest |
| [ ] | M13 | passphrase گواهی OVPN در لاگ Serilog | `ServerBridge/Ssh/SshConnection.cs:15` + `MikrotikDirectService.cs:146-148` | `Log.Information` شامل command کامل با export-passphrase |
| [ ] | M14 | anchor ضعیف regex تزریق (`$` قبل از `\n` تطبیق می‌شود) + `\w` یونیکدی | `ServerBridge/InjectionRegex.cs:7-11`، `ServerBridge/Ssh/ProcessService.cs:129-130` | `\n` انتهایی از `^[0-9a-fA-F]+$` رد می‌شود و به CLI روتر تزریق |
| [ ] | M15 | مبلغ پرداخت از client بدون هیچ bound | `Portal/Controllers/BillingController.cs:21-27` → `BillingApplication.cs:29-43` | مقدار منفی/سرریز وارد math موجودی می‌شود |
| [ ] | M16 | token ادمین RadiusDesk در URL query | `FreeRadius/WebService/RadiusDeskService.cs:441-490` | افشا در proxy/access log |
| [ ] | M17 | sync-over-async در مسیرهای request/job | `Infrastructure/Services/SessionRadiusSyncService.cs:104` (`.Result`)، `AuthApplication.cs:169` (`Task.WaitAll`)، `Infrastructure/Services/ServerEntityExtension.cs:14` | blocking + ریسک deadlock |

## MEDIUM — پرفورمنس

| چک | # | عنوان | مکان | شرح |
|---|---|---|---|---|
| [ ] | M18 | UPDATE تک‌ردیفی fire-and-forget به‌جای bulk موجود | `AccountMonitoringService.cs:66,172-176` | N round-trip + N task بی‌ناظر در هر cycle هشدار |
| [ ] | M19 | `GetAllActiveRadius` بدون cache، ۳-۴ بار در هر job و در هر API call | `SessionRadiusSyncService.cs:24,52,78,104`، `AccountRadiusSyncService.cs:30,55,90,115,155,167` | جدول کوچک؛ cache کوتاه-TTL با `IMemoryCache` (ثبت شده) کافی است |
| [ ] | M20 | await های متوالی مستقل در UpdateTrafficData | `ServerManagementService.cs:110-114` | شبکه (روترها) قبل از شروع MSSQL/Realm تمام می‌شود؛ latency جمعی |
| [ ] | M21 | SSH/tik4net: connection per-op، بدون timeout/keepalive/retry | `ServerBridge/Ssh/SshHandler.cs:15-17`، `ServerBridge/Tik4net/Tik4NetHandler.cs:14-23`، `SshConnection.cs:17` (sync `RunCommand`) | full handshake هر بار؛ روتر hang = توقف job بدون cancellation |
| [ ] | M22 | `SmtpClient` بدون dispose + خواندن template از disk در هر ارسال | `ServerBridge/Email/EmailHandler.cs:24-34`، `OutSource/EmailService.cs:25,49,77` | نشت connection؛ template ثابت است |
| [ ] | M23 | `RegexOptions.Compiled` در هر اجرای script | `ServerBridge/Ssh/ProcessService.cs:109` | کامپایل مجدد الگو در هر CheckOn/ActivateOn |

## MEDIUM — فرانتند

| چک | # | عنوان | مکان | شرح |
|---|---|---|---|---|
| [ ] | M24 | پاسخ‌های out-of-order در estimate (بدون switchMap) | `Frontend/src/app/renewal/renewal.component.ts:126-163` | قیمت/زمان نمایش‌داده‌شده ممکن است از درخواست stale باشد؛ کاربر علیه قیمت اشتباه تمدید می‌کند |
| [ ] | M25 | double-submit روی renewal/pay/register/login | `renewal.component.html:64`، `payment.component.html:68`، `register.component.html:175`، `login.component.ts:82-93` | دکمه‌ها در حین درخواست فعال می‌مانند (تشدیدکننده C4/C5) |
| [ ] | M26 | نشت subscription (هیچ unsubscribe/takeUntil در کل src نیست) | `dashboard.component.ts:210`، `history.component.ts:89`، `traffic-chart.component.ts:66`، `register.component.ts:121` | component های destroyed به root-singleton گوش می‌دهند و HTTP ghost fire می‌کنند |
| [ ] | M27 | translate pipe به‌صورت `pure:false` + متدها در template، بدون OnPush | `Frontend/src/app/@services/translation/translation-pipe.ts:4-7`، `dashboard.component.html` | ترجمه و توابع در هر CD cycle اجرا می‌شوند |

---

## LOW

| چک | # | عنوان | مکان |
|---|---|---|---|
| [ ] | L1 | `printMoney` ضرب دستی با `+',000'` (خرابی اعشار/صفر/منفی) | `Frontend/src/app/@services/message-handler/money-printer.ts:14` |
| [ ] | L2 | static event `OnRenewal` با null-check race، بدون هیچ subscriber | `Backend/Application/Plan/IPlanApplication.cs:8,20-25` |
| [ ] | L3 | شرط‌های مرده: `Count < 0`؛ شاخه غیرقابل دسترس email/mobile | `Backend/Application/Billing/BillingApplication.cs:143`، `Domain/Account/Business/AccountBusiness.cs:44-47` |
| [ ] | L4 | `console.log(result)` باقی‌مانده | `Frontend/src/app/renewal/renewal.component.ts:110` |
| [ ] | L5 | CoreUI CSS از CDN (jsdelivr) — برای محصول ایران‌محور نقطه شکست | `Frontend/src/index.html:11-13` |
| [ ] | L6 | errorHandler خطاهای transport را دفن می‌کند؛ HTML خطای 4xx/5xx ناخوانا | `Frontend/src/app/@services/api-services/http-client.ts:93-104` |
| [ ] | L7 | `DateTime.Now` در همه‌جا + `ToLocalTime()` در converter | `Backend/Portal/Context/UnixTimestampConverter.cs:28` و موارد متعدد؛ جابه‌جایی TZ/DST = اعوجاج انقضا |
| [ ] | L8 | correlated `EXISTS ... order by limit 1` در MySQL (مسیر dormant RadiusDesk) | `Backend/FreeRadius/Repository/UserPlanStateRepository.cs:59-79` |
| [ ] | L9 | login بدون lockout + مقایسه non-constant-time؛ `VpnController` پیام «wrong login password» برای تایید OVPN | `Backend/Application/Authentication/AuthApplication.cs:35`، `Backend/Portal/Controllers/VpnController.cs:35-42` |
| [ ] | L10 | منوی تم انگلیسی «Light/Dark/Auto» در UI فارسی | `Frontend/src/app/default-layout/default-header/default-header.component.html:58-61` |
| [ ] | L11 | در `CheckUniqueData` پنجره TOCTOU بین check و insert بدون unique-constraint fallback | `Backend/Application/Authentication/AuthApplication.cs:198-218` |

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
| **P1** | داده ترافیک و job ها | C1 → H18 → M5 → H16 → H17 → M18 + ایندکس‌های H20 (به‌صورت migration) | اجرای JobInterval دوبار پشت‌سرهم بدون درج تکراری `TrafficData` و بدون نشت connection |
| **P2** | پرداخت فرانتند | C3 → M25 (نیازمند تعیین قرارداد API واقعی: مسیرها/body/redirect) | `yarn build` سبز + مسیر پرداخت با invoice واقعی |
| **P3** | احراز هویت و امنیت | C6 → H1 → H12 → H5 → H6 → H7 → M13 → M14 | تست تغییر پسورد/reset/lockout؛ لاگ بدون secret |
| **P4** | زیرساخت job و I/O | H2 → H3 → H4 → M17 → M19 → M20 → M21 → M22 | job بدون هم‌پوشانی؛ بدون `.Result`/`WaitAll` |
| **P5** | باقی موارد | همه M/L باقی‌مانده (M1-M12, M16, M23, M24, M26, M27, L1-L11) | quick-win های جداگانه |

## سوالات باز (پیش از شروع فاز مربوطه تعیین شود)

- **P2**: آیا مسیر `payment-request`/redirect درگاه از قبل طراحی شده یا قرارداد API پرداخت از نو تعریف شود؟
- **P3**: برای پسورد پرتال (C6) سیاست rollout مهاجرت هش (force-reset در login بعدی یا dual-hash) چیست؟

## طرح راستی‌آزمایی عمومی (پس از هر فاز)

- `dotnet build Backend/Photon-Bypass.sln` و `dotnet test Backend/Photon-Bypass.sln` سبز.
- سناریوی دستی: ثبت‌نام → تمدید بدون پرداخت → صدور فاکتور → callback شبیه‌سازی‌شده (دوبار، برای idempotency) → تطابق ردیف‌های Wallet/Renewal.
- `cd Frontend && yarn build` سبز.

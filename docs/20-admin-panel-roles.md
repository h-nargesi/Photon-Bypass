# docs/20-admin-panel-roles.md — پنل مدیریت: مدل نقش‌ها، تصمیمات D1–D13 و نقشه راه فازبندی‌شده

> **English summary**: Decision record for the admin/manager panel built on the existing single Portal host. Introduces a three-valued `Account.Role` (User/Manager/Admin, admin = one seeded account, no promotion path) plus a `UserTypes.AllowSelfService` flag that makes "dependent user" (وابسته) a first-class concept. Records the agreed decisions D1–D13, the final access matrix, a six-phase implementation roadmap (roles/auth → payer separation in the money path → admin wallet transactions → user management → server CRUD → role-based frontend), risks, and per-phase validation commands. Existing portal flows are never rewritten; only new capabilities are layered in.

> **ماهیت**: ثبت تصمیمات مالک (D1–D13) و نقشه راه پیاده‌سازی فازبندی‌شده. اصل توافق‌شده: هیچ جریان پیاده‌شده پرتال دوباره‌نویسی نمی‌شود؛ فقط قابلیت‌های جدید به لایه‌های موجود اضافه می‌شوند. هاست `Admin` کنار گذاشته می‌شود (یک هاست Portal + یک اپ Angular).

## ۱. زمینه (راستی‌آزمایی‌شده در کد)

- **Delegation موجود**: `AccountEntity.Owner` → `AccountRepository.GetActiveTargetArea` → کش `AccessService` (کلید `TargetArea|{username}`، TTL 2h، پرشده در لاگین از `AuthApplication.CheckUserPassword`) → `ResultHandlerController.LoadJobContext(target)` با 403. فرانت: `user-servcie.ts` + مودال `default-layout/user-selector-modal` انتخابگر target و صفحات فعلی `target` را query-param جابه‌جا می‌کنند.
- **تا قبل از این سند نقشی وجود نداشت**: `UserTypes` فقط فلگ بیزینسی (`OldUser=1`, `AllowMonthly=2`) بود؛ JWT فقط `UniqueName`/`Name` داشت (`AuthController.GenerateToken`) و «ادمین» با شرت‌کات `account.Id == 1` شناخته می‌شد.
- **مسیر پول**: overload داخلی `PlanApplication.Renewal(int account_id, ...)` از همین حالا payer (`invoice.AccountId`) را از target (پسوند `t|` در رشته action) جدا می‌کند؛ اما `Renewal(string target, ...)` ایده target را inject می‌کند و دبت کیف و `CheckMoneyNeed` روی target می‌زنند — در فاز ۲ این سه نقطه به payer منتقل می‌شوند.
- **فاکتور/تسویه**: `SettlementGate.RunExclusively(invoice.AccountId)` قفل حساب payer را می‌گیرد؛ تسویه/رسید/`RunRenewalAfterReceipt` همه به `invoice.AccountId` وصل‌اند — با D4 سازگار.
- **Wallet**: دفتر تراکنش append-only دارد ولی `ActorId` ندارد و endpoint تاریخچه کیف پول وجود ندارد (فاز ۳).
- **ثبت‌نام عمومی** (`AuthController.Register`/`AuthApplication.Register`) فعال بود — با D11 فلگ می‌شود.
- **تنها endpoint عمومی موجود**: `GET /api/basics/prices` (`BasicsController`) — الگوی endpoint عمومی `app-config`.

## ۲. تصمیمات قطعی‌شده با مالک (D1–D13)

| # | تصمیم |
|---|---|
| D1 | زیر دست بودن با self-service مستقل است؛ کاربر زیر دست می‌تواند کاربر کامل پرتال باشد |
| D2 | ستون `Role` سه‌مقداری (`User/Manager/Admin`) + فلگ `UserTypes.AllowSelfService`؛ دو گانه کاربر برگ: «کاربر مستقل / کاربر وابسته» |
| D3 | ساخت کاربر زیر دست توسط ادمین و مدیر (با ماتریس §۳) |
| D4 | «پرداخت‌کننده = عمل‌کننده»: فاکتور/واریز/کسر همیشه روی حساب متقاضی؛ Renewal روی target؛ پول هرگز وارد کیف target نمی‌شود |
| D5 | تراکنش ادمین: Credit+Debit، توضیح اجباری، **موجودی منفی مجاز**، دفتر append-only، ثبت `ActorId` |
| D6 | وابسته: وضعیت/تاریخچه + کانفیگ + چرخش UUID مجاز؛ کیف پول و تمدید ممنوع (UI پنهان + سرور 403) |
| D7 | یک هاست واحد (Portal) + یک اپ Angular با route/layout نقش‌دار |
| D8 | CRUD کامل سرورها در همین مسیر؛ **بدون دست‌زدن به adapter/dispatch/`ServerFeature` مسیر docs/16** |
| D9 | ادمین فقط **یک** حساب است (seed)؛ هیچ مسیر ارتقا به ادمین نیست؛ هیچ‌کس نقش خودش را عوض نمی‌کند؛ ادمین نمی‌تواند خودش را تنزل/غیرفعال کند |
| D10 | ماتریس تخصیص (§۳) |
| D11 | ثبت‌نام عمومی در انتشار اول **غیرفعال** (فلگ کانفیگ `ManagementOptions:AllowRegistration`، پیش‌فرض خاموش)؛ فعال‌سازی بعدی کاربر «مستقل» می‌سازد |
| D12 | وابسته می‌تواند پروفایل و پسورد خودش را ویرایش کند (پسورد اولیه را سازنده ست می‌کند) |
| D13 | مدیر می‌تواند پسورد زیر دست خودش را بازنشانی کند و او را غیرفعال/فعال کند؛ ادمین برای هرکس (بجز خودش — D9) |

## ۳. ماتریس دسترسی نهایی

| قابلیت | وابسته | مستقل | مدیر | ادمین (تنها یکی) |
|---|---|---|---|---|
| دیدن وضعیت/تاریخچه/کانفیگ/چرخش خود | ✅ | ✅ | ✅ | ✅ |
| ویرایش پروفایل/پسورد خود | ✅ (D12) | ✅ | ✅ | ✅ |
| کیف پول و تمدید **خود** | ❌ (403) | ✅ | ✅ | ✅ |
| عمل از طرف دیگران | — | — | فقط زیر دست‌های خودش (بدون دیدن کیف پول آنها) | هر کاربری |
| پرداخت از طرف | — | — | از کیف **خود مدیر** (D4) | از کیف خودش |
| ساخت کاربر | ❌ | ❌ | زیر دست مستقل/وابسته + تغییر مستقل/وابسته فقط برای زیر دست‌های خودش | همه انواع بجز ادمین (با/بدون Owner) |
| تغییر نقش | ❌ | ❌ | ❌ | همه بجز خودش (User↔Manager) |
| attach/detach زیر دست (Owner) | ❌ | ❌ | ❌ | هر کاربری به هر مدیر |
| تراکنش شفاف کیف پول | ❌ | ❌ | ❌ | ✅ (D5) |
| بازنشانی پسورد/غیرفعال‌سازی دیگران | ❌ | ❌ | فقط زیر دست‌های خودش (D13) | هرکس بجز خودش |
| CRUD سرورها | ❌ | ❌ | ❌ | ✅ (D8) |
| دیدن کیف پول دیگران | — | — | ❌ حتی زیر دست‌ها (D6/D10) | ✅ |

گاردهای نقش: تنزل Manager→User نیازمند نبود زیر دست فعال (اول detach/reassign)؛ ادمین نمی‌تواند خودش را غیرفعال/تنزل کند؛ تغییر نقش/Owner تا لاگین بعدی در JWT/کش اعمال می‌شود (انقضای JWT 1h — قابل قبول اعلام شد).

## ۴. نقشه راه پیاده‌سازی فازبندی‌شده

هر فاز یک تحویل‌شدنی مستقل است، به فاز قبلی وابسته است و با تست/اعتبارسنجی خودش بسته می‌شود.

| فاز | تحویل‌شدنی | وابستگی | وضعیت |
|---|---|---|---|
| ۱ | مدل نقش در دیتابیس + authorization (claim/policy) + خاموشی ثبت‌نام | — | ✅ اجرا شد (§۷) |
| ۲ | تفکیک payer در مسیر پول + گاردهای وابسته | ۱ | باز |
| ۳ | تراکنش شفاف ادمین + تاریخچه کیف پول | ۱ (۲ توصیه می‌شود) | باز |
| ۴ | لیست و مدیریت کاربران (مدیر/ادمین) | ۱ | باز |
| ۵ | CRUD سرورها | ۱ | باز |
| ۶ | فرانت نقش‌دار (صفحات مدیر/ادمین، گیت وابسته) | فازهای ۲ تا ۵ | باز |

### فاز ۱ — مدل نقش و authorization

1. ستون `Role TINYINT NOT NULL DEFAULT 0` و seed ادمین (`Account.sql`، username=`admin` با `Role=2` و `UserType=4` = AllowSelfService) — داخل مجموعه‌ی fresh-install؛ روی دیتابیس سناریو هم اعمال می‌شود (ساختار تست از همان پوشه می‌خواند).
2. Domain: enum `AccountRole : byte { User=0, Manager=1, Admin=2 }`؛ `AccountEntity.Role`؛ `UserTypes.AllowSelfService = 4`.
3. JWT/لاگین: `GenerateToken` یک claim نقش (`ClaimTypes.Role`) اضافه می‌کند؛ `UserModel` (پاسخ لاگین/get-user) `Role` + `AllowSelfService` حمل می‌کند (فرانت برای گیت منو لازم دارد). جایگزینی شرت‌کات `account.Id == 1` با `Role == Admin` در `CheckUserPassword`.
4. `IAccessService`: مقدار کش → مدل `TargetAreaModel { Role, Targets }`؛ `CheckAccess` برای `Role==Admin` همیشه true؛ متد `InvalidateTargetArea(username)`؛ به‌روزرسانی تست‌ها.
5. Policyهای `Manager`/`Admin` در `Portal/ServiceFactory.cs` (از claim نقش؛ Manager شامل Admin)؛ کنترلرهای فازهای بعد با `[Authorize(Policy=...)]`.
6. D11: `ManagementOptions.AllowRegistration` (پیش‌فرض false) — گیت در `AuthApplication.Register` (تک‌نقطه اعمال، همه مسیرها را می‌پوشاند)؛ endpoint عمومی `GET /api/basics/app-config` که `{allowRegistration}` را برمی‌گرداند تا فرانت لینک/صفحه ثبت‌نام را پنهان کند (UI در فاز ۶).

### فاز ۲ — تفکیک payer در مسیر پول (حساس‌ترین — تست‌اول)

7. **اول تست‌ها**: گسترش `RenewalRollbackTest`/`BillingApplicationTest` با سناریوها: مدیر-پرداخت‌کننده (فاکتور/واریز/دبت روی مدیر، Renewal روی target)، وابسته-رد-تمدید-خود، rollback.
8. تغییرات موضعی (سه نقطه):
   - `PlanApplication.Renewal(string target, ...)`: گارد وابسته (`target==actor && !AllowSelfService` → 403)؛ resolve ایدی actor با `AccountRepository.GetActiveAccountId` و inject با overload کامل `InjectJobContext(actor_id, actor_username, target)` (آینه مسیر replay) تا `IssuePlanInvoice` فاکتور را روی actor صادر کند.
   - هسته خصوصی `Renewal`: پارامتر payer جدا؛ `CheckMoneyNeed`، ردیف دبت Wallet (`AccountId = payer`) و `RenewalResult.CurrentPrice` روی payer؛ `RenewalEntity`/اعمال پلن/sync/rollback روی target.
   - مسیرهای تسویه/رسید/کال‌بک (`BillingApplication`) بدون تغییر ساختاری می‌مانند چون به `invoice.AccountId` (=payer) و action (دارای target) وصل‌اند؛ فاکتورهای pending قبل از deploy بدون migration درست replay می‌شوند.
9. اصلاح ردیف‌های History: ردیف مالی «از حساب کم شد» → `Target = payer.Id`؛ ردیف «پلن تمدید شد» روی target می‌ماند (Issuer = عمل‌کننده)؛ ردیف خطا روی target می‌ماند.
10. جلوگیری از نشت داده کیف پول در نمای «از طرف»: فیلدهای موجودی/کیف فقط در نمای self پر شوند.
11. فرانت: صفحات پرداخت/رسید فاکتورِ «از طرف» **بدون target** (نمای خود مدیر) باز شوند.
12. گارد وابسته روی همه endpointهای مالی self: `issue-top-up`، `pay`/`get-invoice`، `register-receipt`، `settle-wallet`، صدور/برآورد تمدید، `wallet/history`.

### فاز ۳ — تراکنش شفاف ادمین + تاریخچه کیف پول

13. اسکریپت `WalletActor.sql`: `ALTER TABLE Wallet ADD ActorId INT NULL`؛ `WalletEntity.ActorId`.
14. `WalletController`: `POST /api/wallet/adjust` (Admin: `{target, direction, amount>0, description!}` → ردیف Completed با ActorId + `WalletThresholdSrv.CheckAndApply()`)؛ `GET /api/wallet/history?target=&page=` (خودِ صاحب حساب با `AllowSelfService`، یا ادمین — **مدیر به کیف زیر دست‌ها دسترسی ندارد**).
15. `IWalletRepository.GetHistory(accountId, paging)` + کوئری Dapper صفحه‌بندی سمت سرور.

### فاز ۴ — لیست و مدیریت کاربران

16. `IAccountRepository.SearchAccounts(...)`: join با ویو `TotalPlanState`؛ شامل غیرفعال‌ها؛ مدیر → `Owner=self`؛ ادمین → همه + جستجو + صفحه‌بندی.
17. `UserController`: `GET /api/user/list` (Manager)؛ `POST /api/user/create` (Manager: مدیر → `Owner=self` + مستقل/وابسته + پسورد اولیه؛ ادمین → هر ترکیب بجز ادمین D9؛ خطای 409 از الگوی unique-index موجود)؛ `POST /api/user/set-password` (overload موجود `AccountApplication.ChangePassword(account, password)`؛ مدیر → زیر دست خودش، ادمین → هرکس بجز خودش)؛ `POST /api/user/set-role` (ادمین، بجز خودش؛ تنزل مدیرِ دارای زیر دست فعال → خطا)؛ `POST /api/user/set-owner` (ادمین: attach/detach + `InvalidateTargetArea`)؛ `POST /api/user/set-self-service` (ادمین: هرکس؛ مدیر: فقط زیر دست خودش)؛ `POST /api/user/deactivate` (D13؛ الگوی `SetInactive` موجود).

### فاز ۵ — CRUD سرورها (D8)

18. `ServerController` (Admin): `GET /api/server/list` (بدون کانفیگ رمزگشایی‌شده — فقط فلگ «has-config» و فیلدهای اسکالر)؛ `POST /api/server/create|update|deactivate` (کانفیگ JSON ارسالی، سمت سرور deserialize → `ServerConfigProtection.Protect`؛ نوشتن بدون خواندن). مرز docs/16: هیچ تغییری در keyed dispatch/adapter/`ServerFeature`.

### فاز ۶ — فرانت (همان اپ)

19. `user-servcie.ts`: ذخیره `Role`/`AllowSelfService` از لاگین؛ route guard + آیتم‌های منوی `default-layout` نقش‌محور.
20. وابسته (نمای self): حذف منو/صفحات کیف پول و تمدید؛ داشبورد/تاریخچه/کانفیگ/چرخش می‌ماند؛ ویرایش پروفایل/پسورد می‌ماند (D12).
21. مدیر: صفحه «کاربران من» (جدول + جستجو/صفحه‌بندی + badge مستقل/وابسته + غیرفعال‌ها)، فرم ساخت زیر دست، toggle مستقل/وابسته، بازنشانی پسورد/غیرفعال‌سازی زیر دست (D13)؛ عمل «از طرف» با همان الگوی query-param `target`.
22. ادمین: لیست همه کاربران + set-role/set-owner/set-self-service/deactivate، فرم adjust کیف پول + تاریخچه کیف، صفحات CRUD سرور. انتخابگر «از طرف» ادمین از لیست کاربران (نه فقط targetArea).
23. ثبت‌نام: لینک/صفحه Register با `app-config` عمومی پنهان (D11).

## ۵. ریسک‌ها و نکات

- **مسیر پول** (فاز ۲): بالاترین ریسک؛ با «تست‌اول» (گام ۷) و تغییر موضعی سه‌نقطه‌ای مهار می‌شود؛ semantics rollback و `SettlementGate` دست‌نخورده می‌مانند. قرارداد رشته action (`{username}t|{count}u|{days}d|{gigs}g`) قرارداد unstable بین دو overload تمدید است — parser دست نخورد.
- اصلاح ردیف‌های History (گام ۹) بخشی از همان تراکنش تمدید است — همان rollback را پیش و پس از commit می‌پیماید.
- تغییر شکل کش `AccessService` → تست‌های `AccessService` و فراخوان‌های `LoginEvent` همگام به‌روز شدند.
- همپوشانی docs/16: فقط با رعایت مرز گام ۱۸.
- تغییر نقش/Owner تا توکن بعدی (انقضای JWT 1h) اعمال نمی‌شود — قابل قبول؛ همین‌جا ثبت شد.
- کانفیگ سرور حاوی رازهاست: هرگز در پاسخ GET بازگردانده نشود (نوشتن بدون خواندن).

## ۶. پیش‌نیاز امنیتی (شرط پیش از انتشار هر release آینده)

- تعویض/حذف پسورد seed ادمین (`Account.sql`) — اپراتوری، در ران‌بوک deploy (docs/19).
- انتقال کلید JWT و connection stringها به env/secret (docs/12 security TODOs).

## ۷. ثبت اجرا

- **فاز ۱ (۲۰۲۶-۱۰-۱۶)**: اجرا شد. ستون/seed نقش‌ها داخل `Account.sql` (خودکار در دیتابیس سناریوی تست از مسیر `StructureFilesPath`)؛ `AccountRole`/`AccountEntity.Role`/`UserTypes.AllowSelfService=4`؛ claim نقش در `GenerateToken` + `Role`/`AllowSelfService` در `UserModel` (لاگین و get-user)؛ جایگزینی `Id == 1` با `Role == Admin` در `CheckUserPassword`؛ کش `AccessService` → `TargetAreaModel { Role, Targets }` با bypass ادمین + `InvalidateTargetArea`؛ policyهای `Manager`/`Admin` در `AddPortalServices` (Manager شامل Admin)؛ `ManagementOptions.AllowRegistration` (پیش‌فرض false) با گیت در `AuthApplication.Register` (تک‌نقطه؛ controller به application واگذار می‌کند) + پرچم `AllowSelfService` برای کاربر ثبت‌نامی (D11: مستقل)؛ endpoint عمومی `GET /api/basics/app-config` → `{allowRegistration}`. تست: `ProgramLevelInitializer.Factory` و `ManagementOptionsMoq` صریحاً `AllowRegistration=true` گذاشتند (سناریوهای موجود باید ثبت‌نام کنند)؛ تست‌های جدید: bypass/Invalidate/عدم-bypass-مدیر در `AccessService`، `Register_Disabled*`/پرچم مستقل در `AuthAppTest`، `BasicsApplicationTest.GetAppConfig_MirrorsRegistrationFlag` (هر دو قطب)، `AppConfigTest` (عمومی بودن + مقدار فلگ)، `RolesAuthorizationTest` (claim نقش در توکن، seed اسکریپت در get-user، اعمال policyها روی principalهای validate-شده و سنتزی).

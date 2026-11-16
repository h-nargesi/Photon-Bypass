# تفکیک PanelUser از VpnAccount — سند نیازمندی و نقشه راه اجرا

> وضعیت: نیازمندی با کاربر تأیید شد (مصاحبه ۲۰۲۶-۱۱-۱۶). پیش از اولین انتشار — بدون مهاجرت داده، تغییرات شکستن API/frontend آزاد.

---

## ۱. تصمیمات تأییدشده (D1–D12)

| # | تصمیم |
|---|---|
| D1 | جدول `Account` به دو جدول تفکیک می‌شود: `PanelUser` (لاگین پنل) و `VpnAccount` (اکانت VPN). رابطه `PanelUser 1—N VpnAccount`. |
| D2 | تعداد اکانت VPN هر کاربر در هر realm **بدون سقف** است؛ تفکیک با یوزرنیم یکتای VPN. |
| D3 | یوزرنیم VPN = `<یوزرنیم-پنل><پسوند>` — قالب اجباری با شروع از یوزرنیم پنل؛ پسوند با پیشنهاد سیستم (شماره‌ی افزایشی) و قابل ویرایش کاربر؛ **یکتایی سراسری** باقی می‌ماند. |
| D4 | realm موقع «خرید اکانت جدید» را **کاربر** از لیست realmهای فعال انتخاب می‌کند (نام/وضعیت ظرفیت نمایش داده می‌شود). |
| D5 | تمدید = انباشت روی اکانت VPN انتخابی در همان realm و همان یوزرنیم (مدل انباشت D16 فعلی، per VpnAccount می‌شود). |
| D6 | تمدید اکانتِ **≥ N روز غیرفعال** (پیش‌فرض ۷، **قابل تنظیم** از `ManagementOptions`) → کاربر *باید* realm را دوباره انتخاب کند (می‌تواند همان قبلی را هم انتخاب کند اگر فعال است). یوزرنیم و انباشته‌ی باقی‌مانده با اکانت به realm جدید مهاجرت می‌کنند (همان منطق فعلی `SyncUserAndActive`، با انتخاب کاربر به‌جای `GetAvailableRealm` خودکار). |
| D7 | رابطه‌ی مدیر/زیرمجموعه (`Owner`) روی **PanelUser** می‌ماند (docs/20 حفظ می‌شود). مدیر کاربران زیرمجموعه + اکانت‌های VPNشان را می‌بیند/مدیریت می‌کند. |
| D8 | کیف پول و فاکتور روی **PanelUser** (پرداخت‌کننده = اکانتور؛ docs/20 D4). |
| D9 | ثبت‌نام فقط PanelUser می‌سازد. اکانت VPN + اولین Renewal **در یک تراکنش همراه اولین خرید** ساخته می‌شوند. هیچ اکانت VPN بدون پلن وجود ندارد. |
| D10 | قیمت مستقل از realm است؛ `CalculationMethod` روی PanelUser می‌ماند. |
| D11 | `History` به PanelUser لنگر می‌شود (+ ستون اختیاری `VpnAccountId` برای رویدادهای اکانتی). `ResetPassword` و `TelegramLink` و `TelegramChatId` روی PanelUser. |
| D12 | وضعیت دوپاره حل می‌شود: `PanelUser.Status` فقط Portal (0=Active, 2=Deactivated/بلاک لاگین)؛ `VpnAccount.Status` مستقل (0=Active, 1=Suspended, 2=Deactivated). تعلیق VPN به ازای هر اکانت انجام می‌شود، نه کل کاربر. |

### فرض‌های تأییدشده‌ی همراه
- بدون مهاجرت: اسکریپت‌های `Database/LocalDatabase/` fresh-install بازنویسی می‌شوند (تغییرات در `CREATE TABLE` تاشده می‌شوند، طبق قرارداد فعلی).
- `target` برای کاربر عادی معنایش از «یوزرنیم کاربر دیگر» به «شناسه‌ی اکانت VPN خودش» تغییر می‌کند.

---

## ۲. مدل داده هدف

### جداول جدید/تغییر یافته

**`PanelUser`** (تکامل `Account`؛ ستون‌های VPN حذف می‌شوند):
```
Id, Status(TINYINT 0/2), Username UNIQUE, Password, Owner FK→PanelUser,
Created, Name, Surname, Mobile, Email, IsMobileValid, IsEmailValid,
CalculationMethod FK→Price, UserType, Role, TelegramChatId
```
حذف‌شده‌ها: `VpnPassword`, `LastWarningTime`, `LastCredentialRotateTime`, `SendWarning` (به VpnAccount می‌روند).

**`VpnAccount`** (جدول جدید):
```
Id, UserId FK→PanelUser NOT NULL, RealmId FK→Realm NOT NULL,
Username UNIQUE (سراسری), VpnPassword, Status (0/1/2),
SendWarning, LastWarningTime, LastCredentialRotateTime, Created
```
قاعده‌ی یوزرنیم (D3): باید با یوزرنیمِ مالک شروع شود؛ طول کل ≤ ۳۲ (الگوی فعلی ۱۸ کاراکتری کافی نیست)؛ charset فعلی (`^[a-zA-Z][a-zA-Z0-9_-]{…}[a-zA-Z0-9]$`) حفظ شود؛ پیشنهاد سیستم = پسوند عددی افزایشی اولین مقدار آزاد.

**FKهای بازنویسی‌شده:**
| جدول | تغییر |
|---|---|
| `Renewal` | `AccountId`→`VpnAccountId`؛ **حذف `RestrictedRealmId`** — حقیقت realm روی `VpnAccount.RealmId` است (پیش‌نیاز: بررسی گزارش‌ها/کدهایی که ستون قدیمی را می‌خوانند؛ اگر گزارش تاریخی لازم شد، به‌صورت snapshot نگه داشته شود) |
| `TrafficData` | `AccountId`→`VpnAccountId` (کلید سنتزی x-ui هم `xui:{vpnAccountId}:{renewalId}:{yyyyMMdd}` می‌شود) |
| `Wallet` | `AccountId`→`PanelUserId`؛ `ActorId`→`PanelUserId` |
| `Invoice` | `AccountId`→`PanelUserId`؛ + `VpnAccountId INT NULL` برای فاکتورهای Plan (گرایش: بله) |
| `History` | `Target`/`Issuer`→`PanelUserId`؛ + `VpnAccountId INT NULL` |
| `TelegramLink`، `ResetPassword` | `AccountId`→`PanelUserId` |

**Viewها:** `TotalPlanState`/`PlanState` روی `VpnAccount` بازنویسی می‌شوند (Id در نتیجه = شناسه‌ی اکانت VPN؛ انباشت و بازه‌های ترافیک per `VpnAccountId`).

**ایندکس‌ها:** `IX_PanelUser_Owner` (جانشین IX_Account_Owner)، `IX_VpnAccount_UserId`، `IX_Renewal_VpnAccountId`، `IX_TrafficData_VpnAccountId_StartSession`، `IX_Wallet_PanelUserId`، `IX_History_PanelUser_Created`، `UK_VpnAccount_Username`، ایندکس‌های جدید `History(VpnAccountId)` در صورت نیاز.

**ساده‌سازی‌ی جانبی:** با NOT NULL بودن `VpnAccount.RealmId`، منطق bucket-0 (پخش کاربران بدون realm به همه‌ی realmها) در `AccountRadiusSyncService` حذف می‌شود — گروه‌بندی sync با `VpnAccount.RealmId`.

---

## ۳. جریان‌های هدف

1. **ثبت‌نام** → PanelUser (بدون اکانت).
2. **اولین خرید**: انتخاب realm (از فعال‌ها) + یوزرنیم VPN (پیشنهاد سیستم/ویرایش کاربر) → ساخت `VpnAccount` + `Renewal` اول + sync رادیوس، در یک تراکنش.
3. **خریدهای بعدی** — دو مسیر صریح در UI/API:
   - *تمدید اکانت موجود*: انباشت در همان realm/یوزرنیم.
   - *اکانت جدید*: مثل مرحله‌ی ۲.
4. **تمدید اکانت راکد** (غیرفعالی ≥ `ManagementOptions.RealmReassignInactivityDays`، پیش‌فرض ۷): فرم تمدید، انتخاب realm را اجباری می‌کند؛ مهاجرت = deactivate در realm قدیمی + create در realm جدید (همان `SyncUserAndActive` فعلی) + به‌روزرسانی `VpnAccount.RealmId`.
5. **پنل کاربر**: لیست اکانت‌ها (یوزرنیم، realm، خلاصه‌ی پلن/ترافیک) + انتخاب اکانت فعال برای داشبورد/ترافیک/کانفیگ/تمدید.
6. **مدیر**: لیست زیرمجموعه‌ها + اکانت‌های هرکدام؛ **ادمین**: همه.
7. **هشدارها/غیرفعال‌سازی**: `AccountMonitoringService` و JobInterval به ازای هر `VpnAccount` کار می‌کنند (آستانه‌های ۷/۴۰ روز فعلی)، با کانال‌های اطلاع‌رسانی مالکِ PanelUser؛ throttle (`LastWarningTime`/`SendWarning`) per اکانت.

---

## ۴. فازهای اجرا (به همین ترتیب)

**فاز ۱ — اسکیمای DB:** بازنویسی `Account.sql`→`PanelUser.sql` + `VpnAccount.sql`، به‌روزرسانی FKها در `Renewal/TrafficData/Wallet/Invoice/History/TelegramLink/ResetPassword.sql`، بازنویسی `TotalPlanState.sql`/`PlanState.sql`، `Indexes.sql`؛ هم‌ترازی `Backend/Tests/Data/Sql/DbScenario/init.sql` (تولیدکننده‌ی تست، مرتب‌سازی توپولوژیک خودکار را دارد).

**فاز ۲ — Domain + Infrastructure:** تفکیک `AccountEntity` به `PanelUserEntity`+`VpnAccountEntity`؛ enumهای وضعیت جدید؛ `IAccountRepository`→`IPanelUserRepository`+`IVpnAccountRepository` (+ پیاده‌سازیهای Dapper)؛ `RenewalBusiness`/`PlanStateBusiness` per اکانت VPN؛ بازنویسی `PlanStateRepository` و `TotalPlanState`-خوان‌ها؛ `AccountRadiusSyncService`/`SessionRadiusSyncService` با گروه‌بندی `VpnAccount.RealmId` و حذف bucket-0؛ ثبت در `Infrastructure/ServiceFactory.cs` و `Domain/Account/Business/AccountBusiness.cs` (الگوی یوزرنیم جدید D3).

**فاز ۳ — Application:** `AuthApplication` (لاگین → کاربر + لیست اکانتها)؛ `PlanApplication` (سه مسیر خرید §۳؛ آستانه‌ی راکدی از `ManagementOptions`؛ حذف `-7` هاردکد در `PlanApplication.cs:281`)؛ `VpnApplication` per اکانت؛ `BillingApplication`/`WalletApplication` روی payer پنل؛ `UserManagementApplication` (لیست کاربران با اکانت‌های تودرتو؛ set-status دوسطحی D12)؛ `AccountMonitoringService`/`JobInterval` per اکانت؛ `IJobContext` (Target = اکانت VPN انتخابی) و `AccessService` (کش: نقش + مالکیت اکانت‌ها + زیرمجموعه‌های مدیر).

**فاز ۴ — Portal API:** قرارداد جدید target (شناسه‌ی اکانت VPN)؛ endpointهای جدید: `GET /api/vpn/accounts` (لیست اکانتهای من/زیرمجموعه)، `POST /api/plan/new-account` (خرید با realm+یوزرنیم)، تمدید با `accountId` و انتخاب realm در حالت راکد؛ user-levelها (`get-user`/`edit-user`/`history`/`wallet`) روی PanelUser؛ admin: عملیات per اکانت VPN (تعلیق/فعال). کنترلرها همه از `LoadJobContext` جدید عبور کنند.

**فاز ۵ — Frontend:** سوییچر اکانت در layout (جانشین `user-selector-modal` برای کاربر عادی)، فرم خرید/تمدید با گام انتخاب realm و یوزرنیم، داشبورد/ترافیک/کانفیگ per اکانت، صفحه‌ی `/users` با سطرهای کاربر + اکانت‌های تودرتو، wallet روی کاربر.

**فاز ۶ — تست و مستندات:** بازسازی Factها و Mock repoها (`Backend/Tests`)، به‌روزرسانی `docs/18-test-coverage.md` (§4 matrix) در همان PR طبق حکمریتی؛ سند تصمیم جدید `docs/22-panel-user-vpn-split.md` (D1–D12)؛ به‌روزرسانی `docs/03,08,09,11,12` و ER sketch؛ ثبت در ایندکس `AGENTS.md`.

**اعتبارسنجی هر فاز:** `dotnet build Backend/Photon-Bypass.sln` + `dotnet test Backend/Photon-Bypass.sln`؛ فاز ۵+: `cd Frontend && yarn test --watch=false --browsers=FirefoxHeadless`؛ سناریوی DB با initializer تست ساخته و اجرا شود.

---

## ۵. ریسک‌ها و نکات

- **پرخطرترین قطعه:** بازنویسی `TotalPlanState` (بازه‌های انباشته per `VpnAccountId`) — تست‌های `RenewalStackingTest` باید زودتر از همه سبز شوند.
- کلید سنتزی x-ui در `TrafficData.SessionId` شامل accountId است — هماهنگ با collector (`PlanUsageReader`/docs/17 D7).
- طول یوزرنیم VPN تا ۳۲ — بررسی محدودیت سمت RadiusDesk/UserManager/x-ui (نام کلاینت x-ui).
- `AccessService`/کش `TargetArea` تغییر شکل کامل می‌دهد؛ نکته‌ی امنیتی: هر endpoint باید مالکیت اکانت VPN را از طریق آن اعتبارسنجی کند، نه پارامتر خام.
- حذف `Renewal.RestrictedRealmId` فقط بعد از بررسی همه‌ی مصرف‌کنندگان (گزارش‌ها، `ServerManagementService.GetRealmLoad`، `PlanUsageReader.IsXuiRealm`) — در فاز ۲ فهرست کامل خروجی گرفته شود.

## ۶. نقاط باز (در فاز اجرا بسته می‌شود)

1. شکل نهایی API ادمین برای عملیات per اکانت VPN (تعلیق/تغییر پسورد VPN/چرخش UUID).
2. نگه‌داشتن snapshot تاریخی realm روی Renewal در صورت نیاز گزارش‌ها.
3. UX انتخاب realm (نمایش ظرفیت باقی‌مانده یا فقط نام).

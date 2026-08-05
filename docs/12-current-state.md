# docs/12-current-state.md — Implementation state & starting points

> **English summary**: The customer-facing flow is functional: auth/JWT, account CRUD, renewal + invoice/callback billing, dual-Radius sync, the 60-min Quartz job, email notifications, dynamic Roslyn pricing, and main frontend pages. Incomplete: the `Admin` panel (empty `AddAdminServices`, no controllers), `OutSource/SocialMediaService` (all stubs), `SetOVpnCertificate` (throws `NotImplementedException`), and WhatsApp/Google-Auth (gated or phase 4/5). For each gap there is a "where to start" pointer.

## Implemented / active (پیاده‌سازی‌شده)

- **Auth & JWT**: صدور/اعتبارسنجی توکن، `AccessService` چنداکانتی، فراموشی/بازنشانی پسورد (`docs/08`).
- **Account CRUD**: get-user, full-info, edit-user, change-pass, history.
- **Renewal + Billing**: برآورد قیمت، تمدید با تراکنش، فاکتور (`pay`/`get-invoice`) و کال‌بک درگاه (`payment-callback`) با replay اکشن تمدید (`docs/04`, `docs/08`).
- **Dual-Radius sync**: dispatch با keyed DI روی RadiusDesk و UserManager؛ `RunJob` موازی (`docs/06`).
- **`JobInterval`** (Quartz، ۶۰ دقیقه): `UpdateTrafficData`, `NotifSendServices`, `DeactivateInvalidRadiusUsers`, `InactiveAbandonedUsers`, `CheckUserServerBalance`.
- **Email**: فراموشی پسورد، گواهی، هشدار پایان (`EmailService`/`EmailHandler`).
- **`PriceCalculator`** پویا (Roslyn) با refresh خودکار روی `PriceRepository.OnSave` (`docs/05`).
- **Frontend**: صفحات اصلی Home/Login/Register/Forgot/Reset/Dashboard/History/Edit/Change-pass/Renewal/Payment.
- **MSSQL schema + `TotalPlanState` view** (`docs/09`).

## Stub / incomplete (نیمه‌کاره)

### `Backend/Admin` — پنل مدیریت ساخته نشده

- فقط `Program.cs` (همان زنجیره‌ی `.AddAppServices().AddPortalServices().AddAdminServices()`) و `ServiceFactory.cs` خالی (`AddAdminServices` هیچی ثبت نمی‌کند).
- هیچ کنترلری وجود ندارد؛ Admin فعلاً کلون Portal است.
- **نقطه‌ی شروع**: `Backend/Admin/ServiceFactory.cs:5` (`AddAdminServices`) و افزودن کنترلرهای مدیریت سرور/کاربر/پروفایل با `[Authorize]` و قواعد دسترسی ادمین. مدیریت سرورها/پروفایل/کاربر در `README.md:14-29` و `Architecture/TODO.md:24` آمده.
- توجه: `Backend/Administration/` یک پوشه‌ی یتیم با کنترلر خالی است و **در سولوشن نیست** — آن را با `Admin` اشتباه نگیرید.

### `OutSource/SocialMediaService` — همه‌ی متدها stub

`Backend/OutSource/SocialMediaService.cs` — هر پنج متد `Task.CompletedTask` برمی‌گردانند با `// TODO: Implement`:
- `AlarmServerCapacity(alarms)`
- `FinishServiceAlert(username, phone, type, left)`
- `InvalidPasswordAlert(username)`
- `NewUserRegistrationAlert(account)`
- `SendResetPasswordLink(email, hashCode)`

**نقطه‌ی شروع**: همان فایل + اینترفیس `ISocialMediaService` در `Domain/OutSource`. راهاندازی واقعی واتساپ با `#if SOCIAL` گیت شده (زیر را ببینید).

### `SetOVpnCertificate` — `NotImplementedException`

`Backend/MikrotikRadius/Application/MikrotikDirectService.cs:165` فعلاً فقط اعتبارسنجی یوزرنیم را می‌کند و سپس `throw new NotImplementedException()`. این متد برای پخش گواهی روی NASهای ثانویه استفاده می‌شود (`AccountRadiusSyncService.GetOVpnCertificate` آن را صدا می‌زند).
**نقطه‌ی شروع**: همان فایل + `MikrotikDirectService.GetOVpnCertificate` (که گواهی را می‌سازد) به‌عنوان الگو برای SSH/`ProcessService`.

### WhatsApp — gated پشت `#if SOCIAL`

- `AuthApplication.cs:104` (`#if !SOCIAL`) و `AccountMonitoringService.cs:144` (`#if SOCIAL`): مسیر واتساپ در زمان کامپایل با نماد `SOCIAL` فعال/غیرفعال می‌شود.
- **نقطه‌ی شروع**: تعریف `SOCIAL` در `<DefineConstants>` پروژه‌ی `Application` + پیاده‌سازی واقعی `SocialMediaService` (احتمالاً با HttpClient به WhatsApp Business API). فاز ۴ در `README.md:296`.

### Google Auth — فاز ۵

در `README.md:298` به‌عنوان فاز ۵ لیست شده؛ در دکمه‌ی Login اشاره شده (`README.md:103`). هنوز پیاده‌سازی نشده.

## Mapping to `Architecture/TODO.md`

`Architecture/TODO.md` با `[+]` (انجام‌شده) و `[ ]` (باقی‌مانده) نشانه‌گذاری شده. خلاصه‌ی وضعیت فعلی:

| آیتم | وضعیت فعلی | نقطه‌ی شروع |
|---|---|---|
| قفل برای ساخت آیتم مشترک (پروفایل/لیمیت) `[+]` | انجام‌شده | — |
| email required but mobile `[+]` | انجام‌شده | `AccountBusiness.SetFromModel` |
| handle null-value در front برای plan-info | باقی‌مانده | Frontend plan pages |
| logo/link در ایمیل‌ها | باقی‌مانده | `EmailService` قالب‌ها |
| محدودیت ارسال ایمیل | باقی‌مانده | `AccountBusiness.DelayBetweenWarnings` (نقش throttle) |
| غیرفعال‌سازی انتخاب زمان برای غیرمجازها | باقی‌مانده | Frontend renewal + `AllowMonthly` |
| تفکیک روزانه + زیپ لاگ | باقی‌مانده | Serilog config (`appsettings.json`) |
| ساده‌سازی کوئری‌های repo | باقی‌مانده | `Infrastructure/Repository/*` |
| human test (cloudflare) | باقی‌مانده | Frontend Login/Register (`README.md:101`) |
| امکان پرداخت | انجام‌شده | `BillingApplication` |
| حذف target از ورودی متدهای application | باقی‌مانده | `Application/*` |
| warning radius-inactivated در dashboard | باقی‌مانده | Frontend dashboard |
| اعتبارسنجی فیلد در فرانت هنگام ورود | باقی‌مانده | Frontend validators |
| تغییر سرور بر اساس نوع VPN توسط مشتری | باقی‌مانده | `docs/06` + Frontend |
| تمدید با «اولین اتصال فعال شود» | باقی‌مانده | `SyncUserAndActive` در Mikrotik |
| **`SetOVpnCertificate`** | باقی‌مانده | `MikrotikDirectService.cs:165` |
| مدیریت گواهی‌های قدیمی میکروتیک | باقی‌مانده | `MikrotikDirectService` |
| خروجی List به‌جای Enumerable در tik4net | باقی‌مانده | `MikrotikRadius/ApiWrapper` |
| **پنل مدیریت سرورها** | باقی‌مانده | `Admin` project |
| راه‌اندازی واتساپ | باقی‌مانده | `SocialMediaService` + `#if SOCIAL` |
| انتخاب نوع اطلاع‌رسانی (ایمیل/واتساپ) | باقی‌مانده | `AccountMonitoringService` |

## Phase mapping (فازها)

از `README.md:278-299`:

| Phase | وضعیت | یادداشت |
|---|---|---|
| 1. Web Site Core | جزئی | هسته‌ی وب OK؛ Unit Test هست؛ Cloudflare Human Test و FAQ ناقص. |
| 2. MySql Performance | باز | بهینه‌سازی MySQL radius. |
| 3. Fake Website | باز | نماد/ساماندهی/درگاه جعلی. |
| 4. Send WhatsApp Notif | باز | gated پشت `#if SOCIAL`. |
| 5. Google Auth | باز | در دکمه‌ی Login اشاره شده. |
| 6. Other features | باز | — |

## Security TODOs (یادآوری امنیتی)

- **کلید JWT و connection stringها** در `Backend/Portal/appsettings.json` کامیت شده‌اند (`Issuer:Code`، `LocalDatabaseOptions:ConnectionString`، `RadiusServiceOptions`، `RadDapperOptions`). باید به secret manager / env منتقل شوند. در مستندات فقط با نام کلید ارجاع داده می‌شوند.
- **ادمین پیش‌فرض seed** در `Database/LocalDatabase/Account.sql:24` با یوزرنیم `admin` و هش ثابت — در تولید باید عوض/حذف شود.
- **اعتبارسنجی توکن کال‌بک پرداخت**: در `BillingApplication.PaymentCallback` علامت `// TODO: check token` وجود دارد (`BillingApplication.cs:108`) — امضای کال‌بک درگاه هنوز اعتبارسنجی نمی‌شود.
- **تزریق دستور در SSH**: با regex `^[ \-\.\w\d]+$` مهار می‌شود (`docs/07`)؛ اگر مسیر جدیدی اضافه می‌کنید همین قاعده را حفظ کنید.

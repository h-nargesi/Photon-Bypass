# docs/12-current-state.md — Implementation state & starting points

> **English summary**: The customer-facing flow is functional: auth/JWT, account CRUD, renewal + card-to-card receipt billing (P2), dual-Radius + x-ui sync, the 60-min Quartz job, email notifications, the Telegram social channel (one-time-code chat binding + runtime channel selection — docs/21), dynamic Roslyn pricing, and the role-based management panel on the single Portal host (users/servers/wallet admin — docs/20). Incomplete: `SetOVpnCertificate` (throws `NotImplementedException`, deferred to the OpenVPN phase) and WhatsApp/Google-Auth (WhatsApp now needs only an adapter on the channel-neutral interface). For each gap there is a "where to start" pointer.

## Implemented / active (پیاده‌سازی‌شده)

- **Auth & JWT**: صدور/اعتبارسنجی توکن، `AccessService` چنداکانتی، فراموشی/بازنشانی پسورد (`docs/08`).
- **Account CRUD**: get-user, full-info, edit-user, change-pass, history.
- **Renewal + Billing (P2)**: برآورد قیمت، تمدید با تراکنش، صدور فاکتور/شارژ (`pay`/`get-invoice`)، ثبت رسید کارت‌به‌کارت و تسویه کیف پول — درگاه و `payment-callback` در بازطراحی P2 حذف شدند (`docs/04`, `docs/08`, `docs/15`).
- **Dual-Radius sync**: dispatch با keyed DI روی RadiusDesk و UserManager؛ `RunJob` موازی (`docs/06`).
- **`JobInterval`** (Quartz، ۶۰ دقیقه): `UpdateTrafficData`, `NotifSendServices`, `DeactivateInvalidRadiusUsers`, `InactiveAbandonedUsers`, `CheckUserServerBalance`.
- **Email**: فراموشی پسورد، گواهی، هشدار پایان (`EmailService`/`EmailHandler`).
- **کانال تلگرام (docs/21)**: آداپتور واقعی `ISocialMediaService` با انتخاب runtime کانال (`SocialMediaOptions:Channel`)، اتصال chat با کد یک‌بارمصرف و دیپ‌لینک (`TelegramLink` + poller)، کد بازیابی/هشدارها به chat متصل (تلگرام‌اول، fallback ایمیل)، endpointهای پروفایل `telegram-link` و کارت اتصال فرانت. پیش‌فرض `Channel=None` (no-op) — فعال‌سازی استقرار: docs/19 §۳-الف.
- **`PriceCalculator`** پویا (Roslyn) با refresh خودکار روی `PriceRepository.OnSave` (`docs/05`).
- **Frontend**: صفحات اصلی Home/Login/Register/Forgot/Reset/Dashboard/History/Edit/Change-pass/Renewal/Payment + صفحات مدیریتی نقش‌دار `/users` `/servers` `/wallet` (docs/20 فاز ۶).
- **پنل مدیریت نقش‌دار (docs/20)**: `AccountRole`/`AccountStatus` + policyهای Manager/Admin، مدیریت کاربران، CRUD سرورها، تراکنش شفاف/تاریخچه کیف پول ادمین، غیرفعال‌سازی دووضعیتی — همه روی هاست واحد Portal.
- **MSSQL schema + `TotalPlanState` view** (`docs/09`).

## Stub / incomplete (نیمه‌کاره)

### `Backend/Admin` — پنل مدیریت: اجرا شد روی Portal (هاست جدا کنار گذاشته شد)

- **حل شد (docs/20، فازهای ۱–۶ + غیرفعال‌سازی دووضعیتی ۲۰۲۶-۱۰-۱۹)**: پنل مدیریت به‌جای هاست جدا، روی همان Portal با مدل نقش‌ها (`AccountRole` + `UserTypes.AllowSelfService` + `AccountStatus`) اجرا شد — policyهای `Manager`/`Admin`، مدیریت کاربران (`/api/user/*`)، CRUD سرورها (`/api/server/*`)، تراکنش شفاف/تاریخچه کیف پول (`/api/wallet/*`) و فرانت نقش‌دار (صفحات `/users`، `/servers`، `/wallet` با گاردهای manager/admin/selfService).
- تصمیم D7 (docs/20): **یک هاست واحد (Portal) + یک اپ Angular**؛ پروژه‌ی `Backend/Admin` عمداً خالی ماند (`Program.cs` + `AddAdminServices` خالی) و قرار نیست پر شود.
- توجه: `Backend/Administration/` یک پوشه‌ی یتیم با کنترلر خالی است و **در سولوشن نیست** — آن را با `Admin` اشتباه نگیرید.

### کانال اجتماعی — تلگرام پیاده شد؛ واتساپ = آداپتور آینده

- **تلگرام کامل شد (۲۰۲۶-۱۰-۲۱، docs/21)**: `Backend/OutSource/TelegramMediaService.cs` + `TelegramUpdatePoller`/`TelegramLinkHandler` + کلیدهای DI در `OutSource/ServiceFactory.cs`. استب `SocialMediaService` حذف و با `NoopSocialMediaService` (کانال None) جایگزین شد؛ گیت‌های `#if SOCIAL` از رفته‌اند.
- **واتساپ باقی است ولی دیگر compile-gate ندارد**: کلید `SocialChannel.WhatsApp` رزرو شده؛ `ForgetPassword` مسیر موبایل را با پیام «موبایل هنوز پشتیبانی نشده است» رد می‌کند تا آداپتور برسد.
- **نقطه‌ی شروع واتساپ**: `Backend/OutSource/ServiceFactory.cs:55` — آداپتور جدید (الگوی `TelegramMediaService`، گیرنده از `AccountEntity.MobileNumber` بدون فلوی اتصال) + یک خط `AddLazyKeyedTransient`؛ هیچ تغییری در Application لازم نیست (امضاهای کانال‌خنثی).

### `SetOVpnCertificate` — `NotImplementedException`

`Backend/MikrotikRadius/Application/MikrotikDirectService.cs:165` فعلاً فقط اعتبارسنجی یوزرنیم را می‌کند و سپس `throw new NotImplementedException()`. این متد برای پخش گواهی روی NASهای ثانویه استفاده می‌شود (`AccountRadiusSyncService.GetOVpnCertificate` آن را صدا می‌زند).

**تصمیم (۲۰۲۶-۱۰-۱۸)**: پیاده‌سازی تا فاز پشتیبانی OpenVPN به تعویق افتاد. تا آن زمان کل مسیر گواهی پشت گیت T3 (`SupportsOvpnCert` = بیت `ServerFeature.Ovpn` + Mikrotik) بسته است — تا وقتی هیچ سروری آن بیت را نداشته باشد، این متد فراخوانی نمی‌شود (ممیزی: `Database/Ops/audit-server-ovpn-feature.sql`). هشدار: فعال‌سازی اشتباهی بیت روی realm چند-NAS → `NotImplementedException` (خطای ۵۰۰ خام). این متد فقط برای realmهای ۲+ NAS میکروتیک لازم است؛ realm تک-NAS بدون آن کار می‌کند.

**نقطه‌ی شروع**: همان فایل + `MikrotikDirectService.GetOVpnCertificate` (که گواهی را می‌سازد) به‌عنوان الگو برای SSH/`ProcessService`. همراه با `Architecture/TODO.md:64` (throttle گواهی) و `:61` (مدیریت گواهی‌های قدیمی) در همان فاز انجام شود.

### Google Auth — فاز ۵

در `README.md:298` به‌عنوان فاز ۵ لیست شده؛ در دکمه‌ی Login اشاره شده (`README.md:103`). هنوز پیاده‌سازی نشده.

## Mapping to `Architecture/TODO.md`

`Architecture/TODO.md` با `[+]` (انجام‌شده) و `[ ]` (باقی‌مانده) نشانه‌گذاری شده. خلاصه‌ی وضعیت فعلی:

| آیتم | وضعیت فعلی | نقطه‌ی شروع |
|---|---|---|
| قفل برای ساخت آیتم مشترک (پروفایل/لیمیت) `[+]` | انجام‌شده | — |
| email required but mobile `[+]` | انجام‌شده | `AccountBusiness.SetFromModel` |
| handle null-value در front برای plan-info | باقی‌مانده | Frontend plan pages |
| logo/link در ایمیل‌ها | انجام‌شده | برندینگ CID + `{title}`/`{site_url}`/`{guide_url}`/`{ovpn_installer_url}` در هر ۵ جفت قالب (`EmailService.ApplyBranding` + `EmailHandler.Build`) |
| محدودیت ارسال ایمیل | انجام‌شده | `EmailSendLimiter` — سقف لغزان یک‌ساعته‌ی سراسری (`EmailOptions:MaxSendsPerHour`، پیش‌فرض ۵۰۰، صفر = نامحدود)؛ `DelayBetweenWarnings` همچنان throttle هر-اکانت است |
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
| **پنل مدیریت سرورها** | انجام‌شده | پنل نقش‌دار روی Portal (docs/20 فازهای ۱–۶) |
| راه‌اندازی واتساپ | باقی‌مانده (آداپتور روی کانال خنثی) | `OutSource/ServiceFactory.cs` + docs/21 |
| انتخاب نوع اطلاع‌رسانی (ایمیل/تلگرام) | انجام‌شده | dispatch runtime در `ForgetPasswordByUsername` + `NotifSendServices` (docs/21 D4–D6) |

## Phase mapping (فازها)

از `README.md:278-299`:

| Phase | وضعیت | یادداشت |
|---|---|---|
| 1. Web Site Core | جزئی | هسته‌ی وب OK؛ Unit Test هست؛ Cloudflare Human Test و FAQ ناقص. |
| 2. MySql Performance | باز | بهینه‌سازی MySQL radius. |
| 3. Fake Website | باز | نماد/ساماندهی/درگاه جعلی. |
| 4. Send WhatsApp Notif | تلگرام انجام شد؛ واتساپ باز | کانال اجتماعی با تلگرام فعال شد (docs/21)؛ واتساپ = آداپتور روی کانال خنثی. |
| 5. Google Auth | باز | در دکمه‌ی Login اشاره شده. |
| 6. Other features | باز | — |

## Security TODOs (یادآوری امنیتی)

> فهرست کامل‌تر و اولویت‌بندی‌شده‌ی نقاط ضعف امنیتی/پرفورمنسی که در TODO نیستند: [docs/14-defects-audit.md](14-defects-audit.md).

- **کلید JWT و connection stringها** در `Backend/Portal/appsettings.json` کامیت شده‌اند (`Issuer:Code`، `LocalDatabaseOptions:ConnectionString`، `RadiusServiceOptions`، `RadDapperOptions`) — مقادیر تستی‌اند و با تأیید مالک در repo می‌مانند. مکانیزم پروداکشن: فایل `private/secrets.json` در vhost پلسک که با `AddPleskPrivateSecrets` per-key روی appsettings اولویت می‌گیرد و گارد `ProductionSecretsGuard` در environment غیر Development بالا آمدن با مقادیر تستی را متوقف می‌کند (`Portal/Basical/PleskSecrets.cs` و `Portal/Basical/ProductionSecretsGuard.cs`). ران‌بوک انتشار: [docs/19-deploy-runbook.md](19-deploy-runbook.md). در مستندات فقط با نام کلید ارجاع داده می‌شوند.
- ~~**ادمین پیش‌فرض seed** در `Database/LocalDatabase/Account.sql` با یوزرنیم `admin` و هش ثابت~~ **حل شد**: seed حذف شد؛ ادمین اولین انتشار در زمان دیپلوی با پسورد واقعی اپراتور توسط `DeployCli` ساخته می‌شود (`scripts/deploy.sh` — docs/19).
- ~~**اعتبارسنجی توکن کال‌بک پرداخت**~~ **حل شد**: درگاه پرداخت و `payment-callback` در بازطراحی P2 کامل حذف شدند (پرداخت = کارت‌به‌کارت با ثبت رسید + تسویه، docs/15)؛ دیگر کال‌بکی برای اعتبارسنجی وجود ندارد و علامت `// TODO: check token` از کد رفته است.
- **تزریق دستور در SSH**: با regex `^[ \-\.\w\d]+$` مهار می‌شود (`docs/07`)؛ اگر مسیر جدیدی اضافه می‌کنید همین قاعده را حفظ کنید.

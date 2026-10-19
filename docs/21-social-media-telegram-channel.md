# پیاده‌سازی کانال تلگرام برای SocialMedia با معماری قابل‌تعویض (واتساپ بعداً ارزان)

## هدف

پیاده‌سازی واقعی کانال تلگرام برای هر ۵ کارایی `ISocialMediaService`، به‌گونه‌ای که تعویض/افزودن واتساپ در آینده فقط «یک آداپتور جدید + یک خط ثبت» باشد. گیت‌های کامپایل‌تایم `#if SOCIAL` حذف و با انتخاب runtime جایگزین می‌شوند.

## تصمیم‌های تأییدشده با کاربر

| # | تصمیم |
|---|---|
| D1 | این فاز: تلگرام واقعی + معماری کانال‌خنثی؛ واتساپ بعداً = آداپتور ساده روی `Account.Mobile` (بدون فلوی اتصال) |
| D2 | اتصال chat_id: کد یک‌بارمصرف + دیپ‌لینک `t.me/{bot}?start={CODE}` |
| D3 | ترنسپورت: long-polling در `BackgroundService` روی named HttpClient پروکسی‌دار (سازگار با IIS/Plesk تک‌نمونه‌ای) |
| D4 | `ForgetPassword` ورودی username را هم بپذیرد → کد بازیابی به chat متصل |
| D5 | اولویت ارسال: تلگرام‌اول، وگرنه ایمیل (runtime، جایگزین منطق «واتساپ‌اول» فعلی) |

## اصل طراحی: حل گیرنده داخل آداپتور

اینترفیس فعلی واتساپ‌محور است (`phone`، `email` که موبایل است). تغییر کلیدی: **امضاهای متدها به `AccountEntity`-محور تغییر می‌کنند** و هر آداپتور خودش فیلد گیرنده را انتخاب می‌کند (واتساپ→`MobileNumber`، تلگرام→`TelegramChatId`). سرویس‌های Application هرگز نوع نشانی را نمی‌دانند → هزینه‌ی تعویض کانال حداقلی می‌شود.

---

## مراحل اجرا

### ۱. Domain — اینترفیس خنثی + موجودیت‌ها

`Backend/Domain/OutSource/ISocialMediaService.cs` — بازآرایی امضاها (نام اینترفیس حفظ می‌شود):

```csharp
Task NewUserRegistrationAlert(AccountEntity account);                    // بدون تغییر
Task SendResetPasswordLink(AccountEntity account, string hashCode);      // قبلاً (email, code)
Task InvalidPasswordAlert(AccountEntity account);                        // قبلاً (username) — caller حساب را دارد
Task FinishServiceAlert(AccountEntity account, string planTitle, string remainsTitle); // قبلاً (username, phone, ...)
Task AlarmServerCapacity(IReadOnlyList<string> alarms);                  // بدون تغییر
bool Supports(AccountEntity account);                                    // جدید: WhatsApp→MobileNumber!=null، Telegram→TelegramChatId!=null
```

- `AccountEntity` (`Domain/Account/Entity/AccountEntity.cs`): افزودن `public long? TelegramChatId { get; set; }`
- موجودیت جدید `TelegramLinkEntity` (الگوی `ResetPassEntity`): `[Table("TelegramLink")]`، `Id`، `AccountId`، `Code VARCHAR(64)`، `ExpireDate`، `Created` — مصرف کد = DELETE ردیف (تک‌مصرفی)
- اینترفیس ریپوی جدید `ITelegramLinkRepository` در Domain/Account (الگوی `IResetPassRepository`)

### ۲. دیتابیس (مجموعه‌ی fresh-install — بدون migration طبق AGENTS)

- `Database/LocalDatabase/Account.sql`: ستون `TelegramChatId BIGINT NULL` + ایندکس یکتای فیلترشده `UK_Account_TelegramChatId WHERE TelegramChatId IS NOT NULL` (مشابه `UK_Account_Email`)
- فایل جدید `Database/LocalDatabase/TelegramLink.sql`: کپی الگوی `ResetPassword.sql` (FK با `ON DELETE CASCADE`). مرتب‌سازی توپولوژیک initializer تست خودش ترتیب را حل می‌کند.

### ۳. Infrastructure

- پیاده‌سازی `TelegramLinkRepository : IEditableRepository<TelegramLinkEntity>` روی `DapperRepository<T>` + ثبت `AddLazyTransient` در `Infrastructure/ServiceFactory.cs`
- `IAccountRepository`: متد `SetTelegramChat(int accountId, long? chatId)` (UPDATE ساده)

### ۴. OutSource — آداپتور تلگرام + انتخاب کانال + poller

`Backend/OutSource/ServiceFactory.cs`:

- `TelegramOptions` (با `BindValidateReturn`): `BotToken` [required]، `BotUsername` [required — برای دیپ‌لینک]، `ApiBase` = `https://api.telegram.org`، `Proxy:Address` [required]، `Proxy:Username?`، `Proxy:Password?`، `OperationsChatId (long)` [required — گیرنده‌ی ۲ هشدار اپراتور]
- `SocialMediaOptions`: `Channel` = `None | Telegram | WhatsApp` (پیش‌فرض None)
- named HttpClient `"telegram"` (الگوی `XuiPanel/ServiceFactory.cs:17`): `HttpClientHandler { Proxy = new WebProxy(...) }` — هم ارسال هم poll از آن
- ثبت keyed با الگوی Radius: `AddLazyKeyedTransient<ISocialMediaService, TelegramMediaService>(SocialChannel.Telegram)` + `NoopSocialMediaService` برای `None` (جایگزین استب فعلی، همان رفتار) + دیسپچر انتخاب بر اساس options
- `AddHostedService<TelegramUpdatePoller>()` — اگر Channel != Telegram فقط no-op

کلاس‌های جدید در `Backend/OutSource/`:

- **`TelegramMediaService : ISocialMediaService`** — ارسال با `sendMessage` (متن ساده فارسی، هم‌محتوا با قالب‌های ایمیل). هشدارهای کاربر → `account.TelegramChatId` (null → no-op)؛ `NewUserRegistrationAlert` و `AlarmServerCapacity` → `OperationsChatId`
- **`TelegramUpdatePoller : BackgroundService`** — حلقه‌ی `getUpdates?offset=&timeout=25` با backoff نمایی خطا و لاگ Serilog؛ offset در حافظه (redelivery بعد از recycle بی‌ضرر است چون کدها تک‌مصرف‌اند). هندل `/start CODE`:
  - کد معتبر و منقضی‌نشده → DELETE کد + `SetTelegramChat` (تداخل با ایندکس یکتا → پیام «این چت قبلاً متصل است»)
  - نادرست/منقضی → پاسخ مؤدبانه؛ `/start` خالی → متن راهنما با لینک پورتال

### ۵. Application — حذف `#if SOCIAL` و دیسپچ runtime

- `AuthApplication.CheckUserPassword` (خط ۶۴): `InvalidPasswordAlert(account)` + try/catch بهترین‌تلاش (شکست تلگرام نباید login را ۵۰۰ کند — الگوی کامنت «registration must never fail because of the email»)
- `AuthApplication.ForgetPassword`:
  - حذف کامل بلوک `#if !SOCIAL` / `#else` (خطوط ۱۳۲-۱۵۰). شاخه‌ی موبایل: پیام «موبایل هنوز پشتیبانی نشده است» فقط وقتی Channel != WhatsApp (چک runtime با `IOptions<SocialMediaOptions>`)
  - شاخه‌ی جدید username (بعد از چک email/mobile pattern): `TryConsumeResetRequest` همان limiter → `GetAccount(username)` → اگر `TelegramChatId != null`: INSERT کد بازیابی + `SendResetPasswordLink(account, code)` با try/catch و `UserException` دوستانه (الگوی شاخه‌ی ایمیل خط ۲۰۵)؛ وگرنه پاسخ خنثی
- `AccountMonitoringService.NotifSendServices` (خطوط ۱۶۰-۱۸۸): جایگزینی کل بلوک `#if SOCIAL` با: `if (SocialSrv.Supports(account)) → FinishServiceAlert(account, plan.GetPlanTitle(), remains)` else-if `EmailAddress != null` → ایمیل. تاریخچه/`UpdateWarningTimes` در هر دو شاخه یکسان می‌ماند
- `ServerManagementService.CheckUserServerBalance` و `Register`: فقط تغییر امضا (عبور account)؛ بدون تغییر منطق

### ۶. Portal — endpointهای پروفایل

`Backend/Portal/Controllers/AccountController.cs` (الگوی `ResultHandlerController` + `LoadJobContext`):

- `POST /api/account/telegram-link` → ساخت کد (انقضا ۱۵ دقیقه، باطل‌کردن کدهای pending قبلی همان حساب) → `{ code, url: "https://t.me/{BotUsername}?start={code}" }`
- `GET /api/account/telegram-link` → وضعیت متصل/غیرمتصل
- `DELETE /api/account/telegram-link` → قطع اتصال (null کردن chat)

### ۷. Frontend

- صفحه‌ی `edit-user-info` (route موجود از dashboard خط ۱۲۱): کارت «اتصال تلگرام» — دکمه‌ی ساخت کد → نمایش دکمه‌ی دیپ‌لینک + کد؛ وضعیت در load؛ دکمه‌ی قطع اتصال
- `forgot-password.component.ts`: پذیرش username در ورودی + به‌روزرسانی hint/i18n
- کلیدهای ترجمه‌ی جدید در فایل‌های i18n موجود (جایی که `dashboard.account.*` تعریف شده)

### ۸. امنیت و استقرار

- مقادیر تستِ `TelegramOptions` در `appsettings.json` (عرف ریپو)؛ پروداکشن از `private/secrets.json` (لودر `AddPleskPrivateSecrets` per-key)
- **گسترش `ProductionSecretsGuard.Rules`** (`Portal/Basical/ProductionSecretsGuard.cs:26`): اگر `SocialMediaOptions:Channel == Telegram` آنگاه `TelegramOptions:BotToken` و `Proxy:Address` نباید خالی/تست باشند (الگوی قواعد EmailOptions خط ۳۵)
- IIS: poller همان پیش‌نیاز چرخه‌حیات Quartz `JobInterval` را دارد؛ حلقه‌ی ~۲۵ثانیه‌ای خودش keep-alive است — نکته در `docs/19` تکمیل شود
- Enumeration: پاسخ ForgetPassword خنثی می‌ماند + limiter موجود (۳/ساعت) روی username هم اعمال می‌شود

### ۹. تست‌ها + حکمرانی (در همان PR)

- به‌روزرسانی `SocialMediaServiceMoq` برای امضاهای جدید (۴ مصرف‌کننده در `ServerManagementServiceTest` باید سبز بمانند)
- تست‌های جدید: شاخه‌ی username در ForgetPassword (متصل/غیرمتصل/limiter)؛ تقدم تلگرام‌اول در `NotifSendServices` (+dفایع bookkeeping)؛ poller (کد معتبر متصل می‌کند، منقضی/مصرف‌شده رد، replay بی‌اثر)؛ `TelegramMediaService` (chat مقصد درست کاربر/اپراتور)؛ round-trip اتصال روی DB سناریو (الگوی `RealmCapacityOverrideTest`)
- فرانت: کارت اتصال + ورودی username فراموشی رمز (Karma)
- مستندات: ردیف‌های `docs/18-test-coverage.md` §4؛ `docs/12-current-state.md`؛ `docs/01-overview.md` (منبع خارجی تلگرام+پروکسی)؛ `docs/19`؛ خطوط AGENTS «Known gaps»؛ `Architecture/TODO.md` خط ۶۹ `[x]` و خط ۷۰ `[x]` (با تقدم runtime حل شد)

---

## اعتبارسنجی نهایی

1. `dotnet build Backend/Photon-Bypass.sln` — بدون خطا/هشدار جدید
2. `dotnet test Backend/Photon-Bypass.sln` — سبز، بدون تست skip شده
3. `cd Frontend && yarn test --watch=false --browsers=FirefoxHeadless`
4. دستی با bot token + proxy تستی: `/start` با کد → متصل؛ ForgetPassword با username → کد در چت؛ هشدار ظرفیت → chat اپراتور

## ریسک‌ها / نکته‌های باز

- در دسترس بودن پروکسی = در دسترس بودن تلگرام؛ شکست ارسال هشدار پایان سرویس → fallback ایمیل موجود؛ شکست ارسال کد بازیابی → `UserException` دوستانه (کاربر دوباره تلاش می‌کند)
- سقف ۶۴ کاراکتری start-param تلگرام: کد ~۳۲ کاراکتری Base64URL مشکلی ندارد
- `BotUsername` در options لازم است (ساخت دیپ‌لینک در سمت سرور)
- webhook بعداً اگر لازم شد: تغییر داخلی همان آداپتور، بدون دست‌زدن به اینترفیس

# Docs/19-deploy-runbook.md — ران‌بوک اولین انتشار روی هاست Plesk (FTP دستی + secrets.json)

> **English summary**: First-release deployment runbook. This panel has **never been deployed** — no production tokens exist to revoke, no live data needs migration, and every production secret is generated fresh at this release. Flow: (1) generate all production secrets from scratch; (2) create `private/secrets.json` in the Plesk vhost (outside the web root; loaded per-key over `appsettings.json` by `AddPleskPrivateSecrets`, required-key presence enforced in every environment by `RequiredSettingsGuard`); (3) install the MSSQL database from scratch and create the admin account with the operator's real password via `Scripts/deploy.sh db` (DeployCli — no seeded admin exists); (4) stop the site from the Plesk panel, publish to `httpdocs/` via manual FTP — the `private/` folder is never overwritten — then restart it from the panel; (5) verify: site boots = guard green; a guard message = a required key is missing. Scope: manual FTP + manual Plesk stop/start only (no CI/CD, no Git deploy, no vault, no scripted app pool control).

> **فارسی**: ران‌بوک انتشار اول. این پنل هنوز منتشر نشده است؛ بنابراین هیچ چرخش/ابطال توکن و هیچ migration داده‌ای در کار نیست و همه secretهای پروداکشن در همین انتشار از صفر تولید می‌شوند.

## ۱. قاب‌بندی (تصمیم‌های تأییدشده)

- فایل پایه‌ی `appsettings.json` دیگر هیچ secretی ندارد؛ فیکسچرهای تستی فقط در `appsettings.Development.json` (و `Backend/Tests/appsettings.json` برای هاست تست) می‌مانند. گارد `RequiredSettingsGuard` حضور کلیدهای الزامی را در **همه‌ی environmentها** چک می‌کند (matching مقدار فیکسچر حذف شد — fixture کهنه باعث مرگ بی‌صدا‌ی rule می‌شد).
- مکانیزم ذخیره‌ی secret: فایل `secrets.json` در پوشه‌ی استاندارد `private` کنار `httpdocs` در vhost پلسک — خارج از web root، غیرقابل دانلود با URL، دست‌نخورده توسط deploy.
- روش deploy: آپلود دستی FTP + نصب دیتابیس و ساخت ادمین با `Scripts/deploy.sh` (فریم‌ورک migration وجود ندارد). توقف/راه‌اندازی سایت دستی از پنل Plesk.
- **اولین انتشار است**: دیتابیس از ابتدا با اجرای کل اسکریپت‌ها ساخته می‌شود؛ همه credentialها (کلید JWT، DB/ایمیل) تازه تولید می‌شوند.

## ۲. مکانیزم کانفیگ (چطور کار می‌کند)

| جزء | مکان | رفتار |
|---|---|---|
| لودر `AddPleskPrivateSecrets` | `Backend/Portal/Basical/PleskSecrets.cs`، فراخوان در `Program.cs` هر دو host (Portal/Admin) اولِ chain | اولین فایل موجود از `../private/secrets.json` یا `../../private/secrets.json` (نسبت به ContentRootPath) را با `AddJsonFile` اضافه می‌کند؛ **per-key روی appsettings.json اولویت می‌گیرد** و hierarchy جزئی کافی است (فایل فقط کلیدهای secret را داشته باشد). نبود هر دو = no-op (Development به appsettings برمی‌گردد). |
| گارد `RequiredSettingsGuard` | `Backend/Portal/Basical/RequiredSettingsGuard.cs`، فراخوان در `AddPortalServices` (`Backend/Portal/ServiceFactory.cs`) | presence-based، بدون تطبیق مقدار: در **همه‌ی environmentها** حضور `Issuer:Code` و `LocalDatabaseOptions:ConnectionString` الزامی است؛ بیرون Development سه rule ایمیل هم فعال می‌شود. startup با یک `InvalidOperationException` تجمعی متوقف می‌شود؛ پیام، کلید(های) مفقود + راه‌حل متناسب با environment (Development → `appsettings.Development.json`؛ غیر از آن → مسیرهای مورد انتظار `secrets.json`) را می‌دهد. |
| بایند دیتابیس | `BindValidateReturn<LocalDapperOptions>("LocalDatabaseOptions")` در `Backend/Infrastructure/ServiceFactory.cs` | کلاس `LocalDapperOptions` از سکشن یکپارچه‌ی `LocalDatabaseOptions` بایند می‌شود (قبلاً از نام کلاس می‌خواند و کلید `secrets.json` هرگز به options نمی‌رسید). `DeployCli`/`deploy.sh` همان نام کلید را می‌خوانند — بدون تغییر. |
| تلگرام شرطی | `Backend/OutSource/ServiceFactory.cs` | اعتبارسنجی سخت‌گیرانه‌ی `TelegramOptions` (DataAnnotations + `Proxy:Address`) فقط وقتی `SocialMediaOptions:Channel == Telegram` با `ValidateOnStart` اجرا می‌شود؛ کانال خاموش ⇒ بایند ساده بدون validation — بلوک غایب بوت را نمی‌شکند. |
| تست | `Backend/Tests/Facts/Portal/RequiredSettingsGuardTest.cs` و `Backend/Tests/Facts/Portal/PleskSecretsTest.cs` | گارد: کلیدهای الزامی غایب → throw با نام‌بردن همه + راه‌حل؛ همه حاضر → عبور؛ ایمیل غایب در Development → عبور؛ ایمیل غایب/غیرفعال/خالی در Production → نام‌بردن `EmailOptions:*` + wiring: `AddPortalServices` در Production با کانفیگ خالی throw می‌کند و در Development ثبت را کامل می‌کند. لودر: override با per-key و hierarchy جزئی؛ کاندید دوم برای app تودرتو؛ تقدم کاندید اول؛ no-op وقتی هیچ فایلی نیست. |

کلیدهای گارد (کلید ← شرط):

| کلید کانفیگ | محدوده‌ی environment | شرط |
|---|---|---|
| `Issuer:Code` | همه | نبودن/خالی بودن مقدار |
| `LocalDatabaseOptions:ConnectionString` | همه | نبودن/خالی بودن مقدار |
| `EmailOptions:Enabled` | فقط غیر Development | نبودن مقدار یا `false` بودن |
| `EmailOptions:Address` | فقط غیر Development | خالی/فاصله بودن |
| `EmailOptions:Password` | فقط غیر Development | خالی/فاصله بودن |

نکته‌ی environment: گارد presence در همه‌ی environmentها فعال است؛ ruleهای ایمیل فقط بیرون Development. اگر `ASPNETCORE_ENVIRONMENT` ست نشود، پیش‌فرض **Production** است و ruleهای ایمیل هم اجرا می‌شوند. تست‌های xUnit هاست را با `ASPNETCORE_ENVIRONMENT=Development` بوت می‌کنند (`Backend/Tests/Initializer/ServiceInitializer.cs`) و `WebApplicationFactory` هم به‌طور پیش‌فرض Development است.

## ۳. گام‌های انتشار

### (الف) تولید همه‌ی secretهای پروداکشن از صفر

1. **کلید امضای JWT** (`Issuer:Code`): رشته‌ی base64 تصادفی —
   ```bash
   openssl rand -base64 64
   ```
   معادل PowerShell:
   ```powershell
   $b = [byte[]]::new(64); [Security.Cryptography.RandomNumberGenerator]::Fill($b); [Convert]::ToBase64String($b)
   ```
   خروجی را یک‌جا (بدون شکستن خط) در `Issuer:Code` بگذارید.
2. **دیتابیس محلی MSSQL** (`LocalDatabaseOptions:ConnectionString`): دیتابیس `FastBypass` را با کاربر اختصاصی و پسورد قوی بسازید (نه کاربر تستی repo).
3. **Radius**: secret سراسری لازم ندارد — مسیر RadiusDesk فعلاً dormant است (غیراجرایی؛ Docs/16). هنگام فعال‌سازی واقعی، کانفیگ به‌صورت per-server از جدول `Server` (ستون رمزنگاری‌شده‌ی `Config`) سیم‌کشی می‌شود، نه از `secrets.json`.
4. **ایمیل** (`EmailOptions`): **الزامی** — کانال پیش‌فرض بازیابی/هشدار است (تلگرام اختیاری است و جایگزین اجباری ایمیل نمی‌شود)؛ پروداکشن بدون `Enabled: true` و credential صندوق واقعی بوت نمی‌شود (گارد).
5. **SSH/ServerBridge**: در صورت وجود credential پیش‌فرض در `ServerBridgeOptions`. (credential تک‌تک سرورها در جدول `Server` ذخیره می‌شود و از پنل وارد می‌شود؛ رمزنگاری at-rest آن: Docs/14 M12.)
6. **پرداخت/OVPN** (در صورت استفاده): `ManagementOptions:PaymentCards` (شماره کارت‌های واقعی) و `ManagementOptions:DefaultPrivateKeyOVpn`.
7. **تلگرام** (در صورت فعال‌سازی کانال — Docs/21): bot token واقعی از @BotFather، `BotUsername` بدون `@`، `OperationsChatId` (chat اپراتور — با `/start` گفتگو با بات به دست می‌آید) و `Proxy:Address` (+ Username/Password اختیاری؛ Bot API از سرور بدون پروکسی خروجی در دسترس نیست). فیکسچرهای تستی فقط در `appsettings.Development.json` هستند؛ در پروداکشن با `Channel: "Telegram"` اعتبارسنجی شرطی `ValidateOnStart` کامل بودن کلیدها را الزام می‌کند (Docs/21 §فعال‌سازی)؛ کلیدها فقط با نام در مستندات می‌آیند.

### (ب) ساخت `private/secrets.json`

در vhost پلسک (کنار `httpdocs`) پوشه‌ی `private` را بسازید (در اولین deploy وجودش را تأیید کنید؛ استاندارد پلسک است ولی اگر میزبان نساخته بود، با FTP همان‌جا بسازید) و فایل را **فقط با کلیدهای حاوی credential** بنویسید.

**منبع واحد قالب**: [`private/secrets.example.json`](../private/secrets.example.json) در repo — الگوی placeholder را از همان فایل کپی و مقادیر واقعی را جای‌گزین کنید؛ مقادیر واقعی هرگز در repo/مستندات نمی‌آیند و فقط در `private/secrets.json` واقعی می‌نشینند (که با الگوی `**/private/secrets.json` در `.gitignore` بسته شده است). هر تغییر آینده‌ی قالب باید همین فایل نمونه را ویرایش کند؛ این سند دیگر کپی inline نگه نمی‌دارد تا drift تکرار نشود.

در صورت نیاز بخش‌های `ServerBridgeOptions` و `ManagementOptions` (فقط فیلدهای credential) را به همان شکل اضافه کنید. بقیه‌ی کلیدها از `appsettings.json` منتشرشده خوانده می‌شوند؛ `EmailOptions` عمداً در `appsettings.json` کامیت نمی‌شود و کامل در `secrets.json` می‌آید (`Enabled: true` + credential). کلید عملیاتی `EmailOptions:MaxSendsPerHour` (سقف لغزان یک‌ساعته‌ی سراسری ارسال — پیش‌فرض ۵۰۰، صفر/منفی = نامحدود؛ شیر ایمنی در برابر فال‌بک انبوه به ایمیل هنگام قطع کانال سوشال) secret نیست و اگر سرور SMTP شما سقف ساعتی دیگری دارد، همین‌جا override کنید. بلوک تلگرام فقط اگر کانال فعال می‌شود لازم است: با `Channel: "Telegram"` اعتبارسنجی شرطی startup کامل بودن `BotToken/BotUsername/Proxy:Address/OperationsChatId` را الزام می‌کند (Docs/21 §فعال‌سازی)؛ بدون آن کانال `None` می‌ماند (no-op) و بلوک لازم نیست.

### (ج) نصب دیتابیس از ابتدا + ساخت ادمین (پیش از بالا آوردن app)

روی MSSQL پروداکشن، `Scripts/deploy.sh` کل batch را با ترتیب وابستگی FK و fail-fast اجرا می‌کند و در اولین اجرا ساخت ادمین را هم پیش می‌برد:

```bash
Scripts/deploy.sh db --secrets /path/to/secrets.json   # کپی محلی private/secrets.json
# یا با پارامترهای صریح:
Scripts/deploy.sh db -S <mssql-host> -U <login> -P '<password>'
```

1. `Database.sql` ابتدا بدون initial-catalog اجرا می‌شود (بافت master برای `CREATE DATABASE`؛ ساخت دیتابیس `FastBypass`)؛ ۱۶ اسکریپت بعدی هرکدام با هدر `USE FastBypass` می‌آیند. ترتیب در `MANIFEST` داخل اسکریپت ثابت است و **گارد manifest** هر فایل `.sql` فهرست‌نشده در `Database/LocalDatabase/` را با نام فایل abort می‌کند.
2. مجموعه **fresh-install** و idempotent است: اسکریپت‌های migration/ALTER قدیمی در همین `CREATE TABLE` ها ادغام و حذف شده‌اند — داده‌ی legacy وجود ندارد و هر release همان batch را دوباره اجرا می‌کند (جزئیات و ترتیب کامل: `Database/LocalDatabase/README.md`). ویوی `TotalPlanState` (منبع حقیقت وضعیت پلن‌ها) جزو همان مجموعه ساخته می‌شود.
3. **ساخت ادمین** — seed ادمین وجود ندارد؛ ادمین با پسورد واقعی اپراتور در زمان درج ساخته می‌شود: بعد از batch، اگر جدول `Account` خالی بود اسکریپت username (پیش‌فرض `admin`) و پسورد مخفی می‌پرسد و `DeployCli create-admin` را صدا می‌زند (`Role=Admin` + `UserType=AllowSelfService`؛ گاردهای username تکراری و تک‌ادمینی D9 در خود CLI). سپس `verify` همان credential را smoke-check می‌کند. غیرتعاملی: `--admin-user`/`--admin-password` یا env `DEPLOY_ADMIN_USER`/`DEPLOY_ADMIN_PASSWORD`. برای اجراهای بعدی: `Scripts/deploy.sh admin`.
4. اگر هاست به کاربر اجازه‌ی `CREATE DATABASE` نمی‌دهد، دیتابیس را از پنل Plesk بسازید — `Database.sql` با گارد `IF NOT EXISTS` no-op رد می‌شود و بقیه‌ی batch عادی ادامه می‌یابد.

### (د) publish و آپلود FTP (توقف/راه‌اندازی دستی از پنل Plesk)

توقف و راه‌اندازی سایت **دستی توسط اپراتور از پنل Plesk** انجام می‌شود (تصمیم ثبت‌شده — `app_offline.htm` و Plesk API رد شدند؛ اسکریپت‌پذیر نیست).

1. اپراتور سایت را از پنل Plesk **متوقف** می‌کند.

```bash
dotnet publish Backend/Portal -c Release -o publish/Portal
```

2. خروجی را فایل‌به‌فایل روی `httpdocs/` آپلود کنید (بدون حذف remote). مسیرهای محافظت‌شده — `data-protection-keys/`، `config/`، `events-*.log` — هرگز بازنویسی نمی‌شوند (جدول کامل PRESERVE در `Database/LocalDatabase/README.md`) و پوشه‌ی `private/` اصلاً هدف deploy نیست چون بیرون از `httpdocs` است. اگر app در زیرپوشه‌ی `httpdocs` منتشر شود، کاندید دوم لودر (`../../private/secrets.json`) همان فایل را می‌گیرد. الگوهای `.gitignore` (`**/private/secrets.json` و `appsettings.Production.json`) هم ارسال تصادفی فایل به repo را می‌بندند.

3. اپراتور سایت را از پنل Plesk **راه‌اندازی** می‌کند.

### (هـ) تأیید

- سایت بالا بیاید = گارد سبز = همه‌ی کلیدهای الزامی حاضرند.
- اگر پیام `RequiredSettingsGuard` دیدید، کلیدی جا مانده — نام کلید مقصر و راه‌حل متناسب با environment در خود پیام است.
- خط‌های `host-check` در لاگ (Docs/25 §۴.۱۱): `host-check summary g1=pass g2=pass g3=observing targets=…`. IIS اپ را با اولین درخواست بالا می‌آورد، پس این خط‌ها فقط بعد از یک بار بازکردن سایت در `events-*.log` می‌نشینند. برای سرورهای تانل، در `/servers` با «تست اتصال» هر ردیف را پیش از فعال‌سازی سبز کنید.

## ۴. نکات عملیاتی اولین انتشار

- **بکاپ `data-protection-keys`**: پوشه‌ی کلیدهای Data Protection (Docs/14 M12) باید در بکاپ vhost باشد؛ گم‌شدنش = ورود دوباره‌ی کانفیگ رمزنگاری‌شده‌ی سرورها.
- **ادمین**: seed پیش‌فرض حذف شده است؛ ادمین در گام (ج) با پسورد واقعی اپراتور ساخته و verify می‌شود (`Scripts/deploy.sh db`/`admin` → `DeployCli`) — پسورد پیش‌فرضی برای تعویض پس از نصب وجود ندارد.
- **خطای گارد در لاگ‌های هاست** چیزی مخفی نمی‌کند؛ پیام فقط کلیدها و مسیرها را می‌گوید، نه مقادیر.
- **poller تلگرام با کانال فعال**: همان پیش‌نیاز چرخه‌حیات Quartz `JobInterval` را دارد (recycle مدام = offset از دست می‌رود ولی دوباره-delivery بی‌ضرر است — کدها تک‌مصرف‌اند)؛ حلقه‌ی ~۲۵ ثانیه‌ای `getUpdates` خودش درخواست‌های درحال‌انجام نگه می‌دارد و keep-alive است. خطای 409 در لاگ یعنی یک poller دوم (هاست/نسخه‌ی همزمان دیگر) دارد poll می‌کند.
- Admin host فعلاً skeleton است؛ لودر/گارد برایش وصل شده تا در آینده بدون تغییر مکانیزم آماده باشد (Docs/12).
- **بررسی‌های میزبان در اولین انتشار (G1–G3؛ Docs/25 D11 و §۴.۱۱):** همان بالا آمدن اولین سایت، `HostCapabilityChecks` سه شرط میزبان را می‌سنجد و در `events-*.log` می‌نویسد: `host-check G1 tcp target=… pass=…` برای هر مقصدِ `HostCheckTargets` (پورت SSH سرورها) و هر ردیف ذخیره‌شده‌ی سرور، `host-check G2 loopback-bind pass=…`، `host-check G3 worker-started …`، و در پایان `host-check summary g1=… g2=… g3=observing targets=…`. بعد از راه‌اندازی سایت این خط‌ها را بخوانید: G1 و G2 باید `pass` باشند پیش از آنکه ردیف سروری روی حالت تانل فعال شود؛ قرمز بودن یعنی از هاستینگ بپرسید (معنای هر حالت در Docs/25 §۴.۱۱). G3 را با یک ساعت بی‌کاری بسنجید: دو خط `heartbeat` با uptime کم یعنی recycle، و jobهای پس‌زمینه به ping خارجی نیاز دارند. اجرای دوباره بدون ری‌استارت: `POST /api/server/host-checks`. مقصدهای G1 پیش از آنکه ردیف سروری باشد از `ServerBridgeOptions:HostCheckTargets` می‌آیند (فهرست `host:port`؛ مقدار نامعتبر باعث `OptionsValidationException` و بالا نیامدن میزبان می‌شود) — آن‌ها را در همان گام تنظیم کلیدها (§۲) بنویسید. تأیید شفاهی هاستینگ (خروجی TCP از app pool به پورت SSH، اجازه‌ی bind روی loopback، idle timeout اپ‌پول) هنوز برای قرارداد لازم است؛ لاگ فقط نتیجه را نشان می‌دهد.

## ۵. خارج از دامنه

- چرخش/ابطال مقادیر تستی فعلی (تأیید شد تستی‌اند؛ سیستم هنوز منتشر نشده).
- Azure Key Vault / HashiCorp Vault، CI/CD، Plesk Git deploy — تصمیم‌های آینده.

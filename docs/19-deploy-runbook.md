# docs/19-deploy-runbook.md — ران‌بوک اولین انتشار روی هاست Plesk (FTP دستی + secrets.json)

> **English summary**: First-release deployment runbook. This panel has **never been deployed** — no production tokens exist to revoke, no live data needs migration, and every production secret is generated fresh at this release. Flow: (1) generate all production secrets from scratch; (2) create `private/secrets.json` in the Plesk vhost (outside the web root; loaded per-key over `appsettings.json` by `AddPleskPrivateSecrets`, enforced against committed test values by `ProductionSecretsGuard`); (3) install the MSSQL database from scratch and create the admin account with the operator's real password via `scripts/deploy.sh db` (DeployCli — no seeded admin exists); (4) stop the site from the Plesk panel, publish to `httpdocs/` via manual FTP — the `private/` folder is never overwritten — then restart it from the panel; (5) verify: site boots = guard green; a guard message = a secret was left behind. Scope: manual FTP + manual Plesk stop/start only (no CI/CD, no Git deploy, no vault, no scripted app pool control).

> **فارسی**: ران‌بوک انتشار اول. این پنل هنوز منتشر نشده است؛ بنابراین هیچ چرخش/ابطال توکن و هیچ migration داده‌ای در کار نیست و همه secretهای پروداکشن در همین انتشار از صفر تولید می‌شوند.

## ۱. قاب‌بندی (تصمیم‌های تأییدشده)

- مقادیر تستی فعلی در repo می‌مانند (کاربر تأیید کرد تستی‌اند)؛ گارد `ProductionSecretsGuard` مانع بالا آمدن پروداکشن با آن‌ها می‌شود.
- مکانیزم ذخیره‌ی secret: فایل `secrets.json` در پوشه‌ی استاندارد `private` کنار `httpdocs` در vhost پلسک — خارج از web root، غیرقابل دانلود با URL، دست‌نخورده توسط deploy.
- روش deploy: آپلود دستی FTP + نصب دیتابیس و ساخت ادمین با `scripts/deploy.sh` (فریم‌ورک migration وجود ندارد). توقف/راه‌اندازی سایت دستی از پنل Plesk.
- **اولین انتشار است**: دیتابیس از ابتدا با اجرای کل اسکریپت‌ها ساخته می‌شود؛ همه credentialها (کلید JWT، DB/Radius/ایمیل) تازه تولید می‌شوند.

## ۲. مکانیزم کانفیگ (چطور کار می‌کند)

| جزء | مکان | رفتار |
|---|---|---|
| لودر `AddPleskPrivateSecrets` | `Backend/Portal/Basical/PleskSecrets.cs`، فراخوان در `Program.cs` هر دو host (Portal/Admin) اولِ chain | اولین فایل موجود از `../private/secrets.json` یا `../../private/secrets.json` (نسبت به ContentRootPath) را با `AddJsonFile` اضافه می‌کند؛ **per-key روی appsettings.json اولویت می‌گیرد** و hierarchy جزئی کافی است (فایل فقط کلیدهای secret را داشته باشد). نبود هر دو = no-op (Development به appsettings برمی‌گردد). |
| گارد `ProductionSecretsGuard` | `Backend/Portal/Basical/ProductionSecretsGuard.cs`، فراخوان در `AddPortalServices` (`Backend/Portal/ServiceFactory.cs`) | در environment غیر Development، اگر هر secret همچنان مقدار تستی کامیت‌شده باشد، startup با `InvalidOperationException` متوقف می‌شود؛ پیام، کلید(های) مقصر + مسیرهای مورد انتظار `secrets.json` را می‌دهد. |
| ثابت‌های نشانگر | `CommittedTestSecretFixtures` (همان فایل گارد) | fixture تستی‌اند، نه secret واقعی؛ در مستندات فقط کلید کانفیگ ارجاع داده می‌شود، نه مقدار. |
| تست | `Backend/Tests/Facts/Portal/ProductionSecretsGuardTest.cs` و `Backend/Tests/Facts/Portal/PleskSecretsTest.cs` | گارد: هر ۴ کلید تستی → throw با نام‌بردن همه؛ Development → عبور؛ override کامل → عبور؛ فقط یک کلید → throw فقط با نام همان؛ کلید غایب → عبور (گارد مقادیر تستی را می‌گیرد نه غیبت مقدار) + wiring: `AddPortalServices` در Production با مقادیر تستی throw می‌کند و در Development ثبت را کامل می‌کند. لودر: override با per-key و hierarchy جزئی؛ کاندید دوم برای app تودرتو؛ تقدم کاندید اول؛ no-op وقتی هیچ فایلی نیست. |

نشانگرهای گارد (کلید ← شرط تطبیق):

| کلید کانفیگ | شرط |
|---|---|
| `Issuer:Code` | تساوی دقیق با مقدار تستی کامیت‌شده |
| `LocalDatabaseOptions:ConnectionString` | شامل نام دیتابیس سناریوی تستی (`FastBypass_DbScenario`) |
| `RadDapperOptions:ConnectionString` | شامل نام دیتابیس تستی Radius |
| `RadiusServiceOptions:Password` | تساوی دقیق با مقدار تستی کامیت‌شده |
| `EmailOptions:Enabled` | نبودن مقدار یا `false` بودن |
| `EmailOptions:Address` | خالی/فاصله بودن |
| `EmailOptions:Password` | خالی/فاصله بودن |

نکته‌ی environment: اگر `ASPNETCORE_ENVIRONMENT` ست نشود، پیش‌فرض **Production** است و گارد فعال می‌شود. تست‌های xUnit هاست را با `ASPNETCORE_ENVIRONMENT=Development` بوت می‌کنند (`Backend/Tests/Initializer/ServiceInitializer.cs`) و `WebApplicationFactory` هم به‌طور پیش‌فرض Development است.

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
3. **Radius**: connection string دیتابیس Radius (`RadDapperOptions:ConnectionString`) و credential وب‌سرویس RadiusDesk (`RadiusServiceOptions`) با مقادیر واقعی سرور.
4. **ایمیل** (`EmailOptions`): **الزامی** — کانال پیش‌فرض بازیابی/هشدار است (تلگرام اختیاری است و جایگزین اجباری ایمیل نمی‌شود)؛ پروداکشن بدون `Enabled: true` و credential صندوق واقعی بوت نمی‌شود (گارد).
5. **SSH/ServerBridge**: در صورت وجود credential پیش‌فرض در `ServerBridgeOptions`. (credential تک‌تک سرورها در جدول `Server` ذخیره می‌شود و از پنل وارد می‌شود؛ رمزنگاری at-rest آن: docs/14 M12.)
6. **پرداخت/OVPN** (در صورت استفاده): `ManagementOptions:PaymentCards` (شماره کارت‌های واقعی) و `ManagementOptions:DefaultPrivateKeyOVpn`.
7. **تلگرام** (در صورت فعال‌سازی کانال — docs/21): bot token واقعی از @BotFather، `BotUsername` بدون `@`، `OperationsChatId` (chat اپراتور — با `/start` گفتگو با بات به دست می‌آید) و `Proxy:Address` (+ Username/Password اختیاری؛ Bot API از سرور بدون پروکسی خروجی در دسترس نیست). توکن تستی کامیت‌شده در appsettings توسط گارد بلاک می‌شود؛ کلیدها فقط با نام در مستندات می‌آیند.

### (ب) ساخت `private/secrets.json`

در vhost پلسک (کنار `httpdocs`) پوشه‌ی `private` را بسازید (در اولین deploy وجودش را تأیید کنید؛ استاندارد پلسک است ولی اگر میزبان نساخته بود، با FTP همان‌جا بسازید) و فایل را **فقط با کلیدهای حاوی credential** بنویسید — الگوی زیر با placeholder؛ مقادیر واقعی هرگز در repo/مستندات نمی‌آیند:

```json
{
  "Issuer": {
    "Code": "<base64 from step a-1>"
  },
  "LocalDatabaseOptions": {
    "ConnectionString": "Server=<mssql-host>; Database=FastBypass; User Id=<user>; Password=<password>; Encrypt=True; TrustServerCertificate=True;"
  },
  "RadDapperOptions": {
    "ConnectionString": "Server=<radius-db-host>;Database=<radius-db>;User=<user>;Password=<password>;"
  },
  "RadiusServiceOptions": {
    "BaseUrl": "<radiusdesk-host>",
    "Username": "<user>",
    "Password": "<password>"
  },
  "EmailOptions": {
    "Enabled": true,
    "FullName": "<display name>",
    "Address": "<mailbox address>",
    "Password": "<mailbox password or app password>",
    "Host": "<smtp-host>",
    "Port": 587,
    "EnableSsl": true
  },
  "SocialMediaOptions": {
    "Channel": "Telegram"
  },
  "TelegramOptions": {
    "BotToken": "<real bot token from @BotFather>",
    "BotUsername": "<bot username without @>",
    "OperationsChatId": "<operator chat id>",
    "Proxy": {
      "Address": "<outbound proxy, e.g. http://127.0.0.1:9050>",
      "Username": "<optional>",
      "Password": "<optional>"
    }
  }
}
```

در صورت نیاز بخش‌های `ServerBridgeOptions` و `ManagementOptions` (فقط فیلدهای credential) را به همان شکل اضافه کنید. بقیه‌ی کلیدها از `appsettings.json` منتشرشده خوانده می‌شوند؛ `EmailOptions` عمداً در `appsettings.json` کامیت نمی‌شود و کامل در `secrets.json` می‌آید (`Enabled: true` + credential). کلید عملیاتی `EmailOptions:MaxSendsPerHour` (سقف لغزان یک‌ساعته‌ی سراسری ارسال — پیش‌فرض ۵۰۰، صفر/منفی = نامحدود؛ شیر ایمنی در برابر فال‌بک انبوه به ایمیل هنگام قطع کانال سوشال) secret نیست و اگر سرور SMTP شما سقف ساعتی دیگری دارد، همین‌جا override کنید. بلوک تلگرام فقط اگر کانال فعال می‌شود لازم است: با `Channel: "Telegram"` گارد پروداکشن کامل بودن `BotToken/BotUsername/Proxy:Address/OperationsChatId` را الزام می‌کند (docs/21 §فعال‌سازی)؛ بدون آن کانال `None` می‌ماند (no-op) و بلوک لازم نیست.

### (ج) نصب دیتابیس از ابتدا + ساخت ادمین (پیش از بالا آوردن app)

روی MSSQL پروداکشن، `scripts/deploy.sh` کل batch را با ترتیب وابستگی FK و fail-fast اجرا می‌کند و در اولین اجرا ساخت ادمین را هم پیش می‌برد:

```bash
scripts/deploy.sh db --secrets /path/to/secrets.json   # کپی محلی private/secrets.json
# یا با پارامترهای صریح:
scripts/deploy.sh db -S <mssql-host> -U <login> -P '<password>'
```

1. `Database.sql` ابتدا بدون initial-catalog اجرا می‌شود (بافت master برای `CREATE DATABASE`؛ ساخت دیتابیس `FastBypass`)؛ ۱۴ اسکریپت بعدی هرکدام با هدر `USE FastBypass` می‌آیند. ترتیب در `MANIFEST` داخل اسکریپت ثابت است و **گارد manifest** هر فایل `.sql` فهرست‌نشده در `Database/LocalDatabase/` را با نام فایل abort می‌کند.
2. مجموعه **fresh-install** و idempotent است: اسکریپت‌های migration/ALTER قدیمی در همین `CREATE TABLE` ها ادغام و حذف شده‌اند — داده‌ی legacy وجود ندارد و هر release همان batch را دوباره اجرا می‌کند (جزئیات و ترتیب کامل: `Database/LocalDatabase/README.md`). ویوی `TotalPlanState` (منبع حقیقت وضعیت پلن‌ها) جزو همان مجموعه ساخته می‌شود.
3. **ساخت ادمین** — seed ادمین وجود ندارد؛ ادمین با پسورد واقعی اپراتور در زمان درج ساخته می‌شود: بعد از batch، اگر جدول `Account` خالی بود اسکریپت username (پیش‌فرض `admin`) و پسورد مخفی می‌پرسد و `DeployCli create-admin` را صدا می‌زند (`Role=Admin` + `UserType=AllowSelfService`؛ گاردهای username تکراری و تک‌ادمینی D9 در خود CLI). سپس `verify` همان credential را smoke-check می‌کند. غیرتعاملی: `--admin-user`/`--admin-password` یا env `DEPLOY_ADMIN_USER`/`DEPLOY_ADMIN_PASSWORD`. برای اجراهای بعدی: `scripts/deploy.sh admin`.
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

- سایت بالا بیاید = گارد سبز = همه‌ی secretهای تستی override شده‌اند.
- اگر پیام `ProductionSecretsGuard` دیدید، secret جا مانده — نام کلید مقصر و مسیر مورد انتظار فایل در خود پیام است.

## ۴. نکات عملیاتی اولین انتشار

- **بکاپ `data-protection-keys`**: پوشه‌ی کلیدهای Data Protection (docs/14 M12) باید در بکاپ vhost باشد؛ گم‌شدنش = ورود دوباره‌ی کانفیگ رمزنگاری‌شده‌ی سرورها.
- **ادمین**: seed پیش‌فرض حذف شده است؛ ادمین در گام (ج) با پسورد واقعی اپراتور ساخته و verify می‌شود (`scripts/deploy.sh db`/`admin` → `DeployCli`) — پسورد پیش‌فرضی برای تعویض پس از نصب وجود ندارد.
- **خطای گارد در لاگ‌های هاست** چیزی مخفی نمی‌کند؛ پیام فقط کلیدها و مسیرها را می‌گوید، نه مقادیر.
- **poller تلگرام با کانال فعال**: همان پیش‌نیاز چرخه‌حیات Quartz `JobInterval` را دارد (recycle مدام = offset از دست می‌رود ولی دوباره-delivery بی‌ضرر است — کدها تک‌مصرف‌اند)؛ حلقه‌ی ~۲۵ ثانیه‌ای `getUpdates` خودش درخواست‌های درحال‌انجام نگه می‌دارد و keep-alive است. خطای 409 در لاگ یعنی یک poller دوم (هاست/نسخه‌ی همزمان دیگر) دارد poll می‌کند.
- Admin host فعلاً skeleton است؛ لودر/گارد برایش وصل شده تا در آینده بدون تغییر مکانیزم آماده باشد (docs/12).

## ۵. خارج از دامنه

- چرخش/ابطال مقادیر تستی فعلی (تأیید شد تستی‌اند؛ سیستم هنوز منتشر نشده).
- Azure Key Vault / HashiCorp Vault، CI/CD، Plesk Git deploy — تصمیم‌های آینده.

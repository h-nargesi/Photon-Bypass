# docs/19-deploy-runbook.md — ران‌بوک اولین انتشار روی هاست Plesk (FTP دستی + secrets.json)

> **English summary**: First-release deployment runbook. This panel has **never been deployed** — no production tokens exist to revoke, no live data needs migration, and every production secret is generated fresh at this release. Flow: (1) generate all production secrets from scratch; (2) create `private/secrets.json` in the Plesk vhost (outside the web root; loaded per-key over `appsettings.json` by `AddPleskPrivateSecrets`, enforced against committed test values by `ProductionSecretsGuard`); (3) install the MSSQL database from scratch by running all `Database/LocalDatabase/` scripts (`Database.sql` first); (4) publish to `httpdocs/` via manual FTP — the `private/` folder is never overwritten; (5) verify: site boots = guard green; a guard message = a secret was left behind. Scope: manual FTP only (no CI/CD, no Git deploy, no vault).

> **فارسی**: ران‌بوک انتشار اول. این پنل هنوز منتشر نشده است؛ بنابراین هیچ چرخش/ابطال توکن و هیچ migration داده‌ای در کار نیست و همه secretهای پروداکشن در همین انتشار از صفر تولید می‌شوند.

## ۱. قاب‌بندی (تصمیم‌های تأییدشده)

- مقادیر تستی فعلی در repo می‌مانند (کاربر تأیید کرد تستی‌اند)؛ گارد `ProductionSecretsGuard` مانع بالا آمدن پروداکشن با آن‌ها می‌شود.
- مکانیزم ذخیره‌ی secret: فایل `secrets.json` در پوشه‌ی استاندارد `private` کنار `httpdocs` در vhost پلسک — خارج از web root، غیرقابل دانلود با URL، دست‌نخورده توسط deploy.
- روش deploy: آپلود دستی FTP + اجرای دستی اسکریپت‌های DB (فریم‌ورک migration وجود ندارد).
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
4. **ایمیل** (`EmailOptions`): در صورت فعال بودن ارسال ایمیل، credential صندوق واقعی.
5. **SSH/ServerBridge**: در صورت وجود credential پیش‌فرض در `ServerBridgeOptions`. (credential تک‌تک سرورها در جدول `Server` ذخیره می‌شود و از پنل وارد می‌شود؛ رمزنگاری at-rest آن: docs/14 M12.)
6. **پرداخت/OVPN** (در صورت استفاده): `ManagementOptions:PaymentCards` (شماره کارت‌های واقعی) و `ManagementOptions:DefaultPrivateKeyOVpn`.

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
  }
}
```

در صورت نیاز بخش‌های `EmailOptions`، `ServerBridgeOptions` و `ManagementOptions` (فقط فیلدهای credential) را به همان شکل اضافه کنید. بقیه‌ی کلیدها از `appsettings.json` منتشرشده خوانده می‌شوند.

### (ج) نصب دیتابیس از ابتدا (پیش از بالا آوردن app)

روی MSSQL پروداکشن، اسکریپت‌های `Database/LocalDatabase/` را به ترتیب اجرا کنید:

1. ابتدا `Database.sql` (ساخت دیتابیس `FastBypass` و اشیای پایه).
2. سپس همه‌ی اسکریپت‌های باقی‌مانده‌ی همان پوشه (طبق AGENTS.md)؛ همگی idempotent نوشته شده‌اند. موارد ALTER-based ستون‌های جدید (`AccountCredentialRotation.sql`، `RenewalTrafficState.sql`، …) و ایندکس/sequenceها (`Indexes.sql`، `InvoiceSequence.sql`، `AccountUniqueIndexes.sql`، `PaymentP2.sql`) روی نصب تازه هم باید اجرا شوند؛ `TrafficDataDedup.sql` پاکسازی داده‌ی legacy است و روی دیتابیس خالی no-op.
3. ویوی `TotalPlanState.sql` (منبع حقیقت وضعیت پلن‌ها) حتماً ساخته شود — جزو همان مجموعه است.

### (د) publish و آپلود FTP

```bash
dotnet publish Backend/Portal -c Release -o publish/Portal
```

خروجی را در `httpdocs/` آپلود کنید. پوشه‌ی `private/` **هرگز** بازنویسی نمی‌شود چون هدف deploy نیست (بیرون از `httpdocs`). اگر app در زیرپوشه‌ی `httpdocs` منتشر شود، کاندید دوم لودر (`../../private/secrets.json`) همان فایل را می‌گیرد. الگوهای `.gitignore` (`**/private/secrets.json` و `appsettings.Production.json`) هم ارسال تصادفی فایل به repo را می‌بندند.

### (هـ) تأیید

- سایت بالا بیاید = گارد سبز = همه‌ی secretهای تستی override شده‌اند.
- اگر پیام `ProductionSecretsGuard` دیدید، secret جا مانده — نام کلید مقصر و مسیر مورد انتظار فایل در خود پیام است.

## ۴. نکات عملیاتی اولین انتشار

- **بکاپ `data-protection-keys`**: پوشه‌ی کلیدهای Data Protection (docs/14 M12) باید در بکاپ vhost باشد؛ گم‌شدنش = ورود دوباره‌ی کانفیگ رمزنگاری‌شده‌ی سرورها.
- **ادمین seed**: بعد از نصب، پسورد کاربر seed پیش‌فرض `Account.sql` عوض شود (یادآوری docs/12).
- **خطای گارد در لاگ‌های هاست** چیزی مخفی نمی‌کند؛ پیام فقط کلیدها و مسیرها را می‌گوید، نه مقادیر.
- Admin host فعلاً skeleton است؛ لودر/گارد برایش وصل شده تا در آینده بدون تغییر مکانیزم آماده باشد (docs/12).

## ۵. خارج از دامنه

- چرخش/ابطال مقادیر تستی فعلی (تأیید شد تستی‌اند؛ سیستم هنوز منتشر نشده).
- Azure Key Vault / HashiCorp Vault، CI/CD، Plesk Git deploy — تصمیم‌های آینده.

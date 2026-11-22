# Database/LocalDatabase — fresh-install set

> **English summary**: Fresh-install SQL set for the `FastBypass` MSSQL database. Nothing has shipped yet, so there are **no migration scripts** — every later-added column is folded into its `CREATE TABLE`. Run the batch with `Scripts/deploy.sh db` (it enforces the order below and refuses unlisted `.sql` files). All scripts are idempotent and re-run as one batch on every release. **There is no admin seed**: the first-release admin is created at deploy time by `DeployCli` (`Scripts/deploy.sh db` prompts for it / `Scripts/deploy.sh admin`). The test databases (`FastBypass_*`) are built from this same folder by `Backend/Tests/Initializer/LocalDatabaseInitializer.cs`.

> **فارسی**: مجموعه‌ی نصب اولیه‌ی دیتابیس `FastBypass`. هنوز انتشار نداشته‌ایم — اسکریپت migration وجود ندارد و هر ستون بعدی داخل `CREATE TABLE` ادغام شده است. اجرا با `Scripts/deploy.sh db`. همه‌ی اسکریپت‌ها idempotent هستند و هر release همان batch دوباره اجرا می‌شود. **seed ادمین وجود ندارد**؛ ادمین اول با DeployCli در زمان دیپلوی ساخته می‌شود.

## Run order (manifest)

ترتیب دقیق در آرایه‌ی `MANIFEST` داخل `Scripts/deploy.sh` نگهداری می‌شود:

| # | File | Creates | Notes |
|---|---|---|---|
| 1 | `Database.sql` | database `FastBypass` | بدون `USE` — در بافت master اجرا می‌شود |
| 2 | `Realm.sql` | `Realm` | |
| 3 | `Price.sql` | `Price` | کد Roslyn قیمت‌گذاری در `CalculatorCode` |
| 4 | `PanelUser.sql` | `PanelUser` | سابق `Account.sql` (Docs/22 D1)؛ بدون seed؛ `Role`/`UserType`/`TelegramChatId` ادغام‌شده |
| 5 | `Server.sql` | `Server` | `Config VARCHAR(4000)` |
| 6 | `VpnAccount.sql` | `VpnAccount` | اکانت VPN (Docs/22 D1) |
| 7 | `Wallet.sql` | `Wallet` | |
| 8 | `History.sql` | `History` | |
| 9 | `ResetPassword.sql` | `ResetPassword` | |
| 10 | `TelegramLink.sql` | `TelegramLink` | Docs/21 D2 |
| 11 | `TrafficData.sql` | `TrafficData` | دلتا-ردیف؛ UK inline روی `SessionId` |
| 12 | `Renewal.sql` | `Renewal` | ستون‌های x-ui (Docs/17 D6/D14) ادغام‌شده |
| 13 | `Invoice.sql` | `Invoice` | سابق `PaymentP2.sql` |
| 14 | `InvoiceSequence.sql` | sequence | start ثابت ۱۰۰۰۱؛ بدون وابستگی به Wallet |
| 15 | `Indexes.sql` | indexes | مسیرهای داغ (Docs/14 H20) |
| 16 | `TotalPlanState.sql` | view | منبع حقیقت وضعیت پلن‌ها |
| 17 | `PlanState.sql` | view | |

گارد manifest در `deploy.sh` هر فایل `.sql` جدید این پوشه را که در لیست نیست abort می‌کند — فایل جدید را حتماً به `MANIFEST` اضافه کنید تا از release بعدی جا نماند.

## Usage

```bash
# کل batch + ساخت ادمین در اولین اجرا (پرامپت تعاملی)
Scripts/deploy.sh db --secrets /path/to/private/secrets.json

# با پارامترهای صریح
Scripts/deploy.sh db -S <host> -U <login> -P '<password>'

# دیتابیس با نام دلخواه (توکن FastBypass در USE/CREATE بازنویسی می‌شود — همان
# جایگزینی global که هاست تست با ReplaceDatabaseName انجام می‌دهد) + رد شدن از
# Database.sql وقتی دیتابیس از قبل ساخته شده (مثلاً از پنل هاستینگ):
Scripts/deploy.sh db -S <host> -U <login> -P '<password>' --database MyPanel --no-create-db

# فقط ساخت/تأیید ادمین (اجراهای بعدی)
Scripts/deploy.sh admin --secrets /path/to/private/secrets.json
```

- `--secrets`: کپی محلی `private/secrets.json`؛ کلید `LocalDatabaseOptions:ConnectionString` خوانده می‌شود و اولویت دارد. فایل هرگز echo/لاگ نمی‌شود.
- رمزها فقط از env (`SQLCMDPASSWORD`، `DEPLOY_ADMIN_PASSWORD`) عبور می‌کنند، نه command line.
- لاگ کامل: `Scripts/logs/deploy-<timestamp>.log` (gitignore).

## Idempotent model

هر release **همان batch** را دوباره اجرا می‌کند: همه‌ی اسکریپت‌ها با `IF OBJECT_ID ... IS NULL` / `IF NOT EXISTS` گارد شده‌اند و اجرای دوباره no-op است. چون تغییر ستون‌ها باید داخل همین `CREATE TABLE` ها ادغام شود (نه اسکریپت delta)، مدل برای دیتابیس‌های از-صفر و بازنصب است؛ legacy data وجود ندارد.

## Admin account

- در این پوشه **هیچ seed ادمینی نیست** (تصمیم M29-adjacent، Docs/19): ادمین اول با پسورد واقعی اپراتور و `Role=Admin` + `AllowSelfService` در زمان درج ساخته می‌شود — `Backend/Tools/DeployCli` (`create-admin` / `verify`).
- DeployCli از درج ادمین دوم (Docs/20 D9: تک‌ادمین) و از بازنویسی username تکراری خودداری می‌کند.
- ادمین فقط در تست سناریو به‌صورت seed وجود دارد: `Backend/Tests/Data/Sql/DbScenario/init.sql` (username/password تستی `admin`/`admin`).

## Preserved paths (for the future publish/ftp stage)

| مسیر | مالک | قاعده |
|---|---|---|
| `private/` (کنار `httpdocs/`) | اپراتور | secretهای M29؛ **هرگز** هدف آپلود/حذف نیست |
| `data-protection-keys/**` | اپ (M12) | گم‌شدنش = کانفیگ رمزشده‌ی سرورها غیرقابل بازیابی — بعد از اولین اجرای اپ بکاپ شود |
| `events-*.log` | اپ (Serilog) | لاگ رولینگ |
| `config/**` | اپراتور | `default.ovpn` / کلید خصوصی |
| `appsettings.json` | deploy-managed | بازنویسی مجاز (مقادیر تستی؛ override پروداکشن از `private/secrets.json`) |

گردش‌کار آینده: توقف/راه‌اندازی سایت **دستی از پنل Plesk**؛ آپلود فایل‌به‌فایل با skip-list بالا؛ بدون حذف remote.

## Warnings

- `../DropAll.sql` (پوشه‌ی والد `Database/`) **فقط dev** است — هرگز روی پروداکشن اجرا نشود (همه‌ی آبجکت‌ها را می‌اندازد).
- تست‌ها از همین پوشه دیتابیس سناریو می‌سازند (`StructureFilesPath` در `appsettings.json`) — تغییر این فایل‌ها رفتار تست را تغییر می‌دهد.
- ران‌بوک کامل انتشار: [Docs/19-deploy-runbook.md](../../Docs/19-deploy-runbook.md).

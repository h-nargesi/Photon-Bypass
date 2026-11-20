# Docs/13-xray-integration.md — ادغام دیتاپلین Xray/x-ui

> **English summary**: Photon-Bypass will be reused as the customer panel for an Xray/x-ui data plane (the upstream production infrastructure) instead of Radius. The local-MSSQL-as-source-of-truth pattern stays; a new x-ui adapter replaces the two Radius adapters behind the existing keyed-DI sync contracts. Traffic accounting moves from session rows to cumulative per-client counters: quota = counter minus a per-renewal baseline, and daily/30-day charts are derived from poll deltas. Renewal starts traffic-only (monthly stays disabled). Target host: Windows Server + IIS inside Iran. Prerequisite: probe the exact x-ui (alireza0) v1.12.0 API before writing the adapter. **2026-11-20 update (Docs/23):** references to the upstream infrastructure repo and live hostnames were removed from this doc set, and all repeated test/probe verification now targets the local VirtualBox test VM only.

> **سیاست ۱۴۰۵/۰۸/۲۹ (Docs/23):** ارجاع‌های این سند به ریپوی زیرساخت اجرایی و نام میزبان‌های زنده خنثی شد؛ تست/probe از این پس فقط روی VM لوکال تستی اجرا می‌شود.

## تصمیم (2026-10-02)

- این پروژه پایه‌ی پنل مشتریان زیرساخت بالادستی می‌شود؛ پروژه‌ای جدید ساخته نمی‌شود.
- میزبان: ویندوز سرور داخل ایران با IIS.
- تمدید: فقط ترافیکی در فاز اول؛ گزینه‌ی ماهانه در فرانت و قیمت‌گذاری موجود می‌ماند ولی غیرفعال است.
- پرداخت: جریان فعلی (فاکتور → لینک درگاه → کال‌بک → اجرای مجدد تمدید) همان می‌ماند؛ `// TODO: check token` در `BillingApplication.PaymentCallback` قبل از تولید باید حل شود.
- ارتقا: .NET 8 → .NET 10 در ابتدای کار (8 در نوامبر 2026 به پایان پشتیبانی می‌رسد).

## جای data plane: از Radius به x-ui

زیرساخت مقصد: سرور اجرایی زیرساخت بالادستی (نام میزبان حذف شد — Docs/23) با x-ui (alireza0) v1.12.0 و Xray-core؛ یک inbound کاربر روی 443 (VLESS + XHTTP + REALITY)؛ هر کاربر یک client UUID در همان inbound؛ پنل x-ui فقط روی loopback پشت nftables قابل دسترسی است.

> **به‌روزرسانی ناوگان ۱۴۰۵/۰۷/۲۴ (۲۰۲۶-۱۰-۱۵):** میزبان اول اجرایی نیست. ناوگان زنده: دو سرور زیرساخت بالادستی (نام‌ها حذف شد — Docs/23)، هر دو x-ui **v1.14.0**. قرارداد adapter (`XuiApiService`) دوباره از سورس v1.14.0 کد-تأیید شد — مسیرها و رفتار همه‌ی endpoint های مصرفی با v1.12.0 یکسان است (جزئیات: Docs/17 §4). طبق Docs/23، تأییدهای تکرارشونده فقط روی VM لوکال تستی اجرا می‌شوند.

نگاشت مفاهیم برای adapter جدید:

| مفهوم Radius (فعلی) | معادل x-ui (مقصد) |
| --- | --- |
| permanent user (username/password) | client با UUID در inbound واحد 443 |
| `SyncUserAndActive` | addClient / updateClient روی همان inbound |
| `DeactivateUsers` | حذف یا block همان UUID |
| `ChangeVpnPassword` | بی‌معنا — credential همان UUID/لینک است |
| ارسال config.openvpn | ساخت subscription link با group label (قواعد دامنه‌ی زیرساخت بالادستی) |
| بستن کانکشن میکروتیک | رفتار دقیق باید probe شود (حذف کلاینت، اتصال‌های جاری) |
| `UpdateTrafficData` از radacct (ردیف سشن) | poll شمارنده‌های تجمعی per-client از x-ui |

نقطه‌ی ورود فنی: همان قراردادهای `IInfraAccountRadiusSyncService` / `IInfraSessionRadiusSyncService` با keyed DI (الگوی `Docs/06`)؛ پروژه‌های `RadiusDesk` و `MikrotikRadius` در این مسیر کنار می‌روند و یک adapter جدید (کلید سوم یا جایگزین) ساخته می‌شود. متدهای Radius هرگز مستقیم در Application صدا زده نمی‌شوند — همین قاعده برای adapter جدید هم برقرار است.

## منبع حقیقت

الگوی فعلی حفظ می‌شود و فقط نقش executor عوض می‌شود:

- **MSSQL محلی**: حقیقت کسب‌وکار (اکانت، فروشنده/Owner، کیف پول، تمدید، قیمت، سرورها) — مثل امروز.
- **x-ui**: آینه‌ی اجرا. نوشتن فقط از پنل به x-ui، خواندن آمار فقط از x-ui به پنل.
- **Job آشتی‌سازی**: معادل `DeactivateInvalidRadiusUsers` — کلاینت بدون entitlement معتبر در پنل غیرفعال می‌شود؛ کلاینت ناشناخته در inbound هشدار می‌دهد (امروز x-ui هنوز دستی هم ویرایش می‌شود؛ پس از زندگی پنل، پنل تنها نویسنده است).
- درس عملیاتی پشتیبان این تصمیم: در زیرساخت بالادستی سرور خارجی با restore شدن وضعیت x-ui جایگزین شد؛ با پنل به‌عنوان حقیقت، inbound از DB پنل دوباره push می‌شود.

## مدل ترافیک: از سشن به شمارنده

`TrafficDataEntity` فعلی سشن‌محور است (`SessionId`, `StartSession`/`EndSession`, `DataIn/Out`, `NasId`) و از radacct می‌آید. x-ui سشن و ردیف روزانه ندارد؛ فقط شمارنده‌ی تجمعی per-client (از لحظه ساخت یا آخرین reset). طراحی جایگزین:

1. **سهمیه (حکم)**: هر تمدید ترافیکی یک **baseline** در پنل ثبت می‌کند؛ مصرف چرخه = شمارنده‌ی فعلی − baseline. reset کردن شمارنده در x-ui مبنای محاسبه نیست (حقیقت تجمعی را نابود می‌کند)؛ اگر نسخه‌ی x-ui قطع خودکار روی سقف حجم را پشتیبانی کند، فقط برای enforcement فوری فعال می‌شود.
2. **نمودار روزانه/۳۰روزه (نمایش)**: collector خودش سری زمانی می‌سازد: poll هر N دقیقه → رکورد `UsageSnapshot` (NodeId, UUID, زمان, Up/Down) → delta بین pollهای متوالی → تجمیع `UsageDaily`. precision = بازه‌ی poll + فاصله‌ی flush شمارنده در x-ui.
3. **epoch change**: کاهش شمارنده (reset/ساخت مجدد کلاینت) = لنگر مجدد baseline و علامت‌گذاری روز، نه مصرف منفی.
4. ویو `TotalPlanState` و ایده‌ی «بان‌های ترافیک هر تمدید» مستقیم قابل حمل است — حتی ساده‌تر، چون یک شمارنده روی محور تجمعی حرکت می‌کند به‌جای نگاشت صدها سشن.
5. «آخرین اتصال» از شمارنده به‌دست نمی‌آید؛ یا از قابلیت online/IP-list همان نسخه (موضوع probe) یا تقریبِ تغییر شمارنده در poll اخیر.

## پیش‌نیازهای قبل از نوشتن adapter

- **Probe قرارداد API** نسخه‌ی alireza0 v1.12.0 روی پنل x-ui (یک‌بار اجرا شد؛ از Docs/23 فقط روی VM لوکال): login/session، لیست inbound و کلاینت‌ها، add/update/remove، خواندن آمار per-client، رفتار expiry/depletion، فاصله‌ی flush، دیدن وضعیت آنلاین. تغییر کلاینت در inbound ممکن است Xray را restart کند — در تست لوکال بی‌خطر است.
- **کانال دسترسی**: پنل x-ui فقط loopback است (nftables). گزینه‌ها: tunnel SSH با کلید محدود به forward، agent روی سرور با pull خروجی HTTPS، یا استثنا در فایروال. تصمیم در سمت زیرساخت بالادستی ثبت می‌شود.
- **دامنه‌ی پنل**: zone سوم، جدا از zone کاربران و zone عملیاتی (قواعد زیرساخت بالادستی)؛ گواهی در CT logs دیده می‌شود.

## باز بودن‌های محصولی

- هویت ثبت‌نام (پیش‌فرض فعلی: username + email اجباری، موبایل اختیاری) — تأیید نشده.
- اختیارات فروشنده: فقط مشاهده/تمدید یا ساخت کاربر هم؟ نحوه‌ی اتصال کاربر به فروشنده (کد دعوت؟).
- نحوه‌ی جایگزینی `ChangeVpnPassword` و «Send Config» در UI برای کاربر نهایی.

## ارجاع متقابل

- سمت زیرساخت بالادستی: سند تصمیمات پنل در ریپوی زیرساخت اجرایی (ارجاع حذف شد — Docs/23).
- قواعد دامنه و دسترسی در ریپوی زیرساخت اجرایی (ارجاع حذف شد — Docs/23).

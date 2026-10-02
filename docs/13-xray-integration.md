# docs/13-xray-integration.md — Simorgh / x-ui integration

> **English summary**: Photon-Bypass will be reused as the customer panel for an Xray/x-ui data plane (the Simorgh infrastructure) instead of Radius. The local-MSSQL-as-source-of-truth pattern stays; a new x-ui adapter replaces the two Radius adapters behind the existing keyed-DI sync contracts. Traffic accounting moves from session rows to cumulative per-client counters: quota = counter minus a per-renewal baseline, and daily/30-day charts are derived from poll deltas. Renewal starts traffic-only (monthly stays disabled). Target host: Windows Server + IIS inside Iran. Prerequisite: probe the exact x-ui (alireza0) v1.12.0 API on the live server before writing the adapter. Decision record on the Simorgh side: `docs/panel/README.md` in the Simorgh repo (`~/Developing/Simorgh`).

## تصمیم (2026-10-02)

- این پروژه پایه‌ی پنل مشتریان Simorgh می‌شود؛ پروژه‌ای جدید ساخته نمی‌شود.
- میزبان: ویندوز سرور داخل ایران با IIS.
- تمدید: فقط ترافیکی در فاز اول؛ گزینه‌ی ماهانه در فرانت و قیمت‌گذاری موجود می‌ماند ولی غیرفعال است.
- پرداخت: جریان فعلی (فاکتور → لینک درگاه → کال‌بک → اجرای مجدد تمدید) همان می‌ماند؛ `// TODO: check token` در `BillingApplication.PaymentCallback` قبل از تولید باید حل شود.
- ارتقا: .NET 8 → .NET 10 در ابتدای کار (8 در نوامبر 2026 به پایان پشتیبانی می‌رسد).

## جای data plane: از Radius به x-ui

زیرساخت مقصد (Simorgh): سرور داخل vm01.ir.arvan با x-ui (alireza0) v1.12.0 و Xray-core؛ یک inbound کاربر на 443 (VLESS + XHTTP + REALITY)؛ هر کاربر یک client UUID در همان inbound؛ پنل x-ui فقط روی loopback پشت nftables قابل دسترسی است.

نگاشت مفاهیم برای adapter جدید:

| مفهوم Radius (فعلی) | معادل x-ui (مقصد) |
| --- | --- |
| permanent user (username/password) | client با UUID در inbound واحد 443 |
| `SyncUserAndActive` | addClient / updateClient روی همان inbound |
| `DeactivateUsers` | حذف یا block همان UUID |
| `ChangeVpnPassword` | بی‌معنا — credential همان UUID/لینک است |
| ارسال config.openvpn | ساخت subscription link با group label (قواعد دامنه در Simorgh) |
| بستن کانکشن میکروتیک | رفتار دقیق باید probe شود (حذف کلاینت، اتصال‌های جاری) |
| `UpdateTrafficData` از radacct (ردیف سشن) | poll شمارنده‌های تجمعی per-client از x-ui |

نقطه‌ی ورود فنی: همان قراردادهای `IInfraAccountRadiusSyncService` / `IInfraSessionRadiusSyncService` با keyed DI (الگوی `docs/06`)؛ پروژه‌های `FreeRadius` و `MikrotikRadius` در این مسیر کنار می‌روند و یک adapter جدید (کلید سوم یا جایگزین) ساخته می‌شود. متدهای Radius هرگز مستقیم در Application صدا زده نمی‌شوند — همین قاعده برای adapter جدید هم برقرار است.

## منبع حقیقت

الگوی فعلی حفظ می‌شود و فقط نقش executor عوض می‌شود:

- **MSSQL محلی**: حقیقت کسب‌وکار (اکانت، فروشنده/Owner، کیف پول، تمدید، قیمت، سرورها) — مثل امروز.
- **x-ui**: آینه‌ی اجرا. نوشتن فقط از پنل به x-ui، خواندن آمار فقط از x-ui به پنل.
- **Job آشتی‌سازی**: معادل `DeactivateInvalidRadiusUsers` — کلاینت بدون entitlement معتبر در پنل غیرفعال می‌شود؛ کلاینت ناشناخته در inbound هشدار می‌دهد (امروز x-ui هنوز دستی هم ویرایش می‌شود؛ پس از زندگی پنل، پنل تنها نویسنده است).
- درس عملیاتی پشتیبان این تصمیم: در Simorgh سرور خارجی با restore شدن وضعیت x-ui جایگزین شد؛ با پنل به‌عنوان حقیقت، inbound از DB پنل دوباره push می‌شود.

## مدل ترافیک: از سشن به شمارنده

`TrafficDataEntity` فعلی سشن‌محور است (`SessionId`, `StartSession`/`EndSession`, `DataIn/Out`, `NasId`) و از radacct می‌آید. x-ui سشن و ردیف روزانه ندارد؛ فقط شمارنده‌ی تجمعی per-client (از لحظه ساخت یا آخرین reset). طراحی جایگزین:

1. **سهمیه (حکم)**: هر تمدید ترافیکی یک **baseline** در پنل ثبت می‌کند؛ مصرف چرخه = شمارنده‌ی فعلی − baseline. reset کردن شمارنده در x-ui مبنای محاسبه نیست (حقیقت تجمعی را نابود می‌کند)؛ اگر نسخه‌ی x-ui قطع خودکار روی سقف حجم را پشتیبانی کند، فقط برای enforcement فوری فعال می‌شود.
2. **نمودار روزانه/۳۰روزه (نمایش)**: collector خودش سری زمانی می‌سازد: poll هر N دقیقه → رکورد `UsageSnapshot` (NodeId, UUID, زمان, Up/Down) → delta بین pollهای متوالی → تجمیع `UsageDaily`. precision = بازه‌ی poll + فاصله‌ی flush شمارنده در x-ui.
3. **epoch change**: کاهش شمارنده (reset/ساخت مجدد کلاینت) = لنگر مجدد baseline و علامت‌گذاری روز، نه مصرف منفی.
4. ویو `TotalPlanState` و ایده‌ی «بان‌های ترافیک هر تمدید» مستقیم قابل حمل است — حتی ساده‌تر، چون یک شمارنده روی محور تجمعی حرکت می‌کند به‌جای نگاشت صدها سشن.
5. «آخرین اتصال» از شمارنده به‌دست نمی‌آید؛ یا از قابلیت online/IP-list همان نسخه (موضوع probe) یا تقریبِ تغییر شمارنده در poll اخیر.

## پیش‌نیازهای قبل از نوشتن adapter

- **Probe قرارداد API** نسخه‌ی alireza0 v1.12.0 روی سرور زنده: login/session، لیست inbound و کلاینت‌ها، add/update/remove، خواندن آمار per-client، رفتار expiry/depletion، فاصله‌ی flush، دیدن وضعیت آنلاین. تغییر کلاینت در inbound زنده ممکن است Xray را restart کند — ساعت کم‌بار و حذف کلاینت تست در پایان.
- **کانال دسترسی**: پنل x-ui فقط loopback است (nftables). گزینه‌ها: tunnel SSH با کلید محدود به forward، agent روی سرور با pull خروجی HTTPS، یا استثنا در فایروال. تصمیم در Simorgh ثبت می‌شود.
- **دامنه‌ی پنل**: zone سوم، جدا از zone کاربران و zone عملیاتی (قواعد Simorgh)؛ گواهی در CT logs دیده می‌شود.

## باز بودن‌های محصولی

- هویت ثبت‌نام (پیش‌فرض فعلی: username + email اجباری، موبایل اختیاری) — تأیید نشده.
- اختیارات فروشنده: فقط مشاهده/تمدید یا ساخت کاربر هم؟ نحوه‌ی اتصال کاربر به فروشنده (کد دعوت؟).
- نحوه‌ی جایگزینی `ChangeVpnPassword` و «Send Config» در UI برای کاربر نهایی.

## ارجاع متقابل

- سمت Simorgh: `docs/panel/README.md` در `~/Developing/Simorgh` (تصمیم‌ها، D-های باز، ترتیب اجرا).
- قواعد دامنه و دسترسی: `registry/domains.md` و `servers/vm01.ir.arvan/panel-access.md` در Simorgh.

# docs/16-multi-server-panel.md — پنل چند-سرور: ثبت تصمیمات جلسه ۱۴۰۵/۰۷/۲۲

> **English summary**: Decision record for the multi-server admin panel (session 2026-10-14). Requirement: one panel managing heterogeneous VPN servers and reporting — Mikrotik UserManager (implemented), RadiusDesk (implemented, unlikely to be used), raw FreeRADIUS (explicitly deferred), and an x-ui panel adapter (next up, per `docs/13`). Decided: **D1** rename the misleadingly-named `FreeRadius` project to `RadiusDesk` (approved; **executed 2026-10-16**); **D2** x-ui joins the existing keyed-DI sync dispatch via one central Features→key mapping (interface names unchanged, `Xui = 0x4` inside the `ServerFeature.Radius` mask); **D3** identity mapping = client email `= username`, client UUID stored in the existing `AccountEntity.VpnPassword` column (no schema change); **D4** for x-ui accounts `ChangeVpnPassword` becomes an **automatic UUID rotation** (not user input), with contract/UX/throttle conditions. Open for next session: traffic data model (T1), quota enforcement (T2), capability gating, realm capacity, project name. Includes the probe checklist that blocks adapter implementation. **2026-10-15 update:** T1 and T2 are now closed in [docs/17](17-xui-traffic.md) (D5–D14); T2's closure **overrides** the "panel-only" default recorded in §3 below. **Fleet update (same day, later):** vm01.ir.arvan (v1.12.0) is **not operational** — the live fleet is vm06.ir.parssafe and vm08.ir.parssafe (continuation of vm07 state), both x-ui **v1.14.0**; the adapter contract was re-code-verified against v1.14.0 (route table and handlers for everything we use are identical to v1.12.0 — details in docs/17 §4) and the probe/registration target is now vm06/vm08.

## ۱. نیازمندی و زمینه

پنل باید بتواند سرورهای مختلف را مدیریت کند و گزارش بگیرد. انواع سرور مطرح‌شده توسط مالک:

| نوع سرور | وضعیت | تصمیم این جلسه |
| --- | --- | --- |
| Mikrotik User Manager | تقریباً پیاده‌سازی‌شده (`MikrotikRadius`) | — |
| RadiusDesk | تقریباً پیاده‌سازی‌شده (پروژه‌ی `FreeRadius` فعلی) — «احتمال کم» استفاده | رنیم به `RadiusDesk` و انجماد (D1) |
| FreeRADIUS خام | پیاده‌سازی نشده | **فعلاً شروع نمی‌شود** (تصمیم صریح مالک) |
| پنل x-ui | موضوع `docs/13` (Simorgh) | adapter جدید (D2-D4 + بازها) |

یافته‌های تحلیل این جلسه (خلاصه؛ جزئیات در گفتگوی جلسه):

- **آماده**: مدل `ServerEntity` با `Features` (flags) + کانفیگ رمزنگاری‌شده‌ی per-server (`ServerConfiguration`: `SshConfig`/`WebApiConfig`/`DataBaseInfo`)؛ dispatch با keyed DI (`RadiusType.RadiusDesk`/`UserManager`)؛ fan-out موازی `RunJob`؛ گزارش‌های کسب‌وکار از MSSQL محلی (`ServerManagementService` + ویو `TotalPlanState`).
- **شکاف‌ها**: انتزاع Radius-محور (سوئیچ `Features & ServerFeature.Radius` در ~۹ نقطه‌ی `Infrastructure/Services/AccountRadiusSyncService.cs` و `SessionRadiusSyncService.cs` تکرار شده)؛ پنل `Admin` خالی؛ دو مدل ترافیک ناسازگار (سشن radacct در برابر شمارنده‌ی تجمعی x-ui)؛ قابلیت‌های ویژه‌ی هر بک‌اند (گواهی OVPN فقط Mikrotik؛ `ChangeVpnPassword` برای x-ui بی‌معنا *به معنای پسورد* — با D4 معنای جدید گرفت).
- نکته‌ی مهم: **پروژه‌ی `FreeRadius` فعلی «RadiusDesk» است، نه FreeRADIUS خام** — خواندن از MySQL ولی نوشتن از طریق Web API کیک۴ (`cake4/rd_cake`). FreeRADIUS خام فقط MySQL دارد و adapter آینده‌اش مسیر نوشتن جدا خواهد داشت (SQL مستقیم به `radcheck`/`radusergroup` یا `radclient` از طریق SSH) هرچند می‌تواند خواندن را با `RadDbContext` شریک شود.

جریان صدور گواهی OVPN فعلی (تحلیل همین جلسه، خلاصه): CA واقعی روی روتر Mikrotik است (`LMTCA` + `CLIENT-TEMPLATE` به‌صورت دستی از قبل باید باشند)؛ قالب `config/default.ovpn` در `ServerManagementService.GetDefaultCertificate` پر می‌شود؛ `MikrotikDirectService.GetOVpnCertificate` روی NAS اولِ realm (`nas_list[0]`) گواهی `CLIENT_{username}` را می‌سازد/صادر می‌کند؛ تکثیر به NASهای بعدی با `SetOVpnCertificate` است که `NotImplementedException` می‌اندازد (realm چند-NAS می‌شکند). برای x-ui جایگزین این مسیر subscription link است (probe).

## ۲. تصمیم‌های قطعی‌شده

### D1 — رنیم پروژه‌ی `FreeRadius` → `RadiusDesk` ✅ (تأیید مالک؛ اجرا شد ۱۴۰۵/۰۷/۲۴)

همه‌چیز داخل پروژه از قبل «RadiusDesk» نام دارد (`RadiusDeskService`، `AccountRadiusSyncRadiusDeskService`، `AddRadiusDeskServices`، کلید `RadiusType.RadiusDesk`)؛ فقط نام گمراه‌کننده است.

دامنه‌ی تغییر:
- پوشه + `FreeRadius.csproj` + سطر sln (`Backend/Photon-Bypass.sln:16`).
- `RootNamespace: PhotonBypass.FreeRadius` → `PhotonBypass.RadiusDesk` — حدود ۶۱ using در ~۳۰ فایل؛ عمده داخل خود پروژه + `Tests/Mock/MockFreeRadius` + `Backend/ServiceFactoryHandler/ServiceFactoryHandler.cs:3`.
- اختیاری/زیبایی‌شناسی: کلید HttpClient `"free-radius"`، نام فولدر `MockFreeRadius`.
- **بدون تغییر**: کلید DI، دیتابیس، قراردادها، رفتار. تأیید نهایی: `dotnet build` + `dotnet test`.
- **پروژه‌ی جدید FreeRADIUS الان ساخته نمی‌شود** (پروژه‌ی خالی فقط نام را دوباره مبهم می‌کند). نام `FreeRadius` برای adapter آینده آزاد می‌ماند.

> **اجرا (۱۴۰۵/۰۷/۲۴)**: پوشه/csproj/sln + `RootNamespace: PhotonBypass.RadiusDesk` (همه‌ی usingها) + فولدر `MockFreeRadius`→`MockRadiusDesk` + کلید HttpClient `"free-radius"`→`"radius-desk"`. GUID پروژه در sln حفظ شد؛ `dotnet test` سبز (۳۵۱ پاس/۰ خطا). خطای build ایزوله‌ی `Tik4net/tik4net.tests` از قبل روی HEAD موجود بود و بی‌ربط است.

### D2 — اتصال x-ui به dispatch: نگاشت مرکزی، نام‌ها می‌مانند ✅ (تأیید مالک)

- بیت جدید `Xui = 0x4` داخل ماسک `ServerFeature.Radius` (`0xFF`) — پس کوئری‌های فعلی `ServerRepository` (`Features & Radius > 0`) سرورهای x-ui را هم برمی‌گردانند.
- سوئیچ‌های تکراری (~۹ مورد) در `AccountRadiusSyncService`/`SessionRadiusSyncService` با **یک نگاشت مرکزی** Features→کلید keyed جایگزین می‌شود (مثلاً `ServerFeatureExtensions.GetSyncKey(server)` + حل با `GetRequiredKeyedService`). افزودن دیتاپلین چهارم بعداً = تغییر یک نقطه.
- نام اینترفیس‌ها (`IAccountRadiusSyncService`، `IInfraAccountRadiusSyncService`، …) **دست‌نخورده می‌ماند** (مطابق `docs/13`: «همان قراردادها، کلید سوم»). rename قراردادها به مفهوم ServerKind صریحاً رد شد (churn زیاد در لحظه‌ی اشتباه).
- کلید جدید: `RadiusType.Xui = "Xui"` در `Infrastructure/Radius/RadiusType.cs`.

### D3 — نگاشت هویت: email = username، UUID = ستون `VpnPassword` ✅ (تصمیم مالک)

- بدون تغییر اسکیما: email کلاینت x-ui = username پنل؛ UUID کلاینت در `AccountEntity.VpnPassword` ذخیره می‌شود.
- UUID را **پنل خودش** با `Guid.NewGuid()` در اولین sync می‌سازد (قطعی؛ مستقل از auto-generate نسخه‌ی alireza0).
- پیامد: معنای ستون `VpnPassword` بسته به نوع دیتاپلین اکانت فرق می‌کند — UI نباید برای کاربر x-ui آن را به‌عنوان «پسورد» نمایش دهد.
- مدل «یک دیتاپلین فعال در هر لحظه» (هر اکانت یک realm فعال + غیرفعال‌سازی در بقیه) این نگاشت تک‌مقداری را پشتیبانی می‌کند.

### D4 — `ChangeVpnPassword` برای x-ui = چرخش خودکار UUID ✅ (پیشنهاد مالک؛ توافق با شرایط) — **اجرا شد (جلسه ۲۰۲۶-۱۰-۱۶)**

پیشنهاد مالک: «برای اکانت x-ui، مسیر ChangeVpnPassword باید UUID را به‌صورت خودکار (نه از کاربر) تغییر دهد» — پذیرفته شد. معنای درست: **چرخش credential** (UUID جدید = لغو اثر هر UUID/کانفیگ لو رفته). شرایط ثبت‌شده:

1. **تغییر قرارداد برای جلوگیری از desync**: امضای متد به بازگرداندن effective credential تغییر کند (مثلاً `Task<string>`)؛ adapterهای Radius همان ورودی را برمی‌گردانند؛ adapter x-ui مقدار تولیدشده را؛ و `VpnApplication` مقدار **بازگشتی** را در `account.VpnPassword` ذخیره کند — نه ورودی کاربر (امروز `VpnApplication.cs:52` ورودی را ذخیره می‌کند؛ اگر adapter مقدار دیگری بسازد mapping می‌شکند). تغییر مکانیکی روی دو adapter موجود. ~~**اصلاح جلسه ۲۰۲۶-۱۰-۱۶**~~ — **اصلاح ثبت‌شده**: تولید UUID در **دیسپچر Infrastructure** انجام می‌شود (نه adapter): برای هر درخواست فقط یک UUID ساخته می‌شود، به همه‌ی سرورهای x-ui همان پاس می‌شود («اعمال کن و همان را برگردان») و credential مؤثر به `VpnApplication` برمی‌گردد؛ این‌طوری چند سرور x-ui در یک realm هرگز UUIDهای واگرا نمی‌گیرند.
2. **UX**: برای کاربر x-ui دکمه‌ی «تغییر پسورد VPN» در فرانت → «چرخش شناسه اتصال» با تأییدیه؛ پیام پاسخ: کانفیگ قبلی باطل شد، دوباره دریافت شود. پایداری subscription link تحت چرخش UUID مشروط به probe است (اگر link بر اساس subid جدا باشد، می‌ماند). **اجرا**: مسیر `rotate-ovpn-id` فقط re-auth توکن می‌گیرد (بدون فیلد پسورد)؛ پاسخ فقط پیام است و خود لینک از کانال‌های تحویل گرفته می‌شود.
3. **Throttle الزامی per-account**: هر `updateClient` ممکن است Xray را restart کند (هشدار `docs/13`) — کاربرِ پشت‌سرهم‌زننده می‌تواند کل inbound را برای همه از کار بیندازد. الگوی مشابه `LastWarningTime`؛ فاصله‌ی حداقلی قابل‌تنظیم از کانفیگ. **اجرا**: ستون `Account.LastCredentialRotateTime` (اسکریپت idempotent `AccountCredentialRotation.sql`) + `ManagementOptions.MinUuidRotationMinutes` با پیش‌فرض **۶۰ دقیقه** (صفر = خاموش)؛ گیت فقط برای kind=xui و قبل از dispatch در `VpnApplication`.
4. کانکشن‌های جاری ممکن است تا timeout/restart زنده بمانند (probe).

**ثبت اجرا (۲۰۲۶-۱۰-۱۶)**: قرارداد `Task<string>` روی هر دو سطح (`IAccountRadiusSyncService`/`IInfraAccountRadiusSyncService`)؛ دیسپچر با گارد `UserException` برای realm مخلوط (قبل از fan-out)؛ چرخش = پچ-only UUID روی کلاینت زنده بدون `ResetClientTraffic`/بازنویسی لنگرها و fail-loud وقتی کلاینت روی پنل نیست؛ **تحویل سه‌کاناله‌ی کانفیگ** به تصمیم مالک: ایمیل (قالب جدید `VpnLinkEmail`)، دانلود مستقیم (`GET api/vpn/download`؛ فایل متنی «نام کاربری + لینک vless»؛ برای Mikrotik همان OVPN فعلی) و نمایش/کپی لینک در پنل (`GET api/vpn/config-info`)؛ اجزای لینک vless زنده از `listInbounds` خوانده می‌شوند (`VlessLinkService`) و هیچ کپی در کانفیگ سرور ذخیره نمی‌شود. پرچم `SupportsCredentialRotation` در plan-info برای رندر دکمه‌ها. فعال‌سازی عملیاتی با ثبت سرورهای vm06/vm08 آغاز می‌شود (هیچ گیت کدی روی probe نیست).

## ۳. تصمیم‌های باز (شروع جلسه‌ی بعد از این‌جا)

> **2026-10-15:** T1 و T2 در جلسه‌ی ۲۰۲۶-۱۰-۱۵ **بسته شدند** — تصمیمات D5–D14 در [docs/17](17-xui-traffic.md). بستنِ T2 صریحاً پیش‌فرض «فقط پنل» زیر را **نقض می‌کند** (enforcement سمت سرور است). T3/T4/T5 همچنان پیشنهاد ثبت‌شده هستند (T5 این جلسه به‌صورت اسکلت `XuiPanel` اجرا شد — §۶ docs/17).

### T1 — مدل داده‌ی ترافیک x-ui — ✅ بسته شد (D5–D8، D14 در docs/17)

گزینه‌های روی میز:

1. **پیشنهاد**: جدول `UsageSnapshot(Server, Client, Time, Up, Down)` با Quartz job جدید (interval قابل‌تنظیم، پیش‌فرض ۵ دقیقه) + جدول `TrafficBaseline` به‌ازای هر Renewal×Server؛ delta روزانه به‌صورت ردیف `TrafficDataEntity` نوشته شود تا چارت ۳۰روزه‌ی فعلی (`VpnApplication.TrafficData`)، jobهای هشدار/غیرفعال‌سازی و ویو `TotalPlanState` بدون تغییر کار کنند (مطابق `docs/13`).
2. همان ۱ ولی baseline ستون nullable روی `RenewalEntity` (فرض ضمنی: حداکثر یک سرور x-ui در هر realm؛ مهاجرت سرور/چند‌سروره را کثیف می‌کند).
3. بدون جدول جدید؛ فقط merge مصرف تجمعی در `PlanState.TrafficUsed` (ساده‌ترین؛ ولی نمودار ۳۰روزه از کار می‌افتد و گزارش ادمین ضعیف می‌شود).

ملاحظات فنی که در هر حال لازم است: تشخیص **epoch-change** (کاهش شمارنده = لنگر مجدد baseline، نه مصرف منفی)؛ reset نکردن شمارنده‌ی x-ui به‌عنوان مبنای محاسبه؛ «آخرین اتصال» از شمارنده نمی‌آید (online-status نسخه یا تقریب تغییر شمارنده).

### T2 — Enforcement سهمیه/انقضا — ✅ بسته شد (D13 در docs/17: سمت سرور)

گزینه‌ها: فقط پنل (default مطابق `docs/13`: baseline در پنل + job غیرفعال‌سازی؛ گپ تا poll بعدی) / پنل + پوش expiry و total-GB به x-ui (مشروط به probe و تداخلش با baseline) / پنل الان، سرور بعد از probe.
**پیش‌فرض تا تصمیم بعدی: فقط پنل.** — ~~این پیش‌فرض با D13 نقض شد: enforcement سمت سرور است (پوش per-renewal سهمیه/انقضا + بدون watchdog در کلکتور).~~

### T3 — سازوکار شاخه‌بندی قابلیت‌ها (بحث نشد)

`SendCertEmail`/گواهی OVPN فقط Mikrotik؛ برای x-ui جایگزین subscription link است. شاخه‌بندی با چی؟ پیشنهاد اولیه: kind دیتاپلینِ realm فعال (helper روی `IServerRepository`) + دفاع در adapter با `UserException` + اطلاع‌رسانی kind به فرانت در plan-info تا دکمه‌ها درست رندر شوند.

### T4 — ظرفیت realm برای x-ui (بحث نشد)

`LoadServersCapacity` (`ServerManagementService.cs:147`) بر پهنای‌باند NAS است. پیشنهاد: `MaxClients` در کانفیگ x-ui سرور، مصرف = تعداد پلن فعال realm.

### T5 — نام و ساختار پروژه‌ی x-ui (بحث نشد)

پیشنهاد: `XuiPanel`، قرینه‌ی ساختار `MikrotikRadius` — `WebService/XuiService` (HttpClient)، `Application/` (دو کلاس sync)، `ServiceFactory.AddXuiServices` با ثبت keyed روی `RadiusType.Xui`. کانفیگ سرور در `ServerConfiguration` (احتمالاً reuse `WebApiConfig` + فیلد اختصاصی).
**2026-10-15:** اجرا شد — `Backend/XuiPanel/` با `WebService/XuiApiService` (قرارداد mock تا probe) + کلکتور؛ جزئیات در [docs/17 §6](17-xui-traffic.md).

## ۴. پیش‌نیازهای probe روی سرور زنده (بلوکرِ adapter — از `docs/13` + این جلسه)

1. قرارداد API نسخه‌ی alireza0 v1.12.0: login/session، `listInbounds`، add/update/removeClient، جستجوی کلاینت (by email؟)، فیلدهای ترافیک per-client (up/down)، رفتار reset شمارنده، expiry (timestamp/روز؟) و رفتار depletion، وضعیت آنلاین.
2. آیا update/remove کلاینت در inbound زنده، Xray را restart می‌کند؟ (تعیین‌کننده‌ی interval throttle در D4 و زمان‌بندی sync؛ تست در ساعت کم‌بار + کلاینت تست).
3. کانال دسترسی به پنل loopback (nftables): tunnel SSH با کلید محدود / agent / استثنای فایروال — تصمیم در سمت Simorgh باز است.
4. قابلیت subscription link در این نسخه + پایداری آن تحت چرخش UUID (D4).

## ۵. ترتیب اجرای توافقی (نقشه‌ی راه)

1. ~~**D1** رنیم `FreeRadius`→`RadiusDesk`~~ — ✅ اجرا شد (۱۴۰۵/۰۷/۲۴).
2. **D2** نگاشت مرکزی dispatch + بیت `Xui` + `RadiusType.Xui`.
3. اسکلت پروژه‌ی x-ui (T5) + probe (§۴) — موازی.
4. adapter بر اساس نتیجه‌ی probe + D3/D4.
5. T1 (ترافیک) بعد از تصمیم جلسه‌ی بعد.
6. T3/T4 + شاخه‌های فرانت.

## ۶. جدول قابلیت‌ها (مرجع طراحی T3)

| قابلیت | UserManager | RadiusDesk | FreeRADIUS خام | x-ui |
| --- | --- | --- | --- | --- |
| ساخت/فعال‌سازی کاربر | ✅ tik4net | ✅ cake API | ⚠️ SQL مستقیم | ✅ addClient |
| غیرفعال‌سازی/حذف | ✅ | ✅ | ⚠️ SQL | ✅ |
| تغییر credential | ✅ پسورد | ✅ پسورد | ✅ پسورد | ✅ **چرخش UUID (D4)** |
| گواهی OVPN | ✅ SSH | ❌ | ❌ | ❌ → subscription link |
| لیست/بستن سشن آنلاین | ✅ | ✅ | ✅ radacct | ⚠️ probe |
| منبع ترافیک | سشن | سشن | سشن | شمارنده + ردیف دلتای روزانه (D5/D14) |
| Enforcement سهمیه/انقضا | ✅ سمت سرور | ✅ سمت سرور | ✅ سمت سرور | ✅ **سمت سرور** (D13: پوش per-renewal سهمیه/انقضا؛ پنل فقط هشدار/آشتی) |
| Rate-limit | ✅ | ✅ | ✅ | ❌ شکاف قابلیت (انتظار: نبود — probe) |

## ۷. ارجاع‌ها

- **ادامه‌ی این سند (جلسات ۲۰۲۶-۱۰-۱۵، D5–D14): [docs/17-xui-traffic.md](17-xui-traffic.md)** — بستن T1/T2 + ماتریس probe + ثبت مسائل باز
- طرح کلی x-ui: [docs/13-xray-integration.md](13-xray-integration.md)
- الگوی dispatch فعلی: [docs/06-radius-integration.md](06-radius-integration.md)
- وضعیت پیاده‌سازی/شروع‌ها: [docs/12-current-state.md](12-current-state.md)
- بک‌لاگ مالک: `Architecture/TODO.md:26` («پشتیبانی از پنل v2ray و amnezia»)، `:63` («پنل مدیریت سرورها»)
- کدهای کلیدی: `ServerFeature.cs` · `RadiusType.cs` · `AccountRadiusSyncService.cs` · `SessionRadiusSyncService.cs` · `ServerRepository.cs:88-115` · `VpnApplication.cs:42-65` · `MikrotikDirectService.cs:101-173` · `RadiusDesk/ServiceFactory.cs`

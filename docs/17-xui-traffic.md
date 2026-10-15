# docs/17-xui-traffic.md — مدل ترافیک و گزارش x-ui: تصمیمات جلسات ۲۰۲۶-۱۰-۱۵

> **English summary**: Decision record closing **T1 (x-ui traffic data model)** and **T2 (quota/expiry enforcement)** from docs/16, through two owner dialogues on 2026-10-15. Decided: **D5** delta-row traffic model (per-day rows in `TrafficData`, view computes period totals); **D6 (revised)** the counter anchor lives on `Renewal` as nullable columns (`LastSeenCounter`, `LastPollTime`) — no separate table; **D7** zero rows as liveness proof; **D8** multi-day gap distribution (weights = historical daily average, else uniform; epoch during gap = no fabricated history); **D9** reuse the existing renewal transaction pattern; **D10** manual x-ui edits destroy cumulative truth — epoch alarm is the detector; **D11** period total exact, daily split approximate (amended by D14); **D12** reset-at-renewal is part of the enforcement protocol; **D13 — T2 CLOSED** enforcement is **server-side** (x-ui `total`/`expiry`/`limitIp` pushed per renewal, panel does warnings + reconciliation only, **no watchdog in the collector** — this explicitly overrides docs/16 §3's "panel-only" default); **D14** `Renewal.TrafficUsed` (accumulating, sole-writer-disciplined) is the decision source of truth for x-ui realms, `TrafficData` stays display/audit. Implemented in the first session: schema (`RenewalTrafficState.sql` incl. `SessionId` widening to `VARCHAR(64)`), `TotalPlanState` zero-row `LastConnectTime` patch, the `XuiPanel` project skeleton with central Features→key dispatch (D2), the `XuiTrafficCollector` Quartz job, and the per-dataplane `IPlanUsageReader` decision reads. In a follow-up session (2026-10-15) `XuiApiService`/`IXuiApi`/`XuiClientModel` were **rewritten against the alireza0/x-ui v1.12.0 contract as code-verified from the official source** (`xui/API/inbounds/...` paths, uuid-keyed update/del, `msg`/`obj` envelope, two-layer `settings`, explicit `WebApiConfig.InboundId`, `onlines`, 401→re-login retry); a live vm01 probe (fetch `xui/API/openapi.json`, matrix below) still gates activation.

ادامه‌ی مستقیم [docs/16](16-multi-server-panel.md) (جلسه‌ی ۱۴۰۵/۰۷/۲۲ + مکمل ۲۰۲۶-۱۰-۱۴). این سند T1 و T2 را می‌بندد و قواعد بهره‌برداری پیرامون آن‌ها را ثبت می‌کند.

## ۱. زمینه و کدهای مرجع

- `TotalPlanState.sql` — «باندهای ترافیک» renewal با پنجره‌های جمع تجمعی؛ `LastConnectTime = MAX(StartSession)` روی همه‌ی ردیف‌های TrafficData.
- `PlanApplication.cs:291-365` — تمدید تراکنشی است: tx محلی (کیف پول + Renewal + History) دور `SyncUserAndActive`؛ در خطا rollback + جبران `DeactivateUsers`؛ تست در `RenewalRollbackTest.cs`.
- `AccountMonitoringService.cs:27-109` — غیرفعال‌سازی/حذف از `plan.LastConnectTime`؛ هشدار پایان پلن از `TrafficLeft`.
- `TrafficDataRepository` + `RealmRepository.TryLockTrafficSync` — قفل realm با `LastTrafficSync` (آستانه‌ی ۳۰۰ ثانیه‌ای `RenewalBusiness.UpdateTrafficDataTimeSecondLimit`).
- `PlanController.cs:22` — `PlanState` (شامل `TrafficUsed`) به فرانت؛ مستقل از دیتاپلین.
- Simorgh `docs/panel/README.md` — probe نسخه‌ی alireza0 v1.12.0 هنوز اجرا نشده.

## ۲. تصمیم‌های قطعی‌شده

### D5 — مدل ترافیک ردیف-دلتا ✅

کلکتور x-ui ردیف دلتای روزانه در `TrafficData` می‌نویسد؛ جمع دوره همان `PlanState.TrafficUsed` (ویو) می‌ماند. جدول `TrafficBaseline` ساخته نمی‌شود — ریاضی باندهای ویو از دل ردیف‌های دلتا سهمیه را حساب می‌کند. اینواریانت مالک دوباره بیان می‌شود: *بعد از هر poll موفق، TrafficUsed (باند فعال) == شمارنده‌ی x-ui*.
*اصلاح D14:* ویو منبع نمایش همگانی می‌ماند اما منبع تصمیم فقط برای Radius است؛ برای x-ui منبع تصمیم ستون `Renewal.TrafficUsed` است.

### D6 — لنگر شمارنده روی `Renewal` ✅ (بازنگری جلسه‌ی دوم، تأیید مالک)

ستون‌های `LastSeenCounter BIGINT NULL` و `LastPollTime DATETIME NULL` روی `Renewal` اضافه می‌شود؛ **جدول جدا ساخته نمی‌شود**. دلتا = `current − LastSeenCounter`؛ `current < LastSeenCounter` ⇒ epoch غیرمنتظره ⇒ دلتا = `current` + آلارم (تشخیص ویرایش دستی، D10)؛ renewal جدید ⇒ لنگر NULL ⇒ اولین poll: دلتا = شمارنده، بدون آلارم. چون هنوز چیزی release نشده، سناریوی مهاجرت سرور وسط renewal مطرح نیست و reset-at-renewal (D13) عمر لنگر را دقیقاً برابر عمر renewal می‌کند. لنگر فقط لوله‌کشی مکانیکی است؛ حقیقت مصرف در `TrafficData` + ویو (D5/D11) و — پس از D14 — حقیقت تصمیم در `Renewal.TrafficUsed`. قیود:

1. هر reset شمارنده‌ی آغازشده از پنل (تمدید، چرخش UUID اگر شمارنده را صفر کند، تعویض سرور realm در آینده) باید لنگر را از طریق adapter بازمتن کند.
2. اگر سرور x-ui فعال realm عوض شود، کلکتور دلتاها را از سرور غیرفعال رد می‌کند (بررسی NasId ردیف آخر) تا پاک‌سازی شود — پیاده‌سازی: `GetLastNasId`؛ در تعویض سرور دلتا صفر و لنگر re-base می‌شود + هشدار.

*گزینه‌ی ردشده:* جدول `ClientCounterState` با کلید AccountId×ServerId — مقاوم‌تر برای گذار پنجره‌ای بین سرورها، اما over-engineering برای محصولی که هنوز منتشر نشده.

### D7 — ردیف صفر ✅

روزی یک ردیف برای هر کلاینت فعالِ pollشده حتی وقتی شمارنده تغییر نکرده (مدرک liveness + تشخیص گپ). SessionId مصنوعی باید زیر `UK_TrafficData_NasId_SessionId` یکتا بماند، از جمله تمدید وسط روز (دو ردیف همان روز، دو باند متفاوت): `xui:{accountId}:{renewalId}:{yyyyMMdd}`. **پیامد (اجرا شد):** ستون `SessionId` از `VARCHAR(8)` به `VARCHAR(64)` widening شد (`RenewalTrafficState.sql`) و `LastConnectTime` ویو فقط از ردیف‌های با ترافیک مثبت پیش می‌رود (وگرنه منطق رهاشدگی می‌شکست — پچ `TotalPlanState.sql`).

### D8 — گپ چندروزه ✅

در بازگشت poll، دلتای برگشتی *کل مصرف گپ* است: توزیع مجموع واقعی بین روزهای غایب (وزن = میانگین روزانه‌ی تاریخی کاربر اگر موجود، وگرنه یکنواخت)؛ روز آخر (امروز) باقیمانده‌ی گردکردن را می‌گیرد تا **مجموع سهم‌ها دقیقاً برابر مجموع واقعی** بماند. اگر داخل گپ epoch رخ داده باشد (مجموع نامعلوم، مثلاً restore): ردیف تاریخچه ساختگی نوشته نمی‌شود — فقط ردیف امروز با `current` + آلارم (جهت محافظه‌کارانه D10/D14).

### D9 — تراکنش تمدید ✅

الگوی موجود (`PlanApplication.cs:291-365`) بی‌تغییر برای x-ui: sync داخل tx محلی، rollback + جبران `DeactivateUsers`، job آشتی‌دهی (`DeactivateInvalidRadiusUsers`) به‌عنوان تور ایمنی. پیش‌نیاز pre-production: رفع `// TODO: check token` در `BillingApplication.PaymentCallback`.

### D10 — ویرایش دستی x-ui = مسئله‌ی باز ✅ (ثبت مقایسه)

ویرایش دستی Radius باعث *drift* می‌شود (radacct فقط-الحاقی؛ job آشتی‌دهی می‌کند)؛ ویرایش دستی x-ui می‌تواند *حقیقت تجمعی را نابود* کند (دکمه‌ی reset، ساخت مجدد، restore — تجربه‌ی ثبت‌شده‌ی Simorgh). **آلارم epoch** سازوکار تشخیص است (`XUI-EPOCH-ALARM` در لاگ کلکتور). ثبت شد: در rollback-restore قانون epoch بیش‌شماری می‌کند (جهت محافظه‌کارانه) — قابل قبول، ولی باید در عملیات دیده شود.

### D11 — گزارش/نمایش ✅ (اصلاح D14)

جمع دوره دقیق (`TrafficUsed`)، تفکیک روزانه برای x-ui تقریبی (توزیع گپ/سهم روزانه). متن سربرگ چارت: «تفکیک روزانه تقریبی است؛ مجموع دوره دقیق گزارش می‌شود.» کپی موازی ذخیره‌شده از جمع‌ها ممنوع بود (ریسک desync) — **D14 استثنا ثبت می‌کند**: زیر نظم sole-writer، ستون `Renewal.TrafficUsed` کپی تصمیمِ تحریم‌شده است. نوع دیتاپلین باید به فرانت برسد (تغذیه‌ی T3).

### D12 — دکمه‌ی «reset traffic» ✅

reset در تمدید = بخشی از پروتکل enforcement (D13)، نه بهداشت. وجود reset per-client، ری‌استارت Xray و… = آیتم‌های probe (§۴).

### D13 — T2 بسته شد: enforcement سمت سرور ✅ (تأیید مالک)

پنل هیچ تعهدی برای قطع کاربر در پایان پلن ندارد — این وظیفه‌ی سرور مدیریت کاربر است، دقیقاً مثل RADIUS امروز (سابقه: `UserManagerHelper.cs:12-91` پروفایل/محدودیت را از خود renewal می‌سازد). `SyncUserAndActive` = ساخت/انتساب + پوش محدودیت‌ها؛ jobهای پنل = هشدار + آشتی/پاک‌سازی غیرتمدیدی‌ها. **در کلکتور watchdog نداریم** (نقض پیش‌فرض «فقط پنل» که در docs/16 §3 ثبت شده بود).

کدگذاری (**Encoding A، per-renewal** — آینه‌ی سابقه‌ی Mikrotik و خواسته‌ی reset-at-renewal مالک): در هر `SyncUserAndActive` → reset شمارنده‌ی کلاینت → `total = renewal.TrafficLimit`، `expiry = now + TimeLimitInDays` (وقتی NULL است: کدگذاری «بدون محدودیت» — جزئیات probe)، `limitIp = SimultaneousUser`. چون x-ui سهمیه را با شمارنده‌ی تجمعی کلاینت می‌سنجد (نه از زمان انتساب)، enforcement هر-renewal نیاز به همان reset دارد.

**قاعده‌ی epoch (دو نگرانی جدا):**
- (الف) *بازپوش total از حقیقت پنل بعد از هر رویداد epoch*: تمدید → reset + `total = renewal.TrafficLimit` (سهمیه‌ی کامل جدید)؛ مهاجرت → بازتنظیم لنگر + `total = TrafficLeft`؛ epoch غیرمنتظره → `total = TrafficLeft`. بدون بازپوش، هر reset بی‌total سهمیه‌ی مصرف‌شده را دوباره می‌بخشد — همان چیزی که مالک ممنوع کرد. ⚠️ هشدار پیاده‌سازی: `SyncUserAndActive` فقط در تمدیدِ جدید (PlanApplication) صدا می‌شود؛ اگر روزی مسیر re-sync میانه‌ی renewal (مثلاً آشتی‌دهی) اضافه شود باید از شاخه‌ی `total = TrafficLeft` استفاده کند وگرنه بی‌سروصدا سهمیه را دوباره می‌بخشد.
- (ب) *آلارم فقط برای epoch غیرمنتظره* (reset/restore دستی) برای توجه عملیات.

شاخه‌ی چرخش UUID (منوط به probe): اگر `updateClient` شمارنده‌های per-client را نگه دارد (محتمل — آمار با email کلاینت کلید می‌خورد)، چرخش یک رویداد غیرترافیکی است (نه reset، نه بازپوش)؛ اگر صفر کند، خودِ فلوِ چرخش (سمت adapter، نه کلکتور) باید `total = TrafficLeft` را در لحظه‌ی چرخش بازپوش کند.

### D14 — `Renewal.TrafficUsed` = حقیقت تصمیم برای x-ui ✅ (تأیید مالک، جلسه‌ی دوم)

چهار ستون روی `Renewal`:

| ستون | نقش |
| --- | --- |
| `LastSeenCounter BIGINT NULL` | لنگر مکانیکی — مبنای دلتا، تشخیص epoch (D6) |
| `LastPollTime DATETIME NULL` | تشخیص گپ/liveness (D8) |
| `TrafficUsed BIGINT NOT NULL DEFAULT 0` | حقیقت تصمیم؛ هر poll `+= delta`؛ در epoch غیرمنتظره `current` را می‌شمارد (محافظه‌کاری D10)؛ با renewal جدید implicitly صفر می‌شود |
| `LastActiveTime DATETIME NULL` | سیگنال رهاشدگی؛ فقط وقتی `delta > 0` به‌روز می‌شود |

خوانش‌های تصمیم **per-dataplane** می‌شوند: realmهای x-ui → ستون‌های Renewal (`TrafficLeft = TrafficLimit − TrafficUsed` در renewal فعال)؛ realmهای Radius → ویو، بی‌تغییر. مصرف‌کنندگان: `VpnApplication` (گارد `TrafficLeft < 1`)، `AccountMonitoringService` (هشدار پایان + رهاشدگی) و `PlanApplication` (شرط realm-فاصله‌دار تمدید) — همه از `IPlanUsageReader` می‌خوانند. **اصلاح D11:** کپی ذخیره زیر نظم sole-writer تحریم می‌شود — کلکتور تنها نویسنده‌ی ردیف‌های TrafficData *و* ستون‌های Renewal برای realmهای x-ui است، هر دو در **یک تراکنش**؛ تساوی ویو/ستون by construction برقرار است (اینواریانت D8 + شارژ epoch) و هر واگرایی = باگ = آلارم. **اصلاح D5:** ویو منبع نمایش همگانی (و منبع تصمیم فقط Radius) می‌ماند. منفعت ثبت‌شده: مسیر تصمیم از ویوِ O(rows) با جمع تجمعی به خوانش ستون ایندکس‌شده‌ی O(1) در hot path پایش/هشدار می‌رسد؛ توزیع تقریبی روزهای گپ دیگر هرگز تصمیم را خراب نمی‌کند، فقط چارت را.

**چرا ستون انباشته جایگزین لنگر نمی‌شود؟** بعد از epoch غیرمنتظره لنگر به `current` re-base می‌شود ولی `TrafficUsed` همان `current` را روی تاریخچه سوار می‌کند — این دو به‌طور مشروع واگرا می‌شوند؛ ادغامشان سهمیه را بعد از reset دستی دوباره می‌بخشد. هر چهار ستون کار مجزا دارند؛ «ساده‌سازی» به کمتر ممنوع.

## ۳. قواعد بهره‌برداری

- **Sole-writer**: کلکتور ردیف‌های TrafficData و ستون‌های Renewal را در یک tx محلی می‌نویسد؛ rollback هر دو را دست‌نخورده می‌گذارد (تست شده).
- **قفل realm**: `TryLockTrafficSync` با آستانه‌ی اختصاصی زیر کرانس (`XuiTrafficOptions.LockThresholdSeconds = 240` < `IntervalSeconds = 300`) — آستانه‌ی ۳۰۰ثانیه‌ای Radius دقیقاً برابر کرانس ۵دقیقه‌ای است و روی مرز مسابقه می‌کند. قفل مشترک با `UpdateTrafficData` روی `LastTrafficSync` است؛ گرفتن متناوبش بی‌خطر است (برای realmهای x-ui کار خاصی برای sync ساعتی نیست).
- **یک سرور x-ui در هر realm**: `LoadAllActiveRadius` دداپ per-realm ندارد؛ کلکتور خودش GroupBy/First می‌کند. realm مخلوط x-ui+Radius به‌عنوان دیتاپلین تصمیم پشتیبانی نمی‌شود (بند بازها).
- **کلاینت ناشناس** در پنل x-ui → skip + لاگ (سیگنال آشتی‌دهی)؛ حذف/غیرفعال‌سازی فقط کلاینت‌های «شبه‌نام‌کاربری پنل» را لمس می‌کند (`AccountBusiness.UsernamePattern`) تا کلاینت‌های دستی دیگران روی پنل اختصاصی دست نخورند.
- **`ChangeVpnPassword` برای x-ui فعلاً `NotImplementedException`** می‌اندازد: چرخش UUID (D4) تا پیش از تغییر قرارداد «بازگرداندن credential مؤثر» (تا `VpnApplication` مقدار بازگشتی را در `VpnPassword` ذخیره کند) نباید روشن شود؛ وگرنه mapping می‌شکند.

## ۴. ماتریس probe روی vm01 (تأیید نهایی قرارداد پیش از ثبت سرور)

قرارداد زیر از سورس رسمی `alireza0/x-ui` tag **v1.12.0** کد-تأیید شده و در `XuiApiService` پیاده شده است (جلسه‌ی ۲۰۲۶-۱۰-۱۵، دومی). probe زنده‌ی vm01 فقط تطبیق نسخه‌ی نصب‌شده را قفل می‌کند: fetch `xui/API/openapi.json` + تست کلاینت تستی، پیش از ثبت سرور.

| عملیات | سؤال دقیق | پاسخ کد-تأییدشده از سورس v1.12.0 |
| --- | --- | --- |
| login/session | endpoint، کوکی نشست، TTL، 401 | `POST {webBasePath}login` با JSON `{username,password}`؛ پاسخ **همیشه HTTP 200** با `{success,msg,obj}` — موفقیت فقط از `success`؛ کوکی نشست نام `x-ui` با `Path=basePath`؛ انقضای سشن فقط با هدر `X-Requested-With: XMLHttpRequest` → **401 JSON**، وگرنه 307 redirect به HTML لاگین؛ کلاینت: 401 → پاک‌کردن کوکی → یک re-login + retry |
| listInbounds + آمار | فیلدهای per-client up/down/total؛ ساختار settings/clientStats | **`GET xui/API/inbounds/`** (نه POST list)؛ `id` عددی int؛ کلاینت‌ها داخل فیلد `settings` که خودش **JSON string** است (parse دوطبقه: `settings→clients[]`)؛ آمار در `clientStats[]` با `email, up, down, total, expiryTime, enable, reset, inboundId` |
| addClient | بدنه، inbound مقصد | `POST xui/API/inbounds/addClient` بدنه `{id, settings}`؛ `id` **فقط صریح از کانفیگ** (`WebApiConfig.InboundId`) — «اولین inbound» روی سرور چند-inbound (کاربران 443 + carrier 8443) اشتباه است |
| updateClient + **رفتار شمارنده هنگام تغییر UUID** | آیا `updateClient` شمارنده‌ها را نگه می‌دارد؟ | `POST xui/API/inbounds/updateClient/{uuid-فعلی}`؛ شمارنده‌ها **keyed-by-email** هستند ⇒ تغییر UUID شمارنده‌ها را حفظ می‌کند — پایه‌ی drift-safe بودن کلید UUID و شاخه‌ی چرخش D13 (رویداد غیرترافیکی) |
| delClient | مسیر، رفتار آمار | `POST xui/API/inbounds/{id}/delClient/{uuid}` — کلید **UUID** است |
| resetClientTraffic per-client | همان دکمه‌ی reset؟ | `POST xui/API/inbounds/{id}/resetClientTraffic/{email}` — تنها عملیات کلیدخورده با **email** |
| counter-SET | آیا API برای *ستکردن* شمارنده هست؟ | نیست — reset-at-renewal (D12/D13) همان reset per-client است |
| وضعیت آنلاین | قابل دریافت است؟ | بله: `POST xui/API/inbounds/onlines` → `obj: [{email, ips: {ip: lastSeenMs}}]`؛ متصل شد به `GetActiveConnections` (نگاشت IP→`UserConnectionBinding`) |
| `total` | بایت؟ 0=نامحدود؟ | فیلد `totalGB` = **بایت** (با وجود نام)؛ **0 = نامحدود**؛ «نامحدود» باید صریح 0 سریالایز شود |
| `expiry` | timestamp یا روز؟ 0=هرگز؟ | فیلد `expiryTime` = **ms-epoch**؛ **0 = هرگز**؛ **منفی = فعال‌شدن از اولین مصرف** |
| `limitIp` | نگاشت `SimultaneousUser` | مستقیم |
| rate-limit | وجود دارد؟ | **نه** (شکاف قابلیت نسبت به Radius) |
| flush آمار | فاصله‌ی flush شمارنده‌ها | شمارنده‌های panel DB هر ~۱۰s flush می‌شوند؛ کرانس ۵دقیقه‌ای کلکتور امن است (بدون تغییر) |
| restart-on-update | آیا add/update کلاینت Xray را ری‌استارت می‌کند؟ | باقی‌مانده برای probe زنده (مربوط به throttle در D4) |

نتایج probe زنده → فایل method در Simorgh `docs/methods/` (طبق قواعد آن repo، فقط بعد از اجرا).

## ۵. ثبت مسائل باز

1. **ویرایش دستی** (D10): آلارم epoch مسیر تشخیص است؛ مسیر درمان خودکار تعریف نشده.
2. **بیش‌شماری restore**: قانون epoch روی rollback-restore بیش‌می‌شمارد + اگر enforcement به شمارنده‌ی برگشته نگاه کند ریسک **قطع زودهنگام** (تضاد با بیش‌شماری محافظه‌کارانه در ستون‌ها — دو جهت متفاوت از همان رویداد).
3. **ناسازگاری enforcement انباشته/per-renewal**: مسائل pre-existing (stack شدن renewalها در برابر reset هر بار) — حالا روی x-ui هم با Encoding A موجود است.
4. **realmهای mixed-datum** پشتیبانی نمی‌شوند: ردیف سرور x-ui = `Xui|V2ray` در features؛ realm با دو دیتاپلین تصمیم روشنی ندارد.
5. **T3** (پیشنهاد باقی‌مانده): kind در payload های plan-info + گارد `UserException` در adapterها؛ `SendCertEmail` باید در `VpnApplication` *قبل از* `GetDefaultCertificate` گیت شود.
6. **T4** (پیشنهاد باقی‌مانده): فرمول پهنای‌باند اگر `BandWidth` روی ردیف سرور x-ui ست شود کار می‌کند؛ `MaxClients` به‌عنوان گیت دوم در `GetAvailableRealm`.

## ۶. چه چیزی این جلسه پیاده شد

| قطعه | فایل(ها) |
| --- | --- |
| اسکیما: ۴ ستون Renewal + widening `SessionId` به `VARCHAR(64)` | `Database/LocalDatabase/RenewalTrafficState.sql` (idempotent؛ دستی deploy روی `FastBypass_DbScenario`) |
| پچ ویو: `LastConnectTime` فقط از ردیف‌های ناصفر | `Database/LocalDatabase/TotalPlanState.sql:77` (تغییر معنایی Radius: سشن باز بی‌بایت دیگر last-connect را جلو نمی‌برد) |
| بیت `ServerFeature.Xui = 0x4`، کلید `RadiusType.Xui` | `ServerFeature.cs`، `RadiusType.cs` |
| نگاشت مرکزی Features→کلید (D2، جایگزین ~۹ switch) | `Infrastructure/Radius/ServerFeatureExtensions.cs`؛ بازنویسی `AccountRadiusSyncService`/`SessionRadiusSyncService` با `GetRequiredKeyedService` |
| پروژه‌ی `XuiPanel` (T5) | `WebService/{IXuiApi, XuiApiService, XuiClientModel}` (قرارداد mock)، `Application/{AccountRadiusSyncXuiService, SessionRadiusSyncXuiService, XuiEncoding}`، `ServiceFactory.AddXuiPanelServices` |
| SyncUserAndActive طبق D13 + بازمتن کردن لنگر | `AccountRadiusSyncXuiService.SyncUserAndActive` (reset → total/expiry/limitIp → `LastSeenCounter/LastPollTime = NULL` داخل همان tx تمدید) |
| کلکتور (فقط گزارش/حسابداری — بدون watchdog) | `XuiTrafficCollector` (Quartz، `[DisallowConcurrentExecution]`، کرانس قابل‌تنظیم `XuiTrafficOptions`، پیش‌فرض ۵ دقیقه)، `XuiTrafficSync`، `XuiTrafficAccounting` (D6/D7/D8/D14 خالص و تست‌پذیر)، `XuiTrafficRow` |
| batch repoها | `IRenewalRepository.GetLatestByAccountIds`، `ITrafficDataRepository.{Fetch(nas,sessionIds), GetLastNasId}`، `IRealmRepository.TryLockTrafficSync(ids, threshold)` |
| خوانش تصمیم per-dataplane (D14) | `IPlanUsageReader`/`PlanUsageReader` + بازسیم‌کشی `VpnApplication.SendCertEmail`، `AccountMonitoringService.InactiveAbandonedUsers`، `PlanApplication.Renewal` |
| تست‌ها | `Tests/Facts/Xui/{XuiTrafficAccountingTest, XuiAccountSyncTest, XuiTrafficSyncTest, PlanUsageReaderTest}` + mockهای `XuiApiMoq`، `RenewalRepositoryMoq.GetLatestByAccountIds`، `TrafficDataRepositoryMoq` (با undo تراکنشی)، `RealmRepositoryMoq` |
| **جلسه‌ی دوم ۲۰۲۶-۱۰-۱۵: بازنویسی قرارداد API طبق سورس v1.12.0** | `WebApiConfig.{BasePath,InboundId}`؛ `XuiClientModel` (کدگذاری `totalGB`/`expiryTime` با 0)، `XuiOnlineClientModel`؛ `IXuiApi` (update/del با UUID فعلی، `ListOnlineClients`)؛ `XuiApiService` (مسیرهای `xui/API/...`، پاکت `msg/obj`، parse دوطبقه، هدر AJAX، 401→re-login یک‌باره)؛ syncها uuid-keyed + `GetActiveConnections` از onlines؛ `XuiEncoding.ToExpiryTime→long`؛ تست‌های `XuiApiTransportTest` + `XuiSessionSyncTest` |

## ۷. ترتیب باقی‌مانده

1. **تأیید زنده‌ی vm01** (§۴) — قرارداد از سورس v1.12.0 کد-تأیید و در `XuiApiService` پیاده شد؛ probe (fetch `xui/API/openapi.json` + تست کلاینت تستی) فقط تطبیق نسخه‌ی نصب‌شده را قفل می‌کند و پیش‌نیاز ثبت سرور vm01 است.
2. D1 رنیم `FreeRadius`→`RadiusDesk` (مکانیکی، هر زمان).
3. T3/T4 + شاخه‌های فرانت (نمایش kind دیتاپلین؛ مخفی‌کردن دکمه‌ی پسورد برای x-ui تا D4 کامل شود).
4. D4 کامل: تغییر قرارداد بازگرداندن credential مؤثر + چرخش UUID + throttle.

## ۸. ارجاع‌ها

- تصمیمات جلسه‌ی قبل: [docs/16-multi-server-panel.md](16-multi-server-panel.md) (D1–D4، بازهای T1–T5)
- طرح کلی x-ui: [docs/13-xray-integration.md](13-xray-integration.md)
- الگوی dispatch: [docs/06-radius-integration.md](06-radius-integration.md)
- اسکیمای محلی/ویو: [docs/09-database.md](09-database.md)
- کدهای کلیدی: `RenewalTrafficState.sql` · `TotalPlanState.sql` · `XuiPanel/` · `ServerFeatureExtensions.cs` · `PlanUsageReader.cs` · `XuiTrafficAccounting.cs`

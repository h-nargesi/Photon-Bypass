# Docs/23-xui-local-test-vm.md — انزوای تست و VM لوکال x-ui: تصمیمات جلسه ۱۴۰۵/۰۸/۲۹

> **English summary**: Test-isolation decision record (owner directive, 2026-11-20). **D1** no test — automated or manual — may ever touch live, customer-serving VPN servers; **D2** this repo keeps no references (repo paths, hostnames, scripts) to the upstream production-infrastructure project; historical decision records (Docs/13/16/17/18) keep their content with those references neutralized; **D3** the only sanctioned integration target for the x-ui data plane is a **local VirtualBox VM running a test x-ui panel** (alireza0 v1.14.0) on the host-only network, reset from a `clean-test-state` snapshot per run; **D4** the test infrastructure mirrors the existing Mikrotik pattern — a `XuiPanelInitializer` (poweroff → restore snapshot → start headless → HTTP-login readiness check) plus a test tier that binds the real `XuiApiService` to the VM in place of `XuiApiMoq`; **D5** the one-time live probe of 2026-10-16 (14/14 PASS) stays valid as historical contract evidence — all repeated verification happens on the local VM. §4 is the ready work order for the implementation session (VM runbook, initializer, integration test tier).

## ۱. زمینه (دستور مالک، ۱۴۰۵/۰۸/۲۹ = 2026-11-20)

این ریپو **پنل فروش در حال توسعه** است؛ زیرساخت اجرایی (سرورهای VPN که به مشتریان سرویس می‌دهند) یک برنامه‌ی زنده‌ی جدا است. سه قاعده از مالک:

1. هیچ تستی نباید روی سرورهای زنده و در حال سرویس‌دهی به مشتری اجرا شود.
2. از این پروژه نباید هیچ رفرنسی به پروژه‌ی زیرساخت اجرایی وجود داشته باشد — نه مسیر ریپو، نه hostname، نه اسکریپت.
3. تنها مسیر تست اینتگریشن دیتاپلین x-ui: ساختن یک VM، اجرای پنل تستی x-ui روی آن، و اجرای تست‌های اینتگریشن روی همان پنل تستی.

وضعیت پیش از این جلسه: تمام تست‌های x-ui در `dotnet test` آفلاین بودند (`XuiApiMoq` و `ScriptedHandler`) و probe زنده‌ی ۱۴۰۵/۰۷/۲۴ یک ابزار دستی در ریپوی زیرساخت بود که هرگز جزو سوئیت تست این ریپو نبود. این جلسه ارجاع‌های باقی‌مانده را خنثی کرد (D2) و این سند مسیر تست اینتگریشن آینده را قفل می‌کند (D3/D4).

## ۲. تصمیم‌ها

### D1 — انزوای تست: ممنوعیت قطعی سرور زنده ✅

هیچ تست خودکار یا دستی از این ریپو به سرورهای زنده/در حال سرویس‌دهی وصل نمی‌شود — نه در سوئیت تست، نه به‌صورت probe دستی. probe زنده‌ی ۱۴۰۵/۰۷/۲۴ یک اجرای یک‌باره‌ی تاریخی بود و از این ریپو تکرار نمی‌شود.

### D2 — نبود ارجاع به زیرساخت اجرایی ✅ (اجراشده در همین جلسه)

هیچ مسیر/hostname/اسکریپتی از ریپوی زیرساخت اجرایی در این ریپو نگه داشته نمی‌شود. اسناد تصمیم تاریخی (Docs/13/16/17/18) محتوای تصمیم خود را حفظ می‌کنند ولی ارجاع‌هایشان خنثی شدند («زیرساخت بالادستی» / «نام‌ها حذف شد — Docs/23»). دامنه‌ی fixtureهای تست هم به `xui.test.local` تغییر کرد (`VlessLinkServiceTest`، `EmailServiceTest`) و کامنت `IXuiApi` خنثی شد.

### D3 — تنها مقصد اینتگریشن: VM لوکال VirtualBox ✅

- یک VM دبیانی در VirtualBox با آداپتر **host-only** روی همان شبکه‌ی Mikrotikهای تست (`192.168.56.x`)؛ IP پیشنهادی پنل تستی x-ui: `192.168.56.13` (Mikrotik-Base/Scenario روی `.11`/`.12` هستند).
- نصب x-ui (alireza0) **v1.14.0** + ساخت یک inbound تستی + گرفتن اسنپ‌شات **`clean-test-state`** — قرینه‌ی الگوی `Mikrotik-Base`/`Mikrotik-Scenario`.
- هر اجرای تست با **restore اسنپ‌شات** شروع می‌شود → وضعیت پایه‌ی قطعی، بدون نشست بین اجراها.

### D4 — زیرساخت تست: الگوی `MikrotikInitializer` ✅ (طرح؛ اجرا در جلسه‌ی بعد)

- `XuiPanelInitializer : IOutSourceInitializer, IOutSourceLevelService` قرینه‌ی `Backend/Tests/Initializer/MikrotikInitializer.cs`: `controlvm poweroff` → `snapshot restore clean-test-state` → `startvm --type headless`. تنها تفاوت عملی: **چک آماده‌بودن HTTP** است (POST login به پنل + انتظار envelope موفق `{success:true}`)، نه اتصال SSH.
- سطح تست جدید که به‌جای `XuiApiMoq`، `IXuiApi` واقعی (`XuiApiService`) را به VM بایند می‌کند؛ اتصال از بند کانفیگ جدید در `Backend/Tests/appsettings.json` (IP/اعتبار تستی/`InboundId`).
- تست‌ها فقط کلاینت با email یکتای تستی می‌سازند؛ پاک‌سازی نهایی = restore اسنپ‌شات در ابتدای اجرای بعدی (همان `OutSourceLauncher`).
- تست‌های mock فعلی (`XuiApiMoq`، `ScriptedHandler`، `XuiApiTransportTest`) سر جایشان می‌مانند — لایه‌ی قرارداد سریع؛ لایه‌ی اینتگریشن روی VM اضافه می‌شود.

### D5 — جایگاه probe تاریخی ✅

اجرای یک‌باره‌ی ۱۴۰۵/۰۷/۲۴ (هر دو سرور ۱۴/۱۴ PASS، بدون ری‌استارت Xray) به‌عنوان سوابق تأیید قرارداد v1.14.0 معتبر می‌ماند؛ رکورد کاملش در سمت زیرساخت نگه داشته می‌شود (ارجاع حذف شد — D2). تأییدهای تکرارشونده‌ی قرارداد/رفتار از این پس فقط روی VM لوکال اجرا می‌شوند.

## ۳. ساخت یک‌باره‌ی VM (ران‌بوک در جلسه‌ی اجرا نوشته/تست می‌شود)

1. ساخت VM دبیانی (۲ vCPU / ۱GB RAM کافی است) با آداپتر host-only؛ IP ثابت `192.168.56.13`.
2. نصب x-ui (alireza0) v1.14.0 با نصب‌کننده‌ی رسمی؛ پورت/`WebBasePath`/اعتبار دلخواه تستی (در کانفیگ تست همین ریپو ثبت می‌شود).
3. ساخت inbound تستی (مثلاً VLESS+ws بدون TLS — برای تست API پنل، TLS واقعی لازم نیست) و ثبت `InboundId` آن در کانفیگ تست.
4. `VBoxManage snapshot "Xui-Test" take clean-test-state` — نامک پیشنهادی VM: **`Xui-Test`** (قرینه‌ی `Mikrotik-Base`).

## ۴. کارهای باقی‌مانده (دستور کار آماده‌ی جلسه‌ی اجرا)

1. ~~ران‌بوک/اسکریپت ساخت یک‌باره‌ی VM (§۳)~~ — ✅ اجرا شد (جلسه ۱۴۰۵/۰۸/۲۹): دو اسکریپت در `Scripts/` به سبک `Scripts/deploy.sh` (زیرفرمان‌های `setup|reset|start|stop|status|clean`، پارامترها با env + پیش‌فرض‌های جدول §۳، احترام به `https_proxy`، آرتیفکت‌ها در `~/.cache/photon-bypass/<script>/`):
   - `Scripts/xui-test-vm.sh` — ساخت `Xui-Test`: ایمیج Debian 12 (پین‌شده +verify SHA512) → VDI، seed ISO ابری NoCloud (کلید SSH تولیدی، IP ثابت روی NIC دوم)، نصب x-ui alireza0 v1.14.0 پین‌شده (+verify SHA256)، پیکربندی غیرتعاملی پنل (CLI با fallback روی `x-ui.db`)، ساخت inbound تستی (VLESS+ws) و read-back `InboundId`، سپس اسنپ‌شات `clean-test-state`.
   - `Scripts/mikrotik-test-vms.sh` — ساخت `Mikrotik-Base`/`Mikrotik-Scenario`: ایمیج رسمی CHR RouterOS (پین‌شده، لایسنس free دائمی — فقط کنترل‌پلین تست می‌شود) → VDI، بوت‌استرپ سریال **کلید-دار** (درایور python3 که هر پرامپت مشاهده‌شده را جواب می‌دهد: login → license → دیالوگ اجباری تغییر رمز اولین لاگین → دو دستور شبکه)، نصب بسته‌ی `user-manager` (CHR بدون آن تحویل می‌شود؛ نصب با ریبوت)، `/import` فایل export کاربر (`show-sensitive`؛ تنها مرجع کانفیگ — fixtureهای تستی commit شده در `Scripts/mikrotik-base.rsc`/`Scripts/mikrotik-scenario.rsc` به تصمیم مالک: مقادیر کاملاً تستی، قرارداد اعتبارهای تستی همین ریپو؛ خروجی import هرگز در log چاپ نمی‌شود)، verify (SSH/api 8728/`user-manager` خالی — کاربران باقی‌مانده‌ی export قبل از snapshot پاک می‌شوند) و اسنپ‌شات. **Scenario دیگر clone از Base نیست**: کلون MAC جدید می‌گیرد، RouterOS کارت را `ether2` می‌نامد و همه‌ی bindingهای importشده INVALID می‌شود — هر دو VM از دیسک pristine جداگانه ساخته می‌شوند.
   - **اعتبارسنجی زنده (جلسه ۱۴۰۵/۰۹/۱ = 2026-11-21)**: میکروتیک سرتاسر سبز شد (setup/status/reset/stop/clean بدون دخالت دستی، با نام‌های موضعی `Mikrotik-Test-Base/Scenario` روی `.21/.22` تا با VMهای مرجع موجود تداخل نکند — با env override، پیش‌فرض‌ها دست‌نخورده). xui: بوت + نصب و فعال‌شدن سرویس پنل روی همان هاست ویندوزی سبز شد و سپس **چرخه‌ی کامل xui روی هاست لینوکسی VM تکمیل و سبز شد**: تأیید login پنل (`{success:true}` + کوکی نشست — همان قرارداد `XuiApiService`)، ساخت inbound تستی + read-back `InboundId`، اسنپ‌شات `clean-test-state` و اعتبارسنجی `reset`/`status`. در همین اجرا یک اشکال واقعی قرارداد برطرف شد: `sniffing` هم مثل `settings`/`streamSettings` باید **رشته‌ی JSON** باشد نه object تو در تو — پنل object را با خطای `json: cannot unmarshal object into Go struct field Inbound.sniffing of type string` رد می‌کند (قرینه‌ی `XuiInboundModel.StreamSettings` در همین ریپو). `InboundId` واقعی (=1) در `TestVmOptions` ثبت شد (بند ۳) و سطر `Docs/18` §4 افزوده شد.
   - اصلاحات هاست ویندوزی (WSL + VBoxManage.exe interop): کش روی مسیر قابل‌دیدن برای ویندوز (مثلاً `XDG_CACHE_HOME=/mnt/d/...`) + ترجمه مسیرها با `wslpath`؛ نام چندکلمه‌ای آداپتر host-only و CRLF خروجی VBoxManage؛ سریال از طریق `uartmode tcpserver` + socat به گیت‌وی WSL (named pipe از WSL قابل‌دسترس نیست)؛ کلید SSH خارج از drvfs (در XDG data dir — درایوهای مونت‌شده همه 0777 هستند و ssh کلید را رد می‌کند). روی این هاست (Hyper-V فعال، اجرای NEM): ایمیج Debian باید «generic» باشد نه genericcloud (کرنل cloud زیر NEM پنیک می‌کند)، دیسک نباید resize شود (resize خودِ VBox نگاشت بلاک‌ها را خراب می‌کند)، و VM تک‌CPU ساخته می‌شود.
2. `XuiPanelInitializer` + سیم‌کشی در زیرساخت `OutSourceManager`/سطح سرویس تست.
3. بند کانفیگ تست برای VM در `Backend/Tests/appsettings.json` — بخش `TestVmOptions` (کلید = نام VM؛ Ip/Username/Password/ApiPort یا Port/InboundId) با accessor استاتیکِ `TestVmOptions.Get(key)` از همین جلسه اضافه شده و `MikrotikInitializer.GetIp`/`Check` و fixture قرارداد میکروتیک از آن می‌خوانند (بازنویسی env با `TestVmOptions__<VM>__Ip=…`؛ VMهای فعلی بدون هیچ تغییری کار می‌کنند). مقدار `Xui-Test` با اجرای سبز setup ثبت شد: `InboundId=1` — پنل تازه همیشه اولین inbound را id=1 می‌دهد، پس هر هاستی که اسکریپت را اجرا کند همین مقدار را می‌گیرد؛ مصرف‌کننده‌اش بند ۴ همین فهرست است.
4. لایه‌ی تست اینتگریشن (`IXuiApi` واقعی → VM): قرارداد transport زنده (login/envelope/کوکی/هدر AJAX/401→re-login)، چرخه‌ی کامل کلاینت (add → update با چرخش UUID → reset traffic → del + حذف ردیف stats)، `onlines`، و حسابداری ترافیک با شمارنده‌ی واقعی (دلتا/epoch روی داده‌ی زنده‌ی پنل تستی).
5. به‌روزرسانی سطر مربوط در `Docs/18` §4 در همان PR (قاعده‌ی حاکمیت Docs/18 §7).
6. ~~خنثی‌سازی ارجاع‌های مستندات + fixtureها + کامنت `IXuiApi`~~ — ✅ اجرا شد در همین جلسه (D2).

## ۵. ارجاع‌ها

- تصمیم‌های adapter و هویت x-ui: [Docs/16-multi-server-panel.md](16-multi-server-panel.md) (D2–D4)
- قرارداد API x-ui و ماتریس probe تاریخی: [Docs/17-xui-traffic.md](17-xui-traffic.md) §4
- قاعده‌ی حاکمیت انزوای تست: [Docs/18-test-coverage.md](18-test-coverage.md) §7
- طرح ادغام دیتاپلین x-ui: [Docs/13-xray-integration.md](13-xray-integration.md)
- الگوی زیرساخت VM تست: `Backend/Tests/Initializer/MikrotikInitializer.cs` + `Initializer/OutSourceManager/`

# Docs/25-server-channel.md — کانال ارتباطی پنل با سرورها: تانل SSH با کلید برای x-ui و User Manager

> **English summary**: Server-channel decision record and implementation spec (owner directive, 2026-11-26). **Status: implemented in code (2026-11-26) — phases 0, 1, 2, 3, 4, 4b and 4g of §8. The VM-tier tests `XuiPanelTunnelTest` and `MikrotikTunnelTest`, with the local fixture of §6, passed on 2026-11-27. Still open: the first-publish host verification. Where the code differs from the first draft of this record, §2.1 lists it and the affected sections were updated in place.** The panel reaches every server over **one SSH session authenticated with an ed25519 private key and a pinned host key**; everything else rides that session: x-ui's HTTP API and MikroTik's API (User Manager) through a **local port-forward to the server's own loopback**, RouterOS commands through SSH exec as today. **D1** one channel (SSH + key), no TLS certificate and no public listener on the management path; **D2** x-ui is only ever addressed through its HTTP API, never its database; **D3** per-server `Transport` (`Direct` | `SshTunnel`) plus `PrivateKey`/`PassPhrase`/`HostKeySha256` on `SshConfig`; **D4** the host key is pinned and a mismatch fails closed; **D5** a singleton SSH session pool (lazy connect, keep-alive, idle eviction, reconnect, generation counter); **D6** one loopback forward per session and remote port; **D7** `XuiApiService` keeps its calls — only the transport changes; **D8** `Tik4NetHandler` opens tik4net on the forwarded port; **D9** an admin "test connection" endpoint that reports each step and discovers a host key; **D10** SSH.NET is upgraded to 2026.0.0 with the work; **D11** host-capability checks (outbound TCP to the SSH ports, loopback bind, worker lifetime) run at every start and log their result, so the first publish shows them. §4 is the implementation contract (config JSON, classes, validation, errors, logging, tests; §4.11 the host checks; §4.12 the server seed rows), §5 what must be ready on each server, §6 the mandatory test scenarios on the local test servers, §7 what is already proven and what is not, §8 the rollout order with acceptance criteria, §9 the open items. No live server is ever touched by a test (Docs/23 D1). Appendix A is the proof program that ran against the infrastructure owner's production hosts from a development terminal, Appendix B its report.

## ۱. مسئله

امروز پنل این‌طور به سرورها وصل می‌شود:

- `XuiApiService` مستقیم و بدون تانل به `WebApiConfig.HostName:Port` درخواست HTTP می‌فرستد (`Backend/XuiPanel/WebService/XuiApiService.cs:264`).
- `Tik4NetHandler` مستقیم به API میکروتیک وصل می‌شود (`Api` یا `ApiSsl`) — `Backend/ServerBridge/Tik4net/Tik4NetHandler.cs:16`.
- `SshHandler` فقط پسورد می‌پذیرد، برای هر فراخوانی یک `SshClient` تازه می‌سازد، host key را بررسی نمی‌کند و تانل ندارد — `Backend/ServerBridge/Ssh/SshHandler.cs:16`.

سرورهایی که API مدیریتی‌شان فقط روی loopback گوش می‌دهد (تنها سطح عمومی‌شان sshd است) با این کد اصلاً در دسترس نیستند (Docs/24 §5.1). راه‌حل‌های ممکن (TLS مستقیم با allowlist، پروکسی mTLS، agent، overlay VPN، Xray به‌عنوان حمل‌کننده) سنجیده شدند و کنار گذاشته شدند: یا به گواهی و ری‌استارت سرویس زنده نیاز داشتند، یا به دسترسی مدیر روی هاست پنل (هاست اشتراکی ویندوز: نه RDP، نه نصب سرویس)، یا سطح حمله را بزرگ‌تر می‌کردند. تانل SSH داخل خود پروسه‌ی پنل کمترین تغییر را روی سرورهای زنده دارد (یک خط `authorized_keys` یا یک کاربر با کلید)، هیچ listener عمومی‌ای باز نمی‌کند و روی VMهای لوکال قابل تست است.

## ۲. تصمیم‌ها

### D1 — یک کانال: SSH با کلید؛ بقیه از داخل آن

هر سرور یک جلسه‌ی SSH دارد که با کلید خصوصی ed25519 احراز هویت می‌شود و host key آن پین شده است. درخواست‌های HTTP به x-ui و درخواست‌های API به میکروتیک از طریق **local port-forward** همان جلسه به `127.0.0.1:<پورت سرویس>` روی خود سرور می‌روند. فرمان‌های RouterOS مثل امروز با SSH exec اجرا می‌شوند، فقط با کلید به‌جای پسورد. روی مسیر مدیریتی نه گواهی TLS لازم است و نه listener عمومی؛ داخل تانل، ترافیک با خود SSH رمز شده است. حالت `Direct` (رفتار امروز) برای سرورهایی که سرویس مدیریتی‌شان از بیرون در دسترس است دست‌نخورده می‌ماند.

### D2 — x-ui فقط از طریق API؛ هرگز دیتابیس

پنل با x-ui فقط با API HTTP آن کار می‌کند: `login` و `xui/API/inbounds/*` (`list`، `addClient`، `updateClient/{uuid}`، `{id}/delClient/{uuid}`، `{id}/resetClientTraffic/{email}`، `onlines`). نوشتن مستقیم روی `x-ui.db` ممنوع است: خودِ x-ui پیکربندی را در حافظه نگه می‌دارد و تنها نویسنده‌ی دیتابیس خودش است (تغییر بیرونی را نمی‌بیند یا بازنویسی می‌کند)، اسکیمای دیتابیس داخلی و نسخه‌به‌نسخه است، نوشتن موازی SQLite خطر خرابی دارد، و پنل هم طبق Docs/13 و Docs/17 D10 تنها از راه API و به‌عنوان تنها نویسنده‌ی کلاینت‌ها با x-ui کار می‌کند. در کد فعلی هم هیچ ارجاعی به `x-ui.db` یا SQLite در بک‌اند نیست و همین درست است. (تنها استثناء ابزار ساخت VM تست است که هنگام provisioning از sqlite استفاده می‌کند — زیرساخت تست، نه پنل.)

### D3 — پیکربندی transport روی هر ردیف سرور

`ServerConfiguration` یک فیلد `Transport` می‌گیرد (`Direct` = پیش‌فرض، `SshTunnel`) و `SshConfig` سه فیلد: `PrivateKey`، `PassPhrase`، `HostKeySha256`. کلید خصوصی داخل همان `Config` محافظت‌شده‌ی ردیف ذخیره می‌شود (`ServerConfigProtection`، همان مسیر write-without-read امروز) و هرگز از API خوانده نمی‌شود. جزئیات در §۴.۱.

### D4 — پین کردن host key؛ عدم تطابق = شکست قطعی

در حالت `SshTunnel`، `HostKeySha256` اجباری است و با اثر انگشت کلیدی که سرور ارائه می‌دهد مقایسه می‌شود (فرمت OpenSSH: `SHA256:<base64 بدون padding>`). نبودِ پین یا عدم تطابق، اتصال را **قبل از احراز هویت** قطع می‌کند. پین را مالک زیرساخت از رکورد سرور می‌دهد، یا ادمین آن را یک‌بار با گام discover تست اتصال (D9) می‌گیرد. چرا مهم است: کلید خصوصی پنل فقط به سرورِ درست ارائه می‌شود و وسط‌بودن (MITM) نمی‌تواند آن را به سرور جعلی بخوراند.

### D5 — استخر جلسه‌های SSH (singleton)

یک `ISshTunnelPool` singleton (کنار `ISshHandler`/`ITik4NetHandler` در `Backend/ServerBridge/ServiceFactory.cs`) به‌ازای هر سرور حداکثر یک جلسه نگه می‌دارد: اتصال تنبل، `KeepAliveInterval` از `ServerBridgeOptions`، حذف پس از `SshTunnelIdleSeconds` بی‌فعالیتی، اتصال مجدد پس از خطا، و شمارنده‌ی `Generation` که با هر (re)connect یکی بالا می‌رود. پنل روی هاست اشتراکی IIS اجرا می‌شود: recycle شدن app pool جلسه‌ها را می‌اندازد و استخر در استفاده‌ی بعدی دوباره وصل می‌شود — هیچ سرویس یا تانل دائمی بیرون از پروسه لازم نیست.

### D6 — یک forward روی loopback به‌ازای هر جلسه و هر پورت مقصد

هر جلسه برای هر `remotePort` یک `ForwardedPortLocal("127.0.0.1", 0, "127.0.0.1", remotePort)` می‌سازد (پورت محلی تصادفی، فقط bind روی loopback) و آن را تا پایان جلسه نگه می‌دارد؛ همه‌ی اتصال‌های همان سرور به آن پورت از همان forward می‌گذرند. مقصد همیشه literal `127.0.0.1` است — سرورهای OpenSSH در گزینه‌ی `permitopen` همان رشته‌ی ارسالی را مقایسه می‌کنند و `localhost` را جایگزین نمی‌دانند.

### D7 — x-ui: فقط transport عوض می‌شود

`XuiApiService` همان فراخوانی‌ها، همان کوکی نشست و همان قرارداد پاسخ را نگه می‌دارد. تنها `HttpClient` در حالت تانل از یک provider مخصوص می‌آید که اتصال‌ها را از forward عبور می‌دهد (`SocketsHttpHandler.ConnectCallback`) و proxy را خاموش می‌کند؛ کلاینتِ هر سرور پایدار است و با reconnect یا تغییر `Generation` بازسازی نمی‌شود — فقط اتصال‌هایش از استخر، جلسه‌ی فعلی را می‌پرسند (§۲.۱).

### D8 — میکروتیک: exec با کلید، API روی پورت forward شده

`SshHandler` در حالت تانل با کلید لاگین می‌کند و فرمان‌ها را روی جلسه‌ی مشترک اجرا می‌کند. `Tik4NetHandler` بلافاصله پس از آن، tik4net را روی `127.0.0.1:<پورت forward>` باز می‌کند (`Api` یا `ApiSsl`، طبق `WebApiConfig.Ssl`) با نام کاربری و پسورد API. خود API میکروتیک احراز هویت با کلید ندارد؛ کلید، جلسه‌ی SSH را باز می‌کند و API از داخل آن می‌رود.

### D9 — تست اتصال (فقط ادمین)

`POST /api/server/test-connection` با بدنه‌ی `SaveServerModel` (پیکربندی ذخیره‌نشده هم قابل تست است) مراحل را جداگانه گزارش می‌کند: `tcp`، `host-key`، `auth`، `forward`، `service` — بدون هیچ مقدار محرمانه در پاسخ یا لاگ. اگر `HostKeySha256` خالی باشد، گام `host-key` اثر انگشت ارائه‌شده را برمی‌گرداند و **قبل از احراز هویت** می‌ایستد (discover). دکمه‌ی «تست اتصال» در صفحه‌ی `/servers` (ردیف و مودال ویرایش) همین endpoint را صدا می‌زند.

### D10 — ارتقای SSH.NET به 2026.0.0

NuGet برای SSH.NET 2025.0.0 دو advisory با شدت بالا نشان می‌دهد — CVE-2026-85756 (اجرای دستور از مسیر remote path در `ScpClient`) و CVE-2026-48798 (نوشتن فایل دلخواه در دانلود بازگشتی — recursive — `ScpClient`) — هر دو در 2026.0.0 رفع شده‌اند. پنل `ScpClient` ندارد و این کانال فقط `SshClient` و `ForwardedPortLocal` را به کار می‌برد؛ با این حال ارتقا در همین کار انجام شود تا هشدار `NU1903` از build برود.

### D11 — بررسی‌های میزبان در اولین انتشار، با لاگ (G1–G3)

سه چیز را فقط از روی میزبان واقعی پنل می‌شود دید، و هیچ‌کدام از ترمینال توسعه اثبات نشده‌اند: **G1** — app pool می‌تواند TCP خروجی به پورت SSH سرورها باز کند؟ **G2** — می‌تواند روی `127.0.0.1` bind کند (forward محلی همین‌طور کار می‌کند)؟ **G3** — worker چند وقت زنده می‌ماند (idle timeout و recycle؛ کلکتور ترافیک و jobهای ساعتی فقط تا وقتی زنده است اجرا می‌شوند)؟ تصمیم مالک (۲۰۲۶-۱۱-۲۶): این سه را به‌عنوان «بررسی میزبان» داخل خود برنامه می‌گنجانیم؛ در هر شروعِ پروسه یک‌بار اجرا می‌شوند و نتیجه را با پیشوند ثابت `host-check` در لاگ می‌نویسند — تا **در اولین انتشار، همان بالا آمدن سایت، جواب را در لاگ بنویسد** و پرسش از هاستینگ فقط برای تأیید بماند. مشخصات در §۴.۱۱، گام ران‌بوک در Docs/19 §۴.

### ۲.۱. انحراف‌های پیاده‌سازی از پیش‌نویس این سند (۲۰۲۶-۱۱-۲۶)

بخش‌های §۳ و §۴ بر همین‌ها به‌روز شده‌اند؛ این فهرست برای ردیابی تغییر نسبت به پیش‌نویس است.

1. **کلاینت x-ui پایدار است (D7، §۴.۵)**: در حالت تانل یک `HttpClient` به‌ازای کلید `(Id, Port, Ssl, BasePath)` کش می‌شود و با reconnect یا تغییر `Generation` بازسازی نمی‌شود؛ فقط اتصال‌های آن در `ConnectCallback` از استخر جلسه‌ی **فعلی** می‌پرسند (همیشه آخرین ردیف سرور، نه یک closure کهنه). تکرار روی مرگ تانل: فقط در حالت تانل، فقط برای مسیرهای خواندنی و `login`، یک‌بار، با `ReportTransportFailure` پیش از تکرار، و با سقف ۵ send در هر درخواست منطقی — ترکیب با relogin روی 401 هم از ۵ send فراتر نمی‌رود.
2. **`ISshTunnelPool.Fault(serverId, generation, reason)` صریح و نسل‌آگاه است** (§۴.۲): مصرف‌کننده‌های مسیر forward (`XuiApiService`، `Tik4NetHandler`) نشستی را که استفاده کردند با نسلش گزارش می‌کنند. گزارش دیررس یک نشست قدیمی (مثلاً رویداد خطای آن که با تأخیر روی thread pool اجرا می‌شود) نشست تازه‌ای را که جایگزینش شده نمی‌بندد؛ جاروب بی‌فعالی هم بررسی و برداشتن را زیر قفل همان خانه انجام می‌دهد. (اصلاح پس از نقد کد‌ریویو، ۲۰۲۶-۱۱-۲۷.) `Id <= 0` رد می‌شود.
3. **`Execute` در هر دو transport یکسان معنا دارد** (§۴.۳): `false` برای stderr غیرخالی، exit status مثبت یا متن خطای RouterOS (الگوها به ابتدای خط لنگر شده‌اند تا مقدارهای چاپ‌شده مثبت کاذب ندهند)؛ exception فقط برای timeout و قطع اتصال. مسیر `Direct` هم از seam داخلی `ISshConnector` می‌گذرد و پین داده‌شده در آن هم اعمال می‌شود (پین خالی همچنان پذیرفته می‌شود).
4. **`Tik4NetHandler`** (§۴.۴): شکست یا timeout باز کردن در تانل → `Fault` + `UserException`؛ ولی رد شدن اعتبار API (`TikConnectionLoginException`) جلسه را سالم نگه می‌دارد، چون سرویس از داخل تانل جواب داده است. پورت پیش‌فرض ۸۷۲۸/۸۷۲۹ صریح حل می‌شود.
5. **اعتبارسنجی** (§۴.۶): فقط هدر `-----BEGIN OPENSSH PRIVATE KEY-----` پذیرفته می‌شود؛ پین پیش از ذخیره نرمال می‌شود (trim و حذف `=` انتهایی) و متن کلید trim می‌شود.
6. **تست اتصال** (§۴.۷): در connect از نوع discover گام `auth` به‌عمد «ناموفق» گزارش می‌شود تا پاسخ سبز نباشد؛ سروری که نه بیت `Xui` دارد و نه `UserManager` گام `service` ندارد.
7. **«سرور اجازه‌ی عبور ترافیک از تانل را نداد!»** را استخر خودش نمی‌گوید: listener محلی forward همیشه باز می‌شود و رد شدن کانال از سمت کلاینت با «سرویس ارتباط را بست» یک شکل است (پیوست B یافته ۳)؛ گام `service` تست اتصال هر دو احتمال را نام می‌برد.
8. **مسیر فرمان هم نشست از دست‌رفته را گزارش می‌کند** (§۴.۲ بند ۶): وقتی `ISshHandler` روی تانل وسط فرمان جلسه را از دست بدهد، همان جلسه `Fault` می‌شود و فراخوانی بعدی یک‌بار وصل می‌شود؛ timeout فرمان نشست را خراب علامت نمی‌زند (فرمان کند نشست مرده نیست). `SshCommand` بعد از هر فرمان Dispose می‌شود.
9. **سرور تانل بدون بیت `Xui`/`UserManager` به آدرس سرویس نیازی ندارد** (§۴.۶): قاعده‌ی «آدرس سرویس روی `127.0.0.1`» فقط برای روتر User Manager است؛ NASای که فقط برای اسکریپت و فایل OpenVPN از تانل می‌رسد بدون `WebApiConfig` ذخیره می‌شود و تست اتصالش گام `forward` ندارد. پیش‌نویس این قاعده را «بدون بیت Xui» نوشته بود و چنین سروری را بی‌دلیل رد می‌کرد.
10. **مشاهده‌های SSH.NET روی یک سرور SSH لوکال** (برنامه‌ی آزمایش با paramiko، ۲۰۲۶-۱۱-۲۷؛ VM نیست): ۱۵ فرمان پشت‌سرهم روی یک نشست بدون Dispose هم از سقف ۱۰ کانال نگذشت (سرور کانال را بعد از فرمان می‌بندد)؛ ۱۲ فرمان timeout‌شده با `CancelAsync` فرمان بعدی را گیر نانداخت؛ با قطع اتصال وسط فرمان، `EndExecute` خطای `SshConnectionException` داد، رویداد `ErrorOccurred` برخاست ولی `IsConnected` تا حدود یک ثانیه `true` ماند — همان پنجره‌ای که گزارش صریح مسیر فرمان را لازم می‌کند.
11. **اعتبار پنل x-ui هم اجباری است** (§۴.۶): جدول پلن برای بیت `Xui` فقط «`WebApiConfig` null» را داشت و یک ردیف x-ui بدون نام کاربری/رمز ذخیره می‌شد و تازه در گام `service` می‌شکست. حالا «برای سرور x-ui اعتبار پنل لازم است!» در هر دو transport اعمال می‌شود (نقد کد‌ریویو، ۲۰۲۶-۱۱-۲۷).
12. **سقف send پنج شد، نه چهار** (§۴.۵): پلن «حداکثر ۴ send» نوشته بود؛ ولی درخواستی که بدون کوکی شروع می‌شود برای «مرگ تانل + 401» پنج send لازم دارد (login، درخواست، تکرار، relogin، درخواست) و با سقف چهار با خطای بودجه تمام می‌شد. پنج دقیقاً سقف قاعده‌هاست؛ بودجه فقط نگهبان است.

## ۳. وضعیت امروز کد و چه چیزی تغییر می‌کند

| فایل | امروز | پس از پیاده‌سازی |
|---|---|---|
| `Domain/Servers/JsonType/SshConfig.cs` | `Username`، `Password`، `Port = 22` | + `PrivateKey`، `PassPhrase`، `HostKeySha256` |
| `Domain/Servers/JsonType/ServerConfiguration.cs` | `SshConfig`، `WebApiConfig`، `DataBaseInfo` | + `Transport` (با `JsonStringEnumConverter`) |
| `Domain/Servers/Types/` | — | + `ServerTransport.cs` (`Direct = 0`، `SshTunnel = 1`) |
| `ServerBridge/ServerBridgeOptions.cs` | چهار timeout | + `SshTunnelIdleSeconds` (پیش‌فرض ۹۰۰)؛ + `HostCheckTargets` (فهرست `host:port`) و `HostCheckHeartbeatMinutes` (پیش‌فرض ۱۵) برای D11 |
| `ServerBridge/ServiceFactory.cs` | `ISshHandler`، `ITik4NetHandler` singleton | + `ISshConnector`، `ISshTunnelPool` singleton (و جاروب بی‌فعالی به‌عنوان hosted service)، `TikConnectionFactory`، `TimeProvider`، `IServerChannelProbe`؛ + `IHostedService` مربوط به `HostCapabilityChecks` |
| `ServerBridge/HostChecks/HostCapabilityChecks.cs` | — | جدید (D11): G1/G2/G3 و خط‌های `host-check` (همراه `HostCheckReport.cs`، `HostCheckLogLines.cs`، `HostCheckTarget.cs`، `TcpProbe.cs`)؛ `ServerBridge/Hosted/StoppingToken.cs` سیگنال توقف مشترک این دو hosted service است: میزبان ممکن است دوباره stop شود (حلقه‌ی Run و مالک میزبان) و پیش از توقف دوم dispose شود — توقف idempotent است و بعد از dispose خطا نمی‌دهد |
| `ServerBridge/Tunnel/` | — | جدید: seam داخلی `ISshConnector`/`ISshChannel`/`ISshCommandChannel` و `SshConnector` روی SSH.NET؛ `SshEndpointFactory`؛ `SshTunnelPool`؛ `ServerChannelProbe`؛ پیام‌های §۴.۲ در `SshErrors` |
| `ServerBridge/Ssh/CommandOutcome.cs`، `RouterOsErrorPatterns.cs` | — | جدید (D10): حکم یکسان فرمان در هر دو transport |
| `Domain/Servers/Types/SshTunnelConfig.cs` | — | جدید: هدر کلید OpenSSH و نرمال‌سازی/اعتبارسنجی پین — مشترک بین اعتبارسنجی ادمین و آداپتورها |
| `Domain/Servers/IServerChannelProbe.cs`، `IServiceProbe.cs`، `Model/ConnectionStepModel.cs` | — | جدید (D9): اینترفیس‌های تست اتصال در Domain (Application به ServerBridge ارجاع ندارد) |
| `Application/Management/ServerConnectionTester.cs`، `Model/ServerConnectionReport.cs` | — | جدید (D9): هماهنگ‌کننده‌ی گام‌ها |
| `XuiPanel/WebService/IXuiHttpClientProvider.cs`، `XuiLogin.cs`، `XuiServiceProbe.cs` | — | جدید: provider کلاینت، قرارداد لاگین (از `XuiApiService` بیرون آمد)، گام `service` |
| `MikrotikRadius/Application/MikrotikServiceProbe.cs` | — | جدید: گام `service` روتر |
| `ServerBridge/ServerBridge.csproj` | SSH.NET 2025.0.0 | SSH.NET 2026.0.0 (D10) |
| `ServerBridge/Ssh/SshHandler.cs` | پسورد، `SshClient` تازه در هر فراخوانی | انتخاب حالت؛ کلید + پین در تانل؛ اتصال از استخر |
| `ServerBridge/Ssh/SshConnection.cs` | فقط stderr را خطا می‌داند | + `ExitStatus`؛ + الگوی خطای RouterOS؛ مالکیت `SshClient` مشروط |
| `ServerBridge/Tik4net/Tik4NetHandler.cs` | اتصال مستقیم | اتصال به پورت forward در تانل |
| `XuiPanel/WebService/XuiApiService.cs` | `factory.CreateClient("xui-panel")` | کلاینت از `IXuiHttpClientProvider`؛ تکرار محدود روی مرگ تانل (§۴.۵) |
| `XuiPanel/ServiceFactory.cs` | کلاینت نام‌دار `xui-panel` | + ثبت provider |
| `Application/Management/ServerCrudApplication.cs` | اعتبارسنجی اسکالرها | + قواعد transport/کلید/پین |
| `Portal/Controllers/ServerController.cs` | list/create/update/set-active | + `test-connection`؛ + `host-checks` (اجرای دوباره‌ی دستی D11) |
| `Frontend/…/management/servers/*` | فرم JSON خام | + انتخابگر transport با قالب تانل؛ دکمه‌ی تست اتصال (ردیف و مودال) و نمایش گام‌به‌گام |

## ۴. مشخصات پیاده‌سازی

### ۴.۱. پیکربندی

```csharp
public enum ServerTransport { Direct = 0, SshTunnel = 1 }

public class SshConfig
{
    public string Username { get; set; } = null!;   // اجباری
    public string Password { get; set; } = null!;   // در حالت SshTunnel برای پای SSH استفاده نمی‌شود
    public int Port { get; set; } = 22;
    public string? PrivateKey { get; set; }         // متن کامل کلید خصوصی — فقط هدر OpenSSH پذیرفته می‌شود
    public string? PassPhrase { get; set; }         // اختیاری
    public string? HostKeySha256 { get; set; }      // "SHA256:<base64>" — در SshTunnel اجباری؛ پیش از ذخیره نرمال می‌شود
}

public class ServerConfiguration
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ServerTransport Transport { get; set; } = ServerTransport.Direct;

    public SshConfig? SshConfig { get; set; }
    public WebApiConfig? WebApiConfig { get; set; }
    public DataBaseInfo? DataBaseInfo { get; set; }
}
```

معنای فیلدها در حالت `SshTunnel`:

- `SshConfig` = **پای SSH** (`ServerEntity.IpAddress` میزبان است): پورت SSH، کاربر SSH، کلید، پین.
- `WebApiConfig` = **نقطه‌ی اتصال سرویس از دید خود سرور**: `HostName = "127.0.0.1"`، `Port` = پورت سرویس روی loopback، `Username/Password` = اعتبار خود سرویس (لاگین x-ui، یا API میکروتیک). `XuiLogin.BaseUrl` و `XuiLogin.LoginAsync` (که از `XuiApiService` به `XuiLogin.cs` منتقل شدند و همان رفتار را دارند) روی همین مقادیر کار می‌کنند؛ فقط مقصد اتصال TCP عوض می‌شود.
- برای x-ui: `Ssl` باید `false` باشد (HTTP ساده روی loopback داخل تانل) و `InboundId` مثل امروز ست شود.
- برای میکروتیک: `Ssl = false` یعنی `Api` (پورت ۸۷۲۸ پیش‌فرض)، `Ssl = true` یعنی `ApiSsl` (۸۷۲۹). هر دو داخل تانل مجازند؛ `Api` ساده ترجیح دارد چون به هیچ گواهی وابسته نیست.

نمونه‌ی JSON یک سرور x-ui در حالت تانل (کلیدها **PascalCase** — پارسر case-sensitive است و کلید ناشناخته را بی‌صدا نادیده می‌گیرد، Docs/24 §5.8):

```json
{
  "Transport": "SshTunnel",
  "SshConfig": {
    "Username": "panel",
    "Port": 22,
    "PrivateKey": "-----BEGIN OPENSSH PRIVATE KEY-----\n…\n-----END OPENSSH PRIVATE KEY-----\n",
    "HostKeySha256": "SHA256:<base64>"
  },
  "WebApiConfig": {
    "HostName": "127.0.0.1", "Port": 2053, "Ssl": false, "BasePath": "/",
    "Username": "<x-ui login>", "Password": "<x-ui password>", "InboundId": 1
  }
}
```

نمونه‌ی یک روتر میکروتیک:

```json
{
  "Transport": "SshTunnel",
  "SshConfig": {
    "Username": "<router user>", "Password": "", "Port": 22,
    "PrivateKey": "-----BEGIN OPENSSH PRIVATE KEY-----\n…\n-----END OPENSSH PRIVATE KEY-----\n",
    "HostKeySha256": "SHA256:<base64>"
  },
  "WebApiConfig": {
    "HostName": "127.0.0.1", "Port": 8728, "Ssl": false,
    "Username": "<router user>", "Password": "<API password>"
  }
}
```

اندازه: کلید خصوصی ed25519 حدود ۰٫۵ کیلوبایت است و در سقف `ConfigMaxLength = 4000` (پس از protect) جا می‌شود؛ کلید RSA جا نمی‌شود و پشتیبانی نمی‌شود.

قاعده‌های شکل این فیلدها در `Domain/Servers/Types/SshTunnelConfig.cs` است (مشترک بین اعتبارسنجی ادمین و آداپتورها): `HasOpenSshKey` (متن باید با `-----BEGIN OPENSSH PRIVATE KEY-----` شروع شود)، `NormalizeHostKeyPin` (trim و حذف `=` انتهایی — پس پین با `=` پایانی یا فاصله هم پذیرفته می‌شود) و `IsValidHostKeyPin` (`^SHA256:[A-Za-z0-9+/]{43}$` روی مقدار نرمال‌شده). `Transport` با `JsonStringEnumConverter` خوانده و نوشته می‌شود: ردیفی که این کلید را ندارد `Direct` است.

### ۴.۲. استخر جلسه‌ها — `ISshTunnelPool`

```csharp
public interface ISshTunnelPool
{
    ValueTask<ISshTunnelSession> GetAsync(ServerEntity server, CancellationToken ct = default);
    void Fault(int serverId, int generation, string reason);              // گزارش خطای transport توسط مصرف‌کننده
}

public interface ISshTunnelSession
{
    int Generation { get; }                                    // با هر (re)connect یکی بالا می‌رود
    int ForwardedPort(int remotePort);                         // 127.0.0.1:<پورت تصادفی> → 127.0.0.1:remotePort
    Task<ISshConnection> OpenCommandChannelAsync(TimeSpan commandTimeout, CancellationToken ct = default);
}
```

رفتار:

1. **کلید جلسه** = `ServerEntity.Id`. قبل از استفاده، «اثر انگشت اتصال» جلسه‌ی زنده (`IpAddress`، `SshConfig.Port`، `Username`، `HostKeySha256`، هش `PrivateKey`) با ردیف فعلی مقایسه می‌شود؛ تفاوت یعنی ادمین پیکربندی را عوض کرده → جلسه‌ی قدیمی dispose و جلسه‌ی تازه ساخته می‌شود (بدون ری‌استارت پنل).
2. **اتصال** زیر یک `SemaphoreSlim(1,1)` per سرور؛ هم‌زمانی فراخوانی‌ها فقط یکی وصل می‌کند. `ConnectionInfo.Timeout = SshConnectTimeoutSeconds`. احراز هویت: `PrivateKeyAuthenticationMethod(username, new PrivateKeyFile(stream, passphrase))`.
3. **پین**: در `HostKeyReceived`، `"SHA256:" + e.FingerPrintSHA256` با `HostKeySha256` مقایسه می‌شود؛ نتیجه در یک فیلد محلی (`Match`/`Mismatch`) ثبت می‌شود، نه در نوع exception SSH.NET.
4. **Keep-alive**: `client.KeepAliveInterval = SshKeepAliveIntervalSeconds`.
5. **حذف بی‌فعالی**: تایمر سبک (هر ۶۰ ثانیه)؛ جلسه‌ای که `SshTunnelIdleSeconds` از آخرین فعالیتش گذشته باشد disconnect و حذف می‌شود. کلکتور ترافیک (۳۰۰ ثانیه) و job ساعتی جلسه را گرم نگه می‌دارند.
6. **خطا**: هر `SshException`/`SocketException`/`ObjectDisposedException` هنگام استفاده، جلسه را `Faulted` می‌کند و dispose؛ فراخوانی بعدی `GetAsync` یک‌بار وصل می‌شود. خطا به فراخوانی فعلی به‌صورت `UserException` برمی‌گردد — بدون retry کور برای عملیات نوشتاری. از مسیر فرمان (`ISshHandler` روی تانل) هم همین‌طور است: از دست رفتن جلسه وسط فرمان همان جلسه را `Fault` می‌کند. timeout فرمان نشست را `Faulted` نمی‌کند.
7. **Forward**: `ForwardedPort` برای هر `remotePort` یک‌بار forward می‌سازد (idempotent، زیر قفل) و `BoundPort` را برمی‌گرداند؛ forwardها تا پایان جلسه زنده‌اند و با dispose جلسه بسته می‌شوند.
8. **گزارش خطا**: `Fault(serverId, generation, reason)` فقط جلسه‌ی همان نسل را می‌بندد؛ نشستی که در این میان جایگزینش شده زنده می‌ماند — گزارش دیررس یک نشست قدیمی هرگز نشست تازه را نمی‌بندد. گزارش خود جلسه (رویداد `ErrorOccurred`، شکست فرمان یا forward) با هویت همان جلسه است، نه با شناسه‌ی سرور. جاروب بی‌فعالی بررسی و برداشتن را زیر همان قفل خانه انجام می‌دهد و `GetAsync` نشست را زیر همان قفل «استفاده‌شده» علامت می‌زند، پس جلسه‌ای که تازه تحویل شده جارو نمی‌شود. فراخوانی بعدی `GetAsync` یک‌بار با `Generation` بعدی وصل می‌شود. جلسه‌ای که بدون رویداد مرده باشد (`IsConnected == false`) در `GetAsync` بعدی جایگزین می‌شود. `Id <= 0` (ردیف in-memory) با exception صریح رد می‌شود.
9. **فرمان‌ها سریالی**: `OpenCommandChannelAsync` قفل exec جلسه را می‌گیرد؛ انتظار بیشتر از `commandTimeout` یعنی «خطای اجرای دستور در سرور!»؛ `Dispose` اتصال، فرمان در حال اجرا را cancel و قفل را آزاد می‌کند. جلسه‌ی drop شده بدون انتظار رد می‌شود.
10. **پیاده‌سازی**: `Backend/ServerBridge/Tunnel/SshTunnelPool.cs`، با `SshConnector.cs`/`SshNetChannel.cs` (SSH.NET) پشت seam داخلی `ISshConnector` — در تست‌ها fake. جاروب با `TimeProvider` تزریق‌شده (هر ۶۰ ثانیه) و در توقف میزبان همه‌ی جلسه‌ها بسته می‌شوند. خطوط لاگ `ssh-tunnel connected/connect-failed/disconnected` فقط میزبان، پورت، `Generation`، مدت و اثر انگشت کلید میزبان را دارند.

پیام‌های کاربر (سبک امروز):

| وضعیت | پیام | detail (فقط برای توسعه‌دهنده) |
|---|---|---|
| host key ناهمخوان | «کلید میزبان سرور با مقدار ثبت‌شده مطابقت ندارد!» | `Host key mismatch on {host}:{port}: presented {presented}, pinned {pinned}` |
| host key پین نشده | «برای این سرور کلید میزبان (host key) ثبت نشده است!» | `HostKeySha256 is empty; run the connection test to discover it` |
| کلید خصوصی بارگذاری نشود (متن نامعتبر یا passphrase غلط) | «کلید خصوصی SSH معتبر نیست!» | `The private key could not be loaded for {host}:{port} ({type})` |
| ردیف تانل بدون کاربر یا کلید (مثلاً SQL خام) | پیام‌های اعتبارسنجی §۴.۶: «برای حالت تانل، کاربر SSH الزامی است!» / «کلید خصوصی SSH معتبر نیست!» | `SshTunnel transport requires SshConfig.Username/PrivateKey on server ({id}:{name})` |
| احراز هویت | «احراز هویت SSH ناموفق بود!» | `SSH authentication failed on {host}:{port} as {username}` |
| اتصال | «خطای اتصال به سرور!» | `Cannot connect to {host}:{port} ({step}) after {elapsed} ms` |
| forward رد شد | «سرور اجازه‌ی عبور ترافیک از تانل را نداد!» — استخر این پیام را خودش نمی‌دهد (§۲.۱ بند ۷)؛ متن گام `service` تست اتصال آن را در کنار «سرویس مقصد ارتباط را بست» می‌آورد | `Port-forward to 127.0.0.1:{remotePort} was refused (authorized_keys permitopen / RouterOS forwarding-enabled)` |

### ۴.۳. `SshHandler` و `SshConnection`

- `ConnectTo(server)`: `Direct` → اتصال تازه با پسورد در هر فراخوانی، مثل امروز (سیاست پین `Optional`: پین داده‌شده مقایسه می‌شود، پین خالی پذیرفته می‌شود). `SshTunnel` → `pool.GetAsync(server)` و `session.OpenCommandChannelAsync(SshCommandTimeoutSeconds)`. `Dispose` در حالت تانل فقط فرمان در حال اجرا را cancel می‌کند (جلسه مال استخر است).
- **فرمان‌ها روی یک جلسه سریالی شوند** (`SemaphoreSlim(1,1)` per جلسه برای exec): `SshClient` برای اجرای موازی فرمان از چند نخ تضمینی ندارد و فرمان‌های پنل کوتاه‌اند.
- **تشخیص خطا**: امروز `Execute` فقط `execution.Error` (stderr) را خطا می‌داند. باید: (الف) `ExitStatus >= 0` و غیر صفر → `false`؛ (ب) برای `OsType == Mikrotik`، الگوی خطای RouterOS در **خروجی**: `^failure: `, `expected end of command`, `not enough permissions`, `no such item`, `bad command name`, `syntax error` → `false` با همان متن در `result`. RouterOS exit status قابل اتکا نمی‌فرستد و خطاهایش متنی است (نمونه‌های دیده‌شده: `failure: unknown file type (/file/add; line 1)`، `not enough permissions (9) (/tool/ping; line 1)`، `expected end of command (line 1 column 15)`). کدام کانال این متن‌ها را می‌آورد روی `Mikrotik-Base` ثبت شد (§ پیاده‌سازی پایین): stdout، و exit status ۰ یا ۱.
- این تشخیص برای `MikrotikDirectService` حیاتی است: مثلاً `/certificate sign` که روی RouterOS شکست بخورد نباید با `result = "failure: …"` به‌عنوان موفق برگردد و بعداً همان متن به‌جای گواهی در فایل `.ovpn` بنشیند.
- **پیاده‌سازی** (`Ssh/SshConnection.cs`، `Ssh/CommandOutcome.cs`، `Ssh/RouterOsErrorPatterns.cs`): حکم در `CommandOutcome.Classify` و برای هر دو transport یکی است — (۱) stderr غیرخالی → `false` با متن stderr؛ (۲) در غیر این صورت `ExitStatus > 0` → `false` با خروجی (`-1` یعنی exit status نرسیده و به این شرط نمی‌خورد)؛ (۳) در غیر این صورت روی `Mikrotik`، خطی که با الگوی خطای RouterOS **شروع** شود → `false` با همان خروجی؛ (۴) در غیر این صورت `true`. فرمان ناموفق دیگر exception نمی‌دهد؛ متن خطا (scrub‌شده) در لاگ سطح Warning می‌نشوند. exception فقط برای timeout («خطای اجرای دستور در سرور!») و قطع اتصال («خطای اتصال به سرور!») است. **ثبت کانال (۲۰۲۶-۱۱-۲۷، `Mikrotik-Base`، RouterOS 7.20.6، `MikrotikTunnelTest.RouterOsErrorChannel_IsObserved`)**: هر چهار فرمان ناموفق متن را فقط روی **stdout** آوردند و stderr خالی بود. exit status قابل اتکا نیست — همان کلاس خطا یک‌بار ۰ و یک‌بار ۱ بود: `/ppp active remove *FFF` → exit 0، `no such item (4)`؛ `/tool ping 127.0.0.1 count=1` → exit 0، `not enough permissions (9) (/tool/ping; line 1)`؛ `/user group set photon-panel comment=…` → exit 1، `not enough permissions (9)`؛ `/nosuchcommand` → exit 1، `bad command name nosuchcommand (line 1 column 2)`. الگوی متن stdout لازم است؛ exit status به‌تنهایی کافی نیست. **مشاهده‌ی اول (۲۰۲۶-۱۱-۲۶، `Mikrotik-Scenario`)**: `/certificate add … copy-from=CLIENT-TEMPLATE` که روی VM پاسخ «no such item (/certificate/add; line 1)» گرفت، خطا را روی **stdout** آورد و exit status برای همین خطا یک اجرا 0 و اجرای دیگر 1 بود — بدون الگوی متن stdout این خطا با exit status صفر «موفق» می‌شد. همین مشاهده نشان داد ایمیج این VM گواهی `CLIENT-TEMPLATE` را ندارد و دو تست سناریوی دانلود OpenVPN تا آنجا فقط با zip معیوب سبز می‌ماندند (Docs/18 §۵ بند ۱۳).

### ۴.۴. `Tik4NetHandler`

```csharp
// SshTunnel
var session = await pool.GetAsync(server);
var localPort = session.ForwardedPort(config.Port ?? (config.Ssl ? 8729 : 8728));
var connection = ConnectionFactory.CreateConnection(config.Ssl ? TikConnectionType.ApiSsl : TikConnectionType.Api);
await connection.OpenAsync("127.0.0.1", localPort, config.Username, config.Password);
return connection;   // مصرف‌کننده مثل امروز using می‌کند
```

`Direct` بدون تغییر. تایماوت `Tik4NetOpenTimeoutSeconds` مثل امروز. در حالت تانل، شکست یا timeout باز کردن → `pool.Fault(server.Id, session.Generation, …)`، dispose اتصال و `UserException` «خطای اتصال به سرور!»؛ رد شدن اعتبار API (`TikConnectionLoginException`) جلسه را نگه می‌دارد و خطای خود کتابخانه را برمی‌گرداند. پورت تهی در هر دو حالت به ۸۷۲۸ (`Api`) یا ۸۷۲۹ (`ApiSsl`) حل می‌شود. باز کردن با `TikConnectionOpener.OpenAsync` انجام می‌شود (عمومی؛ همان تایماوت، بدون `Task.Delay` رهاشده) — گام `service` تست اتصال هم از همین کمک و از delegate عمومی `TikConnectionFactory` می‌گیرد. `ApiSsl` کتابخانه vendored هر گواهی‌ای را می‌پذیرد (`ApiConnection.ValidateServerCertificate`) — داخل تانل بی‌ضرر است (اعتماد از host key می‌آید)؛ اگر روزی مستقیم استفاده شد، pin کردن گواهی لازم است.

### ۴.۵. `XuiApiService` و provider کلاینت

```csharp
internal interface IXuiHttpClientProvider
{
    HttpClient Get(ServerEntity server);   // Direct: کلاینت نام‌دار «xui-panel»؛ SshTunnel: کلاینت همان سرور
    void ReportTransportFailure(ServerEntity server);   // فقط SshTunnel: pool.Fault(server.Id, نسل آخرین نشستِ اتصال‌های همین provider, …)
}
```

- `SendAsync` به‌جای `factory.CreateClient(ServiceFactory.HttpClientKeyName)` از `http_provider.Get(server)` می‌گیرد.
- در حالت تانل برای هر سرور یک `HttpClient` با `SocketsHttpHandler` اختصاصی cache می‌شود، کلید cache = `(server.Id, WebApiConfig.Port, Ssl, BasePath)`. کلاینت با reconnect یا تغییر `Generation` بازسازی و dispose نمی‌شود: ورودی cache آخرین `ServerEntity` دریافتی را نگه می‌دارد و `ConnectCallback` با آن از استخر می‌پرسد — پس پس از تغییر کلید/پین، اتصال تازه جلسه را از روی ردیف فعلی می‌سازد. همه‌ی کلاینت‌ها با dispose پروایدر آزاد می‌شوند. timeout و هدرهای پیش‌فرض از کلاینت نام‌دار `xui-panel` آینه می‌شوند.

```csharp
var handler = new SocketsHttpHandler
{
    UseProxy = false,                                    // مقصد loopback است؛ پروکسی سیستمی نباید ببیند
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
    ConnectCallback = async (_, ct) =>
    {
        var current = await pool.GetAsync(latest);      // latest = آخرین ردیف این کلید؛ همیشه جلسه‌ی فعلی، نه یک closure کهنه
        var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, current.ForwardedPort(remotePort), ct);
        return tcp.GetStream();                         // SocketException/IOException → pool.Fault(server.Id, session.Generation, …) و rethrow
    },
};
```

- هدر `Accept: application/json` مثل کلاینت نام‌دار. `remotePort = WebApiConfig.Port` (اجباری در حالت تانل).
- **Retry**: اگر جلسه بدون آنکه استخر بفهمد بمیرد (اتصال نیمه‌باز)، اولین درخواست روی اتصال pooled با `HttpRequestException` (زنجیره‌ی inner: `IOException`/`SocketException`) شکست می‌خورد. قانون (در `XuiApiService.SendAsync`): فقط در حالت تانل، فقط برای مسیرهای خواندنی (`xui/API/inbounds/`، `xui/API/inbounds/onlines`) و خودِ `login`، یک‌بار: `provider.ReportTransportFailure(server)` (→ `pool.Fault`) و سپس تکرار. برای مسیرهای نوشتاری، کد وضعیت غیر 2xx، timeout و `success=false` خطا به فراخوانی برمی‌گردد (اجرای بعدی job یا عمل کاربر دوباره تلاش می‌کند؛ retry کور `addClient` ممکن است کلاینت تکراری بسازد). **سقف send**: هر درخواست منطقی حداکثر ۵ send واقعی دارد (تلاش × relogin روی 401 × یک تکرار)؛ وقتی بودجه ناکافی است همان خطای transport برمی‌گردد. **گزارش نسل‌آگاه است**: `ReportTransportFailure` نسلِ نشستی را می‌دهد که اتصال‌های همین provider آخرین بار از آن رفته‌اند، پس نشستی که در این میان جایگزین شده دست نمی‌خورد. **شروع سرد**: درخواستی که بدون کوکی شروع می‌شود هم مرگ تانل و هم 401 بعد از آن را در همان ۵ send برمی‌گرداند (login، درخواست، تکرار، relogin، درخواست) — پنج دقیقاً سقف قاعده‌هاست و بودجه فقط نگهبان است.
- کوکی نشست (`session_cookies`) مثل امروز؛ کوکی به TCP وابسته نیست، پس پس از اتصال مجدد معتبر می‌ماند و در ۴۰۱ مسیر relogin موجود اجرا می‌شود.

### ۴.۶. اعتبارسنجی در `ServerCrudApplication`

در `Validate`، وقتی `model.Config != null`:

| شرط | پیام کاربر |
|---|---|
| `Transport == SshTunnel` و `SshConfig` null یا `Username` خالی | «برای حالت تانل، کاربر SSH الزامی است!» |
| `Transport == SshTunnel` و `PrivateKey` خالی یا بدون هدر `-----BEGIN OPENSSH PRIVATE KEY-----` | «کلید خصوصی SSH معتبر نیست!» |
| `Transport == SshTunnel` و `HostKeySha256` خالی یا ناهم‌خوان با `^SHA256:[A-Za-z0-9+/]{43}$` | «کلید میزبان (host key) ثبت نشده یا نامعتبر است!» |
| `SshConfig.Port` خارج از ۱..۶۵۵۳۵ | «پورت SSH نامعتبر است!» |
| بیت `Xui` و `WebApiConfig` null | «برای سرور x-ui پیکربندی وب لازم است!» |
| بیت `Xui` و `WebApiConfig.Username` یا `Password` خالی | «برای سرور x-ui اعتبار پنل لازم است!» |
| بیت `Xui`، حالت تانل، و `HostName != "127.0.0.1"` یا `Ssl == true` یا `Port` خالی | «در حالت تانل، آدرس پنل x-ui باید `127.0.0.1` با پورت مشخص و بدون SSL باشد!» |
| بیت `UserManager`، حالت تانل، و `WebApiConfig` null یا `HostName != "127.0.0.1"` یا `Port` خالی | «در حالت تانل، آدرس سرویس باید `127.0.0.1` با پورت مشخص باشد!» |
| بیت `UserManager` و `WebApiConfig.Username/Password` خالی | «برای User Manager اعتبار API لازم است!» |

`ConfigMaxLength` (۴۰۰۰ پس از protect) مثل امروز اعمال می‌شود.

نکته‌های پیاده‌سازی: سه قاعده‌ی «بیت `Xui` و `WebApiConfig` null»، «بیت `Xui` و اعتبار پنل» و «بیت `UserManager` و اعتبار API» در **هر دو** transport اعمال می‌شوند (سروری با بیت Radius بدون پیکربندی وب در زمان اجرا هم کار نمی‌کند)؛ قاعده‌ی پورت SSH هم در هر دو است. پین پیش از اعتبارسنجی و ذخیره نرمال می‌شود و متن کلید و نام کاربر trim می‌شوند؛ بقیه‌ی مقدارها دست نمی‌خورند. همان قاعده‌ها در `Update` هم اجرا می‌شوند. سرور تانلی که هیچ بیت `Xui`/`UserManager` ندارد (مثلاً NAS فقط برای اسکریپت و فایل OpenVPN) به آدرس سرویس نیازی ندارد؛ پیش‌نویس این قاعده را «بدون بیت Xui» نوشته بود.

### ۴.۷. endpoint تست اتصال

`POST /api/server/test-connection` — `[Authorize(Policy = ServiceFactory.AdminPolicy)]`، `ResultHandlerController`، `ApiResult`؛ بدنه `SaveServerModel`. اگر `Id > 0` و `Config == null`، پیکربندی ذخیره‌شده استفاده می‌شود (داخلی خوانده می‌شود، هرگز برگردانده نمی‌شود).

پیاده‌سازی در `Application/Management/ServerConnectionTester.cs` (`IServerConnectionTester`) روی دو probe: `IServerChannelProbe` (گام‌های کانال، یک جلسه‌ی یک‌بارمصرف — هرگز از استخر) و `IServiceProbe` (گام `service`، keyed با `ServerFeature.Xui`/`UserManager`). بدنه می‌تواند فقط `{ "id": n }` باشد (اکشن ردیف). وقتی `Config` تهی است نشانی، ویژگی‌ها و سیستم‌عاملِ فرم — و اگر نیامده بود ردیف ذخیره‌شده — استفاده می‌شود؛ هیچ پیکربندی‌ای نبود → «پیکربندی سرور برای تست در دسترس نیست!»؛ ردیف نبود → «سرور پیدا نشد!»؛ `Id` منفی → «شناسه سرور نامعتبر است!». گارد ادمین در عمق هم اعمال می‌شود (403). نتایج probeها هیچ‌وقت exception نیستند: هر شکست یک گام ناموفق با متن است. بدنه از نوع `TestServerConnectionModel` است و مثل payload ذخیره اعتبارسنجی نمی‌شود (فرم ناقص هم قابل تست است؛ `SshConfig.Password` در ردیف تانل می‌تواند تهی باشد).

| گام | چه کاری | نتیجه‌ی موفق |
|---|---|---|
| `tcp` | اتصال TCP به `IpAddress:SshConfig.Port` با `SshConnectTimeoutSeconds` | متصل شد + مدت |
| `host-key` | handshake تا دریافت کلید میزبان؛ اگر پین خالی است: برگرداندن `SHA256:…` و توقف (در این حالت گام `auth` به‌عمد ناموفق گزارش می‌شود تا پاسخ سبز نباشد — §۲.۱ بند ۶) | تطابق (یا «کشف شد») |
| `auth` | احراز هویت با کلید (یا پسورد در Direct) | موفق + نام الگوریتم کلید میزبان |
| `forward` | ساخت forward به `127.0.0.1:WebApiConfig.Port` و یک اتصال TCP آزمایشی به آن — فقط برای سروری که بیت `Xui` یا `UserManager` دارد | پورت محلی + مدت |
| `service` | x-ui: `POST login` و دریافت کوکی؛ میکروتیک: لاگین tik4net و `/system/identity/print`. سروری با هیچ‌کدام از بیت‌های `Xui`/`UserManager` این گام را ندارد | «ورود موفق» |

پاسخ (`ServerConnectionReport`، روی wire camelCase): `Passed` (کل)، فهرست گام‌ها (`Step`، `Passed`، `Detail`) + `HostKeySha256` (اگر کشف شد) + `MachineName` (`Environment.MachineName` — نام میزبان پنل برای رکورد عملیاتی). بدون کلید، بدون پسورد، بدون کوکی. `Detail` فقط پورت، مدت، اثر انگشت و پیام عمومی دارد.

### ۴.۸. لاگ و محرمانگی

- `Log.Information` در connect/reconnect/disconnect جلسه: میزبان، پورت، `Generation`، مدت — هرگز `Config`، کلید، passphrase یا پسورد.
- `CommandScrubber` همین حالا `password="…"`/`passphrase="…"` را در فرمان‌ها ماسک می‌کند؛ پوشش `export-passphrase` را نگه دارد.
- `Detail` در `UserException` فقط از جدول §۴.۲.

### ۴.۹. فرانت‌اند (`/servers`)

- انتخابگر «نحوه‌ی اتصال» (`Direct`/`SshTunnel`) بالای جعبه‌ی JSON: انتخاب تانل، قالب کامل کانفیگ تانل را در جعبه‌ی خالی می‌نویسد و در کانفیگ موجود کلید `transport` را می‌گذارد یا برمی‌دارد؛ کانفیگی که خودش `transport` نام ببرد انتخابگر را می‌برد (جعبه تصمیم می‌گیرد، انتخابگر فقط تکمیل می‌کند).
- در جدول: اکشن «تست اتصال» برای هر ردیف (فقط `id` → پیکربندی ذخیره‌شده) → مودال نتیجه با گام‌ها به ترتیب اجرا و متن هر گام.
- در مودال ساخت/ویرایش: «تست اتصال همین فرم» — همان بدنه‌ی ثبت، بدون ذخیره. اگر تست اثر انگشت کشف کرد و مال همین فرم بود، دکمه‌ی «ثبت این اثر انگشت در کانفیگ» مقدار را در `sshConfig.hostKeySha256` جعبه‌ی JSON می‌نویسد؛ در تست ردیفِ ذخیره‌شده فقط مقدار و راهنما نشان داده می‌شود، چون کانفیگ ذخیره‌شده هرگز خوانده نمی‌شود.
- کلیدهای ترجمه در `Frontend/public/texts/management.json` زیر `management.servers` (فارسی، الگوی پروژه).

### ۴.۱۰. میزبان پنل (IIS اشتراکی)

- فقط مسیر in-process: هیچ سرویس، تانل یا کلاینت بیرونی روی هاست نصب نمی‌شود.
- پیش‌نیازها که باید با هاستینگ تأیید شود: خروجی TCP از app pool به پورت SSH سرورها؛ اجازه‌ی bind روی `127.0.0.1`؛ رفتار idle timeout/recycle اپ‌پول (jobهای پس‌زمینه فقط تا وقتی worker زنده است اجرا می‌شوند — Docs/19). هر سه در هر شروع پروسه با لاگ بررسی می‌شوند (§۴.۱۱، D11) و تست اتصال هر سرور همان دو مورد اول را برای آن ردیف نشان می‌دهد.
- Recycle = از دست رفتن جلسه‌ها؛ کلکتور و job ساعتی در اجرای بعدی دوباره وصل می‌شوند.

### ۴.۱۱. بررسی‌های میزبان (G1–G3): در هر شروع، با لاگ — D11

یک `IHostedService` سبک به نام `HostCapabilityChecks` در `Backend/ServerBridge/HostChecks/`، ثبت‌شده در `ServiceFactory.cs` کنار `ISshTunnelPool`. در `StartAsync` پس از آماده‌شدن DI اجرا می‌شود، بدون مسدودکردن شروع (یک task پس‌زمینه؛ کل کار چند اتصال TCP با تایماوت `SshConnectTimeoutSeconds` است).

| بررسی | چه کاری | خط لاگ (سطح Information) | معیار سبز |
|---|---|---|---|
| G1 — خروجی TCP | برای هر مقصد یک `TcpClient.ConnectAsync` و بستن فوری. مقصدها = `ServerBridgeOptions.HostCheckTargets` (فهرست `host:port`) ∪ نقطه‌ی SSH هر ردیف ذخیره‌شده‌ی سرور (`IpAddress:SshConfig.Port`)؛ بدون تکرار | `host-check G1 tcp target={host}:{port} pass={true\|false} ms={n} error={SocketErrorCode یا none}` | `pass=true` برای هر مقصد |
| G2 — bind روی loopback | `new TcpListener(IPAddress.Loopback, 0)`؛ `Start()`؛ خواندن پورت؛ `Stop()` | `host-check G2 loopback-bind pass={true\|false} port={n} error={نوع exception یا none}` | `pass=true` |
| G3 — طول عمر worker | در هر شروع یک خط؛ سپس هر `HostCheckHeartbeatMinutes` (پیش‌فرض ۱۵) یک خط | `host-check G3 worker-started pid={pid} machine={MachineName} os={RuntimeInformation.OSDescription}` و `host-check G3 heartbeat uptime={d.hh:mm:ss}` | مشاهده‌پذیر، نه سبز/قرمز: دو خط heartbeat با uptime کم پس از یک ساعت بی‌کاری = recycle |

- خط جمع‌بندی در پایان هر اجرا: `host-check summary g1={pass|fail|no-targets} g2={pass|fail} g3=observing targets={n}`.
- **هیچ مقدار محرمانه‌ای** در این خط‌ها نیست: فقط host، پورت، زمان، نوع خطا و نام ماشین — هرگز کلید، پسورد یا `Config`.
- **چرا در شروع:** اولین انتشار با بالا آمدن سایت همراه است. مقصدها از پیش در `appsettings.Production.json` (یا `private/secrets.json` طبق Docs/19 §۲) نوشته می‌شوند؛ نیازی به ردیف سروری نیست و جواب G1 همان لحظه در `events-*.log` می‌نشیند.
- **اجرای دوباره‌ی دستی:** همان کلاس از طریق `POST /api/server/host-checks` (policy ادمین، مثل D9) قابل فراخوانی است تا اپراتور بعد از پاسخ هاستینگ یا تغییر مقصدها، بدون ری‌استارت دوباره ببیند. شکل پاسخ (`HostCheckReport`): `targets[]` (`target`، `passed`، `milliseconds`، `error`)، `outboundTcp` (true/false، null = no-targets)، `loopbackBind`، `loopbackPort`، `uptime` و `summary` (همان متن خط لاگ). مقدارهای `HostCheckTargets` روی start اعتبارسنجی می‌شوند (هر مورد `host:port`؛ مقدار نامعتبر = `OptionsValidationException` و بالا نیامدن میزبان)؛ `SshTunnelIdleSeconds` (۶۰..۸۶۴۰۰) و `HostCheckHeartbeatMinutes` (۱..۱۴۴۰) هم `Range` دارند.
- **رابطه با D9:** تست اتصال هر سرور، G1 و G2 را برای آن ردیف نشان می‌دهد؛ این بخش تصویر کلی میزبان را می‌دهد، پیش از آنکه ردیفی وجود داشته باشد.
- **اگر قرمز بود:** G1 فقط برای پورت ۲۲ → پورت دوم روی sshd میزبان‌ها (کار مالک زیرساخت)؛ G1 کلاً یا G2 → روش جایگزین (پروکسی mTLS) — تصمیم با مالک. G3 recycle نشان داد → یک ping خارجی روی یک endpoint سبک، با بازه‌ای کمتر از idle timeout (Docs/19 §۴).

### ۴.۱۲. ردیف‌های سرور (seed) — شکل لازم در حالت تانل

خواست مالک (۲۰۲۶-۱۱-۲۶): کارهای پنل همین‌جا ثبت شود. ردیف‌های اولیه‌ی سرور با SQL خام آماده شده‌اند و شکلشان مسیر مستقیمِ امروز است: ردیف MikroTik با `Ssl: true` روی پورت API-SSL، و ردیف‌های x-ui با `HostName` = نام ops و پورت پنل. در حالت تانل هر ردیف باید این شکل را بگیرد:

| ردیف | `SshConfig` (پایه‌ی SSH) | `WebApiConfig` (نقطه‌ی اتصال سرویس از دید خود سرور) |
|---|---|---|
| روتر MikroTik | `"Transport": "SshTunnel"`؛ `Username` = کاربر پنل در group پنج‌policy؛ `Port` = پورت SSH؛ `PrivateKey`؛ `HostKeySha256`؛ `Password` خالی (احراز هویت پایه‌ی SSH با کلید است) | `HostName: "127.0.0.1"`؛ `Port` = پورت API ساده؛ **`Ssl: false`** — API-SSL روی روتر بازنشسته شده و دیگر سرویس نمی‌دهد؛ `Username`/`Password` = اعتبار API (پسوردِ همان کاربر) |
| هر میزبان x-ui | `"Transport": "SshTunnel"`؛ `Username` = کاربری که خط forward-only را دارد؛ `Port` = پورت SSH؛ `PrivateKey`؛ `HostKeySha256`؛ `Password` خالی | `HostName: "127.0.0.1"` (نه نام ops)؛ `Port` = پورت پنل روی loopback؛ `Ssl: false`؛ `BasePath`، `Username`/`Password` (لاگین x-ui) و `InboundId` مثل امروز |

- کلیدهای JSON **PascalCase** می‌مانند (Docs/24 §3): `"Transport"`، `"PrivateKey"`، `"HostKeySha256"`. مقدارها از رکورد مالک زیرساخت می‌آید: کلید خصوصی هر میزبان، اثر انگشت host key و پورت‌ها — یک کلید به‌ازای هر میزبان. نشانی ستون `IpAddress` ردیف، نشانی فعلی میزبان است.
- **در SQL خام راز ننویسید.** `ServerConfigProtection.Unprotect` مقداری را که با `protected:` شروع نمی‌شود دست‌نخورده برمی‌گرداند؛ یعنی `Config`ی که با SQL خام وارد شود متن ساده در دیتابیس می‌ماند — و در حالت تانل این متن کلید خصوصی SSH و پسوردها را در بر دارد. مسیر درست: ردیف را با SQL خام **بدون `Config`** و با `IsActive = 0` بسازید (نام، نشانی، سیستم‌عامل، ویژگی‌ها، realm، ظرفیت)، بعد `Config` را از طریق API ادمین `/servers` وارد کنید که آن را با Data Protection محافظت می‌کند. تا وقتی ابزاری برای ساختن «حالت محافظت‌شده» در `Scripts/deploy.sh` نیست، ورود از API ادمین تنها راه است (بک‌لاگ §۹).
- کد از این فیلدها آگاه است (پیاده‌سازی ۲۰۲۶-۱۱-۲۶): ردیفی که `"Transport": "SshTunnel"` دارد از همان اول تلاش می‌کند با کلید و پین وصل شود — پس ردیف تانل را با `IsActive = 0` بسازید و پیش از فعال‌سازی با تست اتصال سبز کنید (فاز ۵ §۸).

## ۵. پیش‌نیازهای سمت سرور (مالک زیرساخت آماده می‌کند)

این‌ها کار پنل نیست، اما بدون آن‌ها حالت تانل کار نمی‌کند؛ تست اتصال دقیقاً می‌گوید کدام گام مانده است.

### ۵.۱. میزبان x-ui (Linux/OpenSSH)

- پورت SSH از هاست پنل در دسترس؛ یک کاربر با کلید (بدون پسورد).
- یک خط در `authorized_keys` آن کاربر — دقیقاً با این شکل:

```
from="<panel-egress-ip>",restrict,port-forwarding,permitopen="127.0.0.1:<panel-port>",command="/bin/false" ssh-ed25519 AAAA… panel
```

| گزینه | چه می‌کند |
|---|---|
| `from="…"` | کلید فقط از IP خروجی پنل؛ اگر IP ثابت نیست حذف شود |
| `restrict` | PTY، forwarding و `~/.ssh/rc` را می‌بندد — اما اجرای فرمان را نه؛ به همین دلیل `command=` هم لازم است |
| `port-forwarding` | پس از `restrict`، forwarding را برمی‌گرداند (هم local و هم remote) |
| `permitopen="127.0.0.1:<port>"` | مقصد forward فقط همان پورت loopback؛ رشته‌ی literal، بدون name lookup |
| `command="/bin/false"` | هر exec با این کلید بی‌نتیجه و غیر‌صفر تمام می‌شود؛ forwarding دست‌نخورده می‌ماند (کلاینتِ فقط-forward کانال session باز نمی‌کند) |

- پنل x-ui روی `127.0.0.1:<panel-port>` گوش می‌دهد (نه روی همه‌ی interfaceها)؛ `WebApiConfig` مطابق §۴.۱.
- sshd به تغییر نیاز ندارد: `AllowTcpForwarding` پیش‌فرض `yes` است و `authorized_keys` در زمان لاگین خوانده می‌شود (بدون reload). `StrictModes` فعال است → مجوزهای `~/.ssh` و `authorized_keys` درست باشد.
- نتیجه‌ی `command="/bin/false"`: با کلید فوق هیچ `ProcessService`/اسکریپتی روی آن سرور اجرا نمی‌شود؛ `ISshConnection.Execute` باید `false` برگرداند (§۴.۳). اگر سروری اسکریپت SSH دارد، مالک کلید دوم (با exec) می‌دهد.

### ۵.۲. روتر MikroTik (RouterOS 7)

- یک کاربر اختصاصی پنل در یک **group سفارشی** با دقیقاً پنج policy: `ssh`، `api`، `read`، `write`، `sensitive`. هر سه group داخلی (`read`، `write`، `full`) همه‌ی `ssh`/`api`/`sensitive` و موارد اضافه (مثل `reboot`، `password`، `web`، `winbox`) را دارند و برای این کار زیادند.
- کلید عمومی پنل روی همان کاربر: `/user ssh-keys import public-key-file=<key>.pub user=<user>` (فایل آپلودی را بعدش حذف کنید). RouterOS گزینه‌ی per-key (`from=`، `restrict`) ندارد؛ محدودیت از group و فایروال می‌آید.
- `/ip ssh set forwarding-enabled=local` — این سوئیچ **جهانی** است و پیش‌فرض آن `no`؛ بدون آن هر درخواست تانلی در گام `forward` شکست می‌خورد.
- بعد از import حتماً `/ip ssh print` را ببینید: یک‌بار دیده شد که import کلید، `password-authentication` را به `yes-if-no-key` انداخت و لاگین پسوردی کاربران دارای کلید را شکست.
- منبع اتصال پنل را فایروال روتر محدود می‌کند (whitelist پورت SSH با نشانی پنل)، چون کلیدهای RouterOS گزینه‌ی `from=` ندارند. روی میزبان‌های x-ui همین کار را `from=` در خط `authorized_keys` می‌کند (§۵.۱).
- کفایت پنج policy برای سه عمل (`/certificate sign` با CA، `/ppp active remove`، `close-session`) هنوز روی روتر تولید دیده نشده: سناریوهای §۶ آن را روی روتر تستی می‌سنجند و اولین استفاده‌ی واقعی هم زیر نظر است.
- سرویس API — **حالت نهایی، انتخاب مالک (۲۰۲۶-۱۱-۲۶):** سرویس ساده‌ی `api` فقط برای خود روتر (`/ip service set api disabled=no address=127.0.0.1`) و بازنشستگی `api-ssl` — هیچ گواهی لازم نیست. نکته: محدودیت `address` روی سرویس دست‌دادن TCP را نمی‌بندد (پورت handshake را می‌پذیرد و بعد ارتباط را می‌بندد)؛ برای سکوت پورت از بیرون یک rule drop در فایروال روتر لازم است. `api-ssl` از داخل تانل هم کار می‌کند، ولی دیگر دلیل خوبی برای نگه‌داشتنش نیست.
- host key: RouterOS به‌طور پیش‌فرض فقط کلید RSA ارائه می‌دهد؛ SSH.NET با `rsa-sha2-*` توافق می‌کند (اثبات‌شده). `ssh-keyscan` در برابر RouterOS کامل نمی‌شود؛ اثر انگشت را با گام discover تست اتصال بگیرید.

### ۵.۳. چه چیزی پنل روی روتر اجرا می‌کند و چه policy می‌خواهد

| فرمان / فراخوانی | مسیر | policy |
|---|---|---|
| `/ppp active print …` | SSH | `read` |
| `/ppp active remove …` | SSH | `write` |
| `/certificate print/add/sign/export-certificate` | SSH | `read`، `write`، `sensitive` (export) |
| `/file print/remove/get`، `:put [/file get … contents]` | SSH | `read`، `write` |
| `/user-manager/user` print/add/set/remove | API | `api`، `read`، `write` |
| `/user-manager/session` print، `close-session` | API | `api`، `read`، `write` |
| `/user-manager/profile`، `limitation`، `profile-limitation`، `user-profile` | API | `api`، `read`، `write` |
| ورود SSH | SSH | `ssh` |

### ۵.۴. میزبان پنل

خروجی TCP به پورت SSH سرورها، bind روی loopback، و رفتار app pool (§۴.۱۰) — این سه در اولین انتشار با لاگ بررسی می‌شوند (§۴.۱۱، D11).

## ۶. تست

قاعده‌ی Docs/23 D1: هیچ تستی به سرور زنده وصل نمی‌شود؛ مقصد اینتگریشن فقط VMهای لوکال است.

| لایه | تست | چه چیزی را ثابت می‌کند | وضعیت (۲۰۲۶-۱۱-۲۶) |
|---|---|---|---|
| واحد | `SshTunnelPoolTest` — از طریق seam داخلی `ISshConnector` (fake در `Tests/Mock/MockServerBridge/FakeSshConnector.cs`) | اتصال تنبل (حتی با فراخوانی هم‌زمان)، reconnect پس از `Fault` و پس از رویداد خطا، `Generation`، rebuild روی تغییر پای SSH، حذف بی‌فعال با `Sweep`، idempotent بودن `ForwardedPort`، رد `Id <= 0`، قفل exec و `Dispose`؛ گزارش نسل قدیمی و رویداد خطای دیررس نشست تازه را نمی‌بندد؛ فرمانِ از دست داده جلسه را Fault می‌کند ولی timeout نه؛ شکست forward؛ جاروب جلسه‌ی تازه‌تحویل‌شده را نمی‌بندد؛ هر پیام خطای §۴.۲ بدون هیچ راز در `detail` | ✅ |
| واحد | `SshHandlerTest`، `CommandOutcomeTest` | اتصال فرمان در هر دو transport (کلاینت تازه با پسورد در `Direct` بدون کلید/پین، جلسه‌ی استخر در تانل)؛ حکم فرمان: stderr، exit status، متن RouterOS؛ exception فقط timeout/قطع اتصال | ✅ |
| واحد | `ServerChannelProbeTest` | با `ISshConnector` فیک و listener لوکال: توقف در tcp بسته؛ `Direct` با پین خالی ادامه می‌دهد؛ تانل با پین خالی پیش از auth می‌ایستد و اثر انگشت را می‌دهد؛ ناهمخوانی در `host-key` می‌ایستد؛ گام `forward` | ✅ |
| واحد | `Tik4NetHandlerTest` | `Direct` مثل امروز؛ تانل روی پورت forward؛ شکست/timeout → `Fault` + `UserException`؛ رد اعتبار API جلسه را نگه می‌دارد؛ پورت پیش‌فرض ۸۷۲۸/۸۷۲۹ | ✅ |
| واحد | `XuiHttpClientProviderTest`، `XuiServiceProbeTest`، `MikrotikServiceProbeTest` | کلاینت پایدار per کلید و handler بدون proxy؛ `ConnectCallback` روی listener لوکال و آخرین ردیف؛ پورت بسته → `Fault`؛ گام `service` هر دو سرویس (HTTP لوکال / tik4net فیک) | ✅ |
| واحد | `ServerCrudApplicationTest` (بخش قاعده‌های کانال) | هر ۸ قاعده‌ی §۴.۶ (در create و update)، اینکه `Direct` بدون کلید/پین همچنان پذیرفته می‌شود، نرمال‌سازی پین، رد هدرهای RSA/PKCS#8، رفت‌وبرگشت `Transport` به‌صورت رشته | ✅ |
| واحد | `ServerConnectionTesterTest` (هر دو probe mock) | ترتیب و توقف گام‌ها؛ انتخاب probe با بیت `Xui`/`UserManager`؛ پورت forward به گام `service`؛ فرم ذخیره‌نشده در برابر ردیف ذخیره‌شده؛ 403؛ هیچ رازی در پاسخ | ✅ |
| قرارداد | `XuiApiTransportTest` (scripted handler موجود؛ همه‌ی تست‌های `Direct` بدون تغییر) | در حالت تانل URL پایه `http://127.0.0.1:<port>/<base>/` (با و بدون `/` انتهایی)؛ تکرار یک‌بار روی مرگ تانل فقط برای مسیرهای خواندنی و `login`؛ سقف ۵ send — شروع سرد هم مرگ تانل و 401 را برمی‌گرداند و ارسال ششم هرگز رخ نمی‌دهد؛ بدون تکرار برای نوشتن/timeout/کد وضعیت/رد لاگین و در `Direct` | ✅ |
| سناریو | `ServerCrudTest` (host واقعی + MSSQL) | policy ادمین روی `test-connection` و `host-checks` (۴۰۳ برای کاربر و مدیر)؛ شکل پاسخ `host-checks`؛ توقف تست اتصال در tcp بسته بدون ذخیره‌ی چیزی؛ بایند `"Transport": "SshTunnel"` از بدنه‌ی JSON و قاعده‌های تانل به‌صورت 400 | ✅ ۸/۸ |
| واحد | `HostCapabilityChecksTest` (D11) | G1 با یک listener لوکال به‌عنوان مقصد (pass) و یک پورت بسته (fail)؛ G2 روی loopback؛ مقصدها از `HostCheckTargets` و ردیف‌های سرور بدون تکرار؛ `no-targets`؛ خط‌های `host-check` بدون هیچ راز؛ `StartAsync` پاسخ‌دهنده‌ی مقصد را منتظر نمی‌ماند؛ `HostCheckTargets` نامعتبر روی start شکست می‌خورد. هیچ سرور زنده‌ای در کار نیست | ✅ |
| اینتگریشن | `XuiPanelTunnelTest` روی `Xui-Test` | `XuiApiService` واقعی از تانل: login، list، add/update/del کلاینت؛ pin غلط → شکست قطعی؛ exec با کلید محدود → `false` | ✅ ۲۰۲۶-۱۱-۲۷، دو اجرای پیاپی |
| اینتگریشن | `MikrotikTunnelTest` روی `Mikrotik-Base` | کلید + پین؛ `ForwardedPortLocal` از داخل RouterOS؛ `Tik4NetHandler` روی پورت forward؛ `test`/`policy` رد می‌شود؛ کدام کانال خطای RouterOS را می‌آورد | ✅ ۲۰۲۶-۱۱-۲۷، دو اجرای پیاپی؛ کانال خطا در §۴.۳ |

fixture کانال روی VMهای لوکال تابع‌های اسکریپت است، نه ویرایش `.rsc` (D5): `apply_tunnel_fixture` / `apply_pki` / `write_host_key_pin` / `verify_tunnel_fixture` را `setup`، `provision` و `refresh` هر دو اسکریپت صدا می‌زنند. `provision` روی VM موجود اعمال می‌کند؛ `refresh` از `clean-test-state` برمی‌گرداند، fixture را اعمال و verify می‌کند، و همان نام اسنپ‌شات را با take → delete → rename عوض می‌کند تا VM هیچ لحظه‌ای بدون اسنپ‌شات نماند. کلید خصوصی و پین در XDG data dir می‌مانند و از `TestVmOptions` خوانده می‌شوند. سرویس `api` روی VM تستی برای شبکه‌ی host-only باز می‌ماند (چک آماده‌باش `MikrotikInitializer` به آن وابسته است)؛ محدودکردن آن به loopback انتخاب مالک زیرساخت در تولید است. فایل‌های `mikrotik-base.rsc` / `mikrotik-scenario.rsc` دست نخورده‌اند.

ردیف‌های مربوط در `Docs/18` §4 و در صورت لزوم شکاف تازه در §5 در همان PR (قاعده‌ی §7 همان سند).

**سناریوهای الزامی روی سرورهای تستی (خواست مالک، ۲۰۲۶-۱۱-۲۶):** هر کلاس اینتگریشن با چند سناریو نوشته شود، نه یک مسیر خوش‌شانس؛ همه فقط روی VMهای لوکال (Docs/23 D1).

`XuiPanelTunnelTest` روی `Xui-Test` (کلید forward-only روی کاربر تستی نصب باشد):

1. مسیر کامل: login، `xui/API/inbounds/`، `addClient` → دیده‌شدن در list → `updateClient` → `resetClientTraffic` → `delClient` → نبودن در list.
2. `onlines` با یک کلاینت آنلاین و یک کلاینت آفلاین.
3. پین غلط → قطع شدن **پیش از** احراز هویت و نرسیدن هیچ درخواستی به سرویس؛ پین درست و کلید غلط → رد شدن احراز هویت.
4. exec با کلید forward-only → `Execute` `false` (خروجی خالی، exit 1)؛ forward به پورت غیرمجاز → خطای صریح، بدون retry کور.
5. افت جلسه وسط کار: بستن اجباری جلسه‌ی SSH → درخواست بعدی روی جلسه‌ی تازه با `Generation` جدید ادامه می‌یابد؛ درخواست نوشتاریِ نیمه‌کاره retry نمی‌شود.
6. دو درخواست هم‌زمان روی یک سرور → هر دو از یک جلسه، بدون خطای هم‌زمانی.
7. `BasePath` با و بدون `/` انتهایی → یک URL.
8. fixture در دو شکل: `authorized_keys` سالم، و `authorized_keys` با خط خالی اول و بدون newline انتهایی (شکلی که روی یک میزبان تولید دیده شد — پیوست B).

`MikrotikTunnelTest` روی `Mikrotik-Base` (کاربر تستی در group پنج‌policy با کلید import‌شده، `forwarding-enabled=local`، CA و template گواهی تستی):

1. کلید + پین؛ `ForwardedPortLocal` به پورت API؛ `Tik4NetHandler` روی پورت forward با `OpenAsync`.
2. عملیات واقعی پنل با همان group: `/certificate add` از روی template، `/certificate sign` با CA تستی → گواهی با CA درست در `/certificate print`؛ سپس `/certificate export-certificate` با passphrase و `:put [/file get … contents]`؛ پاک‌سازی کامل در teardown.
3. `/ppp active print` با `where` → خالی یا یک ردیف؛ `/ppp active remove` روی id ناموجود → خطای RouterOS به‌صورت شکست برگردد، نه موفقیت با خروجی خالی. **۲۰۲۶-۱۱-۲۷:** شکل‌های کد پنل روی گروه پنج‌policy سبز شدند (`*FFF` → `no such item` و `Execute` برابر `false`؛ `remove [find session-id=…]` بی‌نتیجه بدون خطا تمام می‌شود). نشست واقعی برپا نشد — پایین، «در اولین استفاده‌ی واقعی دیده می‌شود».
4. `/user-manager/user` با print/add/set/remove روی یک کاربر تستی؛ `/user-manager/session close-session` روی id ناموجود → خطا سطح‌بندی شود. **۲۰۲۶-۱۱-۲۷:** `SyncUserAndActive` / `ChangeVpnPassword` / `DeactivateUser` / `ActivateUser` / `RemoveUsers` با گروه پنج‌policy سبز شدند. `close-session` با id ناموجود دستور را می‌شناسد (نه `no such command`) و `TikCommandTrapException` با متن `missing =.id=` برمی‌گرداند، نه `TikNoSuchItemException` و نه موفقیت خاموش — `.id` روی `SessionModel` فقط‌خواندنی است و این تله یعنی کلمه‌ی `.id` به روتر نرسیده. بستن نشست واقعی همان بند ۳ است.
5. رد شدن policyها با همان کاربر: `/tool ping` → `not enough permissions` و `Execute` `false`؛ `/user group set` → رد.
6. کانال خطا: هر خطای RouterOS روی کدام استریم می‌آید (stdout یا stderr) و `SshCommand.ExitStatus` چه می‌شود؛ نتیجه در `SshConnection.Execute` (§۴.۳) اعمال و در سند ثبت شود.
7. فرمان‌های `print`: اگر پنل `where` می‌سازد، `without-paging` **قبل از** `where` باشد — روی RouterOS 7.21.4 شکلِ `… where … without-paging` جواب خالی می‌دهد. **۲۰۲۶-۱۱-۲۷، RouterOS 7.20.6 (`Mikrotik-Base`)**: همان رفتار. `print where name=… without-paging` فقط سرستون را چاپ کرد (صفر ردیف) و `print without-paging where name=…` همان کاربر را برگرداند. کد پنل امروز `without-paging` نمی‌سازد؛ ترتیب بالا برای روزی است که بسازد.
8. متد همزمان `ApiConnection.Open` برای `ApiSsl` روی .NET 8 + OpenSSL 3 → `no protocols available`؛ یک تست که مصرف آن را آشکار کند. **۲۰۲۶-۱۱-۲۷:** `Tik4NetHandlerTest.TikConnectionOpener_NeverUsesTheSynchronousOpen` با mock از نوع Strict فقط `OpenAsync` را می‌پذیرد. تست زنده‌ی `ApiSsl` روی VM assert نمی‌شود.

**نشست PPP واقعی — در اولین استفاده‌ی واقعی دیده می‌شود (۲۰۲۶-۱۱-۲۷، `Mikrotik-Base`، RouterOS 7.20.6).** روی وضعیت قابل‌دورریختن، پیش از `refresh`: پروفایل PPP تستی، `/ppp aaa set use-radius=yes` (روی 7.20.6 ویژگی `use-radius` مال پروفایل نیست و `expected end of command` می‌دهد)، سرور L2TP بدون IPsec، `/radius` به `127.0.0.1`، مدخل `/user-manager router` با همان secret، یک کاربر user-manager با `profile-25G-10d`، و کلاینت L2TP با `connect-to=192.168.56.11` و بعد `127.0.0.1`. کلاینت `connecting` را لاگ کرد و همان ثانیه `terminating... - session closed` شد. هیچ خط RADIUS، هیچ ردیف `/ppp active` و هیچ `/user-manager session` دیده نشد. VM چهارم ساخته نشد. سه سناریو — ردیف واقعی در `GetActivePppConnections` و `CloseConnection` با session id، بستن نشست user-manager با نام کاربر، و دیدن همان نشست در `/ppp active` — در اولین استفاده‌ی واقعی دیده می‌شوند. `refresh` وضعیت تمیز را برگرداند؛ `use-radius` داخل اسنپ‌شات نیست.

`HostCapabilityChecksTest` (D11): در جدول بالا.

## ۷. شواهد — چه چیزی اثبات شده و چه چیزی نه

| ادعا | وضعیت | کجا / چگونه |
|---|---|---|
| SSH.NET 2025.0.0 با کلید ed25519 و پین SHA256 به RouterOS 7.21.4 لاگین می‌کند؛ پین محاسبه‌شده همان اثر انگشت OpenSSH است | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | یک برنامه‌ی C# مستقل با همان کتابخانه، فرمان فقط‌خواندنی روی روتر تولید مالک زیرساخت (رکورد سمت زیرساخت) |
| SSH.NET + کلید + پین + `ForwardedPortLocal` + `SocketsHttpHandler.ConnectCallback` به OpenSSH و پنل x-ui می‌رسد | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | روی `Xui-Test` (Debian 12، پنل روی پورت ۲۰۸۷): پاسخ `200` از داخل تانل |
| با کلید محدود (`command="/bin/false"`)، exec توسط SSH.NET رد می‌شود و forward فقط به پورت مجاز باز می‌شود | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | روی میزبان x-ui تولید مالک زیرساخت با خط نصب‌شده، از ترمینال توسعه با همین کتابخانه‌ها: exec خالی با exit 1؛ forward به پورت دیگر رد شد؛ لاگین واقعی x-ui و `xui/API/inbounds/` از داخل تانل `200` (رکورد سمت زیرساخت). `XuiPanelTunnelTest` همچنان برای رگرسیون لازم است |
| `ForwardedPortLocal` از داخل RouterOS کار می‌کند و لاگین tik4net از آن می‌گذرد | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | روی روتر تولید مالک زیرساخت (RouterOS 7.21.4، `forwarding-enabled=local`) با کاربر و کلید پنل، از ترمینال توسعه: forward به پورت API-SSL و خواندن identity (رکورد سمت زیرساخت). `MikrotikTunnelTest` همچنان برای رگرسیون لازم است |
| پنج policy برای نیمه‌ی SSH کار پنل روی روتر کافی است (read، write، export، file read؛ رد شدن `test` و `policy`) | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | با کاربر پنل روی روتر تولید، فقط‌خواندنی/آزمایشی با پاک‌سازی؛ لاگین API و یک خواندن از داخل تانل هم با آن group ثابت شد؛ `/certificate sign` و `/ppp active remove` هنوز با آن اجرا نشده‌اند |
| OpenSSH 9.6 (ایمیج Ubuntu 24.04) forwarding را پیش‌فرض روشن دارد و drop-in موجود آن را نمی‌بندد | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | `sshd -T` روی دو میزبان x-ui تولید: `allowtcpforwarding yes`، `permitopen any`، `gatewayports no` |
| RouterOS 7.21.4 خاصیت `forwarding-enabled` دارد (پیش‌فرض `no`) و اتصال خود روتر به پورت API روی loopback توسط input فایروال drop نمی‌شود | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | `/ip ssh print` و probe لوپ‌بک روی روتر تولید |
| خود API میکروتیک احراز هویت با کلید ندارد (فقط نام کاربری/پسورد) | مستند | MikroTik «API» |
| import کلید در RouterOS می‌تواند `password-authentication` را بیندازد | یک‌بار دیده شد (۲۰۲۶-۱۰-۲۳)؛ در اجرای ۲۰۲۶-۱۱-۲۶ رخ نداد | رکورد سمت زیرساخت |
| متد همزمان `ApiConnection.Open` برای `ApiSsl` فقط TLS 1.0 تحمیل می‌کند (`SslProtocols.Tls`) و روی .NET 8 + OpenSSL 3 با `SSL routines::no protocols available` شکست می‌خورد | یک‌بار دیده شد (۲۰۲۶-۱۱-۲۶) | در همان برنامه‌ی اثبات، پیش از رسیدن هر بایتی به روتر. `Tik4NetHandler` پنل از `OpenAsync` استفاده می‌کند که پیش‌فرض سیستم را می‌گوید و کار می‌کند — در حالت تانل فقط `OpenAsync`، یا اصلاح tik4net |

این اثبات‌ها از ترمینال توسعه انجام شدند، نه از میزبان پنل: سرورها و کتابخانه‌های پنل را ثابت می‌کنند، نه محدودیت‌های هاستینگ. G1–G3 را D11 (§۴.۱۱) در اولین انتشار با لاگ می‌سنجد. متن کامل برنامه‌ی اثبات پیوست A است و گزارش هر اجرا پیوست B.

## ۸. ترتیب اجرا و معیار پذیرش

| فاز | کار | معیار پذیرش |
|---|---|---|
| ۰ | ارتقای `Renci.SshNet` به 2026.0.0 | build بدون `NU1903`؛ همه‌ی تست‌ها سبز |
| ۱ | `ServerTransport`، فیلدهای `SshConfig`، `ISshTunnelPool` + ثبت singleton، `SshHandler`/`SshConnection` (کلید، پین، ExitStatus، الگوی RouterOS) | تست‌های واحد §۶ سبز؛ `Direct` بدون تغییر (`ProcessServiceTest` و تست‌های میکروتیک فعلی سبز)؛ روی `Mikrotik-Base` ثبت شود که خطای RouterOS از کدام کانال می‌آید |
| ۲ | `IXuiHttpClientProvider` + `XuiApiService` + retry؛ کلید محدود روی `Xui-Test` و `XuiPanelTunnelTest` | کلاینت واقعی از تانل؛ pin غلط → شکست قطعی؛ exec با کلید محدود → `false` |
| ۳ | `Tik4NetHandler` در حالت تانل؛ fixture میکروتیک + `MikrotikTunnelTest` | لاگین API از داخل تانل؛ رد شدن policyهای بسته |
| ۴ | اعتبارسنجی `ServerCrudApplication`، `test-connection`، فرانت `/servers` | ۷ قاعده؛ گام‌ها؛ discover؛ بدون راز در پاسخ/لاگ |
| ۴‌ب | مستندات همان PR: `Docs/07`، `08`، `09`، `12`، `18`، `19`، بستن §5.1 `Docs/24`، `AGENTS.md` | — |
| ۴‌ج | D11: `HostCapabilityChecks` + `HostCheckTargets`/`HostCheckHeartbeatMinutes` + `POST /api/server/host-checks` + `HostCapabilityChecksTest` + گام اولین انتشار در Docs/19 | در اولین انتشار، لاگ یک خط `host-check summary` با `g1=pass g2=pass` برای همه‌ی مقصدها دارد؛ G1/G2 قرمز پیش از آنکه ردیفی فعال شود دیده می‌شود |
| ۵ | آنبوردینگ تولید: پیش‌نیازهای §۵ آماده است؛ ردیف هر سرور روی `SshTunnel`؛ تست اتصال روی هاست واقعی سبز؛ سپس فعال‌سازی ردیف | تست اتصال پنج گام سبز؛ اولین poll ترافیک و اولین job ساعتی بدون خطا |

**وضعیت (۲۰۲۶-۱۱-۲۷)**: فازهای ۰، ۱، ۲، ۳، ۴، ۴‌ب و ۴‌ج در کد هستند. `XuiPanelTunnelTest` و `MikrotikTunnelTest` روی fixture لوکال دو بار پیاپی سبز شدند (۳۱ تست با تست واحد `TikConnectionOpener_NeverUsesTheSynchronousOpen`). باز مانده: فاز ۵ (آنبوردینگ تولید) و سه سناریوی نشست واقعی پایین §۶. build بدون `NU1903` است (SSH.NET 2026.0.0).

## ۹. موارد باز

1. **Forward پایدار در برابر forward به‌ازای هر اتصال**: D6 یک listener پایدار روی loopback (پورت تصادفی) به‌ازای هر جلسه می‌سازد؛ ساده و بی‌نشت. اگر روزی خواستید پنجره‌ی exposure روی هاست اشتراکی را ببندید، نسخه‌ی به‌ازای هر اتصال (forward داخل `ConnectCallback`، stop با dispose استریم) کار آینده است.
2. **UI ساخت‌یافته برای کلید/پین**: انتخابگر transport، قالب تانل و دکمه‌ی «ثبت اثر انگشت» آماده است؛ فیلدهای ساخت‌یافته به‌جای JSON خام (textarea کلید، پورت، پین) کار آتی است.
3. **`ConfigMaxLength = 4000`**: با کلید ed25519 کافی است؛ اگر روزی چند کلید یا فیلد بیشتر شد، سقف و ستون دیتابیس بازبینی شود.
4. **`ssh-keyscan` و RouterOS**: پین فقط از discover یا رکورد مالک زیرساخت.
5. **پسورد SSH در حالت Direct**: مثل امروز می‌ماند (سرورهایی که سرویسشان از بیرون در دسترس است).
6. **مجرای خروجی TCP از هاست پنل و دو شرط دیگر میزبان (G1–G3)**: با D11 در اولین انتشار با لاگ بررسی می‌شود (§۴.۱۱)؛ فقط تأیید شفاهی هاستینگ باز می‌ماند.
7. **پیاده‌سازی**: فازهای ۰ تا ۴‌ج §۸ در کد است و تست‌های VM §۶ روی fixture لوکال سبز شده‌اند (۲۰۲۶-۱۱-۲۷). باقی‌مانده: فاز ۵ (آنبوردینگ تولید). ردیف بدون `Transport` همچنان مسیر مستقیم و با پسورد دارد.
8. **ردیف‌های seed سرور**: شکل فعلی مسیر قدیمی است و رازها را متن ساده در دیتابیس می‌گذارد (§۴.۱۲) — بازنویسی بعد از فاز ۴؛ ورود `Config` از API ادمین، یا یک ابزار «حالت محافظت‌شده» در `Scripts/deploy.sh`.
9. **سناریوهای تست روی VMهای لوکال** (§۶): کلاس‌های اینتگریشن نوشته شده‌اند و روی اسنپ‌شات فریزشده سبز شده‌اند (۲۰۲۶-۱۱-۲۷). سه عمل میکروتیک با گروه پنج‌policy اجرا شد. کانال خطای RouterOS روی `Mikrotik-Base` ثبت شد (§۴.۳): stdout، exit status ۰ یا ۱. سه سناریوی نشست واقعی PPP در اولین استفاده‌ی واقعی دیده می‌شوند (بالای §۶). فاز ۵ باز است.
10. **اولین اتصال از نشانی خود پنل** (`from=` روی خطوط میزبان‌های x-ui و whitelist پورت SSH روی روتر): فقط تست اتصال خود پنل روی هاست واقعی می‌تواند آن را نشان دهد.

## ۱۰. ارجاع‌ها

- انزوای تست و VMهای لوکال: [Docs/23-xui-local-test-vm.md](23-xui-local-test-vm.md)
- قرارداد API x-ui و مدل ترافیک: [Docs/17-xui-traffic.md](17-xui-traffic.md)، [Docs/13-xray-integration.md](13-xray-integration.md)
- هندلرهای ServerBridge: [Docs/07-server-bridge.md](07-server-bridge.md)
- هاست پنل و پیش‌نیازهای دیپلوی: [Docs/19-deploy-runbook.md](19-deploy-runbook.md)
- آیتم باز «پنل‌های loopback-only»: [Docs/24-server-capacity-realm-title.md](24-server-capacity-realm-title.md) §5.1
- حاکمیت پوشش تست: [Docs/18-test-coverage.md](18-test-coverage.md) §7
- منابع بیرونی (خوانده‌شده ۲۰۲۶-۱۱-۲۶): `sshd(8)` OpenBSD (گزینه‌های `authorized_keys`)؛ مستندات MikroTik «SSH»، «User» و «Certificates»؛ سورس SSH.NET تگ 2025.0.0 (`ConnectionInfo.cs`، `ForwardedPortLocal.cs`، `HostKeyEventArgs.cs`)؛ GitHub Advisory Database (CVE-2026-85756، CVE-2026-48798)
- برنامه‌ی اثبات و گزارش آن: پیوست A و پیوست B همین سند

## پیوست A — برنامه‌ی اثبات (متن کامل، بدون هیچ مقدار محرمانه)

همان برنامه‌ای که پیوست B گزارش اجرایش است: یک پروژه‌ی کنسول خارج از این ریپو، روی همان کتابخانه‌های پنل (SSH.NET 2025.0.0 و `tik4net.dll` ساخته‌شده‌ی خود پنل). **الگوی پیاده‌سازی است، نه بخشی از برنامه‌ی پنل** — به کد برنامه دست زده نمی‌شود؛ هر چیزی از اینجا که وارد کد شود از مسیر فازهای §۸ می‌گذرد. میزبان، پورت، کلید و پین از آرگومان می‌آید؛ پسورد و اعتبار از فایل خوانده می‌شوند و هرگز چاپ نمی‌شوند. در فهرست زیر دو چیز نسبت به نسخه‌ی اجراشده خنثی شده تا نامی از زیرساخت در آن نباشد: برچسب فایل backup در `addkey`/`removekey` و مسیر مطلق `tik4net.dll` در csproj.

`ssh-poc.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SSH.NET" Version="2025.0.0" />
    <Reference Include="tik4net">
      <HintPath><clone>/Backend/Tik4net/tik4net/bin/Debug/net8.0/tik4net.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

`Program.cs`:

```csharp
// Proof program for Docs/25 (2026-11-26): the panel-side half of the SSH channel, in C# on the panel's
// own libraries (SSH.NET 2025.0.0 + the panel's tik4net.dll). Nothing in this file is a secret:
// passwords, credentials and cookies are read from files inside the process and only statuses print.
//
// modes (options are key=value arguments):
//   exec        one command over SSH                          (command=)
//   http        local forward + HTTP through it               (targetport=, path= | secretfile=)
//   routerapi   local forward + RouterOS API login (tik4net)  (apiport=, apitype=api|api-ssl, apipasswordfile=)
//   addkey      append the panel's forward-only line to /root/.ssh/authorized_keys (pub=, permitport=, [fromip=])
//   removekey   restore /root/.ssh/authorized_keys from a backup (backup=<file name under /root>)
//
// common: host= port= user= key=<private key file> pin=SHA256:…

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Renci.SshNet;
using Renci.SshNet.Common;
using tik4net;

var options = args.Select(a => a.Split('=', 2)).ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "");
var mode = options.GetValueOrDefault("mode", "");
string Opt(string name) => options.GetValueOrDefault(name, "");
string Req(string name) => Opt(name).Length > 0 ? Opt(name) : throw new ArgumentException($"missing {name}=");

var host = Req("host");
var port = int.Parse(Req("port"));
var user = Req("user");
var pin = Opt("pin");

using var client = Connect(host, port, user, Opt("key"), Opt("password"), pin);

switch (mode)
{
    case "exec":
    {
        using var command = client.CreateCommand(Req("command"));
        var output = command.Execute();
        Console.WriteLine($"exit={command.ExitStatus} output={output.Trim().Replace("\n", " | ")}");
        break;
    }

    case "http":
    {
        var targetPort = int.Parse(Req("targetport"));
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectCallback = (_, ct) => OpenForwardedStreamAsync(client, targetPort, ct),
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };

        if (Opt("secretfile").Length > 0)
        {
            await XuiLoginAsync(http, targetPort, Opt("secretfile"));
        }
        else
        {
            var path = Opt("path").Length > 0 ? Opt("path") : "/";
            using var response = await http.GetAsync($"http://tunnel:{targetPort}{path}");
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase} through the forward, body bytes={body.Length}");
        }
        break;
    }

    case "routerapi":
    {
        var apiPort = int.Parse(Req("apiport"));
        var apiType = Opt("apitype") == "api-ssl" ? TikConnectionType.ApiSsl : TikConnectionType.Api;
        var password = File.ReadAllText(Req("apipasswordfile")).Trim();

        var forward = new ForwardedPortLocal("127.0.0.1", 0, "127.0.0.1", (uint)apiPort);
        client.AddForwardedPort(forward);
        forward.Start();
        Console.WriteLine($"forward 127.0.0.1:{forward.BoundPort} -> 127.0.0.1:{apiPort} ({apiType})");

        using var api = ConnectionFactory.CreateConnection(apiType);
        // OpenAsync, as the panel's Tik4NetHandler does: the synchronous Open forces TLS 1.0 for ApiSsl
        // (SslProtocols.Tls) and fails on .NET 8 + OpenSSL 3 with "no protocols available".
        await api.OpenAsync("127.0.0.1", (int)forward.BoundPort, user, password);
        var command = api.CreateCommand("/system/identity/print");
        Console.WriteLine($"router-api name={command.ExecuteScalar("name")}");
        api.Close();
        forward.Stop();
        break;
    }

    case "addkey":
    {
        var pub = File.ReadAllText(Req("pub")).Trim();
        var comment = pub[(pub.LastIndexOf(' ') + 1)..];
        var from = Opt("fromip").Length > 0 ? "from=\"" + Opt("fromip") + "\"," : "";
        var line = from + "restrict,port-forwarding,permitopen=\"127.0.0.1:" + int.Parse(Req("permitport"))
                   + "\",command=\"/bin/false\" " + pub;
        var script = string.Join("\n", new[]
        {
            "set -e",
            "AUTH=/root/.ssh/authorized_keys",
            $"if grep -q ' {comment}$' \"$AUTH\"; then echo already-present; exit 0; fi",
            "cp -p \"$AUTH\" \"/root/authorized_keys.bak-panel-$(date +%Y%m%d)\"",
            // a file without a trailing newline would get the new line glued onto the previous key's
            // comment (seen once on a production host) — add the newline first
            "if [ -n \"$(tail -c1 \"$AUTH\")\" ]; then echo >> \"$AUTH\"; fi",
            $"printf '%s\\n' '{line}' >> \"$AUTH\"",
            "echo \"lines=$(wc -l < \"$AUTH\")\"",
        });
        using var command = client.CreateCommand(script);
        var output = command.Execute();
        Console.WriteLine($"addkey exit={command.ExitStatus} output={output.Trim().Replace("\n", " | ")}");
        break;
    }

    case "removekey":
    {
        var script = string.Join("\n", new[]
        {
            "set -e",
            "AUTH=/root/.ssh/authorized_keys",
            "BAK=/root/" + Req("backup"),
            "if [ -f \"$BAK\" ]; then cp -p \"$BAK\" \"$AUTH\"; fi",
            "echo \"lines=$(wc -l < \"$AUTH\")\"",
        });
        using var command = client.CreateCommand(script);
        var output = command.Execute();
        Console.WriteLine($"removekey exit={command.ExitStatus} output={output.Trim().Replace("\n", " | ")}");
        break;
    }

    default:
        throw new ArgumentException("unknown mode");
}

client.Disconnect();

static SshClient Connect(string host, int port, string user, string keyPath, string password, string pin)
{
    AuthenticationMethod authentication = keyPath.Length > 0
        ? new PrivateKeyAuthenticationMethod(user, new PrivateKeyFile(keyPath))
        : new PasswordAuthenticationMethod(user, password);

    var client = new SshClient(new ConnectionInfo(host, port, user, authentication) { Timeout = TimeSpan.FromSeconds(20) });
    string? seen = null;
    client.HostKeyReceived += (_, e) =>
    {
        seen = "SHA256:" + e.FingerPrintSHA256;
        if (pin.Length > 0)
        {
            e.CanTrust = string.Equals(seen, pin, StringComparison.Ordinal);
        }
    };
    client.Connect();
    Console.WriteLine($"connected {user}@{host}:{port} host-key={seen}{(pin.Length > 0 ? " (pinned)" : " (NOT pinned)")}");
    return client;
}

static async ValueTask<Stream> OpenForwardedStreamAsync(SshClient client, int targetPort, CancellationToken token)
{
    var forward = new ForwardedPortLocal("127.0.0.1", 0, "127.0.0.1", (uint)targetPort);
    client.AddForwardedPort(forward);
    forward.Start();
    var tcp = new TcpClient();
    await tcp.ConnectAsync(IPAddress.Loopback, (int)forward.BoundPort, token);
    Console.WriteLine($"forward 127.0.0.1:{forward.BoundPort} -> 127.0.0.1:{targetPort}");
    return tcp.GetStream();
}

// Mirrors XuiApiService.LoginAsync of the panel: POST {base}login with JSON {username,password}; the
// envelope is {success,msg,obj}; the session cookie is the first part of Set-Cookie.
static async Task XuiLoginAsync(HttpClient http, int targetPort, string secretFile)
{
    var fields = File.ReadLines(secretFile)
        .Select(line => line.Split(':', 2))
        .Where(p => p.Length == 2)
        .GroupBy(p => p[0].Trim(), StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First()[1].Trim());

    var basePath = fields.GetValueOrDefault("WebBasePath") ?? "/";
    if (!basePath.StartsWith('/')) basePath = "/" + basePath;
    if (!basePath.EndsWith('/')) basePath += "/";
    var baseUrl = $"http://tunnel:{targetPort}{basePath}";

    using var login = new HttpRequestMessage(HttpMethod.Post, baseUrl + "login")
    {
        Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                username = fields.GetValueOrDefault("Username") ?? "",
                password = fields.GetValueOrDefault("Password") ?? "",
            }),
            Encoding.UTF8, "application/json"),
    };
    using var response = await http.SendAsync(login);
    var body = await response.Content.ReadAsStringAsync();
    var success = false;
    try
    {
        success = JsonDocument.Parse(body).RootElement.GetProperty("success").ValueKind == JsonValueKind.True;
    }
    catch (Exception)
    {
    }

    var hasCookie = response.Headers.TryGetValues("Set-Cookie", out var cookies);
    var cookiePair = hasCookie ? string.Join("; ", cookies!.Select(c => c.Split(';').First())) : "";
    Console.WriteLine($"x-ui login HTTP {(int)response.StatusCode} success={success} cookie={hasCookie} body-bytes={body.Length}");
    if (!response.IsSuccessStatusCode || !success || !hasCookie)
    {
        return;
    }

    using var list = new HttpRequestMessage(HttpMethod.Get, baseUrl + "xui/API/inbounds/");
    list.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
    list.Headers.TryAddWithoutValidation("Cookie", cookiePair);
    using var listed = await http.SendAsync(list);
    var listBody = await listed.Content.ReadAsStringAsync();
    var count = -1;
    try
    {
        count = JsonDocument.Parse(listBody).RootElement.GetProperty("obj").GetArrayLength();
    }
    catch (Exception)
    {
    }

    Console.WriteLine($"x-ui inbounds HTTP {(int)listed.StatusCode} count={count}");
}
```

## پیوست B — گزارش اثبات، ۲۰۲۶-۱۱-۲۶

همه‌ی اجراها از ترمینال توسعه انجام شدند، با برنامه‌ی پیوست A؛ مقصدها میزبان‌های تولید مالک زیرساخت بودند (نام و نشانی آن‌ها در رکورد سمت زیرساخت است) به‌جز ردیف ۲ که روی VM لوکال `Xui-Test` بود. این اجراها سرورها و کتابخانه‌های پنل را ثابت می‌کنند؛ میزبان پنل (G1–G3) نه — آن با D11 در اولین انتشار سنجیده می‌شود. در ستون خروجی، `<identity>` نام دستگاه روتر و `<n>` شماره‌ی پورت تصادفی forward است.

| # | زمان | حالت | مقصد و کلید | خروجی | نتیجه |
|---|---|---|---|---|---|
| ۱ | شب | `exec` | روتر MikroTik (RouterOS 7.21.4)، کاربر پنل در group پنج‌policy، کلید ed25519 + پین | `connected … host-key=SHA256:… (pinned)`؛ `exit=0 output=name: <identity>` | ✅ لاگین با کلید و پین |
| ۲ | شب | `http` با `path=/` | `Xui-Test` (Debian 12، پنل روی ۲۰۸۷) | `forward 127.0.0.1:<n> -> 127.0.0.1:2087`؛ `HTTP 200 … body bytes=9652` | ✅ forward + HTTP |
| ۳ | ~۱۱:۱۰ | `routerapi` با `api-ssl` | روتر، بعد از روشن‌شدن forwarding | تلاش اول: `TikConnectionSSLErrorException … SSL routines::no protocols available` (سمت کلاینت، پیش از رسیدن هر بایتی به روتر). با `OpenAsync`: `forward 127.0.0.1:<n> -> 127.0.0.1:8729 (ApiSsl)`؛ `router-api name=<identity>` | ✅ |
| ۴ | ~۱۱:۱۵ | `exec`، `http` | میزبان x-ui اول، با خط کلید محدود نصب‌شده | `exit=1 output=` (خالی)؛ پورت غیرمجاز: چهار تلاش و هر بار `The response ended prematurely`؛ پورت پنل: `x-ui login HTTP 200 success=True cookie=True`، `x-ui inbounds HTTP 200 count=2` | ✅ |
| ۵ | ~۱۱:۱۵ | OpenSSH کلاینت | همان میزبان، همان کلید | `404` از داخل تانل (ریشه‌ی `/`، چون پنل base path دارد) | ✅ |
| ۶ | ~۱۴:۰۲ | مثل ۴ | میزبان x-ui دوم | همان سه نتیجه؛ append در تلاش اول درست | ✅ |
| ۷ | ~۱۴:۰۵ | `routerapi` با `api` | روتر، سرویس ساده‌ی API فقط برای خود روتر | `forward 127.0.0.1:<n> -> 127.0.0.1:8728 (Api)`؛ `router-api name=<identity>` — دو بار، پیش و پس از rule فایروال | ✅ |
| ۸ | ~۱۴:۰۵ | `curl` از بیرون | پورت ساده‌ی API روتر | پیش از rule فایروال: `000` و exit 52 (دست‌دادن TCP و بستن فوری)؛ پس از rule: `000` و exit 28 (timeout) و شمارنده‌ی rule = ۷ بسته | ✅ پورت از بیرون ساکت |

**یافته‌ها برای پیاده‌سازی پنل:**

1. **متد همزمان `ApiConnection.Open` برای `ApiSsl` فقط TLS 1.0 می‌پذیرد** (`SslProtocols.Tls`) و روی .NET 8 + OpenSSL 3 با `no protocols available` شکست می‌خورد. `Tik4NetHandler` پنل از `OpenAsync` استفاده می‌کند که پیش‌فرض سیستم را می‌گوید؛ در حالت تانل فقط `OpenAsync`.
2. **`authorized_keys` بدون newline انتهایی:** روی یکی از دو میزبان فایل با یک خط خالی شروع می‌شد و newline انتهایی نداشت؛ append ساده خط جدید را به انتهای comment کلید قبلی چسباند (کلید ترمینال همچنان کار می‌کرد؛ فایل از backup برگشت و درست اضافه شد). قانون برای `addkey`: اگر بایتهای آخر فایل newline نیست، اول یک newline اضافه کن — همان کاری که کد پیوست A می‌کند. چون پنل خودش خط اضافه نمی‌کند، این یافته مال رویه‌ی نصب مالک زیرساخت است؛ در test `XuiPanelTunnelTest` fixture باید همین حالت را هم بپوشاند.
3. **محدودیت `address` روی سرویس RouterOS جلوی دست‌دادن TCP را نمی‌گیرد:** از بیرون handshake کامل می‌شود و بعد سرویس ارتباط را می‌بندد. برای سکوت پورت، یک rule drop در فایروال لازم است (آخر زنجیره، تا شماره‌ی ruleهای اپراتور جابه‌جا نشود). پیامدش برای پنل: از سمت کلاینت، «سرویس مقصد ارتباط را بست» و «سرور اجازه‌ی forward نداد» (`permitopen`) یک شکل دارند — اتصال محلی پذیرفته می‌شود و بعد بسته. پس گام `forward` در تست اتصال (§۴.۷) فقط «کانال باز شد» را می‌گوید و این دو را از هم جدا نمی‌کند؛ تنها گام `service` (لاگین واقعی) مسیر را ثابت می‌کند. در گزارش تست، شکستِ `service` پس از `forward` موفق را با همین دو احتمال توضیح دهید.
4. **فرمان اشتباه RouterOS در exec غیرتعاملی با کلاینت OpenSSH، `ssh` را با exit 0 برمی‌گرداند و خطا فقط در متن خروجی است** (`expected end of command …`). برای پنل (§۴.۳) همین را در فاز ۱ روی `Mikrotik-Base` با SSH.NET ثبت کنید (`SshCommand.ExitStatus` و اینکه خطا روی کدام کانال می‌آید) و تا آن زمان هم `ExitStatus` و هم متن خروجی را بررسی کنید.
5. **`SshCommand.ExitStatus` با کلید `command="/bin/false"`:** خروجی خالی و `ExitStatus = 1` — همان چیزی که §۴.۳ برای `ISshConnection.Execute` می‌خواهد (`false`).

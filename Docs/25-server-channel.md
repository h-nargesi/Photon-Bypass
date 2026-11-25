# Docs/25-server-channel.md — کانال ارتباطی پنل با سرورها: تانل SSH با کلید برای x-ui و User Manager

> **English summary**: Server-channel decision record and implementation spec (owner directive, 2026-11-26). **Status: specified, not implemented.** The panel reaches every server over **one SSH session authenticated with an ed25519 private key and a pinned host key**; everything else rides that session: x-ui's HTTP API and MikroTik's API (User Manager) through a **local port-forward to the server's own loopback**, RouterOS commands through SSH exec as today. **D1** one channel (SSH + key), no TLS certificate and no public listener on the management path; **D2** x-ui is only ever addressed through its HTTP API, never its database; **D3** per-server `Transport` (`Direct` | `SshTunnel`) plus `PrivateKey`/`PassPhrase`/`HostKeySha256` on `SshConfig`; **D4** the host key is pinned and a mismatch fails closed; **D5** a singleton SSH session pool (lazy connect, keep-alive, idle eviction, reconnect, generation counter); **D6** one loopback forward per session and remote port; **D7** `XuiApiService` keeps its calls — only the transport changes; **D8** `Tik4NetHandler` opens tik4net on the forwarded port; **D9** an admin "test connection" endpoint that reports each step and discovers a host key; **D10** SSH.NET is upgraded to 2026.0.0 with the work. §4 is the implementation contract (config JSON, classes, validation, errors, logging, tests), §5 what must be ready on each server, §7 what is already proven and what is not, §8 the rollout order with acceptance criteria. No live server is ever touched by a test (Docs/23 D1).

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

`XuiApiService` همان فراخوانی‌ها، همان کوکی نشست و همان قرارداد پاسخ را نگه می‌دارد. تنها `HttpClient` در حالت تانل از یک provider مخصوص می‌آید که اتصال‌ها را از forward عبور می‌دهد (`SocketsHttpHandler.ConnectCallback`)، proxy را خاموش می‌کند و با تغییر `Generation` جلسه کلاینت را از نو می‌سازد.

### D8 — میکروتیک: exec با کلید، API روی پورت forward شده

`SshHandler` در حالت تانل با کلید لاگین می‌کند و فرمان‌ها را روی جلسه‌ی مشترک اجرا می‌کند. `Tik4NetHandler` بلافاصله پس از آن، tik4net را روی `127.0.0.1:<پورت forward>` باز می‌کند (`Api` یا `ApiSsl`، طبق `WebApiConfig.Ssl`) با نام کاربری و پسورد API. خود API میکروتیک احراز هویت با کلید ندارد؛ کلید، جلسه‌ی SSH را باز می‌کند و API از داخل آن می‌رود.

### D9 — تست اتصال (فقط ادمین)

`POST /api/server/test-connection` با بدنه‌ی `SaveServerModel` (پیکربندی ذخیره‌نشده هم قابل تست است) مراحل را جداگانه گزارش می‌کند: `tcp`، `host-key`، `auth`، `forward`، `service` — بدون هیچ مقدار محرمانه در پاسخ یا لاگ. اگر `HostKeySha256` خالی باشد، گام `host-key` اثر انگشت ارائه‌شده را برمی‌گرداند و **قبل از احراز هویت** می‌ایستد (discover). دکمه‌ی «تست اتصال» در صفحه‌ی `/servers` (ردیف و مودال ویرایش) همین endpoint را صدا می‌زند.

### D10 — ارتقای SSH.NET به 2026.0.0

NuGet برای SSH.NET 2025.0.0 دو advisory با شدت بالا نشان می‌دهد — CVE-2026-85756 (اجرای دستور از مسیر remote path در `ScpClient`) و CVE-2026-48798 (نوشتن فایل دلخواه در دانلود بازگشتی — recursive — `ScpClient`) — هر دو در 2026.0.0 رفع شده‌اند. پنل `ScpClient` ندارد و این کانال فقط `SshClient` و `ForwardedPortLocal` را به کار می‌برد؛ با این حال ارتقا در همین کار انجام شود تا هشدار `NU1903` از build برود.

## ۳. وضعیت امروز کد و چه چیزی تغییر می‌کند

| فایل | امروز | پس از پیاده‌سازی |
|---|---|---|
| `Domain/Servers/JsonType/SshConfig.cs` | `Username`، `Password`، `Port = 22` | + `PrivateKey`، `PassPhrase`، `HostKeySha256` |
| `Domain/Servers/JsonType/ServerConfiguration.cs` | `SshConfig`، `WebApiConfig`، `DataBaseInfo` | + `Transport` (با `JsonStringEnumConverter`) |
| `Domain/Servers/Types/` | — | + `ServerTransport.cs` (`Direct = 0`، `SshTunnel = 1`) |
| `ServerBridge/ServerBridgeOptions.cs` | چهار timeout | + `SshTunnelIdleSeconds` (پیش‌فرض ۹۰۰) |
| `ServerBridge/ServiceFactory.cs` | `ISshHandler`، `ITik4NetHandler` singleton | + `ISshTunnelPool` singleton |
| `ServerBridge/Ssh/SshHandler.cs` | پسورد، `SshClient` تازه در هر فراخوانی | انتخاب حالت؛ کلید + پین در تانل؛ اتصال از استخر |
| `ServerBridge/Ssh/SshConnection.cs` | فقط stderr را خطا می‌داند | + `ExitStatus`؛ + الگوی خطای RouterOS؛ مالکیت `SshClient` مشروط |
| `ServerBridge/Tik4net/Tik4NetHandler.cs` | اتصال مستقیم | اتصال به پورت forward در تانل |
| `XuiPanel/WebService/XuiApiService.cs` | `factory.CreateClient("xui-panel")` | کلاینت از `IXuiHttpClientProvider` |
| `XuiPanel/ServiceFactory.cs` | کلاینت نام‌دار `xui-panel` | + ثبت provider |
| `Application/Management/ServerCrudApplication.cs` | اعتبارسنجی اسکالرها | + قواعد transport/کلید/پین |
| `Portal/Controllers/ServerController.cs` | list/create/update/set-active | + `test-connection` |
| `Frontend/…/management/servers/*` | فرم JSON خام | + دکمه‌ی تست اتصال و نمایش مراحل |

## ۴. مشخصات پیاده‌سازی

### ۴.۱. پیکربندی

```csharp
public enum ServerTransport { Direct = 0, SshTunnel = 1 }

public class SshConfig
{
    public string Username { get; set; } = null!;   // اجباری
    public string Password { get; set; } = null!;   // در حالت SshTunnel برای پای SSH استفاده نمی‌شود
    public int Port { get; set; } = 22;
    public string? PrivateKey { get; set; }         // متن کامل کلید خصوصی (OpenSSH یا PEM)
    public string? PassPhrase { get; set; }         // اختیاری
    public string? HostKeySha256 { get; set; }      // "SHA256:<base64>" — در SshTunnel اجباری
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
- `WebApiConfig` = **نقطه‌ی اتصال سرویس از دید خود سرور**: `HostName = "127.0.0.1"`، `Port` = پورت سرویس روی loopback، `Username/Password` = اعتبار خود سرویس (لاگین x-ui، یا API میکروتیک). `BuildBaseUrl` و `LoginAsync` بدون تغییر روی همین مقادیر کار می‌کنند؛ فقط مقصد اتصال TCP عوض می‌شود.
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

### ۴.۲. استخر جلسه‌ها — `ISshTunnelPool`

```csharp
public interface ISshTunnelPool
{
    ValueTask<ISshTunnelSession> GetAsync(ServerEntity server, CancellationToken ct = default);
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
6. **خطا**: هر `SshException`/`SocketException`/`ObjectDisposedException` هنگام استفاده، جلسه را `Faulted` می‌کند و dispose؛ فراخوانی بعدی `GetAsync` یک‌بار وصل می‌شود. خطا به فراخوانی فعلی به‌صورت `UserException` برمی‌گردد — بدون retry کور برای عملیات نوشتاری.
7. **Forward**: `ForwardedPort` برای هر `remotePort` یک‌بار forward می‌سازد (idempotent، زیر قفل) و `BoundPort` را برمی‌گرداند؛ forwardها تا پایان جلسه زنده‌اند و با dispose جلسه بسته می‌شوند.

پیام‌های کاربر (سبک امروز):

| وضعیت | پیام | detail (فقط برای توسعه‌دهنده) |
|---|---|---|
| host key ناهمخوان | «کلید میزبان سرور با مقدار ثبت‌شده مطابقت ندارد!» | `Host key mismatch on {host}:{port}: presented {presented}, pinned {pinned}` |
| host key پین نشده | «برای این سرور کلید میزبان (host key) ثبت نشده است!» | `HostKeySha256 is empty; run the connection test to discover it` |
| احراز هویت | «احراز هویت SSH ناموفق بود!» | `SSH authentication failed on {host}:{port} as {username}` |
| اتصال | «خطای اتصال به سرور!» | `Cannot connect to {host}:{port} ({step}) after {elapsed} ms` |
| forward رد شد | «سرور اجازه‌ی عبور ترافیک از تانل را نداد!» | `Port-forward to 127.0.0.1:{remotePort} was refused (authorized_keys permitopen / RouterOS forwarding-enabled)` |

### ۴.۳. `SshHandler` و `SshConnection`

- `ConnectTo(server)`: `Direct` → مسیر امروز بدون تغییر. `SshTunnel` → `pool.GetAsync(server)` و ساخت یک `ISshConnection` روی `SshClient` جلسه. `Dispose` در حالت تانل فقط فرمان در حال اجرا را cancel می‌کند (جلسه مال استخر است).
- **فرمان‌ها روی یک جلسه سریالی شوند** (`SemaphoreSlim(1,1)` per جلسه برای exec): `SshClient` برای اجرای موازی فرمان از چند نخ تضمینی ندارد و فرمان‌های پنل کوتاه‌اند.
- **تشخیص خطا**: امروز `Execute` فقط `execution.Error` (stderr) را خطا می‌داند. باید: (الف) `ExitStatus >= 0` و غیر صفر → `false`؛ (ب) برای `OsType == Mikrotik`، الگوی خطای RouterOS در **خروجی**: `^failure: `, `expected end of command`, `not enough permissions`, `no such item`, `bad command name`, `syntax error` → `false` با همان متن در `result`. RouterOS exit status قابل اتکا نمی‌فرستد و خطاهایش متنی است (نمونه‌های دیده‌شده: `failure: unknown file type (/file/add; line 1)`، `not enough permissions (9) (/tool/ping; line 1)`، `expected end of command (line 1 column 15)`). کدام کانال (stdout یا stderr) این متن‌ها را می‌آورد باید در فاز ۱ روی `Mikrotik-Base` ثبت شود؛ تا آن زمان هر دو مسیر خطا فرض شوند.
- این تشخیص برای `MikrotikDirectService` حیاتی است: مثلاً `/certificate sign` که روی RouterOS شکست بخورد نباید با `result = "failure: …"` به‌عنوان موفق برگردد و بعداً همان متن به‌جای گواهی در فایل `.ovpn` بنشیند.

### ۴.۴. `Tik4NetHandler`

```csharp
// SshTunnel
var session = await pool.GetAsync(server);
var localPort = session.ForwardedPort(config.Port ?? (config.Ssl ? 8729 : 8728));
var connection = ConnectionFactory.CreateConnection(config.Ssl ? TikConnectionType.ApiSsl : TikConnectionType.Api);
await connection.OpenAsync("127.0.0.1", localPort, config.Username, config.Password);
return connection;   // مصرف‌کننده مثل امروز using می‌کند
```

`Direct` بدون تغییر. تایماوت `Tik4NetOpenTimeoutSeconds` مثل امروز. `ApiSsl` کتابخانه vendored هر گواهی‌ای را می‌پذیرد (`ApiConnection.ValidateServerCertificate`) — داخل تانل بی‌ضرر است (اعتماد از host key می‌آید)؛ اگر روزی مستقیم استفاده شد، pin کردن گواهی لازم است.

### ۴.۵. `XuiApiService` و provider کلاینت

```csharp
internal interface IXuiHttpClientProvider
{
    HttpClient Get(ServerEntity server);   // Direct: کلاینت نام‌دار «xui-panel»؛ SshTunnel: کلاینت همان سرور
}
```

- `SendAsync` به‌جای `factory.CreateClient(ServiceFactory.HttpClientKeyName)` از `http_provider.Get(server)` می‌گیرد.
- در حالت تانل برای هر سرور یک `HttpClient` با `SocketsHttpHandler` اختصاصی cache می‌شود، کلید cache = `(server.Id, session.Generation)`؛ با تغییر generation کلاینت قدیمی dispose و کلاینت تازه ساخته می‌شود (اتصال‌های pooled که به جلسه‌ی مرده وصل بودند دیگر استفاده نمی‌شوند).

```csharp
var handler = new SocketsHttpHandler
{
    UseProxy = false,                                    // مقصد loopback است؛ پروکسی سیستمی نباید ببیند
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
    ConnectCallback = async (_, ct) =>
    {
        var current = await pool.GetAsync(server);      // همیشه جلسه‌ی فعلی، نه یک closure کهنه
        var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, current.ForwardedPort(remotePort), ct);
        return tcp.GetStream();
    },
};
```

- هدر `Accept: application/json` مثل کلاینت نام‌دار. `remotePort = WebApiConfig.Port` (اجباری در حالت تانل).
- **Retry**: اگر جلسه بدون آنکه استخر بفهمد بمیرد (اتصال نیمه‌باز)، اولین درخواست روی اتصال pooled با `HttpRequestException` (داخل: `IOException`/`SocketException`) شکست می‌خورد. قانون: کلاینت آن سرور invalidate شود و **فقط برای مسیرهای خواندنی** (`xui/API/inbounds/` و `onlines`) یک‌بار تکرار شود؛ برای مسیرهای نوشتاری خطا به فراخوانی برمی‌گردد (اجرای بعدی job یا عمل کاربر دوباره تلاش می‌کند؛ retry کور `addClient` ممکن است کلاینت تکراری بسازد).
- کوکی نشست (`session_cookies`) مثل امروز؛ کوکی به TCP وابسته نیست، پس پس از اتصال مجدد معتبر می‌ماند و در ۴۰۱ مسیر relogin موجود اجرا می‌شود.

### ۴.۶. اعتبارسنجی در `ServerCrudApplication`

در `Validate`، وقتی `model.Config != null`:

| شرط | پیام کاربر |
|---|---|
| `Transport == SshTunnel` و `SshConfig` null یا `Username` خالی | «برای حالت تانل، کاربر SSH الزامی است!» |
| `Transport == SshTunnel` و `PrivateKey` خالی یا بدون `PRIVATE KEY-----` | «کلید خصوصی SSH معتبر نیست!» |
| `Transport == SshTunnel` و `HostKeySha256` خالی یا ناهم‌خوان با `^SHA256:[A-Za-z0-9+/]{43}$` | «کلید میزبان (host key) ثبت نشده یا نامعتبر است!» |
| `SshConfig.Port` خارج از ۱..۶۵۵۳۵ | «پورت SSH نامعتبر است!» |
| بیت `Xui` و `WebApiConfig` null | «برای سرور x-ui پیکربندی وب لازم است!» |
| بیت `Xui`، حالت تانل، و `HostName != "127.0.0.1"` یا `Ssl == true` یا `Port` خالی | «در حالت تانل، آدرس پنل x-ui باید `127.0.0.1` با پورت مشخص و بدون SSL باشد!» |
| بیت `UserManager` و `WebApiConfig.Username/Password` خالی | «برای User Manager اعتبار API لازم است!» |

`ConfigMaxLength` (۴۰۰۰ پس از protect) مثل امروز اعمال می‌شود.

### ۴.۷. endpoint تست اتصال

`POST /api/server/test-connection` — `[Authorize(Policy = ServiceFactory.AdminPolicy)]`، `ResultHandlerController`، `ApiResult`؛ بدنه `SaveServerModel`. اگر `Id > 0` و `Config == null`، پیکربندی ذخیره‌شده استفاده می‌شود (داخلی خوانده می‌شود، هرگز برگردانده نمی‌شود).

| گام | چه کاری | نتیجه‌ی موفق |
|---|---|---|
| `tcp` | اتصال TCP به `IpAddress:SshConfig.Port` با `SshConnectTimeoutSeconds` | متصل شد + مدت |
| `host-key` | handshake تا دریافت کلید میزبان؛ اگر پین خالی است: برگرداندن `SHA256:…` و توقف | تطابق (یا «کشف شد») |
| `auth` | احراز هویت با کلید (یا پسورد در Direct) | موفق + نام الگوریتم کلید میزبان |
| `forward` | ساخت forward به `127.0.0.1:WebApiConfig.Port` و یک اتصال TCP آزمایشی به آن | پورت محلی + مدت |
| `service` | x-ui: `POST login` و دریافت کوکی؛ میکروتیک: لاگین tik4net و `/system/identity/print` | «ورود موفق» |

پاسخ: فهرست گام‌ها (`Step`، `Passed`، `Detail`) + `HostKeySha256` (اگر کشف شد) + `MachineName` (`Environment.MachineName` — نام میزبان پنل برای رکورد عملیاتی). بدون کلید، بدون پسورد، بدون کوکی. `Detail` فقط پورت، مدت، اثر انگشت و پیام عمومی دارد.

### ۴.۸. لاگ و محرمانگی

- `Log.Information` در connect/reconnect/disconnect جلسه: میزبان، پورت، `Generation`، مدت — هرگز `Config`، کلید، passphrase یا پسورد.
- `CommandScrubber` همین حالا `password="…"`/`passphrase="…"` را در فرمان‌ها ماسک می‌کند؛ پوشش `export-passphrase` را نگه دارد.
- `Detail` در `UserException` فقط از جدول §۴.۲.

### ۴.۹. فرانت‌اند (`/servers`)

- در جدول: اکشن «تست اتصال» برای هر ردیف → نمایش نتیجه‌ی گام‌به‌گام.
- در مودال ویرایش: دکمه‌ی «تست اتصال» که JSON پیکربندی داخل textarea را (پیش از ذخیره) می‌فرستد؛ اگر `HostKeySha256` خالی بود، اثر انگشت کشف‌شده با دکمه‌ی «درج در پیکربندی» نمایش داده شود.
- کلیدهای ترجمه در `TranslationService` (فارسی/انگلیسی) مطابق الگوی `management.servers.*`.

### ۴.۱۰. میزبان پنل (IIS اشتراکی)

- فقط مسیر in-process: هیچ سرویس، تانل یا کلاینت بیرونی روی هاست نصب نمی‌شود.
- پیش‌نیازها که باید با هاستینگ تأیید شود: خروجی TCP از app pool به پورت SSH سرورها؛ اجازه‌ی bind روی `127.0.0.1`؛ رفتار idle timeout/recycle اپ‌پول (jobهای پس‌زمینه فقط تا وقتی worker زنده است اجرا می‌شوند — Docs/19). هر سه با تست اتصال در همان هاست قابل مشاهده‌اند.
- Recycle = از دست رفتن جلسه‌ها؛ کلکتور و job ساعتی در اجرای بعدی دوباره وصل می‌شوند.

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
- سرویس API: یا `api-ssl` (هر گواهی؛ داخل تانل اعتبار گواهی مهم نیست) یا سرویس ساده‌ی `api` فقط برای خود روتر (`/ip service set api disabled=no address=127.0.0.1`) و بازنشستگی `api-ssl` — هیچ گواهی لازم نیست.
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

خروجی TCP به پورت SSH سرورها، bind روی loopback، و رفتار app pool (§۴.۱۰).

## ۶. تست

قاعده‌ی Docs/23 D1: هیچ تستی به سرور زنده وصل نمی‌شود؛ مقصد اینتگریشن فقط VMهای لوکال است.

| لایه | تست | چه چیزی را ثابت می‌کند |
|---|---|---|
| واحد | `SshTunnelPoolTest` — از طریق یک seam داخلی (`ISshConnector` که `SshClient` واقعی را پشت interface می‌گذارد) | اتصال تنبل، reconnect پس از خطا، `Generation`، حذف بی‌فعال، rebuild روی تغییر پیکربندی، idempotent بودن `ForwardedPort` |
| واحد | `ServerCrudApplication` validation tests | هر ۷ قاعده‌ی §۴.۶، و اینکه `Direct` بدون کلید/پین همچنان پذیرفته می‌شود |
| واحد | `ServerConnectionTestApplication` با هندلرهای mock | ترتیب و توقف گام‌ها؛ توقف discover پیش از auth؛ هیچ رازی در پاسخ |
| قرارداد | `XuiApiTransportTest` (scripted handler موجود) | در حالت تانل URL پایه `http://127.0.0.1:<port>/<base>/` و `Ssl=false`؛ بدون proxy |
| اینتگریشن | `XuiPanelTunnelTest` روی `Xui-Test` | `XuiApiService` واقعی از تانل: login، list، add/update/del کلاینت؛ pin غلط → شکست قطعی؛ exec با کلید محدود → `false` |
| اینتگریشن | `MikrotikTunnelTest` روی `Mikrotik-Base` | کلید + پین؛ `ForwardedPortLocal` از داخل RouterOS؛ `Tik4NetHandler` روی پورت forward؛ `test`/`policy` رد می‌شود؛ کدام کانال خطای RouterOS را می‌آورد |

تغییر fixture میکروتیک: `Scripts/mikrotik-base.rsc` تنها مرجع پیکربندی VM است و اسنپ‌شات `clean-test-state` بعد از import آن گرفته می‌شود (Docs/23 §4.1). برای `MikrotikTunnelTest` لازم است: یک کاربر تستی در group سفارشی پنج‌policy، کلید عمومی تستی import شده روی آن کاربر، و `forwarding-enabled=local` → fixture ویرایش شود و `Scripts/mikrotik-test-vms.sh setup` (یا اعمال دستی + snapshot تازه) دوباره اجرا شود. کلید خصوصی تستی توسط اسکریپت در XDG data dir ساخته می‌شود (الگوی `Scripts/xui-test-vm.sh`) و commit نمی‌شود؛ تست آن را از `TestVmOptions` می‌خواند. سرویس `api` روی VM تستی برای شبکه‌ی host-only باز بماند (چک آماده‌باش `MikrotikInitializer` به آن وابسته است)؛ محدودکردن آن به loopback انتخاب مالک زیرساخت در تولید است.

ردیف‌های مربوط در `Docs/18` §4 و در صورت لزوم شکاف تازه در §5 در همان PR (قاعده‌ی §7 همان سند).

## ۷. شواهد — چه چیزی اثبات شده و چه چیزی نه

| ادعا | وضعیت | کجا / چگونه |
|---|---|---|
| SSH.NET 2025.0.0 با کلید ed25519 و پین SHA256 به RouterOS 7.21.4 لاگین می‌کند؛ پین محاسبه‌شده همان اثر انگشت OpenSSH است | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | یک برنامه‌ی C# مستقل با همان کتابخانه، فرمان فقط‌خواندنی روی روتر تولید مالک زیرساخت (رکورد سمت زیرساخت) |
| SSH.NET + کلید + پین + `ForwardedPortLocal` + `SocketsHttpHandler.ConnectCallback` به OpenSSH و پنل x-ui می‌رسد | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | روی `Xui-Test` (Debian 12، پنل روی پورت ۲۰۸۷): پاسخ `200` از داخل تانل |
| با کلید محدود (`command="/bin/false"`)، exec توسط SSH.NET رد می‌شود و `Execute` `false` می‌شود | ❌ هنوز نه | نخستین کار فاز ۲ روی `Xui-Test` |
| `ForwardedPortLocal` از داخل RouterOS کار می‌کند و لاگین tik4net از آن می‌گذرد | ❌ هنوز نه | نخستین کار فاز ۳ روی `Mikrotik-Base` |
| پنج policy برای نیمه‌ی SSH کار پنل روی روتر کافی است (read، write، export، file read؛ رد شدن `test` و `policy`) | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | با کاربر پنل روی روتر تولید، فقط‌خواندنی/آزمایشی با پاک‌سازی؛ `/certificate sign`، `/ppp active remove` و نیمه‌ی API هنوز با آن group اجرا نشده‌اند |
| OpenSSH 9.6 (ایمیج Ubuntu 24.04) forwarding را پیش‌فرض روشن دارد و drop-in موجود آن را نمی‌بندد | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | `sshd -T` روی دو میزبان x-ui تولید: `allowtcpforwarding yes`، `permitopen any`، `gatewayports no` |
| RouterOS 7.21.4 خاصیت `forwarding-enabled` دارد (پیش‌فرض `no`) و اتصال خود روتر به پورت API روی loopback توسط input فایروال drop نمی‌شود | ✅ اثبات‌شده (۲۰۲۶-۱۱-۲۶) | `/ip ssh print` و probe لوپ‌بک روی روتر تولید |
| خود API میکروتیک احراز هویت با کلید ندارد (فقط نام کاربری/پسورد) | مستند | MikroTik «API» |
| import کلید در RouterOS می‌تواند `password-authentication` را بیندازد | یک‌بار دیده شد (۲۰۲۶-۱۰-۲۳)؛ در اجرای ۲۰۲۶-۱۱-۲۶ رخ نداد | رکورد سمت زیرساخت |

## ۸. ترتیب اجرا و معیار پذیرش

| فاز | کار | معیار پذیرش |
|---|---|---|
| ۰ | ارتقای `Renci.SshNet` به 2026.0.0 | build بدون `NU1903`؛ همه‌ی تست‌ها سبز |
| ۱ | `ServerTransport`، فیلدهای `SshConfig`، `ISshTunnelPool` + ثبت singleton، `SshHandler`/`SshConnection` (کلید، پین، ExitStatus، الگوی RouterOS) | تست‌های واحد §۶ سبز؛ `Direct` بدون تغییر (`ProcessServiceTest` و تست‌های میکروتیک فعلی سبز)؛ روی `Mikrotik-Base` ثبت شود که خطای RouterOS از کدام کانال می‌آید |
| ۲ | `IXuiHttpClientProvider` + `XuiApiService` + retry؛ کلید محدود روی `Xui-Test` و `XuiPanelTunnelTest` | کلاینت واقعی از تانل؛ pin غلط → شکست قطعی؛ exec با کلید محدود → `false` |
| ۳ | `Tik4NetHandler` در حالت تانل؛ fixture میکروتیک + `MikrotikTunnelTest` | لاگین API از داخل تانل؛ رد شدن policyهای بسته |
| ۴ | اعتبارسنجی `ServerCrudApplication`، `test-connection`، فرانت `/servers` | ۷ قاعده؛ گام‌ها؛ discover؛ بدون راز در پاسخ/لاگ |
| ۴‌ب | مستندات همان PR: `Docs/07`، `08`، `09`، `12`، `18`، `19`، بستن §5.1 `Docs/24`، `AGENTS.md` | — |
| ۵ | آنبوردینگ تولید: پیش‌نیازهای §۵ آماده است؛ ردیف هر سرور روی `SshTunnel`؛ تست اتصال روی هاست واقعی سبز؛ سپس فعال‌سازی ردیف | تست اتصال پنج گام سبز؛ اولین poll ترافیک و اولین job ساعتی بدون خطا |

## ۹. موارد باز

1. **Forward پایدار در برابر forward به‌ازای هر اتصال**: D6 یک listener پایدار روی loopback (پورت تصادفی) به‌ازای هر جلسه می‌سازد؛ ساده و بی‌نشت. اگر روزی خواستید پنجره‌ی exposure روی هاست اشتراکی را ببندید، نسخه‌ی به‌ازای هر اتصال (forward داخل `ConnectCallback`، stop با dispose استریم) کار آینده است.
2. **UI ساخت‌یافته برای کلید/پین**: فعلاً JSON خام؛ فرم ساخت‌یافته (textarea کلید + دکمه‌ی discover + مسیر `Transport`) کار آتی است.
3. **`ConfigMaxLength = 4000`**: با کلید ed25519 کافی است؛ اگر روزی چند کلید یا فیلد بیشتر شد، سقف و ستون دیتابیس بازبینی شود.
4. **`ssh-keyscan` و RouterOS**: پین فقط از discover یا رکورد مالک زیرساخت.
5. **پسورد SSH در حالت Direct**: مثل امروز می‌ماند (سرورهایی که سرویسشان از بیرون در دسترس است).
6. **مجرای خروجی TCP از هاست پنل**: باید با هاستینگ تأیید شود؛ تست اتصال گام `tcp` را نشان می‌دهد.

## ۱۰. ارجاع‌ها

- انزوای تست و VMهای لوکال: [Docs/23-xui-local-test-vm.md](23-xui-local-test-vm.md)
- قرارداد API x-ui و مدل ترافیک: [Docs/17-xui-traffic.md](17-xui-traffic.md)، [Docs/13-xray-integration.md](13-xray-integration.md)
- هندلرهای ServerBridge: [Docs/07-server-bridge.md](07-server-bridge.md)
- هاست پنل و پیش‌نیازهای دیپلوی: [Docs/19-deploy-runbook.md](19-deploy-runbook.md)
- آیتم باز «پنل‌های loopback-only»: [Docs/24-server-capacity-realm-title.md](24-server-capacity-realm-title.md) §5.1
- حاکمیت پوشش تست: [Docs/18-test-coverage.md](18-test-coverage.md) §7
- منابع بیرونی (خوانده‌شده ۲۰۲۶-۱۱-۲۶): `sshd(8)` OpenBSD (گزینه‌های `authorized_keys`)؛ مستندات MikroTik «SSH»، «User» و «Certificates»؛ سورس SSH.NET تگ 2025.0.0 (`ConnectionInfo.cs`، `ForwardedPortLocal.cs`، `HostKeyEventArgs.cs`)؛ GitHub Advisory Database (CVE-2026-85756، CVE-2026-48798)

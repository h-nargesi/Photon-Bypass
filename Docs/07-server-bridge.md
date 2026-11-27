# Docs/07-server-bridge.md — Server bridge (SSH/Process/Script)

> **English summary**: `ServerBridge` is the adapter layer to NAS/Mikrotik hardware. It exposes four handlers (SSH, Tik4Net, Api, Email) and the `ProcessService` engine that runs a `ProcessEntity` (a bundle of `ScriptEntity`) over SSH against a server, using a shared `ProcessContext` (`Dictionary<string,string>`) to inject variables (`{key}`) and parse output (`#regex#index:name` or `return`). Variable values are validated against `^[ \-\.\w\d]+$` before injection.

## Handlers (دستگیره‌ها)

ثبت در `Backend/ServerBridge/ServiceFactory.cs`:

| Handler | Lifetime | Backed by | Purpose |
|---|---|---|---|
| `ISshHandler` → `SshHandler` | Singleton | `Renci.SshNet` (`SshClient`/`SshConnection`) | اجرای دستور روی روتر/NAS از طریق SSH. |
| `ITik4NetHandler` → `Tik4NetHandler` | Singleton | `tik4net` (wrapper) | دسترسی به API Mikrotik. |
| `IApiHandler` → `ApiHandler` | Scoped | `HttpClient` | فراخوانی‌های REST عمومی (مثلاً RadiusDesk خودش HttpClient جدا دارد). |
| `IEmailHandler` → `EmailHandler` | Scoped | `SmtpClient` | ارسال ایمیل؛ options `EmailOptions` (`Address`/`Password`/`FullName`). |
| `ResourceSynchronization` | Singleton | — | قفل/هماهنگی منابع مشترک. |
| `ISshConnector` → `SshConnector` | Singleton | `Renci.SshNet` (`SshClient`، `ForwardedPortLocal`) | seam داخلی SSH (Docs/25): تصمیم host key و classify خطا؛ در تست‌ها fake. |
| `ISshTunnelPool` → `SshTunnelPool` | Singleton (+ hosted service جاروب) | `ISshConnector` | یک جلسه‌ی SSH به‌ازای هر سرور در حالت `SshTunnel` (Docs/25 D5): اتصال تنبل، keep-alive، حذف بی‌فعال، `Fault`، `Generation`. |
| `TikConnectionFactory` | Singleton | `tik4net` (`ConnectionFactory.CreateConnection`) | ساخت اتصال tik4net پشت یک delegate (آداپتور و تست اتصال). |
| `IServerChannelProbe` → `ServerChannelProbe` | Singleton | `ISshConnector` | گام‌های `tcp`/`host-key`/`auth`/`forward` تست اتصال ادمین روی یک جلسه‌ی یک‌بارمصرف (Docs/25 D9). |
| `IHostCapabilityChecks` → `HostCapabilityChecks` | Singleton (+ hosted service) | `TcpClient`/`TcpListener` | بررسی‌های میزبان G1–G3 در هر شروع و با `POST /api/server/host-checks` (Docs/25 D11). |

> نکته: SSH/Tik4Net **Singleton** هستند چون state اتصال را مدیریت می‌کنند و reuse می‌شوند؛ Api/Email **Scoped** چون per-request رفتار می‌کنند.

> `ISshHandler`/`ITik4NetHandler` state اتصال را نگه نمی‌دارند: `SshHandler` در `Direct` برای هر فراخوانی یک کانال تازه می‌سازد و در `SshTunnel` اتصال را از `ISshTunnelPool` قرض می‌گیرد؛ استخر است که جلسه‌ها را نگه می‌دارد (Docs/25).

> کانال SSH با کلید و تانل (حالت `SshTunnel`): تصمیم و مشخصات کامل در [Docs/25](25-server-channel.md) — **پیاده‌سازی شده (۲۰۲۶-۱۱-۲۶)**. هر سرور با `ServerConfiguration.Transport` مسیر خود را انتخاب می‌کند: `Direct` مثل امروز (پسورد، پین اختیاری) یا `SshTunnel` با کلید ed25519 و host key پین‌شده روی یک جلسه‌ی مشترک؛ منطق SSH.NET پشت seam داخلی `ISshConnector` در `Backend/ServerBridge/Tunnel/` نشسته تا تست‌ها بدون شبکه باشند. SSH.NET با این کار به 2026.0.0 ارتقا یافت (دو advisory با شدت بالا؛ پنل `ScpClient` ندارد).

## Process / Script engine (موتور اسکریپت)

هسته‌ی منطق «اجرای فرآیند روی سرور» در `Backend/ServerBridge/Ssh/ProcessService.cs`:

- **`ProcessEntity`** (`Domain/Servers/Entity/ProcessEntity.cs`) — یک فرآیند شامل سه دسته‌ی اسکریپت: `Enable`، `Disable`، `Check` (هرکدام `IEnumerable<ScriptEntity>`). فیلد `OsType` تعیین می‌کند فقط روی کدام OS قابل اجراست.
- **`ScriptEntity`** (`Domain/Servers/Entity/ScrtipEntity.cs`) — یک اسکریپت با `Content` و `OutputPattern`.
- متدهای اصلی روی `IProcessService`:
  - `ActivateOn(process, server, context)` → اجرای `Enable`.
  - `DeactivateOn(process, server, context)` → اجرای `Disable`.
  - `CheckOn(process, server, context)` → اجرای `Check` (اگر `null` باشد، `false` برمی‌گردد).

### `ProcessContext` — the shared variable bag

`ProcessContext` (`ProcessService.cs:133`) یک `Dictionary<string,string>` است که **بین اسکریپت‌های یک Process مشترک** است — همان «کانتکست متغیر مشترک» در `README.md:20`.

ویژگی‌ها:
- `Error` — پیام خطای آخرین اسکریپت ناموفق.
- `Content` (`List<string>`) — خروجی‌های خام جمع‌شده از اسکریپت‌هایی که `OutputPattern == "return"` دارند.
- `Result` — joinِ `Content` با `\n`.

### Variable injection (تزریق متغیر)

قبل از اجرای هر اسکریپت، `InjectContextInScript` تمام `{key}`ها را با مقدار جایگزین می‌کند، **اما** مقدار ابتدا با regex اعتبارسنجی می‌شود:

- `ValidCharacters`: `^[ \-\.\w\d]+$` (`ProcessService.cs:129`).
- اگر مقدار نامعتبر باشد، `Exception("Invalid character for inject: {key}={value}")` پرتاب می‌شود — این مکانیزم اصلی جلوگیری از تزریق دستور است.

### Output parsing (پارس خروجی)

`OutputPattern` سه حالت دارد (`ProcessService.cs:60-77`):
1. خالی → خروجی نادیده گرفته می‌شود (فقط success/fail).
2. `"return"` → کل خروجی به `context.Content` اضافه می‌شود.
3. رشته‌ی pattern → با `ReadScriptResult` پارس می‌شود.

فرمت pattern با جداکننده‌ی `#` و ساختار `regex#index:name`:

```
# <regex>#<group-index>:<variable-name>#<group-index>:<variable-name> ; <regex>#...
```

- الگوی تجزیه: `OutputPatternParse` = `#\s+regex:` (`ScriptPatterns.cs:19`).
- هر گروه تطبیق‌یافته در context به‌صورت `context[name] = match.Groups[index].Value` نوشته می‌شود؛ بنابراین خروجی یک اسکریپت می‌تواند ورودی اسکریپت بعدی شود.

### حکم موفقیت فرمان — `ISshConnection.Execute`

یکسان در هر دو transport و در `CommandOutcome.Classify` (`Backend/ServerBridge/Ssh/CommandOutcome.cs`، Docs/25 D10):

1. stderr غیرخالی → `false`، `result` = متن stderr.
2. در غیر این صورت `ExitStatus > 0` → `false`، `result` = خروجی (`-1` یعنی exit status نرسیده و به این شرط نمی‌خورد).
3. در غیر این صورت روی `Mikrotik`، خطی که با الگوی خطای RouterOS شروع شود (`^failure: `، `expected end of command`، `not enough permissions`، `no such item`، `bad command name`، `syntax error` — `RouterOsErrorPatterns.cs`) → `false`، `result` = خروجی.
4. در غیر این صورت `true`، `result` = خروجی.

فرمان ناموفق exception نمی‌دهد (متن خطا در لاگ سطح Warning می‌نشیند)؛ `ProcessService` همان رفتار قبلی را دارد: `context.Error = result` و `return false`. exception فقط برای timeout («خطای اجرای دستور در سرور!») و قطع اتصال («خطای اتصال به سرور!») است. در حالت تانل فرمان‌های یک جلسه سریالی می‌شوند: `ISshConnection.Dispose` فرمان در حال اجرا را cancel می‌کند و جلسه را آزاد می‌کند. از دست رفتن جلسه وسط فرمان در حالت تانل همان جلسه را از استخر بیرون می‌اندازد (`Fault` با هویت همان جلسه)، پس فراخوانی بعدی یک‌بار دوباره وصل می‌شود؛ timeout فرمان نشست را نگه می‌دارد.

### Script definition syntax (نحو تعریف اسکریپت)

الگوهای regex در `Backend/ServerBridge/Ssh/ScriptPatterns.cs` (برای parserهای تعریف متنی اسکریپت/پروسس — اگر از فرمت متن استفاده شود):

- `ProcessParser`: `^#\s*\[Process\s*:\s*([\w \-]+)\]`
- `ScriptParser`: `^#\s*\[Script\s*:\s*([\w \-])\s*:\s*(enable|disable|check)\]`
- `OutputParser`: `^#\s*\[Output\s*:\s*(result:regex)\]`
- `CommentRemover`: حذف خطوط `#...`.
- `InjectionRegex` (در `MikrotikRadius` برای اعتبارسنجی یوزرنیم پیش از SSH، `MikrotikDirectService`).

## Security (امنیت)

- **تزریق دستور**: تنها مقادیر مطابق `^[ \-\.\w\d]+$` تزریق می‌شوند؛ بقیه fail می‌شوند.
- **اعتبارسنجی ورودی کاربر**: قبل از SSH مستقیم، شناسه‌هایی مثل `username` با regex جداگانه (مثلاً `InjectionRegex.Username`) بررسی می‌شوند (`MikrotikDirectService.cs:167`).
- **هیچ اسرار دستگاهی** نباید در `ProcessEntity.ScriptEntity.Content` هاردکد شود؛ از context و config بیایید.

## Flow (جریان اجرای یک Process)

```mermaid
sequenceDiagram
    participant APP as Application/Mgmt
    participant PS as ProcessService
    participant SSH as ISshHandler (SshConnection)
    participant SRV as Mikrotik NAS
    APP->>PS: ActivateOn(process, server, context)
    PS->>SSH: ConnectTo(server)
    loop هر اسکریپت در Enable
        PS->>PS: InjectContextInScript ({key} replacement + validate)
        PS->>SSH: Execute(command)
        SSH->>SRV: run shell
        SRV-->>SSH: stdout/stderr
        alt OutputPattern == return
            PS->>PS: context.Content.Add(raw)
        else OutputPattern pattern
            PS->>PS: ReadScriptResult(regex#index:name) -> context[name]
        end
        opt failure
            PS->>PS: context.Error = result; return false
        end
    end
    PS-->>APP: success bool (+ context)
```

## Where it is used (کاربردها)

- **`MikrotikDirectService`** (ساخت/پخش گواهی OpenVpn، اعمال تغییرات روی NAS).
- **فرآیندهای مدیر** روی سرورها (فعال/غیرفعال تانل‌ها، راه‌اندازی) — `README.md:18-20`.
- پنل مدیریت سرورها در `/servers` است (Docs/20 فاز ۵) و از این جلسه تست اتصال و انتخابگر transport را دارد (Docs/25 D9).

# پلان: تست‌های integration برای XuiPanel + پوشش شکاف‌های تست MikrotikRadius

> زبان‌های اجرا: این پلان فقط فایل‌های تست و مستندات docs/18 را لمس می‌کند؛ هیچ تغییری در کد production لازم ندارد.

## ۰. تصمیم‌های قطع‌شده با مالک

| # | تصمیم |
|---|---|
| D1 | تست integration برای x-ui علیه **VM واقعی VirtualBox** اجرا شود (parity کامل با الگوی MikrotikRadius: snapshot restore + بوت + health-check). |
| D2 | دامنه x-ui: **لایه IXuiApi + سرویس‌های سینک keyed** (`RadiusType.Xui`). کلکتور ترافیک/DB خارج دامنه (بدون ترافیک واقعی VPN سیگنال جدیدی نمی‌دهد). |
| D3 | سه متد بی‌تست `SessionRadiusSyncUserManagerService` با **unit + mock موجود** پوشش داده شوند (`Tik4NetHandlerMoq` + JSON + رویداد `OnExecute`). |
| D4 | سه متد بی‌تست SSH در `MikrotikDirectService` هم داخل دامنه است (unit با `ConfigurableSshHandlerMoq`). |
| D5 | آماده‌سازی VM پنل x-ui با **چک‌لیست دستی یک‌باره** (بخش ۴) + `XuiInitializer` برای چرخه حیات. |

## ۱. زمینه (خلاصه از کد)

- الگوی integration موجود: `Backend/Tests/Initializer/MikrotikInitializer.cs` (implementer برای `IOutSourceInitializer` + `IOutSourceLevelService`)؛ تست‌ها با ارث‌بری از `OutSourceLevelServiceInitializer` و فراخوانی `scope.Register<MikrotikInitializer>(key, this)` (مثال: `Facts/Mikrotik/SyncUserActiveInactiveRemoveTest.cs`).
- `OutSourcePool` بر اساس کلید `(key, typeof(TInitializer))` فقط یک‌بار snapshot-restore می‌کند؛ پس هر تست باید با **ایمیل‌های یکتا** (`kiloint-…`) مستقل بماند.
- در سطح OutSource، mock های `IUnitLevelService` (مثل `XuiApiMoq` و `Tik4NetHandlerMoq`) ثبت نمی‌شوند → `IXuiApi` واقعی (`XuiApiService` + HttpClient واقعی به `192.168.56.13:5443`) و `ITik4NetHandler` واقعی وصل می‌شوند.
- `SessionRadiusSyncXuiService`: متدهای close عمداً no-op و `GetTrafficData` عمداً لیست خالی — فقط `GetActiveConnections` (خواندن onlines) معنا دارد؛ یک smoke کوچک کافی است.
- تست‌های transport واحد موجود (`XuiApiTransportTest`) مسیر 401→re-login، خطای inbound اشتباه و قرارداد مسیرها را از قبل پوشش می‌دهند — در integration تکرار نمی‌شوند.
- شکاف‌های MikrotikRadius: docs/18 §5.1 (سه متد session) + `MikrotikDirectService` ۴۶.۹٪ (سه متد SSH بی‌تست) + `UserManagerHelper` ۷۷.۷٪.
- `SyncUserAndActive` برای x-ui با `renewal.Id = 0` بدون هیچ DB اجرا می‌شود (شاخه‌ی `RenewalRepo.Save` فقط با `Id > 0`).

## ۲. کار A — زیرساخت integration برای x-ui

### A1. `Backend/Tests/Initializer/XuiInitializer.cs` (جدید)
- کپی ساختاری از `MikrotikInitializer`: implements `IOutSourceInitializer`, `IOutSourceLevelService` + متد static `CreateInstance(IServiceCollection)` (کلاس‌های `IOutSourceLevelService` با reflection در `ServiceInitializer.AddDefaultServices` خودکار ثبت می‌شوند).
- `HostIps`: `{ "Xui-Base", "192.168.56.13" }` (اگر IP در شبکه host-only اشغال بود، فقط همین ثابت عوض می‌شود).
- `Initialize/Clear`: همان فرمان‌های VBoxManage (`controlvm poweroff`، `snapshot restore clean-test-state`، `startvm --type headless`) — می‌توان قطعات مشترک را با یک base-class کوچک (`VBoxVmInitializer`) از `MikrotikInitializer` استخراج کرد تا duplication نداشته باشیم (بازآرایی مجاور، مطابق quality bar).
- `Check`: به‌جای SSH، health-check با HTTP — `POST http://192.168.56.13:5443/login` با `{"username":"admin","password":"admin"}` تا وقتی envelope با `success:true` برگردد (timeout 120s، حلقه 1s) — یعنی دقیقاً همان مسیری که `XuiApiService.LoginAsync` می‌رود.

### A2. `Backend/Tests/Facts/Xui/Integration/XuiApiRoundTripTest.cs` (جدید)
- `OutSourceLevelServiceInitializer` + `scope.Register<XuiInitializer>("Xui-Base", this)`.
- ServerEntity مشترک: `Features = Xui | V2ray`، `Config.WebApiConfig = { HostName = "192.168.56.13", Port = 5443, Ssl = false, Username = "admin", Password = "admin", InboundId = 1 }` (ثابت فایل).
- resolve سرویس واقعی: `scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>()` یا مستقیم `IXuiApi`.
- تست‌ها (هر کدام ایمیل یکتا؛ snapshot فقط یک‌بار per-run ری‌استور می‌شود):
  1. `LoginAndList_OnFreshPanel_ReturnsEmptyClientsAndStats` — ListClients/ListClientTraffic خالی، ListOnlineClients خالی (شکل onlines با lastSeen ثانیه‌ای).
  2. `AddClient_GetClient_RoundTripsContractFields` — AddClient با totalGB/expiryTime/limitIp/enable → GetClient همان مقادیر را برمی‌گرداند (پارس دولایه settings روی پنل واقعی).
  3. `UpdateClient_RotatingUuid_KeepsStatsRowKeyedByEmail` — بعد از UpdateClient با UUID جدید: کلاینت UUID جدید دارد **و** ردیف ClientStats همان ایمیل با up/down قبلی باقی است (فرض D4 — همان مورد ۱۴تایی probe).
  4. `ResetClientTraffic_EmailKeyed_ZeroesCounters` — بعد از ResetClientTraffic، up/down کلاینت صفر شده.
  5. `RemoveClient_UuidKeyed_RemovesClientAndStatsRow` — حذف، هم از settings هم از ClientStats (رفتار probed-شده).
  6. `GetInbound_ReturnsManagedInboundTransport` — id/port/protocol/streamSettings اینباند id=1.

### A3. `Backend/Tests/Facts/Xui/Integration/XuiAccountSyncIntegrationTest.cs` (جدید)
- همان initializer/fixture؛ سرویس: `GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.Xui)`.
- تست‌ها (اکانت‌های ساختگی `VpnAccountEntity` با `VpnPassword` = UUID ثابت؛ `RenewalEntity` با **Id=0** تا DB وارد نشود):
  1. `SyncUserAndActive_NewAccount_PushesLimitsAndEnables` — کلاینت ساخته می‌شود با `totalGB = CumulativeTrafficLeft ?? TrafficLimit ?? 0`، `expiryTime`، `limitIp = SimultaneousUser`، `enable = true` (راستی از طریق خود `IXuiApi.GetClient`).
  2. `SyncUserAndActive_ExistingClient_UpdatesAndResetsTraffic` — دوباره صدا زدن → update (نه duplicate) + reset ترافیک.
  3. `ChangeVpnPassword_RotatesUuidAndKeepsStats` — UUID جدید برمی‌گردد، روی پنل اعمال می‌شود، stats زنده می‌ماند؛ برای کاربر غایب → `UserException`.
  4. `DeactivateThenActivate_TogglesEnableOnly` — enable=false/true بدون تغییر UUID.
  5. `DeactivateUserExceptAndRemoveUsers_OnlyManagedPatternClients` — یک کلاینت خارج از `UsernamePattern` (مثلاً `manual-client!`) دست‌نخورده می‌ماند (گارد IsManagedClient).

### A4. `Backend/Tests/Facts/Xui/Integration/XuiSessionSyncIntegrationTest.cs` (جدید، کوچک)
- `GetActiveConnections_NoOnlineClient_ReturnsEmpty` و `GetTrafficData_IsEmptyByDesign` — قفل قرارداد عمدیِ فعلی روی پنل واقعی.

## ۳. کار B — پوشش شکاف‌های MikrotikRadius (unit، بدون VM)

### B1. `Backend/Tests/Facts/Mikrotik/SessionRadiusSyncUserManagerTest.cs` (جدید، `UnitLevelServiceInitializer`)
- سرویس مستقیم: `scope.ServiceProvider.GetRequiredService<SessionRadiusSyncUserManagerService>()` یا keyed `RadiusType.UserManager`؛ mock: `Tik4NetHandlerMoq` + `Data/Mikrotik/user-manager-session-print.json`.
- **نکته مهم**: شمارنده‌های `SessionRadiusTest` به تعداد ردیف‌های همین JSON وابسته‌اند — ردیف موجود را تغییر ندهید؛ در صورت نیاز فقط ردیف‌های جدید با `.server-id` جدید که در آن تست شمرده نمی‌شوند. اول محتوای JSON را بخوانید و یوزرنیم‌های موجود را انتخاب کنید.
- تست‌ها:
  1. `GetActiveConnections_FiltersByUsernameAndActive_Yes` — فقط session های active=yes همان کاربر؛ نگاشت فیلدها (CallerId/NasIpAddress/SessionId/State/UpTime).
  2. `GetActiveConnections_NoMatchingSession_ReturnsEmpty` و `InvalidUsername_ThrowsBeforeConnect` (بدون فراخوانی OnExecute).
  3. `CloseConnectionByUsername_ClosesEverySessionWithIdParam` — اشتراک در `Tik4NetHandlerMoq.OnExecute`؛ برای هر session منطبق یک `close-session` با پارامتر `.id=*<Id>`؛ session کاربر دیگر بسته نمی‌شود.
  4. `CloseConnectionByUsername_NoSessions_NoCloseCall`.
  5. `CloseConnectionBySessionId_ClosesOnlyThatSession` + `InvalidSessionId_Throws` + `UnknownSessionId_NoCloseCall`.

### B2. توسعه `Backend/Tests/Facts/Mikrotik/MikrotikDirectServiceTest.cs` (unit، `ConfigurableSshHandlerMoq`)
- `CloseConnection_Success_RunsRemoveThenVerifyPrint` — توالی `/ppp active remove [find session-id=0x…]` سپس `/ppp active print where session-id=0x…`.
- `CloseConnection_RemoveFails_ThrowsUnsuccessful` — پاسخ fail → «Closing connection was unsuccessful!».
- `CloseConnection_InvalidSessionId_ThrowsBeforeSsh`.
- `CloseConnections_FansOutToAllServers_AndInvalidUsername_Throws` — چند سرور → اتصال به همه؛ خطای یک سرور منتشر می‌شود.
- `GetActivePppConnections_ParsesRows` — خروجی واقعی‌نمای `ppp active print` (چند ردیف با caller-id/uptime/session-id) → `UserConnectionBinding` با `UpTime = hh:mm:ss`؛ خروجی تهی/ناهم‌خوان → لیست خالی؛ username نامعتبر → throw؛ سرور null → `ArgumentNullException`؛ username خالی → `[]` بدون SSH.

### B3. جاروی کوچک `UserManagerHelper` (در همان فایل B1 یا `UserManagerHelperTest` جدید)
- ابتدا خطوط قرمز coverage را از `Backend/Tests/TestResults/coverage-report/` ببینید؛ موارد محتمل: شاخه unlimited در `GetProfile` (name="profile"، `NameForUsers="Unlimited"`)، مسیر create در `GetRateLimit` (`limit-speed-{rate}M`)، و exception های «multiple of 25 gigabytes». تست‌های هدفمند از طریق `connection.CheckLimitations`/`GetProfile` روی connection ساختگی از `Tik4NetHandlerMoq`.

## ۴. کار C — چک‌لیست دستی آماده‌سازی VM (یک‌بار، توسط مالک روی ماشین dev دارای VirtualBox)

1. ساخت VM لینوکس مینیمال (Ubuntu 22.04 یا Debian 12؛ ۱ vCPU، 1GB RAM) با نام **Xui-Base**؛ آداپتر NAT + Host-Only روی همین vboxnet موجود (192.168.56.0/24)؛ IP استاتیک **192.168.56.13**.
2. نصب **alireza0/x-ui v1.14.0** (اسکریپت رسمی نصب — VM باید اینترنت داشته باشد فقط در همین فاز).
3. تنظیم پنل: پورت **5443**، بدون web base-path (پیش‌فرض `/`)، یوزر/پسورد **admin/admin**، بدون محدودیت IP (یا فقط اجازه subnet host-only).
4. ساخت یک inbound: **vless + tcp** (بدون TLS)، پورت دلخواه (مثلاً 10086) — inbound اول id=**1** می‌گیرد؛ برای اطمینان یک کلاینت تستی بسازید و بعد پاکش کنید تا snapshot با اینباید **خالی** گرفته شود.
5. خاموش کردن VM و: `VBoxManage snapshot "Xui-Base" take clean-test-state`.
6. Smoke از host: `curl -i -X POST http://192.168.56.13:5443/login -d '{"username":"admin","password":"admin"}'` (بعد از استارت VM) باید `Set-Cookie: x-ui=…` بدهد.

> اگر IP/پورت/پسورد تفاوت کرد، فقط ثابت‌های `XuiInitializer` و fixture تست‌ها عوض می‌شوند — هیچ چیز دیگری وابسته نیست.

## ۵. کار D — مستندات (الزامی، همان PR — قاعده حاکمیت docs/18 §7)

- **§4 matrix**: ردیف جدید «integration پنل x-ui روی VM واقعی (round-trip API + سینک‌های keyed)» با ارجاع به docs/17 §4؛ به‌روزرسانی ردیف‌های مربوط به سرویس‌های سینک MikrotikRadius با تست‌های جدید.
- **§5.1**: علامت‌گذاری حل‌شده (به سبک ~~…~~ ✅ موجود با ذکر تست‌ها) برای سه متد session؛ اگر B2/B3 هم شکاف ثبت‌شده دارند، مشابه.
- **§3 baseline**: بعد از اجرای coverage (فرمان‌های AGENTS.md)، عدد MikrotikRadius بازسنجی و ثبت شود؛ یادداشت شود که تست‌های integration فقط روی ماشین دارای VM اجرا می‌شوند.

## ۶. ترتیب اجرا و وابستگی‌ها

1. B1+B2+B3 (unit — بدون VM، قابل اجرا/راستی فوری): `dotnet test Backend/Tests/Tests.csproj --filter "FullyQualifiedName~Facts.Mikrotik"`.
2. A1 (`XuiInitializer` + بازآرایی base مشترک VBox).
3. کار C توسط مالک (موازی با ۴ و ۵).
4. A2→A3→A4 (integration tests).
5. D (docs) + بازسنجی coverage.

## ۷. ریسک‌ها و نکات

- **اجرای روی ماشین بدون VM**: مانند سوئیت Mikrotik فعلی، تست‌های integration با خطای واضح fail می‌شوند (کنوانسیون موجود؛ skip پویا در xunit 2.9 موجود نیست). در پلان docs ذکر شود.
- **snapshot یک‌بار per-run restore می‌شود** (OutSourcePool) → تست‌ها باید با ایمیل‌های یکتا مستقل باشند؛ ترتیب اجرای xUnit هم نباید فرض شود.
- **id اینباید باید 1 باشد**؛ اگر نصب پنل اینباید دیگری ساخت، یا پاکش کنید یا `InboundId` را در fixture تنظیم کنید.
- `XuiApiService` کوکی سشن را per-`ServerEntity.Id` کش می‌کند — fixture باید Id ثابت (مثلاً 50) داشته باشد.
- ردیف‌های JSON موجود Mikrotik دست نمی‌خورند (شمارنده‌های SessionRadiusTest).
- کوکی سشن پنل بین snapshot-restore ها نو می‌شود — مشکلی نیست چون هر run لوگین تازه می‌زند.

## ۸. اعتبارسنجی نهایی

- روی ماشین دارای VM: `dotnet test Backend/Tests/Tests.csproj` (کل سوئیت سبز؛ تست‌های جدید x-ui integration پاس).
- فقط unit: `dotnet test Backend/Tests/Tests.csproj --filter "FullyQualifiedName~Facts.Mikrotik|FullyQualifiedName~Facts.Xui"` (تست‌های integration جدید بدون VM fail می‌شوند — مورد انتظار).
- coverage طبق فرمان‌های AGENTS.md؛ به‌روزرسانی §3 docs/18.

## ۹. خارج از دامنه (صریح)

- integration کلکتور ترافیک x-ui با DB واقعی (سیگنال محدود بدون ترافیک واقعی VPN).
- تست integration برای سه متد session علیه روتر واقعی با کلاینت RADIUS accounting دست‌ساز (پیشنهاد شد، مالک unit را انتخاب کرد).
- `SetOVpnCertificate` (NotImplemented عمداً)، آداپتور RadiusDesk، شکاف‌های فرانتند/کنترلر Portal (§5.5)، ریسک‌های موازی‌سازی سناریو (§5.8) و `EmailHandlerMoq.OnSend` (§5.9) — کار آتی جدا.

# docs/08-portal-api.md — REST API surface

> **English summary**: Seven controllers under `/api/[controller]`. JWT Bearer auth, key/issuer from `appsettings.json` (`Issuer:Code` Base64, `Issuer:Name`), token valid 1 hour (`expires_in=3600`). All controllers extend `ResultHandlerController`, which runs `LoadJobContext(target?)` to validate multi-account access and returns `ApiResult`/`ApiResult<T>`. The table below is verified against `Backend/Portal/Controllers/*.cs`.

## Authentication (احراز هویت)

- **JWT Bearer** در `AddPortalServices` (`Backend/Portal/ServiceFactory.cs:16`).
- کلید امضا از `Configuration["Issuer:Code"]` (رشته‌ی Base64) به `SymmetricSecurityKey` تبدیل و روی `IdentityHelper.Key` (static) ست می‌شود.
- Issuer/Audience از `Configuration["Issuer:Name"]`. اعتبارسنجی issuer/audience/lifetime/signing-key همگی فعال؛ `ClockSkew = Zero`.
- صدور توکن در `AuthController.Login` (`AuthController.cs:44`) → `expires_in = 3600`.
- سرویس احراز هویت توکن را در هدر `Authorization: Bearer <token>` می‌خواند (frontend).

## Base controller & result contract

- `ResultHandlerController` (`Backend/Portal/Basical/ResultHandlerController.cs:12`) کلاس پایه‌ی همه‌ی کنترلرها (به‌جز `BasicsController` که مستقیم `ControllerBase` را استفاده می‌کند).
- **`LoadJobContext(string? target)`** (`:18`): `Username` را از Identity، `Target` را از پارامتر (یا همان Username) می‌گذارد؛ اگر `target` متفاوت از کاربر باشد، `IAccessService.CheckAccess` را می‌زند و در صورت رد، `UserException(403)` پرتاب می‌کند. همیشه `JobContext.Target` را بخوانید، نه query خام.
- **`ApiResult`** (`Shared/Result/ApiResult.cs`): `Code` (short)، `Message` (نمایش به کاربر)، `Developer` (فقط dev)، `MessageMethod`، و در نسخه‌ی generic، `Data`.
- کمکی‌های static روی کنترلر پایه: `SafeApiResult` (null → 204)، `UnauthorizedApiResult` (401)، `BadRequestApiResult`.

## Exception middleware

`Backend/Portal/Basical/ExceptionHandlingMiddleware.cs`:
- نسخه‌ی Production: خطا را به `ApiResult` با کد HTTP مناسب تبدیل می‌کند؛ پیام عمومی `"خطای غیرمنتظره‌ای رخ داده است!"`.
- نسخه‌ی Development (`ExceptionHandlingMiddlewareInDevelopment`): علاوه بر موارد بالا، `Developer` را با `Message + StackTrace` پر می‌کند.
- `UserException` → کد از `HttpCode` یا `400`، پیام از `UserMessage`.
- سایر استثناها → `500`.
- انتخاب نسخه بسته به `app.Environment.IsDevelopment()` در `Program.cs`.

## Endpoint table (جدول endpointها)

> Auth: **Y** = `[Authorize]` لازم، **N** = عمومی. `target` در ستون Input یعنی query/body اختیاری `target`.

| Method | Path | Input | Controller:Method | Auth | Notes |
|---|---|---|---|---|---|
| GET | `/api/basics/prices` | — | `BasicsController.GetPrices` | N | لیست پلن‌ها/قیمت‌ها برای صفحه‌ی Home. |
| POST | `/api/auth/token` | body `TokenContext` | `AuthController.Login` | N | لاگین؛ خروجی `access_token`/`token_type`/`expires_in`. |
| POST | `/api/auth/forget-pass` | body `ResetPasswordContext` | `AuthController.ForgetPassword` | N | ارسال کد بازنشانی. |
| POST | `/api/auth/reset-pass` | body `ChangePasswordContext` | `AuthController.ResetPassword` | N | تغییر پسورد با کد. |
| POST | `/api/auth/register` | body `RegisterModel` | `AuthController.Register` | N | ثبت‌نام. |
| GET | `/api/account/get-user` | — | `AccountController.GetUser` | Y | اطلاعات کاربر جاری. |
| GET | `/api/account/full-info` | `?target` | `AccountController.GetFullInfo` | Y | اطلاعات کامل target. |
| POST | `/api/account/edit-user` | `?target` + body `EditUserModel` | `AccountController.EditUser` | Y | ویرایش اطلاعات target. |
| POST | `/api/account/change-pass` | body `ChangePasswordContext` | `AccountController.ChangePassword` | Y | تغییر پسورد لاگین (بر اساس کاربر جاری). |
| GET | `/api/account/history` | `?target` + query `HistoryContext` | `AccountController.GetHistory` | Y | تاریخچه‌ی target. |
| POST | `/api/vpn/change-ovpn` | body `ChangeOvpnContext` | `VpnController.ChangeOvpnPassword` | Y | تغییر پسورد OpenVpn؛ نیازمند تأیید پسورد اکانت. |
| GET | `/api/vpn/send-cert-email` | `?target` | `VpnController.SendCertEmail` | Y | ایمیل کانفیگ/گواهی. |
| GET | `/api/vpn/traffic-data` | `?target` | `VpnController.TrafficData` | Y | داده‌های ترافیک (نمودار). |
| GET | `/api/connection/current-con-state` | `?target` | `ConnectionController.GetCurrentConnectionState` | Y | وضعیت اتصال‌های فعال. |
| POST | `/api/connection/close-con` | body `CloseConnectionContext` | `ConnectionController.CloseConnection` | Y | بستن کانکشن (`Server` + `SessionId`). |
| GET | `/api/plan/plan-state` | `?target` | `PlanController.GetPlanState` | Y | وضعیت پلن (از `TotalPlanState`). |
| GET | `/api/plan/plan-info` | `?target` | `PlanController.GetPlanInfo` | Y | اطلاعات پلن جاری. |
| POST | `/api/plan/estimate` | body `RenewalContext` | `PlanController.Estimate` | Y | برآورد قیمت تمدید. |
| POST | `/api/plan/renewal` | body `RenewalContext` | `PlanController.Renewal` | Y | همیشه صدور فاکتور تمدید (خالص‌سازی با کیف پول)؛ اجرای پلن فقط از مسیر تسویه انجام می‌شود. |
| POST | `/api/billing/pay` | body `PayRequest {Value, Target?}` | `BillingController.Pay` | Y | صدور فاکتور TopUp (کد فاکتور برمی‌گرداند؛ bounds ۱..۱۰۰٬۰۰۰). |
| GET | `/api/billing/get-invoice` | `?code` (int) `&target?` | `BillingController.GetInvoice` | Y | مدل کامل فاکتور: `code, kind, status, totalPrice, walletDeduction, payable, walletBalance, allowWallet, hasReceipt, items[], cardInfo[]`. |
| POST | `/api/billing/register-receipt` | multipart: `code, target?, file XOR text` | `BillingController.RegisterReceipt` | Y | ثبت رسید (تصویر یا متن) → `Verifying`؛ برای فاکتور Plan، اجرای Renewal. اولین endpoint مولتی‌پارت (سقف درخواست ۳MB؛ فرمت‌های jpg/jpeg/png/webp). |
| POST | `/api/billing/settle-wallet` | body `SettleWalletRequest {Code, Target?}` | `BillingController.SettleWallet` | Y | تسویه فاکتور Plan از کیف پول (فقط با موجودی کافی؛ انتقال اتمیک `Pending→Canceled`). |
| POST | `/api/wallet/adjust` | body `WalletAdjustRequest {Target?, Direction, Amount, Description}` | `WalletController.Adjust` | Y + policy **Admin** | تراکنش شفاف ادمین (docs/20 D5/فاز ۳): ردیف Completed با `ActorId` + اجرای قاعده آستانه؛ `Direction` ∈ {1=Credit, -1=Debit}، `Amount>0`، توضیح اجباری (≤۱۲۷). |
| GET | `/api/wallet/history` | `?target?&page=1` | `WalletController.GetHistory` | Y | تاریخچه کیف پول target با صفحه‌بندی سمت سرور (۲۰ ردیف، newest-first؛ پاکت `PagedResult {page,pageSize,totalCount,totalPages,items[]}`)؛ فقط خودِ صاحب حساب مستقل یا ادمین — مدیر ۴۰۳. |
| GET | `/api/user/list` | `?search?&page=1` | `UserController.GetList` | Y + policy **Manager** | لیست صفحه‌بندی‌شده کاربران (مدیر → فقط زیر دست‌ها شامل غیرفعال‌ها؛ ادمین → همه) با خلاصه‌ی آخرین پلن از `TotalPlanState`. |
| POST | `/api/user/create` | body `CreateUserModel` | `UserController.Create` | Y + policy **Manager** | ساخت کاربر (docs/20 فاز ۴): مدیر → `Owner=self`/`Role=User` اجباری؛ ادمین → `Role` (بجز Admin) و `Owner` (مدیر فعال)؛ پسورد اولیه الزامی؛ تکراری → 409. |
| POST | `/api/user/set-password` | body `{Target, Password}` | `UserController.SetPassword` | Y + policy **Manager** | بازنشانی پسورد (مدیر → زیر دست خودش؛ ادمین → هرکس بجز خودش). |
| POST | `/api/user/set-role` | body `{Target, Role}` | `UserController.SetRole` | Y + policy **Admin** | فقط User↔Manager؛ خود/ادمین ممنوع (D9)؛ تنزل مدیرِ دارای زیر دست فعال → 400. |
| POST | `/api/user/set-owner` | body `{Target, Owner?}` | `UserController.SetOwner` | Y + policy **Admin** | attach (به مدیر فعال، بدون چرخه) / detach (`Owner=null`) + بی‌اعتبارسازی کش دسترسی مالک‌ها. |
| POST | `/api/user/set-self-service` | body `{Target, Allow}` | `UserController.SetSelfService` | Y + policy **Manager** | فلگ مستقل/وابسته (ادمین: هرکس؛ مدیر: فقط زیر دست خودش). |
| POST | `/api/user/set-active` | body `{Target, Active}` | `UserController.SetActive` | Y + policy **Manager** | فعال/غیرفعال (D13؛ ادمین: هرکس بجز خودش — D9). |
| GET | `/api/server/list` | — | `ServerController.GetList` | Y + policy **Admin** | لیست سرورها (docs/20 فاز ۵/D8): فقط فیلدهای اسکالر + `hasConfig` + نام realm — کانفیگ ذخیره‌شده (متنی یا محافظت‌شده) هرگز در پاسخ نیست. |
| POST | `/api/server/create` | body `SaveServerModel` | `ServerController.Create` | Y + policy **Admin** | ساخت سرور فعال؛ اعتبارسنجی IPv4/طول نام و دامنه (≤۱۵)/پهنای باند مثبت/OS معتبر/بیت‌های شناخته‌شده‌ی `Features`/realm موجود؛ تکرار IP یا نام یا دامنه → 409؛ کانفیگ JSON ارسالی سمت سرور deserialize و `ServerConfigProtection.Protect` می‌شود (نوشتن بدون خواندن). |
| POST | `/api/server/update` | body `SaveServerModel` با `Id>0` | `ServerController.Update` | Y + policy **Admin** | به‌روزرسانی کامل فیلدهای اسکالر؛ `config` تهی → blob محافظت‌شده‌ی موجود بدون تغییر حمل می‌شود؛ ارسال config → جایگزینی کامل با Protect تازه (+ گارد طول ۴۰۰۰). |
| POST | `/api/server/set-active` | body `{Id, Active}` | `ServerController.SetActive` | Y + policy **Admin** | فعال/غیرفعال کردن سرور — toggle دوجهته (به‌جای `deactivate` تک‌جهته‌ی گام ۱۸ docs/20؛ انحراف ثبت‌شده، هم‌ارز فاز ۴). |

> توجه: همه‌ی endpoint های `BillingController` دارای `[Authorize]` هستند و `LoadJobContext(target)` را صدا می‌زنند (چنداکانتی). `payment-callback` در فاز P2 حذف شد (درگاه پرداخت وجود ندارد). endpointهای `WalletController` (فاز ۳ پنل) همین الگو را دارند؛ `adjust` علاوه بر آن policy ادمین می‌خواهد. endpointهای `UserController` (فاز ۴ پنل) عمداً target را از `LoadJobContext(target)` عبور نمی‌دهند — رابطه actor→target در application زنده از دیتابیس اعتبارسنجی می‌شود (زیر دستِ تازه‌ساخته در همان نشست قابل مدیریت است). endpointهای `ServerController` (فاز ۵ پنل) همگی policy ادمین دارند و `LoadJobContext()` بدون target صدا می‌زنند؛ نقش ادمین علاوه بر policy در عمق application هم چک می‌شود.

## Request DTOs (Context)

در `Backend/Portal/Context/` (۶ فایل) به‌علاوه‌ی `ResetPasswordContext` در `Application/Authentication/Model/`:

| DTO | Used by | Fields |
|---|---|---|
| `TokenContext` | `/auth/token` | `Username`, `Password` |
| `ResetPasswordContext` | `/auth/forget-pass` | `EmailMobile` |
| `ChangePasswordContext` | `/auth/reset-pass`, `/account/change-pass` | `Token` (کد/پسورد قدیم), `Password` |
| `ChangeOvpnContext` | `/vpn/change-ovpn` | `Target`, `Token` (پسورد اکانت), `Password` |
| `CloseConnectionContext` | `/connection/close-con` | `Target`, `Server`, `SessionId` |
| `HistoryContext` | `/account/history` | `From`, `To` |
| `RenewalContext` | `/plan/estimate`, `/plan/renewal` | `Target`, `SimultaneousUserCount`, `Days?`, `Gigabytes?` |

مدل‌های دامنه‌ای که به‌عنوان body استفاده می‌شوند:
- `RegisterModel` (`Domain/Account/Model/RegisterModel.cs`) — `extends EditUserModel`.
- `EditUserModel` (`Domain/Account/Model/FullUserModel.cs`) — `Firstname`, `Lastname`, `Email`, `Mobile` و …

## Differences vs `Architecture/analyse.md`

مستند قدیمی `analyse.md` (نوشته‌شده قبل از پیاده‌سازی نهایی) با پیاده‌سازی فعلی این تفاوت‌ها را دارد:

| Topic | `analyse.md` | Current implementation |
|---|---|---|
| `/auth/logout` | GET موجود بود | حذف شده؛ فرانت با پاک کردن توکن لاگ‌اوت می‌کند (مسیر `logout` به `LoginComponent` می‌رود). |
| `/auth/forget-pass` request | `{ emailMobile }` | body `ResetPasswordContext` با فیلد `EmailMobile` (یکسان). |
| `/connection/close-con` | بدون فیلد سرور | اکنون `Server` + `SessionId` (`CloseConnectionContext`). |
| `/billing/*` | در analyse نبود | اضافه شده: `pay`, `get-invoice`, `register-receipt`, `settle-wallet` (`payment-callback` حذف شد). |
| `/plan/renewal` request | شامل `type`/`value` | اکنون `Days`/`Gigabytes`/`SimultaneousUserCount` (`RenewalContext`). |

## Request topology

```mermaid
flowchart LR
    C[Client / Frontend] -->|HTTPS| PRX[Angular proxy /api -> :5226]
    PRX --> MW[ExceptionHandlingMiddleware]
    MW --> AUTH[UseAuthentication - JWT Bearer]
    AUTH --> AUTHZ[UseAuthorization]
    AUTHZ --> CTL[Controller : ResultHandlerController]
    CTL -->|LoadJobContext| ACC[IAccessService + IJobContext]
    CTL --> APP[Application use-case]
    APP --> REPO[Repositories]
    REPO --> DB[(Local MSSQL)]
    APP --> RAD[Radius dispatchers]
    RAD --> EXT[(rad-db / rad-api / Mikrotik SSH)]
    CTL -->|ApiResult| MW --> C
```

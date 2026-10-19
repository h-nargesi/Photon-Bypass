# docs/11-business-flows.md — End-to-end scenarios

> **English summary**: Step-by-step walkthroughs of the common user journeys and the periodic background behaviors: Register, Login, Dashboard/traffic, Renewal (with invoice), traffic sync, capacity management, deactivation cascade, and multi-account delegation. Each has a short Mermaid sequence diagram and the key code anchors.

## 1. Register (ثبت‌نام)

`AuthApplication.Register` → اعتبارسنجی (regex در `AccountBusiness`) → ساخت `AccountEntity` (`CreateFromModel`) → هش پسورد لاگین + `VpnPassword` تصادفی → ذخیره در local DB → هشدار اجتماعی (WhatsApp `#if SOCIAL`).

```mermaid
sequenceDiagram
    participant U as کاربر
    participant CTL as AuthController
    participant APP as AuthApplication
    participant ACC as AccountApplication/Repo
    participant SOC as SocialMediaService
    U->>CTL: POST /api/auth/register (RegisterModel)
    CTL->>APP: Register(model)
    APP->>APP: AccountBusiness.CreateFromModel (username/email/mobile regex)
    APP->>APP: hash password + random VpnPassword
    APP->>ACC: save AccountEntity
    APP->>SOC: NewUserRegistrationAlert (stub / SOCIAL)
    APP-->>U: success
```

> در طراحی اولیه قرار بود کاربر هم‌زمان در ردیوس ثبت شود (`analyse.md:325`)؛ وضعیت فعلی را در کد بررسی کنید (`docs/12`).

## 2. Login (ورود)

`AuthController.Login` → `AuthApplication.CheckUserPassword` (local DB) → ساخت `TargetArea` (مدیر همه‌ی زیرمجموعه‌ها را می‌بیند) → صدور JWT → `AccessService.LoginEvent` (set cache `TargetArea|{username}`).

```mermaid
sequenceDiagram
    participant U
    participant CTL as AuthController
    participant APP as AuthApplication
    participant ACC as AccountRepo
    participant ACCS as AccessService (IMemoryCache)
    U->>CTL: POST /api/auth/token (TokenContext)
    CTL->>APP: CheckUserPassword(user, pass)
    APP->>ACC: load account + verify hash
    alt پسورد اشتباه
        APP-->>U: 401 (در صورت فعال بودن SOCIAL: InvalidPasswordAlert)
    else موفق
        APP-->>CTL: TargetModel + TargetArea
        CTL->>ACCS: LoginEvent(user, area keys)  %% cache
        CTL->>CTL: GenerateToken (HmacSha512, expires 1h)
        CTL-->>U: access_token / expires_in=3600
    end
```

## 3. Dashboard / traffic (داشبورد و مصرف)

- `UpdateTrafficData` (در `JobInterval`) به‌صورت دوره‌ای ترافیک را از radius pull می‌کند و در `TrafficData` ذخیره می‌کند.
- صفحه‌ی داشبورد `GET /api/plan/plan-state` را می‌زند که از view `TotalPlanState` وضعیت جاری را می‌خواند (`docs/09`).
- نمودار ۳۰ روزه از `GET /api/vpn/traffic-data` سرو می‌شود.

```mermaid
sequenceDiagram
    participant Job as JobInterval (60m)
    participant MNG as ServerManagementService
    participant RAD as Radius rad-db/rad-api
    participant TD as TrafficDataRepository
    Job->>MNG: UpdateTrafficData
    MNG->>RAD: pull traffic per account
    MNG->>TD: merge TrafficDataEntity
    Note over TD: TotalPlanState view استفاده از این رکوردها
```

## 4. Renewal (تمدید — با فاکتور)

جریان کلیدی `PlanApplication.Renewal`:

1. برآورد قیمت با `PriceCalculator` (Roslyn، کد در DB).
2. بررسی موجودی با `WalletBusiness.CheckMoneyNeed`.
3. **اگر موجودی کافی است**: تراکنش local + `SyncUserAndActive` روی radius + commit (در صورت خطا، `DeactivateUsers` + rollback).
4. **اگر موجودی ناکافی است**: صدور فاکتور با `BillingApplication.GenerateInvoiceCode` و `Action = "{target}t|{count}u|{days}d|{gigabytes}g"`؛ کاربر به درگاه می‌رود.
5. در کال‌بک (`PaymentCallback`)، فاکتور completed می‌شود و `PlanApp.Renewal(accountId, walletId, action)` دوباره اجرا می‌شود؛ این بار موجودی کافی است و تمدید انجام می‌شود.
6. اگر تعداد کاربر کاهش یافته باشد، کانکشن‌های اضافه بسته می‌شوند.

```mermaid
sequenceDiagram
    participant U
    participant CTL as PlanController
    participant APP as PlanApplication
    participant BILL as BillingApplication
    participant WAL as WalletRepo
    participant DEL as OnRenewalDelegation
    participant RAD as AccountRadiusSyncService
    U->>CTL: POST /api/plan/renewal (RenewalContext)
    CTL->>APP: Renewal(target,count,days,gigs)
    APP->>APP: PriceCalculator.CalculatePrice
    APP->>WAL: GetBalance
    alt موجودی ناکافی
        APP->>BILL: GenerateInvoiceCode(action=t|u|d|g)
        APP-->>U: InvoiceCode -> درگاه
        Note over U: پرداخت
        U->>CTL: GET /api/billing/payment-callback?code
        CTL->>BILL: PaymentCallback(code)
        BILL->>WAL: mark Completed
        BILL->>APP: Renewal(accountId, walletId, action)
        APP->>APP: parse action (t/u/d/g)
    end
    APP->>WAL: BeginTransactionAsync
    APP->>WAL: Save Debit + Renewal
    APP->>DEL: OnRenewalDelegation (انتخاب realm/سرور)
    APP->>RAD: SyncUserAndActive(account, renewal)
    opt تعداد کاربر کمتر
        APP->>RAD: close extra connections
    end
    APP->>WAL: CommitAsync
    APP-->>U: RenewalResult
```

## 5. Traffic sync (همگام‌سازی دوره‌ای ترافیک)

در `JobInterval` (هر ۶۰ دقیقه) به‌عنوان اولین کار سریال: `ServerManagementService.UpdateTrafficData` از radius pull می‌کند، `TrafficDataEntity` را merge می‌کند و `Realm.LastTrafficSync` را به‌روزرسانی می‌کند.

## 6. Capacity management (مدیریت ظرفیت)

`ServerManagementService.CheckUserServerBalance`:
- نرخ مصرف realm را از ترافیک ۳۰ روز گذشته نسبت به `BandWidth` محاسبه می‌کند.
- اگر <۱۰٪ یا >۹۰٪ باشد هشدار ادمین (`SocialMediaService.AlarmServerCapacity`).
- هنگام تمدید جدید، realm کم‌بار برای `RestrictedRealmId` انتخاب می‌شود (`PlanRepo.GetTopRestrictedRealmId`).

```mermaid
flowchart LR
    TD[TrafficData 30d] --> CALC[Realm rate vs BandWidth]
    CALC -->|<10% or >90%| ALARM[AlarmServerCapacity]
    CALC --> PICK[Pick low-load realm]
    RENEW[New Renewal] --> PICK --> RESTRICT[RestrictedRealmId]
```

## 7. Deactivation cascade (آبشار غیرفعال‌سازی)

در `JobInterval` (موازی با Task.WaitAll):
- **`InactiveAbandonedUsers`** (`AccountMonitoringService`): اکانت‌های رهاشده پس از `MaxDaysDeactivatePlanToDisable=7` روز غیرفعال می‌شوند و پس از `MaxDaysDeactivatePlanToDelete=40` روز از radius حذف می‌شوند (آبشار ۴۰روزه اکانت را `Deactivated` می‌کند — `SetDeactivated`).
- **`DeactivateInvalidRadiusUsers`** (`AccountRadiusSyncService`): کاربرانی که پلن معتبر ندارند **یا `Status != Active` دارند** در radius غیرفعال می‌شوند (`DeactivateUserExcept` realm) — این sweep تضمین می‌کند تعلیق/غیرفعال‌سازی مدیریتی حتی اگر sync فوری شکست خورده باشد حداکثر ظرف یک ساعت روی همه‌ی سرورها اعمال شود.
- **`NotifSendServices`** (`AccountMonitoringService`): هشدار پایان پلن به‌صورت ایمیل/واتساپ با throttle `DelayBetweenWarnings=20h` — لیست ورودی از قبل `Status != Deactivated` فیلتر می‌شود.

```mermaid
flowchart TB
    PS[All PlanState] --> A{InactiveAbandonedUsers}
    PS --> B{DeactivateInvalidRadiusUsers}
    MON[monitored list: Status != 2] --> A
    MON --> C{NotifSendServices}
    A -->|>7 day inactive| DA[Deactivate in Radius]
    A -->|>40 day inactive| RM[Remove from Radius + SetDeactivated]
    B -->|no valid plan or Status != 0| DA
    C -->|IsFinishing &amp; !OverWarningTime| EM[Email + WhatsApp SOCIAL]
    C -->|update LastWarningTime| TH[throttle 20h]
```

## 7b. Two-stage deactivation (غیرفعال‌سازی دووضعیتی — docs/20 D13)

`POST /api/user/set-status` وضعیت سه‌مقداری `AccountStatus` را تنظیم می‌کند (جایگزین `set-active`):

| وضعیت | لاگین پنل | VPN | تمدید |
|---|---|---|---|
| `Active=0` | ✅ | فعال (در valid-set ساعتی) | ✅ |
| `VpnSuspended=1` | ✅ | غیرفعال در همه‌ی سرورها | ❌ `UserException` با پیام «سرویس VPN شما توسط مدیریت غیرفعال است…» |
| `Deactivated=2` | ❌ (401 از `CheckUserPassword`/`RebuildTargetArea`) | غیرفعال | ❌ (از `GetActiveUser` رد می‌شود) |

- ترنزیشن آزاد بین هر سه وضعیت (شامل 2→1: بازگرداندن لاگین بدون فعال‌سازی VPN)؛ same-status → موفق بدون عارضه جانبی.
- بعد از ذخیره، sync فوری best-effort (`ActivateUsers`/`DeactivateUsers`) داخل try/catch اجرا می‌شود — شکست فقط `Log.Error` می‌دهد و sweep ساعتی آشتی می‌کند؛ بستن اتصال‌های زنده کار خود سرور مدیریت کاربر است (D-D).
- ترنزیشن‌های درگیر `Deactivated` کش `TargetArea|{username}` را بی‌اعتبر می‌کنند.
- قاعده‌ی نگاشت در کد: «portal-accessible» = `Status in (0,1)` (لاگین/دسترسی/بازیابی رمز) و «VPN-valid» = `Status = 0` (کانفیگ فعال، برآورد ظرفیت، قاعده‌ی آستانه‌ی کیف پول).
- قاعده‌ی کیف پول (`WalletThresholdService`): فقط اکانت‌های `Active` را لمس می‌کند — بازیابی موجودی هرگز VPNِ تعلیق‌شده توسط مدیر را روشن نمی‌کند.

## 8. Multi-account delegation (تکلیف زیرمجموعه)

کاربر مدیر با `target` زیرمجموعه‌ی خود عمل می‌کند:

```mermaid
sequenceDiagram
    participant M as Manager (frontend)
    participant CTL as Controller
    participant ACCS as AccessService
    participant JC as JobContext
    participant APP as Application
    M->>CTL: request ?target=subuser
    CTL->>ACCS: LoadJobContext(subuser) -> CheckAccess(manager, subuser)
    alt دسترسی مجاز
        ACCS->>JC: set Target=subuser
        CTL->>APP: use JobContext.Target
        APP-->>M: result for subuser
    else غیرمجاز
        ACCS->>CTL: UserException(403)
        CTL-->>M: 403 ApiResult
    end
```

> نکته: اکانت با `Id==1` (admin) در سطح دسترسی به همه‌ی کاربران دسترسی دارد (تعریف `TargetArea` در لاگین).

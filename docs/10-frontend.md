# docs/10-frontend.md — Angular frontend (summary)

> **English summary**: Angular 19 standalone app using CoreUI 5 layout, Angular Material, and Chart.js (`@coreui/angular-chartjs`). Dev server proxies `/api` → `localhost:5226` (`proxy.conf.json`). An injected `API_BASE_URL = 'api'` token is prepended to all API URLs. Routes: public Home/Register/Login/Forgot + a `DefaultLayoutComponent` shell with children (dashboard, history, edit info, change password/ovpn, renewal, payment). The `UserService` manages multi-account `target` and caches it in `localStorage`.

## Stack

از `Frontend/package.json`:
- **Angular 19** (standalone) + CDK + Material + animations.
- **CoreUI 5** (`@coreui/angular`, `@coreui/coreui`, `@coreui/icons-angular`, `@coreui/utils`).
- **Chart.js 4** (`chart.js` + `@coreui/angular-chartjs` + `@coreui/chartjs`).
- `lodash-es`, `ngx-scrollbar`, `rxjs`.
- Test: Karma + Jasmine.

## Build & run

```bash
cd Frontend
yarn install
yarn start     # ng serve با proxy به :5226
yarn build     # تولید
yarn test      # Karma/Jasmine
```

Proxy در `Frontend/proxy.conf.json`: مسیر `/api` → `http://localhost:5226` (هدف بک‌اند Portal).

## API base & HTTP client

- `app.config.ts` تزریق `{ provide: API_BASE_URL, useValue: 'api' }` را ثبت می‌کند. این توکن در `HttpClientHandler` به‌عنوان پیشوند به همه‌ی URLها اضافه می‌شود (`http-client.ts:28`).
- ثابت‌های مسیر در `app-api-url.ts` **بدون** پیشوند `/api` هستند (`/basics`, `/auth`, `/account`, `/vpn`, `/connection`, `/plan`)؛ چون `API_BASE_URL = 'api'`، مسیر نهایی `api/<controller>/<action>` می‌شود که با route بک‌اند `/api/[controller]` تطابق دارد (Note: billing در این فایل ثابت ندارد — مستقیم استفاده می‌شود).
- الگوی `HttpClientHandler`: متدهای `call`/`get` (GET) و `job`/`post` (POST) و `authorization` (POST + ذخیره‌ی توکن در `localStorage[user.bearer]`). هر کدام هدر `Authorization: Bearer <token>` را از localStorage می‌سازند و خروجی را به `ApiResult` نگاشت می‌کنند.
- مدیریت خطا: `errorHandler` یک `ApiResult` با کد می‌سازد و `status()` بر اساس محدوده‌ی کد وضعیت تعیین می‌کند (`uiFatal` ≥600، `serverFatal` ≥500، `error` ≥400، `warning` ≥300، `success` ≥200، در غیر این صورت `info`).

## Routes (مسیریابی)

از `Frontend/src/app/app.routes.ts`:

- عمومی (بدون layout):
  - `''` → `HomeComponent`
  - `register` → `RegisterComponent`
  - `login` / `logout` → `LoginComponent`
  - `forgot-password` → `ForgotPasswordComponent`
  - `reset-password` → `ChangePasswordComponent`
  - `**` → redirect به `''`
- زیر `DefaultLayoutComponent` (children):
  - `dashboard`, `history`, `edit-user-info` (→ RegisterComponent), `edit-account-info` (→ RegisterComponent), `change-password` (→ ChangePasswordComponent), `change-ovpn-password` (→ ChangePasswordComponent), `renewal`, `payment`.

## Structure (ساختار)

```mermaid
flowchart LR
    subgraph App[Frontend/src/app]
        Routes[app.routes.ts]
        Cfg[app.config.ts - providers]
        Layouts[layouts: default-layout]
        Pages[pages: home/login/register/.../dashboard/...]
        subgraph Svcs[@services]
            Api[api-services<br/>HttpClientHandler, api-base-service]
            Auth[auth-services: AuthService, UserService]
            LS[local-storage]
            I18n[translation]
            Val[validators]
            Msg[message-handler]
        end
        Models[@models: api-result, user-info, ...]
        Icons[@icons]
    end
    Pages --> Svcs
    Svcs -->|HttpClient /api| BE[Backend :5226]
```

- `@services/api-services/` — `HttpClientHandler` (الگوی call/get/job/post/authorization)، `ApiBaseService`، `FakeDataService`، مدل‌های `app-api-url.ts`/`app-base-path.ts`.
- `@services/auth-services/` — `AuthService`, `UserService` (**نکته**: نام فایل `user-servcie.ts` با غلط املایی در سورس ثبت شده).
- `@services/{local-storage,translation,validators,message-handler}`.
- `@models/` — `api-result`, `user-info`, `user-plan-info`, `traffic-data`, `history-record`, `connection-state`, `prices`, `basics`, `data-model`, `meta-model`.
- `@icons/` — آیکن‌های CoreUI.

## Multi-account in frontend (چنداکانت در فرانت)

`UserService` (`@services/auth-services/user-servcie.ts`):
- `targetUser` / `targetName` / `hasSubUsers` — target فعلی زیرمجموعه.
- `onTargetChanged` (Observable) — رویداد تغییر target.
- پس از fetch کاربر، `setTraget(...)` از `LocalStorageService.get(['user','target'])` target ذخیره‌شده را اعمال می‌کند (`:49`).
- target در `localStorage` کلید `['user','target']` کش می‌شود.
- اگر کاربر `targetArea` نداشته باشد، set target fail می‌شود.

## Result contract

`ApiResult` در فرانت با `code` و تابع `status()` که نمایش (toast/dialog) را تعیین می‌کند. `MessageMethod` هم روش نمایش (مثلاً `toaster`) را کنترل می‌کند. این قرارداد با `ApiResult` بک‌اند (`docs/08`) هم‌خوان است.

## Design references

- صفحات پروفایل از CoreUI Free Angular template (`README.md:94`).
- صفحه‌ی Home از قالب‌های nicepage (`README.md:73-75`).
- ماکت‌های صفحه در `Design/*.png`.

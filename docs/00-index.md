# docs/00-index.md — Documentation map

> **English summary**: This index lists every file in the `docs/` set with a one-line purpose and a "read this when…" table. Start here to pick the right file. Files are numbered to suggest a reading order (overview → deep technical → flows → state).

هر فایل این پوشه با یک عنوان انگلیسی و توضیحات فارسی نگاشته شده است. اگر برای بار اول مستندات را می‌خوانید، به‌ترتیب شماره‌ها پیش بروید. جدول «برای چه کاری این فایل را بخوان» به شما می‌گوید هر فایل را کِی باز کنید.

## Files

| # | File | One-line purpose |
|---|---|---|
| 00 | [docs/00-index.md](00-index.md) | This map. |
| 01 | [docs/01-overview.md](01-overview.md) | نگاه محصول: دو وب‌سایت، نقش‌ها، منابع خارجی، فازبندی. |
| 02 | [docs/02-architecture.md](02-architecture.md) | پروژه‌های سولوشن، وابستگی‌ها، و ریشه‌ی DI. |
| 03 | [docs/03-domain.md](03-domain.md) | Entityها، enumها، و قواعد کسب‌وکار (Business). |
| 04 | [docs/04-application.md](04-application.md) | سرویس‌های Application، تراکنش‌ها، و `JobInterval`. |
| 05 | [docs/05-infrastructure.md](05-infrastructure.md) | Repositoryها، Dapper، Entity events، و PriceCalculator. |
| 06 | [docs/06-radius-integration.md](06-radius-integration.md) | همگام‌سازی Radius دوگانه (RadiusDesk + UserManager). |
| 07 | [docs/07-server-bridge.md](07-server-bridge.md) | موتور SSH/Process/Script روی سرورها. |
| 08 | [docs/08-portal-api.md](08-portal-api.md) | جدول کامل endpointهای REST. |
| 09 | [docs/09-database.md](09-database.md) | اسکیمای MSSQL، view کلیدی `TotalPlanState`، و دیتابیس Radius. |
| 10 | [docs/10-frontend.md](10-frontend.md) | خلاصه‌ی اپ Angular 19. |
| 11 | [docs/11-business-flows.md](11-business-flows.md) | سناریوهای end-to-end (ثبت‌نام، تمدید، ترافیک و …). |
| 12 | [docs/12-current-state.md](12-current-state.md) | پیاده‌سازی‌شده در برابر نیمه‌کاره + نقطه‌های شروع. |

## Read this file when…

| You want to… | Read |
|---|---|
| Understand the product in plain terms | [01-overview](01-overview.md) |
| Add/rename a project or wire a new DI registration | [02-architecture](02-architecture.md) |
| Add/change an entity, enum, or business rule | [03-domain](03-domain.md) |
| Add a use-case or a background job | [04-application](04-application.md) |
| Add a repository or change data access | [05-infrastructure](05-infrastructure.md) |
| Work with RadiusDesk or Mikrotik UserManager | [06-radius-integration](06-radius-integration.md) |
| Write/debug a NAS script or SSH command | [07-server-bridge](07-server-bridge.md) |
| Add/verify an HTTP endpoint | [08-portal-api](08-portal-api.md) |
| Change the DB schema or understand plan-state math | [09-database](09-database.md) |
| Change a frontend page/route | [10-frontend](10-frontend.md) |
| Trace a real user journey (register → renew → warn) | [11-business-flows](11-business-flows.md) |
| Find a stub to implement | [12-current-state](12-current-state.md) |

## Related legacy docs

- `Architecture/structure.md` — original DDD layer sketch (cross-linked to `docs/02`).
- `Architecture/analyse.md` — original page-by-page external-resource analysis (cross-linked to `docs/08`/`docs/11`).
- `Architecture/TODO.md` — original backlog (mapped in `docs/12`).
- `README.md` — original Persian feature list and phase plan.

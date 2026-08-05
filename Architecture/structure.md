# Photon Bypass Structure (legacy note)

> **English summary**: This is the original Persian DDD layer sketch written before the implementation was finalized. The current, authoritative architecture documentation lives in [`docs/02-architecture.md`](../docs/02-architecture.md) (projects, dependencies, DI composition root) and [`AGENTS.md`](../AGENTS.md) (entry point). Read this file for the original intent; cross-check against `docs/` for what was actually built.

> **خلاصه**: این یادداشت قدیمی لایه‌بندی DDD است که پیش از پیاده‌سازی نهایی نوشته شده است. مستندات معتبر و به‌روز معماری در [`docs/02-architecture.md`](../docs/02-architecture.md) و [`AGENTS.md`](../AGENTS.md) قرار دارد؛ این فایل را برای درک نیت اولیه بخوانید و با `docs/` تطبیق دهید.

---

## Domain-Driven Design

## Presentation
API (Management): Portal, Management
Portal (Portal): Application

## Application
Management (Management): Domain, Business
Application (Portal): Domain, Business

## Domain
Business
Domain

## Infrastructure
Infrastructure: Domain
Data Repository: Infrastructure, Domain
Mikrotik Radius: Server Bridge, Domain
FreeRadius: Infrastructure, Server Bridge, Domain
Outsource (Email/Media): Infrastructure, Domain
Mikrotik Setup: Server Bridge, Domain

## Basical Services
Server Bridge: SSH, API
Shared

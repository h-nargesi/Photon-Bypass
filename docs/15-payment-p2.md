# docs/15-payment-p2.md — طراحی فاز P2: پرداخت کارت‌به‌کارت و کیف پول

> **English summary**: Final design and implementation spec for audit phase P2, replacing the original "fix frontend payment contract" scope with the product owner's design (`Architecture/peyment-P2.md`). Two payment paths (wallet top-up, direct plan payment), manual card-to-card with receipt registration as the only money-in channel (no gateway, no OCR in P2), a `Verifying` transaction state whose credit counts immediately, invoice netting against wallet balance, and a backend-only wallet-threshold deactivation rule. Admin approval/rejection is deferred to the (not-yet-started) admin panel phase. All findings below were verified against code on 1405/07 (2026-10).
>
> **فارسی**: طراحی نهایی و مشخصات اجرایی فاز P2 ممیزی. دامنه از «اصلاح قرارداد API پرداخت فرانتند» به طرح جدید صاحب محصول ارتقا یافت. مسیرهای پرداخت، مدل داده، قرارداد API، حسابداری هر مسیر، تست‌ها و دیپلوی اینجا ثبت شده است.

## منابع

| سند | نقش |
|---|---|
| `Architecture/peyment-P2.md` | سند طراحی اولیه صاحب محصول (متن اصلی + تصمیمات جلسه ۱۴۰۵/۰۷ در انتهای آن) |
| این فایل | مشخصات فنی قابل اجرا + تصمیمات قطعی + یافته‌های راستی‌آزمایی‌شده |
| `docs/14-defects-audit.md` | فاز P0–P5 ممیزی؛ آیتم‌های C3 و M25 داخل همین فاز حل می‌شوند |

## تصمیمات قطعی (جلسه ۱۴۰۵/۰۷)

| موضوع | تصمیم |
|---|---|
| درگاه پرداخت آنلاین | **وجود ندارد**؛ کانال ورود پول فقط کارت‌به‌کارت است. سوال باز P2 در `docs/14` (طراحی `payment-request`/redirect) منسوخ شد. |
| مسیرهای پرداخت | ۱) افزایش موجودی کیف پول ۲) پرداخت مستقیم پلن (بعد از تایید پلن، صفحه فاکتور با انتخاب روش: کیف پول / کارت‌به‌کارت) |
| صفحه فاکتور | **همیشه** بعد از تایید پلن نمایش داده می‌شود؛ اجرای فوریِ مسیر موجودی-کافی حذف می‌شود و «پرداخت از کیف پول» با دکمه تایید روی فاکتور انجام می‌شود |
| خالص‌سازی (netting) | اگر موجودی کافی نباشد، مبلغ کیف پول از فاکتور کسر و «مبلغ قابل پرداخت» نشان داده می‌شود؛ `payable = قیمت − موجودی` (موجودی منفی بدهی را اضافه می‌کند) |
| رسید | تصویر **یا** متن (XOR — فقط یکی قابل درج است)؛ یک رسید برای هر فاکتور |
| مدل اعتبار | با **ثبت رسید** تراکنش به وضعیت `Verifying` می‌رود و اعتبار آن **بلافاصله** معتبر است (در موجودی شمرده می‌شود): پلن فعال می‌شود یا موجودی بالا می‌رود |
| OCR (تبدیل تصویر به متن) | Tesseract برای **فازهای بعدی** پذیرفته شد؛ در P2 نیست. تصویر برای بررسی ادمین ذخیره می‌شود؛ متن رسید = تایپ کاربر |
| پنل ادمین | **فاز جداگانه** (پنل هنوز شروع نشده). صفحات و endpoint های تایید/رد تراکنش و کسر اعتبار در رد (`Verifying→Failed`) آنجا می‌آیند. P2 فقط مدل داده را آماده می‌کند. |
| اطلاعات کارت به کارت | شماره کارت/بانک/صاحب حساب از `appsettings` خوانده و در صفحه فاکتور/پیش‌نمایش نمایش داده می‌شود |
| آستانه کیف پول | **در P2 فقط بک‌اند**: تنظیم آستانه + بررسی دوره‌ای + غیرفعال/فعال‌سازی کانفیگ‌ها در همه سرورها (بدون تغییر `Account.IsActive` — کاربر باید بتواند وارد شود و بپردازد) |
| `payment-callback` | endpoint حذف می‌شود (بدون caller معتبر، بدون درگاه، `TODO: check token` حل‌نشده)؛ منطق settlement در endpoint های جدید بازی‌استفاده می‌شود |
| C3 و M25 ممیزی | اصلاحات قرارداد API و double-submit داخل همین فاز انجام می‌شوند |

## یافته‌های راستی‌آزمایی‌شده روی کد فعلی (پایه طراحی)

1. فرانتند به `PLAN_API_URL` (`'/plan'`) زنگ می‌زند ولی بک‌اند در `BillingController` روی `/api/billing/...` سرو می‌کند — همه فراخوانی‌های پرداخت 404 (`Frontend/src/app/payment/payment-service.ts:10,17`، `payment-modal.service.ts:11`، `app-api-url.ts:6`).
2. `payment-request` اصلاً در بک‌اند وجود ندارد؛ مودال افزایش موجودی مرده است (`payment-modal.component.ts:65`).
3. `isNumber(code)` روی string همیشه false → صفحه payment همیشه به dashboard ریدایرکت می‌شود (`payment.component.ts:51`).
4. بک‌اند `RenewalResult` فقط `currentPrice` و `invoiceCode` برمی‌گرداند (`Backend/Application/Plan/Model/RenewalResult.cs`)؛ `result.moneyNeeds` در فرانتند همیشه `undefined` است → شاخه «نیاز به پرداخت» هرگز اجرا نمی‌شود (`renewal.component.ts:111`).
5. navigate به payment با invoice hard-code `"10"` (`renewal.component.ts:114`).
6. `Pay([FromBody] int value)` ولی فرانت `POST {value}` می‌فرستد → binding fail (`BillingController.cs:21`)؛ فرانت خروجی عددی pay را URL فرض می‌کند (`payment.component.ts:65`).
7. قالب `payment.component.html` فیلدهای `sum/discount/totalSum` را ожидار دارد که `InvoiceModel` بک‌اند هرگز نمی‌فرستد.
8. `GetBalance` فقط ردیف‌های `Completed` را جمع می‌زند (`WalletRepository.cs:90-100`) — با مدل «اعتبار Verifying» ناسازگار است و باید `Verifying` را هم بشمارد.
9. فاکتور فعلی خالص نیست: invoice به‌مقدار **کل قیمت** صادر می‌شود (`PlanApplication.cs:228`) و موجودی قبلی دست‌نخورده می‌ماند.
10. `CheckMoneyNeed` سمانتیک OldUser دارد: `money_need = estimate − balance`؛ OldUser با موجودی منفی مجاز به تمدید است (`WalletBusiness.cs:8-18`) — پایه فرمول payable.
11. `CheckUserServerBalance` درباره **ظرفیت سرورها** است نه موجودی کاربر (`ServerManagementService.cs:77-96`) — با قاعده آستانه اشتباه نشود.
12. `UserTypes` فلگ Admin ندارد؛ `Backend/Admin` فقط stub است → هرگونه تایید/رد در فاز ادمین.

## مدل داده هدف

### وضعیت تراکنش

`BalanceStatus` مقدار `Verifying = 4` می‌گیرد (بعد از `Pending, Completed, Failed, Canceled`):

```
Pending   → فاکتور صادر شده، رسیدی ثبت نشده (در موجودی شمرده نمی‌شود)
Verifying → رسید ثبت شده، منتظر بررسی ادمین (در موجودی شمرده می‌شود)
Completed → تایید شده (فاز ادمین)
Failed    → رد شده (فاز ادمین؛ اعتبار پس گرفته می‌شود)
Canceled  → تسویه از کیف پول (پول بیرونی وارد نشده)
```

`WalletRepository.GetBalance` جمع `Status in (Completed, Verifying)` می‌شود.

### جدول جدید `Invoice` (والد فاکتور + رسید)

| ستون | نوع | توضیح |
|---|---|---|
| `Code` | int, PK | از `InvoiceSequence` (موجود از P0) |
| `AccountId` | int | صاحب فاکتور |
| `Kind` | tinyint | 1=TopUp, 2=Plan |
| `TotalPrice` | int | قیمت کامل (پلن) یا مبلغ شارژ |
| `WalletDeduction` | int | کسر کیف پول = `max(min(balance, estimate), 0)` در زمان صدور |
| `Payable` | int | `estimate − balance` (ممکن است از قیمت کامل بیشتر باشد اگر balance منفی باشد) |
| `Action` | nvarchar(200) | پارامترهای تمدید (الگوی فعلی `PlanApplication.cs:231`) |
| `Status` | tinyint | هم‌ارزش `BalanceStatus`؛ انتقال اتمیک روی Wallet و Invoice با هم |
| `ReceiptImage` / `ReceiptText` / `ReceiptAt` | varbinary(max) / nvarchar(1000) / datetime2 | رسید؛ check constraint: هر دو با هم non-null ممنوع (XOR کامل در app) |
| `Created` | datetime2 | |

اسکریپت: `Database/LocalDatabase/PaymentP2.sql` (idempotent، اجرای دستی روی `FastBypass` قبل از release؛ بدون backfill — کدهای قدیمی بدون رکورد Invoice با نمایش فقط-ردیف‌ها handle می‌شوند).

### تنظیمات (`ManagementOptions` + `appsettings.json`)

- `PaymentCards`: لیست {BankName, CardNumber, HolderName} — نمایش در صفحه فاکتور
- `WalletDeactivationThreshold`: int? — null/≤0 = قاعده خاموش
- `ReceiptMaxBytes`: پیش‌فرض 2MB

## قرارداد API هدف (`BillingController`)

| Endpoint | شرح |
|---|---|
| `POST /api/billing/pay` `{value, target?}` | صدور فاکتور TopUp (کد فاکتور برمی‌گرداند)؛ bounds فعلی M15 حفظ |
| `GET /api/billing/get-invoice?code&target` | مدل کامل: code, kind, status, totalPrice, walletDeduction, payable, walletBalance, allowWallet, items[], cardInfo[], hasReceipt |
| `POST /api/billing/register-receipt` (multipart: code, target?, file XOR text) | ثبت رسید → `Verifying` → settlement (Plan: اجرای Renewal؛ TopUp: هیچ) |
| `POST /api/billing/settle-wallet` `{code, target?}` | فقط Plan با `allowWallet`؛ credit→Canceled + دبیت TotalPrice + اجرای Renewal |
| ~~`GET /api/billing/payment-callback`~~ | **حذف** |

همه با `LoadJobContext(target)` طبق قرارداد چنداکانت. منطق idempotent و atomic-check-and-set اصلاحات C5 (`GetByWalletCredit`، `WHERE Status=@from`) به مسیرهای جدید منتقل می‌شود.

## حسابداری مسیرها

فرض: `E`=قیمت پلن، `B`=موجودی (شامل Verifying):

| مسیر | رخداد | موجودی پس از |
|---|---|---|
| شارژ کیف پول (کارت‌به‌کارت) | credit `P` → Verifying | `B + P` |
| پلن، کیف پول کافی (`B≥E`) | دبیت `E` (Completed)؛ credit→Canceled | `B − E` |
| پلن، ناکافی (`0<B<E`) | credit `E−B` → Verifying؛ سپس دبیت `E` | `0` |
| پلن، موجودی منفی (OldUser) | credit `E−B` → Verifying؛ سپس دبیت `E` | `0` (بدهی در فاکتور تسویه شد) |
| رد تراکنش توسط ادمین (فاز بعد) | Verifying→Failed | کاهش به اندازه اعتبار (ممکن است منفی → قاعده آستانه) |

قاعده اجرایی مهم: مسیر اجرای Renewal در settlement اگر در زمان اجرا money-need>0 داشت (رقابت/خرید موازی) باید `UserException` بدهد، **نه** صدور فاکتور جدید.

## قاعده آستانه کیف پول (بک‌اند فقط)

- سرویس جدید `WalletThresholdService` (ثبت در `Application/ServiceFactory.cs`).
- کوئری گروهی: `sum(Amount*Direction) where Status in (Completed,Verifying) group by AccountId having < @threshold`.
- زیر آستانه → غیرفعال‌سازی کاربر Radius در همه سرورها با پریمیتیو‌های موجود `IAccountRadiusSyncService` (idempotent طبق الگوی اصلاح H16: skip اگر قبلاً disabled)؛ بالای آستانه → فعال‌سازی مجدد. `Account.IsActive` دست نمی‌خورد. رکورد History برای هر تغییر.
- فراخوانی: JobInterval ساعتی + بعد از settlement های موفق (register-receipt / settle-wallet). اصلاح ساختاری H3 (هم‌پوشانی job) خودش P4 است.

## تغییرات فرانتند (خلاصه)

1. `BILLING_API_URL = '/billing'` در `app-api-url.ts`؛ هدایت هر دو سرویس پرداخت به آن.
2. مودال افزایش موجودی → `POST /billing/pay` → navigate به payment با کد واقعی.
3. `payment.component.ts`: رفع `isNumber` (تبدیل `+code` + `Number.isFinite`)؛ صفحه فاکتور جدید: اقلام، کسر کیف پول، قابل پرداخت، اطلاعات کارت؛ روش «کیف پول» فقط با `allowWallet` → `settle-wallet`؛ «کارت‌به‌کارت» با `payable>0` → فرم رسید (فایل XOR متن) → `register-receipt` (FormData) → پیام «ثبت شد / در انتظار تایید». حذف `window.location.href`.
4. `user-plan-info.ts`: `RenewalResult {currentPrice, moneyNeeds?, invoiceCode?}` + مدل واقعی `PaymentInvoice`.
5. `renewal.component.ts`: navigate با `result.invoiceCode`؛ حذف `console.log` (L4).
6. M25 (double-submit): دکمه‌های renewal / payment / payment-modal / register / login در حین درخواست disable.
7. کلیدهای ترجمه فارسی جدید.

## تست و اعتبارسنجی

- xUnit (الگوی Mock موجود): XOR و مالکیت و idempotency رسید، اجرای یک‌بار Renewal، تسویه کیف پول مرزی، payable منفی-balance، رفتار آستانه (زیر/بالا/خاموش).
- `dotnet build` + `dotnet test` + `cd Frontend && yarn build` سبز.
- سناریوی دستی dev: شارژ (مودال → فاکتور → رسید → موجودی بالا)؛ تمدید ناکافی (فاکتور با کسر → رسید → پلن فعال)؛ تمدید کافی (تایید کیف پول → پلن فعال)؛ رفرش صفحه فاکتور.

## خارج از دامنه P2 (فازهای بعد)

- پنل ادمین: صفحات و endpoint های تایید/رد، کسر در رد (`Verifying→Failed`)، تکمیل (`Verifying→Completed`).
- OCR رسید با Tesseract (لوکال؛ فقط ارقام مبلغ/کد رهگیری ارزش استخراج دارد).
- ادغام درگاه پرداخت آنلاین.
- آیتم‌های ممیزی خارج از پرداخت: H10/H11 (احراز هویت فرانتند)، H13، H14.

## ریسک‌های پذیرفته‌شده

- تا قبل از فاز ادمین، رسید جعلی اعتبار دائمی دارد (مدل اعتمادِ طرح صاحب محصول؛ قاعده آستانه پادزهر عملیاتی آن است).
- OldUser های با مانده منفی، به‌محض فعال‌شدن تنظیم آستانه، غیرفعال می‌شوند — تنظیم باید آگاهانه روشن شود.
- `docs/08-portal-api.md` و `docs/09-database.md` بعد از پیاده‌سازی این فاز باید به‌روز شوند.

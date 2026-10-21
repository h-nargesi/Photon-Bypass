# docs/21-social-media-telegram-channel.md — کانال تلگرام با معماری آداپتور قابل‌تعویض (D1–D6)

> **English summary**: Telegram is the first real `ISocialMediaService` channel, shipped behind a channel-neutral adapter architecture: method signatures are `AccountEntity`-based, each adapter resolves its own recipient field (Telegram → `AccountEntity.TelegramChatId`, a future WhatsApp → `MobileNumber`), and the active channel is selected at **runtime** via `SocialMediaOptions:Channel` — the compile-time `#if SOCIAL` gates are gone. Chat binding uses a one-time Base64Url code delivered through the `t.me/{bot}?start={code}` deep link and consumed by a long-poll `BackgroundService`. `ForgetPassword` now accepts usernames (code → connected chat, else email fallback, else neutral). WhatsApp later = one adapter + one keyed registration line.

## تصمیم‌های تأییدشده (D1–D6)

| # | تصمیم | پیاده‌سازی |
|---|---|---|
| D1 | این فاز: تلگرام واقعی + معماری کانال‌خنثی؛ واتساپ بعداً = آداپتور ساده روی `Account.Mobile` (بدون فلوی اتصال) | `SocialChannel.WhatsApp` فقط seam کلید DI است؛ آداپتور ثبت نشده → فعال‌سازیش در دیسپچر fail-fast خطا می‌دهد |
| D2 | اتصال chat_id: کد یک‌بارمصرف + دیپ‌لینک `t.me/{bot}?start={CODE}` | `TelegramLinkEntity` (DELETE-output = مصرف اتمیک)، `HashHandler.GenerateUrlSafeCode()` (Base64Url ~۴۳ کاراکتر؛ سقف ۶۴ کاراکتری start-param) |
| D3 | ترنسپورت: long-polling در `BackgroundService` روی named HttpClient پروکسی‌دار | `TelegramUpdatePoller` (`Backend/OutSource/TelegramUpdatePoller.cs:18`) — حلقه‌ی `getUpdates?timeout=25`، backoff نمایی، offset در حافظه |
| D4 | `ForgetPassword` ورودی username را هم بپذیرد → کد بازیابی به chat متصل | `AuthApplication.ForgetPasswordByUsername` (`Backend/Application/Authentication/AuthApplication.cs:198`) |
| D5 | اولویت ارسال: تلگرام‌اول، وگرنه ایمیل (runtime) | `ISocialMediaService.Supports` (`Backend/Domain/OutSource/ISocialMediaService.cs:30`) — مصرف در `ForgetPasswordByUsername` و `NotifSendServices` (`AccountMonitoringService.cs:169`) |
| D6 | ForgetPassword با username و بدون تلگرامِ متصل → fallback به ایمیل معتبر؛ وگرنه پاسخ خنثی | همان متد — `else if (account.EmailAddress != null) → SendResetCodeViaEmail`، وگرنه پاسخ خنثیِ بی‌تفاوت از unknown-account |

## اصل طراحی: حل گیرنده داخل آداپتور

امضاهای `ISocialMediaService` حساب‌محورند (`AccountEntity` + `Supports`)؛ سرویس‌های Application هرگز نوع نشانی را نمی‌دانند. آداپتورها با کلید `SocialChannel` به‌صورت keyed ثبت می‌شوند (الگوی Radius) و یک دیسپچرِ factory غیر-keyedِ `ISocialMediaService` بر اساس `SocialMediaOptions:Channel` انتخاب می‌کند (`Backend/OutSource/ServiceFactory.cs:55-63`):
`None → NoopSocialMediaService` (جایگزین استب قبلی، همان رفتار no-op) / `Telegram → TelegramMediaService`.

## نقشه‌ی پیاده‌سازی (as-built)

| لایه | فایل‌ها |
|---|---|
| Domain | `ISocialMediaService` (امضاهای خنثی + `Supports`)، `SocialChannel`، `Model/SocialMediaOptions`، `AccountEntity.TelegramChatId` (`AccountEntity.cs:51` — بدون فلگ اعتبار؛ خودِ اتصال = احراز)، `TelegramLinkEntity`، `ITelegramLinkRepository`، `IAccountRepository.SetTelegramChat` |
| Database | `Account.sql` (ستون `TelegramChatId BIGINT NULL` + ایندکس یکتای فیلترشده `UK_Account_TelegramChatId`)، `TelegramLink.sql` (الگوی `ResetPassword.sql`، FK با `ON DELETE CASCADE`) |
| Infrastructure | `TelegramLinkRepository` (مصرف اتمیک `Consume` = delete-output)، `AccountRepository.SetTelegramChat` (`AccountRepository.cs:243` — خطای یکتایی 2601/2627 عمداً خاموش نمی‌شود تا هندلر توضیحش بدهد) |
| OutSource | `TelegramOptions` (BotToken/BotUsername/ApiBase/OperationsChatId/Proxy + اعتبارسنجی Proxy:Address)، `ITelegramBotClient` + `TelegramBotClient` (429 → احترام به Retry-After با یک retry)، `TelegramMediaService` (هشدارهای کاربر → chat حساب، ثبت‌نام/ظرفیت → `OperationsChatId`، متن‌های هم‌محتوا با قالب‌های ایمیل)، `TelegramLinkHandler` (منطق `/start` — قابل‌تست جدا از poller)، `TelegramUpdatePoller` (public، ثبت در `AddPortalServices` — پایین را ببینید)، `NoopSocialMediaService` |
| Application | `AuthApplication`: شاخه‌ی username + best-effort برای `InvalidPasswordAlert`/`NewUserRegistrationAlert`؛ `AccountMonitoringService.NotifSendServices`: social-first + حفظ stamp-after-success؛ `ServerManagementService`: `AlarmServerCapacity` داخل try/catch؛ `AccountApplication`: `CreateTelegramLink`/`GetTelegramLinkStatus`/`DisconnectTelegram` (انقضای کد ۱۵ دقیقه، باطل‌کردن کدهای pending قبلی) |
| Portal | endpointهای `GET/POST/DELETE /api/account/telegram-link` با `target` (والد می‌تواند زیرمجموعه را متصل کند؛ دیپ‌لینک در کنترلر کامل می‌شود چون فقط همان `BotUsername` را می‌بیند) — `AccountController.cs:90-115`؛ گسترش `ProductionSecretsGuard` (فیکسچر توکن + قواعد شرطی کانال)؛ ثبت poller |
| Frontend | کارت «اتصال تلگرام» در `RegisterComponent` حالت ویرایش (دریافت کد → دیپ‌لینک + کد + کپی، وضعیت در load، قطع اتصال)، `RegisterService` (سه متد + primitive جدید `remove` در `HttpClientHandler`)، ورودی «ایمیل / نام کاربری» در forgot-password، i18n (`register.json` بخش `telegram`، `forgot-password.json`) |

## چرا poller در `AddPortalServices` ثبت شده نه در OutSource؟

هاست Admin سرویس‌های OutSource را کلون می‌کند؛ ثبت poller آنجا یعنی دو حلقه‌ی `getUpdates` همزمان = خطای 409 تلگرام. `AddPortalServices` متعلق به پورتال است (`Portal/ServiceFactory.cs:85`). توجه: Admin فعلاً `AddPortalServices` را هم صدا می‌زند (اسکلت است و deploy نمی‌شود)؛ اگر روزی هر دو هاست همزمان بالا آمدند، poller دومی با 409 و backoff لاگ می‌خورد (degraded، نه فاجعه). poller وقتی `Channel != Telegram` است بلافاصله idle می‌شود.

## فعال‌سازی در استقرار

1. `appsettings.json` مقادیر تست دارد (عرف repo). پروداکشن: `private/secrets.json` با per-key override (لودر `AddPleskPrivateSecrets`) — کلیدها: `TelegramOptions:BotToken` (الزامی — گارد فیکسچر کامیت‌شده را می‌بندد)، `BotUsername`، `OperationsChatId`، `Proxy:Address` (+ Username/Password اختیاری).
2. `SocialMediaOptions:Channel` را `"Telegram"` بگذارید؛ گارد پروداکشن در این حالت کامل بودن `BotToken/BotUsername/Proxy:Address/OperationsChatId` را الزام می‌کند (`ProductionSecretsGuard.cs:43-65`).
3. `OperationsChatId` = chat اپراتور (هشدار ثبت‌نام + ظرفیت) — از `/start` گفتگو با بات به دست می‌آید.
4. IIS/Plesk: poller همان پیش‌نیاز چرخه‌حیات Quartz `JobInterval` را دارد؛ حلقه‌ی ~۲۵ ثانیه‌ای خودش keep-alive است (docs/19 §۴).
5. Enumeration: پاسخ ForgetPassword روی هر سه مسیر خنثی است و limiter موجود (۳/ساعت) روی username هم اعمال می‌شود.

## تست‌ها

ردیف‌های کامل در `docs/18-test-coverage.md` §۴ (جدول docs/21). خلاصه: `TelegramLinkHandlerTest` (کد معتبر/منقضی/replay/حساب غیرفعال/چت تکراری با `SqlException` واقعی/`/start` خالی/ترافیک غیر-start)، `TelegramMediaServiceTest` (chat مقصد کاربر/اپراتور + no-op بدون chat)، `TelegramLinkRoundTripTest` (روی DB سناریو با ریپوهای واقعی + ایندکس یکتا)، شاخه‌های username در ForgetPassword (متصل/fallback/خنثی/limiter/شکست ارسال)، تقدم social-first در `NotifSendServices` (+ stamp-after-success و ایزوله‌سازی خطا)، `AccountAppTest` (صدور کد/وضعیت/قطع)، `ProductionSecretsGuardTest` (قواعد تلگرام)، و specهای فرانت (کارت اتصال + ورودی username).

## ریسک‌ها / نکته‌های باز

- در دسترس بودن پروکسی = در دسترس بودن تلگرام؛ شکست ارسال هشدار پایان سرویس → حساب unstamped می‌ماند و اجرای ساعتی بعدی retry می‌کند؛ شکست ارسال کد بازیابی → `UserException` دوستانه (کد ذخیره می‌ماند، کاربر دوباره تلاش می‌کند).
- offset آپدیت در حافظه است: بعد از recycle آخرین آپدیت دوباره تحویل می‌شود — بی‌ضرر (کدها تک‌مصرف و `SetTelegramChat` آیدمپوتنت است). offset قبل از هندل جلو می‌رود تا آپدیت مسموم حلقه را روی همان آفست قفل نکند.
- webhook بعداً اگر لازم شد: تغییر داخلی poller/کلاینت، بدون دست‌زدن به اینترفیس.
- واتساپ: فقط کلید `SocialChannel.WhatsApp` و آرگومان‌های `MobileNumber`-محور در `NoopSocialMediaService.Supports`/دیسپچر باقی مانده — آداپتور + یک خط ثبت.
- پنل ادمین برای دیدن وضعیت تلگرام حساب‌ها خارج از دامنه بود (پنل ادمین اصلاً ساخته نشده).

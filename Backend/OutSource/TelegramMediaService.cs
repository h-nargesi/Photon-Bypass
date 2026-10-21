using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;

namespace PhotonBypass.OutSource;

/// <summary>
/// Telegram adapter for <see cref="ISocialMediaService"/>: user-facing
/// warnings go to <see cref="AccountEntity.TelegramChatId"/> (a null chat is
/// a silent no-op), operator alerts to TelegramOptions:OperationsChatId. The
/// message bodies mirror the plain-text email templates so both channels
/// carry the same content.
/// </summary>
internal sealed class TelegramMediaService(
    ITelegramBotClient bot_client,
    IOptions<TelegramOptions> telegram_options,
    IOptions<BrandingOptions> branding_options) : ISocialMediaService
{
    public bool Supports(AccountEntity account)
    {
        return account.TelegramChatId != null;
    }

    public async Task SendResetPasswordLink(AccountEntity account, string hash_code)
    {
        if (account.TelegramChatId is not long chat_id) return;

        var branding = branding_options.Value;

        await bot_client.SendMessage(chat_id, $"""
            سلام،
            درخواست بازیابی رمز عبور برای حساب {account.Username} ثبت شده است.

            کد بازیابی: {hash_code}

            لطفاً این کد را در قسمت مربوطه وارد کنید. این کد برای مدت محدودی معتبر است.
            در صورتی که شما این درخواست را ارسال نکرده‌اید، این پیام را نادیده بگیرید.

            تیم پشتیبانی | {branding.Title}
            {branding.SiteUrl}
            """);
    }

    public async Task InvalidPasswordAlert(AccountEntity account)
    {
        if (account.TelegramChatId is not long chat_id) return;

        var branding = branding_options.Value;

        await bot_client.SendMessage(chat_id, $"""
            تلاش ناموفق برای ورود به حساب {account.Username} با کلمه عبور اشتباه ثبت شد.

            اگر این تلاش از سمت شما نبوده است، لطفاً هرچه سریع‌تر کلمه عبور خود را تغییر دهید.

            تیم پشتیبانی | {branding.Title}
            {branding.SiteUrl}
            """);
    }

    public async Task FinishServiceAlert(AccountEntity account, string plan_title, string remains_title)
    {
        if (account.TelegramChatId is not long chat_id) return;

        var branding = branding_options.Value;
        var fullname = string.IsNullOrWhiteSpace(account.Fullname) ? account.Username : account.Fullname;

        await bot_client.SendMessage(chat_id, $"""
            {fullname} عزیز،
            سرویس شما از نوع {plan_title} به‌زودی منقضی خواهد شد.

            زمان باقی‌مانده: {remains_title}

            برای جلوگیری از قطع دسترسی، می‌توانید سرویس خود را پیش از انقضا تمدید نمایید.

            تیم پشتیبانی | {branding.Title}
            {branding.SiteUrl}
            """);
    }

    public Task NewUserRegistrationAlert(AccountEntity account)
    {
        return SendToOperations($"""
            حساب کاربری جدید ساخته شد.

            نام کاربری: {account.Username}
            نام: {account.Fullname}
            ایمیل: {account.Email ?? "-"}
            موبایل: {account.Mobile ?? "-"}
            """);
    }

    public Task AlarmServerCapacity(IReadOnlyList<string> alarms)
    {
        return SendToOperations("هشدار ظرفیت سرورها:\n\n" + string.Join("\n", alarms.Select(alarm => $"- {alarm}")));
    }

    private Task SendToOperations(string text)
    {
        return bot_client.SendMessage(telegram_options.Value.OperationsChatId, text);
    }
}

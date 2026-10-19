using System.Text.RegularExpressions;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Domain.Account.Business;

public static partial class AccountBusiness
{
    public const int MaxDaysDeactivatePlanToDelete = 40;
    public const int DelayBetweenWarnings = 20;
    public const int MaxDaysDeactivatePlanToDisable = 7;
    public const string PicturePath = "map/to/picture/";

    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 48;

    /// <summary>
    /// Enforces the portal-password policy server-side. The frontend already
    /// applies the very same contract (register/change-password forms use
    /// <c>PasswordValidators.ValidationPattern</c> with an 8..48 length window);
    /// every path that sets a password — register, reset-pass, change-pass and
    /// the admin/manager set-password — must call this so bypassing the UI
    /// cannot produce a weak or empty password.
    /// </summary>
    public static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            password.Length is < PasswordMinLength or > PasswordMaxLength ||
            !PasswordPattern().Match(password).Success)
        {
            throw new UserException(
                $"کلمه عبور باید حداقل {PasswordMinLength} کاراکتر و شامل عدد، حروف کوچک و بزرگ انگلیسی یا فارسی باشد!",
                $"Invalid password: length={password?.Length ?? 0}, " +
                $"policy=[{PasswordMinLength}..{PasswordMaxLength}] chars, one digit + (mixed-case latin or persian letters)");
        }
    }

    [GeneratedRegex(@"(?=.*[\d\u06F0-\u06F9])((?=.*[a-z])(?=.*[A-Z])|(?=.*[\u0622-\u0648\u067E-\u06CC])).+")]
    private static partial Regex PasswordPattern();

    public static AccountEntity CreateFromModel(RegisterModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username))
        {
            throw new UserException("نام کاربری خالیست!");
        }

        if (!UsernamePattern().Match(model.Username).Success)
        {
            throw new UserException("این نام کاربری غیرمجاز است!", $"Invalid Username: {model.Username}");
        }

        var account = new AccountEntity
        {
            Username = model.Username,
        };

        account.SetFromModel(model);

        return account;
    }

    public static void SetFromModel(this AccountEntity account, EditUserModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.Mobile))
        {
            throw new UserException("حداقل یکی از دو فیلد موبایل یا ایمیل باید پر باشد!");
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            account.Email = null;
            account.IsEmailValid = false;
        }
        else if (!EmailPattern().Match(model.Email).Success)
        {
            throw new UserException("این ایمیل غیرمجاز است!", $"Invalid Email: {model.Email}");
        }
        else
        {
            if (account.Email != model.Email)
                account.IsEmailValid = false;

            account.Email = model.Email;
        }

        if (string.IsNullOrWhiteSpace(model.Mobile))
        {
            account.Mobile = null;
            account.IsMobileValid = false;
        }
        else if (!MobileNumberPattern().Match(model.Mobile).Success)
        {
            throw new UserException("این شماره موبایل غیرمجاز است!", $"Invalid Mobile: {model.Mobile}");
        }
        else
        {
            if (model.Mobile.StartsWith('0'))
                model.Mobile = "+98" + model.Mobile[1..];

            if (account.Mobile != model.Mobile)
                account.IsMobileValid = false;

            account.Mobile = model.Mobile;
        }

        account.Name = model.Firstname;
        account.Surname = model.Lastname;
    }

    public static double IsReachedMaxInactivityDaysToDisable(this AccountEntity account, DateTime? last_connect_time)
    {
        var last_activity = last_connect_time ?? account.Created;

        var expired_days = (DateTime.Now - last_activity).TotalDays - MaxDaysDeactivatePlanToDisable;

        return expired_days < 0 ? 0 : expired_days;
    }

    public static double IsReachedMaxInactivityDaysToDelete(this AccountEntity account, DateTime? last_connect_time)
    {
        var last_activity = last_connect_time ?? account.Created;

        var expired_days = (DateTime.Now - last_activity).TotalDays - MaxDaysDeactivatePlanToDelete;

        return expired_days < 0 ? 0 : expired_days;
    }

    public static bool OverWarningTime(this AccountEntity account)
    {
        return account.LastWarningTime.HasValue &&
               (DateTime.Now - account.LastWarningTime.Value).TotalHours < DelayBetweenWarnings;
    }

    public static void UpdateWarningTime(this AccountEntity account)
    {
        account.LastWarningTime = DateTime.Now;
    }

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9_-]{3,16}[a-zA-Z0-9]$")]
    public static partial Regex UsernamePattern();

    [GeneratedRegex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$")]
    public static partial Regex EmailPattern();

    [GeneratedRegex(@"^(\+\d{2}|0)\d{10}$")]
    public static partial Regex MobileNumberPattern();
}

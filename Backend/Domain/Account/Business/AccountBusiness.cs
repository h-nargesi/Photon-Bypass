using PhotonBypass.Domain.Session.Entity;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Domain.Session.Business;

public static class AccountBusiness
{
    private const int MaxDeactivatePlan = 20;
    private const int DelayBetweenWarnings = 20;
    
    public static void SetFromModel(this AccountEntity account, EditUserModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.Mobile))
        {
            throw new UserException("حداقل یکی از دو فیلد موبایل یا ایمیل باید پر باشد!");
        }

        account.Name = model.Firstname;
        account.Surname = model.Lastname;

        if (account.Email != model.Email)
            account.EmailValid = false;

        if (account.Mobile != model.Mobile)
            account.MobileValid = false;

        account.Email = model.Email;
        account.Mobile = model.Mobile;
    }

    public static AccountEntity CreateFromModel(RegisterModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username))
        {
            throw new UserException("نام کاربری خالیست!");
        }

        if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.Mobile))
        {
            throw new UserException("حداقل یکی از دو فیلد موبایل یا ایمیل باید پر باشد!");
        }

        return new AccountEntity
        {
            Username = model.Username,
            Email = model.Email,
            EmailValid = false,
            Mobile = model.Mobile,
            MobileValid = false,
            Name = model.Firstname,
            Surname = model.Lastname,
        };
    }

    public static int IsRichMaxDeactiveTime(this AccountEntity account, DateTime? last_connect_time)
    {
        var last_activity = last_connect_time ?? account.CreatedTime;

        var expired_days = MaxDeactivatePlan - (int)(last_activity - DateTime.Now).TotalDays;
        
        return expired_days < 0 ? 0 : expired_days;
    }

    public static bool OverWarningTime(this AccountEntity account)
    {
        return account.WarningTimes.HasValue &&
               (account.WarningTimes.Value - DateTime.Now).TotalHours <= DelayBetweenWarnings;
    }

    public static void UpdateWarningTime(this AccountEntity account)
    {
        account.WarningTimes = DateTime.Now;
    }
}

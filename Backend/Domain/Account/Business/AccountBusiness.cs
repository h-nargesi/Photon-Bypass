using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Domain.Account.Business;

public static class AccountBusiness
{
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

    public static AccountEntity CreateFromModel(EditUserModel model)
    {
        return new AccountEntity
        {
            CloudId = StaticRepo.Value.WebCloudId,
            Username = user.Username,
            Email = model.Email,
            EmailValid = false,
            Mobile = model.Mobile,
            MobileValid = false,
            Name = model.Firstname,
            Surname = model.Lastname,
        };
    }
}

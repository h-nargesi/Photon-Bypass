using PhotonBypass.Domain.Account.Model;
using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.FreeRadius.Business;

public static class PermanentUserBusiness
{
    public static void SetFromModel(this PermanentUserEntity user, EditUserModel model)
    {
        user.Name = model.Firstname;
        user.Surname = model.Lastname;
        user.Email = model.Email;
        user.Phone = model.Mobile;
    }
}

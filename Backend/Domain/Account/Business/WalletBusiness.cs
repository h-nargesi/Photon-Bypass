using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account.Business;

public static class WalletBusiness
{
    public static bool CheckMoneyNeed(this AccountEntity account, int balance, int estimate, out int money_need)
    {
        if (balance < 0 || !account.UserType.HasFlag(UserTypes.OldUser) && balance < estimate)
        {
            money_need = estimate - balance;
            return true;
        }

        money_need = 0;
        return false;
    }
}
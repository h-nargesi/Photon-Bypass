using FluentAssertions;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Test.Facts.BasicFunctions.Business;

public class WalletBusinessTest
{
    [Fact]
    public void CheckMoneyNeed_Test()
    {
        Assert.True(new AccountEntity().CheckMoneyNeed(-10, 10, out var money_need));
        Assert.Equal(20, money_need);

        Assert.True(new AccountEntity { UserType = UserTypes.OldUser }.CheckMoneyNeed(-10, 10, out money_need));
        Assert.Equal(20, money_need);

        Assert.True(new AccountEntity().CheckMoneyNeed(5, 10, out money_need));
        Assert.Equal(5, money_need);

        Assert.False(new AccountEntity { UserType = UserTypes.OldUser }.CheckMoneyNeed(5, 10, out money_need));
        Assert.Equal(0, money_need);

        Assert.False(new AccountEntity().CheckMoneyNeed(15, 10, out money_need));
        Assert.Equal(0, money_need);

        Assert.False(new AccountEntity { UserType = UserTypes.OldUser }.CheckMoneyNeed(15, 10, out money_need));
        Assert.Equal(0, money_need);
    }
}
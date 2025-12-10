using FluentAssertions;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Test.BasicFunctions.Business;

public class AccountBusinessTest
{
    private Action? action;

    [Fact]
    public void EmptyAccount_ShouldThrow_EmailOrMobile()
    {
        action = () => AccountBusiness.CreateFromModel(new RegisterModel());
        action.Should().Throw<UserException>();

        action = () => new AccountEntity().SetFromModel(new EditUserModel());
        action.Should().Throw<UserException>();
    }

    [Fact]
    public void Account_ShouldThrow_InvalidEmail()
    {
        action = () => new AccountEntity().SetFromModel(new EditUserModel { Email = "abc" });
        action.Should().Throw<UserException>();

        action = () => new AccountEntity().SetFromModel(new EditUserModel { Email = "abc@al" });
        action.Should().Throw<UserException>();

        action = () => new AccountEntity().SetFromModel(new EditUserModel { Email = "abc.al" });
        action.Should().Throw<UserException>();

        action = () => new AccountEntity().SetFromModel(new EditUserModel { Email = "ab^@abc.com" });
        action.Should().Throw<UserException>();
    }

    [Fact]
    public void Account_ShouldThrow_InvalidMobile()
    {
        action = () => new AccountEntity().SetFromModel(new EditUserModel { Mobile = "abc" });
        action.Should().Throw<UserException>();

        action = () => new AccountEntity().SetFromModel(new EditUserModel { Mobile = "1234567890" });
        action.Should().Throw<UserException>();

        action = () => new AccountEntity().SetFromModel(new EditUserModel { Mobile = "12345678901" });
        action.Should().Throw<UserException>();

        action = () => new AccountEntity().SetFromModel(new EditUserModel { Mobile = "0123456789012" });
        action.Should().Throw<UserException>();

        action = () => new AccountEntity().SetFromModel(new EditUserModel { Mobile = "+01234567890" });
        action.Should().Throw<UserException>();
    }

    [Fact]
    public void Account_ShouldThrow_InvalidUsername()
    {
        action = () => AccountBusiness.CreateFromModel(new RegisterModel { Username = "_hamed" });
        action.Should().Throw<UserException>();

        action = () => AccountBusiness.CreateFromModel(new RegisterModel { Username = "hamed_" });
        action.Should().Throw<UserException>();

        action = () => AccountBusiness.CreateFromModel(new RegisterModel { Username = "9hamed" });
        action.Should().Throw<UserException>();

        action = () => AccountBusiness.CreateFromModel(new RegisterModel { Username = "hamed0" });
        action.Should().Throw<UserException>();

        action = () => AccountBusiness.CreateFromModel(new RegisterModel { Username = "h12345678901234567890h" });
        action.Should().Throw<UserException>();
    }

    [Fact]
    public void ShouldNotBeEmpty_valid_EmailOrMobile()
    {
        action = () => new AccountEntity().SetFromModel(new EditUserModel { Email = "abc@email.com" });
        action.Should().NotThrow<UserException>();

        action = () => new AccountEntity().SetFromModel(new EditUserModel { Mobile = "+989125157305" });
        action.Should().NotThrow<UserException>();

        action = () => new AccountEntity().SetFromModel(new EditUserModel { Mobile = "09125157305" });
        action.Should().NotThrow<UserException>();
    }

    [Fact]
    public void CheckMoneyNeed_Test()
    {
        Assert.True(new AccountEntity { Balance = -10 }.CheckMoneyNeed(10, out var money_need));
        Assert.Equal(20, money_need);

        Assert.True(new AccountEntity { Balance = -10, UserType = UserTypes.OldUser }
            .CheckMoneyNeed(10, out money_need));
        Assert.Equal(20, money_need);

        Assert.True(new AccountEntity { Balance = 5 }.CheckMoneyNeed(10, out money_need));
        Assert.Equal(5, money_need);

        Assert.False(new AccountEntity { Balance = 5, UserType = UserTypes.OldUser }
            .CheckMoneyNeed(10, out money_need));
        Assert.Equal(0, money_need);

        Assert.False(new AccountEntity { Balance = 15 }.CheckMoneyNeed(10, out money_need));
        Assert.Equal(0, money_need);

        Assert.False(new AccountEntity { Balance = 15, UserType = UserTypes.OldUser }
            .CheckMoneyNeed(10, out money_need));
        Assert.Equal(0, money_need);
    }

    [Fact]
    public void IsReachedMaxInactivityDaysToDisable()
    {
        const int max = AccountBusiness.MaxDaysDeactivatePlanToDisable;

        Assert.Equal(0, new AccountEntity().IsReachedMaxInactivityDaysToDisable(DateTime.Now.AddDays(1 - max)));

        var value = new AccountEntity().IsReachedMaxInactivityDaysToDisable(DateTime.Now.AddDays(-max));
        Assert.True(value > 0);
        Assert.True(value < 1);

        Assert.Equal(10, (int)new AccountEntity().IsReachedMaxInactivityDaysToDisable(DateTime.Now.AddDays(-10 - max)));
    }

    [Fact]
    public void IsReachedMaxInactivityDaysToDelete()
    {
        const int max = AccountBusiness.MaxDaysDeactivatePlanToDelete;

        Assert.Equal(0, new AccountEntity().IsReachedMaxInactivityDaysToDelete(DateTime.Now.AddDays(1 - max)));

        var value = new AccountEntity().IsReachedMaxInactivityDaysToDelete(DateTime.Now.Date.AddDays(-max));
        Assert.True(value > 0);
        Assert.True(value < 1);

        Assert.Equal(10, (int)new AccountEntity().IsReachedMaxInactivityDaysToDelete(DateTime.Now.AddDays(-10 - max)));
    }

    [Fact]
    public void OverWarningTime()
    {
        const int max = AccountBusiness.DelayBetweenWarnings;
        Assert.True(new AccountEntity { WarningTimes = DateTime.Now.AddHours(1 - max) }.OverWarningTime());
        Assert.False(new AccountEntity { WarningTimes = DateTime.Now.AddHours(-1 - max) }.OverWarningTime());
        Assert.False(new AccountEntity { WarningTimes = null }.OverWarningTime());
    }
}
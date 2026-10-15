using FluentAssertions;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Test.Facts.BasicFunctions.Business;

public class WalletBusinessTest
{
    [Theory]
    // negative balance always needs money, even for an old user (docs/15)
    [InlineData(UserTypes.None, -5, 10, 15, true)]
    [InlineData(UserTypes.OldUser, -5, 10, 15, true)]
    // new user below the estimate must pay the difference; old user is trusted
    [InlineData(UserTypes.None, 5, 10, 5, true)]
    [InlineData(UserTypes.OldUser, 5, 10, 0, false)]
    // balance at or above the estimate never needs money
    [InlineData(UserTypes.None, 15, 10, 0, false)]
    [InlineData(UserTypes.OldUser, 15, 10, 0, false)]
    // zero/zero is a no-op
    [InlineData(UserTypes.None, 0, 0, 0, false)]
    public void CheckMoneyNeed_Theory(UserTypes user_type, int balance, int estimate, int expected_need, bool expected)
    {
        var account = new AccountEntity { UserType = user_type };

        var result = account.CheckMoneyNeed(balance, estimate, out var money_need);

        result.Should().Be(expected);
        money_need.Should().Be(expected_need);
    }
}

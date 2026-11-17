using FluentAssertions;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Test.Facts.BasicFunctions.Business;

public class AccountBusinessTest
{
    private Action? action;

    [Fact]
    public void EmptyAccount_ShouldThrow_EmailOrMobile()
    {
        action = () => AccountBusiness.CreateFromModel(new RegisterModel());
        action.Should().Throw<UserException>();

        action = () => new PanelUserEntity().SetFromModel(new EditUserModel());
        action.Should().Throw<UserException>();
    }

    [Fact]
    public void Account_ShouldThrow_InvalidEmail()
    {
        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Email = "abc" });
        action.Should().Throw<UserException>();

        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Email = "abc@al" });
        action.Should().Throw<UserException>();

        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Email = "abc.al" });
        action.Should().Throw<UserException>();

        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Email = "ab^@abc.com" });
        action.Should().Throw<UserException>();
    }

    [Fact]
    public void Account_ShouldThrow_InvalidMobile()
    {
        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Mobile = "abc" });
        action.Should().Throw<UserException>();

        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Mobile = "1234567890" });
        action.Should().Throw<UserException>();

        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Mobile = "12345678901" });
        action.Should().Throw<UserException>();

        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Mobile = "0123456789012" });
        action.Should().Throw<UserException>();

        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Mobile = "+01234567890" });
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
        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Email = "abc@email.com" });
        action.Should().NotThrow<UserException>();

        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Mobile = "+989125157305" });
        action.Should().NotThrow<UserException>();

        action = () => new PanelUserEntity().SetFromModel(new EditUserModel { Mobile = "09125157305" });
        action.Should().NotThrow<UserException>();
    }

    [Fact]
    public void IsReachedMaxInactivityDaysToDisable()
    {
        const int max = AccountBusiness.MaxDaysDeactivatePlanToDisable;

        Assert.Equal(0, new VpnAccountEntity().IsReachedMaxInactivityDaysToDisable(DateTime.Now.AddDays(1 - max)));

        var value = new VpnAccountEntity().IsReachedMaxInactivityDaysToDisable(DateTime.Now.AddDays(-max));
        Assert.True(value > 0);
        Assert.True(value < 1);

        Assert.Equal(10, (int)new VpnAccountEntity().IsReachedMaxInactivityDaysToDisable(DateTime.Now.AddDays(-10 - max)));
    }

    [Fact]
    public void IsReachedMaxInactivityDaysToDelete()
    {
        const int max = AccountBusiness.MaxDaysDeactivatePlanToDelete;

        Assert.Equal(0, new VpnAccountEntity().IsReachedMaxInactivityDaysToDelete(DateTime.Now.AddDays(1 - max)));

        var value = new VpnAccountEntity().IsReachedMaxInactivityDaysToDelete(DateTime.Now.Date.AddDays(-max));
        Assert.True(value > 0);
        Assert.True(value < 1);

        Assert.Equal(10, (int)new VpnAccountEntity().IsReachedMaxInactivityDaysToDelete(DateTime.Now.AddDays(-10 - max)));
    }

    [Fact]
    public void OverWarningTime()
    {
        const int max = AccountBusiness.DelayBetweenWarnings;
        Assert.True(new VpnAccountEntity { LastWarningTime = DateTime.Now.AddHours(1 - max) }.OverWarningTime());
        Assert.False(new VpnAccountEntity { LastWarningTime = DateTime.Now.AddHours(-1 - max) }.OverWarningTime());
        Assert.False(new VpnAccountEntity { LastWarningTime = null }.OverWarningTime());
    }

    [Fact]
    public void ValidateVpnUsername_AcceptsPanelUsernameWithCharsetSuffix()
    {
        // Docs/22 D3: bare panel username (first account) and readable suffixes
        AccountBusiness.ValidateVpnUsername("hamed", "hamed");
        AccountBusiness.ValidateVpnUsername("hamed", "hamed2");
        AccountBusiness.ValidateVpnUsername("hamed", "hamed_x-1");
    }

    [Theory]
    [InlineData("hamed", "hame")]              // must start with the owner's username
    [InlineData("hamed", "other2")]             // a different owner's prefix
    [InlineData("hamed", "hamed/x")]            // suffix charset is [a-zA-Z0-9_-]
    [InlineData("hamed", "hamed.2")]
    [InlineData("hamed", "hamed 2")]
    [InlineData("hamed", "")]
    public void ValidateVpnUsername_RejectsInvalidUsernames(string panel_username, string vpn_username)
    {
        action = () => AccountBusiness.ValidateVpnUsername(panel_username, vpn_username);
        action.Should().Throw<UserException>();
    }

    [Fact]
    public void ValidateVpnUsername_LengthBound()
    {
        var panel_username = new string('a', AccountBusiness.VpnUsernameMaxLength - 1);

        AccountBusiness.ValidateVpnUsername(panel_username, panel_username + "a");

        action = () => AccountBusiness.ValidateVpnUsername(panel_username, panel_username + "ab");
        action.Should().Throw<UserException>();
    }

    [Fact]
    public void SuggestVpnUsernameSequence_PlainThenIncrementalSuffix()
    {
        var sequence = AccountBusiness.SuggestVpnUsernameSequence("hamed").Take(5).ToArray();

        sequence.Should().Equal("hamed", "hamed2", "hamed3", "hamed4", "hamed5");
    }

    [Fact]
    public void SuggestVpnUsernameSequence_StopsAtTheLengthCap()
    {
        var panel_username = new string('a', AccountBusiness.VpnUsernameMaxLength - 1);

        var candidates = AccountBusiness.SuggestVpnUsernameSequence(panel_username).ToList();

        // single-digit suffixes still fit at exactly the cap (32); the
        // two-digit "+10" (33) is the first to exceed it
        candidates.Should().Equal(
            panel_username,
            panel_username + "2",
            panel_username + "3",
            panel_username + "4",
            panel_username + "5",
            panel_username + "6",
            panel_username + "7",
            panel_username + "8",
            panel_username + "9");

        var capped = new string('b', AccountBusiness.VpnUsernameMaxLength);

        AccountBusiness.SuggestVpnUsernameSequence(capped).Should().Equal(capped);
    }
}
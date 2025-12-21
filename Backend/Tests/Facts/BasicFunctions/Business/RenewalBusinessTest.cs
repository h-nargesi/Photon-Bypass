using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Test.Facts.BasicFunctions.Business;

public class RenewalBusinessTest
{
    [Fact]
    public void RenewalValidation_EmptyTraffic_NormalUser()
    {
        var account = new AccountEntity
        {
            UserType = UserTypes.None,
        };

        var renewal = new RenewalEntity
        {
            TrafficLimit = null,
            SimultaneousUser = 1,
        };

        var has_error = renewal.RenewalValidation(account, out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void RenewalValidation_UnroundedTraffic_NormalUser()
    {
        var account = new AccountEntity
        {
            UserType = UserTypes.None,
        };

        var renewal = new RenewalEntity
        {
            TrafficLimit = 23 * (long)StaticValues.BytesInGigDouble,
            SimultaneousUser = 1,
        };

        var has_error = renewal.RenewalValidation(account, out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void RenewalValidation_ValidTraffic_NormalUser()
    {
        var account = new AccountEntity
        {
            UserType = UserTypes.None,
        };

        var renewal = new RenewalEntity
        {
            TrafficLimit = 25 * (long)StaticValues.BytesInGigDouble,
            SimultaneousUser = 1,
        };

        var has_error = renewal.RenewalValidation(account, out var user_exception);

        Assert.False(has_error);
        Assert.Null(user_exception);
        Assert.Equal(120, validation.TimeLimitInDays);
    }

    [Fact]
    public void RenewalValidation_EmptyTraffic_AllowMonthlyUser()
    {
        var account = new AccountEntity
        {
            UserType = UserTypes.AllowMonthly,
        };

        var renewal = new RenewalEntity
        {
            TrafficLimit = null,
            TimeLimitInDays = null,
            SimultaneousUser = 1,
        };

        var has_error = renewal.RenewalValidation(account, out var user_exception);
    }

    [Fact]
    public void RenewalValidation_UnroundedTraffic_AllowMonthlyUser()
    {
        var account = new AccountEntity
        {
            UserType = UserTypes.AllowMonthly,
        };

        var renewal = new RenewalEntity
        {
            TrafficLimit = 23 * (long)StaticValues.BytesInGigDouble,
            SimultaneousUser = 1,
        };

        var has_error = renewal.RenewalValidation(account, out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void RenewalValidation_UnroundedTime_AllowMonthlyUser()
    {
        var account = new AccountEntity
        {
            UserType = UserTypes.AllowMonthly,
        };

        var renewal = new RenewalEntity
        {
            TimeLimitInDays = 25,
            SimultaneousUser = 1,
        };

        var has_error = renewal.RenewalValidation(account, out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void RenewalValidation_ValidTraffic_AllowMonthlyUser()
    {
        var account = new AccountEntity
        {
            UserType = UserTypes.AllowMonthly,
        };

        var renewal = new RenewalEntity
        {
            TrafficLimit = 25 * (long)StaticValues.BytesInGigDouble,
            SimultaneousUser = 2,
        };

        var has_error = renewal.RenewalValidation(account, out var user_exception);

        Assert.False(has_error);
        Assert.Null(user_exception);
        Assert.Equal(120, validation.TimeLimitInDays);
    }

    [Fact]
    public void RenewalValidation_ValidTimes_AllowMonthlyUser()
    {
        var account = new AccountEntity
        {
            UserType = UserTypes.AllowMonthly,
        };

        var renewal = new RenewalEntity
        {
            TimeLimitInDays = 90,
            SimultaneousUser = 1,
        };

        var has_error = renewal.RenewalValidation(account, out var user_exception);

        Assert.False(has_error);
        Assert.Null(user_exception);
    }
}
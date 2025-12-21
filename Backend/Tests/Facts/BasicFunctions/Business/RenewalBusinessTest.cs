using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
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
        Assert.Equal(90, (int?)renewal.TimeLimitInDays);
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

        var table = new (byte Users, int Traffic, int Days)[]
        {
            (1, 25, 3 * 30),
            (1, 50, 4 * 30),
            (1, 75, 5 * 30),
            (1, 100, 6 * 30),
            (1, 150, 8 * 30),
            (1, 200, 10 * 30),
            (1, 300, 14 * 30),

            (2, 25, 3 * 30 - 20),
            (2, 50, 4 * 30 - 20),
            (2, 75, 5 * 30 - 20),
            (2, 100, 6 * 30 - 20),
            (2, 150, 8 * 30 - 20),
            (2, 200, 10 * 30 - 20),
            (2, 300, 14 * 30 - 20),

            (3, 25, 3 * 30 - 30),
            (3, 50, 4 * 30 - 30),
            (3, 75, 5 * 30 - 30),
            (3, 100, 6 * 30 - 30),
            (3, 150, 8 * 30 - 30),
            (3, 200, 10 * 30 - 30),
            (3, 300, 14 * 30 - 30),

            (4, 25, 3 * 30 - 35),
            (4, 50, 4 * 30 - 35),
            (4, 75, 5 * 30 - 35),
            (4, 100, 6 * 30 - 35),
            (4, 150, 8 * 30 - 35),
            (4, 200, 10 * 30 - 35),
            (4, 300, 14 * 30 - 35),

            (5, 25, 3 * 30 - 40),
            (5, 50, 4 * 30 - 40),
            (5, 75, 5 * 30 - 40),
            (5, 100, 6 * 30 - 40),
            (5, 150, 8 * 30 - 40),
            (5, 200, 10 * 30 - 40),
            (5, 300, 14 * 30 - 40),

            (6, 25, 3 * 30 - 45),
            (6, 50, 4 * 30 - 45),
            (6, 75, 5 * 30 - 45),
            (6, 100, 6 * 30 - 45),
            (6, 150, 8 * 30 - 45),
            (6, 200, 10 * 30 - 45),
            (6, 300, 14 * 30 - 45),
        };

        foreach (var (Users, Traffic, Days) in table)
        {
            var renewal = new RenewalEntity
            {
                TrafficLimit = Traffic * (long)StaticValues.BytesInGigDouble,
                SimultaneousUser = Users,
            };

            var has_error = renewal.RenewalValidation(account, out var user_exception);

            Assert.False(has_error);
            Assert.Null(user_exception);
            Assert.Equal(Days, (int?)renewal.TimeLimitInDays);
        }
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
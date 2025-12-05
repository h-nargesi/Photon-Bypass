using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Test.BasicFunctions.Business;

public class RenewalBusinessTest
{
    [Fact]
    public void RenewalValidation_EmptyTraffic()
    {
        var has_error = new RenewalEntity { TrafficLimit = null }.RenewalValidation(out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }
    
    [Fact]
    public void RenewalValidation_UnroundedTraffic()
    {
        var has_error = new RenewalEntity { TrafficLimit = 23 * (long)StaticValues.BytesInGigDouble }
            .RenewalValidation(out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }
    
    [Fact]
    public void RenewalValidation_ValidTraffic()
    {
        var has_error = new RenewalEntity { TrafficLimit = 25 * (long)StaticValues.BytesInGigDouble }
            .RenewalValidation(out var user_exception);

        Assert.False(has_error);
        Assert.Null(user_exception);
    }
}
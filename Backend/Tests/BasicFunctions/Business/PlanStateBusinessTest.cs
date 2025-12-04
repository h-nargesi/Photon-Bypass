using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Test.BasicFunctions.Business;

public class PlanStateBusinessTest
{
    [Fact]
    public void IsFinishing()
    {
        Assert.True(new PlanStateEntity { TrafficLeft = 1, TrafficLimit = 1000 }.IsFinishing());
        Assert.True(new PlanStateEntity { ExpirationDate = DateTime.Now.AddDays(1), TimeLimitInDays = 1000 }.IsFinishing());
        Assert.False(new PlanStateEntity
        {
            ExpirationDate = DateTime.Now.AddDays(3),
            TimeLimitInDays = 1000,
            TrafficLeft = 3, 
            TrafficLimit = 1000,
        }.IsFinishing());
    }
}
using FluentAssertions;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Test.Facts.BasicFunctions.Business;

public class PlanStateBusinessTest
{
    [Theory]
    // Finishing = strictly less than 10% of time or traffic left (P5/M8)
    [InlineData(99, 1000, true)]      // traffic 9.9% left → finishing
    [InlineData(100, 1000, false)]    // traffic exactly 10% left → not finishing
    [InlineData(101, 1000, false)]    // traffic 10.1% left → not finishing
    public void IsFinishing_TrafficBoundary(double traffic_left, long traffic_limit, bool expected)
    {
        var entity = new PlanStateEntity
        {
            TrafficLeft = (long)traffic_left,
            TrafficLimit = traffic_limit,
        };

        entity.IsFinishing().Should().Be(expected);
    }

    [Theory]
    [InlineData(99, true)]    // time 9.9% left → finishing
    [InlineData(101, false)]  // time 10.1% left → not finishing
    public void IsFinishing_TimeBoundary(double days_left, bool expected)
    {
        var entity = new PlanStateEntity
        {
            ExpirationDate = DateTime.Now.AddDays(days_left),
            TimeLimitInDays = 1000,
        };

        entity.IsFinishing().Should().Be(expected);
    }

    [Fact]
    public void IsFinishing_WhenEitherSideIsFinishing()
    {
        // traffic under threshold alone
        new PlanStateEntity
        {
            ExpirationDate = DateTime.Now.AddDays(500),
            TimeLimitInDays = 1000,
            TrafficLeft = 1,
            TrafficLimit = 1000,
        }.IsFinishing().Should().BeTrue();

        // time under threshold alone
        new PlanStateEntity
        {
            ExpirationDate = DateTime.Now.AddDays(1),
            TimeLimitInDays = 1000,
            TrafficLeft = 900,
            TrafficLimit = 1000,
        }.IsFinishing().Should().BeTrue();

        // both comfortably above threshold
        new PlanStateEntity
        {
            ExpirationDate = DateTime.Now.AddDays(300),
            TimeLimitInDays = 1000,
            TrafficLeft = 300,
            TrafficLimit = 1000,
        }.IsFinishing().Should().BeFalse();
    }
}

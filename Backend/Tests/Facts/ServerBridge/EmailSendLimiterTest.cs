using FluentAssertions;
using PhotonBypass.ServerBridge.Email;

namespace PhotonBypass.Test.Facts.ServerBridge;

public class EmailSendLimiterTest
{
    [Fact]
    public void CapReached_RejectsFurtherReservations()
    {
        var limiter = new EmailSendLimiter(2);

        limiter.TryReserve().Should().BeTrue();
        limiter.TryReserve().Should().BeTrue();
        limiter.TryReserve().Should().BeFalse("the third send attempt inside one hour exceeds the cap");
    }

    [Fact]
    public void WindowSlides_ReservationsExpireAfterAnHour()
    {
        var time = new FakeTimeProvider();
        var limiter = new EmailSendLimiter(1, time);

        limiter.TryReserve().Should().BeTrue();
        limiter.TryReserve().Should().BeFalse();

        time.Advance(TimeSpan.FromHours(1));

        limiter.TryReserve().Should().BeTrue("the reservation left the sliding window exactly one hour later");
    }

    [Fact]
    public void WindowSlides_BoundaryReservationsStillCountInsideTheHour()
    {
        var time = new FakeTimeProvider();
        var limiter = new EmailSendLimiter(1, time);

        limiter.TryReserve().Should().BeTrue();

        time.Advance(TimeSpan.FromHours(1) - TimeSpan.FromSeconds(1));

        limiter.TryReserve().Should().BeFalse("59m59s after the reservation the window still holds it");
    }

    [Fact]
    public void ZeroCap_IsUnlimited()
    {
        var time = new FakeTimeProvider();
        var limiter = new EmailSendLimiter(0, time);

        for (var attempt = 0; attempt < 1_000; attempt++)
        {
            limiter.TryReserve().Should().BeTrue();
        }
    }

    [Fact]
    public void NegativeCap_IsUnlimited()
    {
        var limiter = new EmailSendLimiter(-1);

        for (var attempt = 0; attempt < 1_000; attempt++)
        {
            limiter.TryReserve().Should().BeTrue();
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        public void Advance(TimeSpan span)
        {
            now += span;
        }

        public override DateTimeOffset GetUtcNow() => now;
    }
}

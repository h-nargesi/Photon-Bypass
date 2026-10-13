using FluentAssertions;
using PhotonBypass.Portal.Context;

namespace PhotonBypass.Test.Facts.BasicFunctions.Portal;

public class HistoryContextTest
{
    [Fact]
    public void EmptyFilters_YieldNullDates()
    {
        var context = new HistoryContext();

        context.From.Should().BeNull();
        context.To.Should().BeNull();
        context.FromDateTime.Should().BeNull();
        context.ToDateTime.Should().BeNull();
    }

    [Fact]
    public void UnixMillisecondEpoch_ConvertsToLocalDateTime()
    {
        var local = new DateTime(2026, 10, 13, 15, 30, 0, DateTimeKind.Local);
        var epoch = new DateTimeOffset(local).ToUnixTimeMilliseconds();

        var context = new HistoryContext { From = epoch };

        context.FromDateTime.Should().Be(local);
    }

    [Fact]
    public void UnixMillisecondEpoch_To_ConvertsToLocalDateTime()
    {
        var local = new DateTime(2026, 10, 13, 23, 59, 59, 999, DateTimeKind.Local);
        var epoch = new DateTimeOffset(local).ToUnixTimeMilliseconds();

        var context = new HistoryContext { To = epoch };

        context.ToDateTime.Should().Be(local);
    }
}

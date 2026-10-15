using FluentAssertions;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Xui.Application;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

public class XuiTrafficAccountingTest
{
    private const int NasId = 50;

    private static RenewalEntity Renewal(
        long? seen_counter = null, DateTime? last_poll = null, long traffic_used = 0, DateTime? last_active = null)
    {
        return new RenewalEntity
        {
            Id = 7,
            AccountId = 3,
            TrafficLimit = 25 * StaticValues.BytesInGigLong,
            TimeLimitInDays = 30,
            LastSeenCounter = seen_counter,
            LastPollTime = last_poll,
            TrafficUsed = traffic_used,
            LastActiveTime = last_active,
        };
    }

    private static XuiClientTrafficModel Stats(long up, long down)
    {
        return new XuiClientTrafficModel { Email = "User3", Up = up, Down = down };
    }

    [Fact]
    public void FirstPoll_DeltaIsWholeCounter_NoAlarm()
    {
        var renewal = Renewal();

        var result = XuiTrafficAccounting.Compute(NasId, DateTime.Now, Stats(30, 70), renewal, null, null);

        result.Delta.Should().Be(100);
        result.EpochAlarm.Should().BeFalse();
        result.NewTrafficUsed.Should().Be(100);
        result.NewSeenCounter.Should().Be(100);
        result.NewLastActiveTime.Should().NotBeNull();
    }

    [Fact]
    public void NormalPoll_DeltaIsCounterDifference()
    {
        var renewal = Renewal(seen_counter: 1000, last_poll: DateTime.Now.AddMinutes(-5), traffic_used: 1000);

        var result = XuiTrafficAccounting.Compute(NasId, DateTime.Now, Stats(500, 600), renewal, null, null);

        result.Delta.Should().Be(100);
        result.EpochAlarm.Should().BeFalse();
        result.NewTrafficUsed.Should().Be(1000 + 100);
        result.NewSeenCounter.Should().Be(1100);
    }

    [Fact]
    public void UnexpectedEpoch_ChargesCurrentOnTopAndAlarms()
    {
        var renewal = Renewal(seen_counter: 5000, last_poll: DateTime.Now.AddMinutes(-5), traffic_used: 5000);

        var result = XuiTrafficAccounting.Compute(NasId, DateTime.Now, Stats(30, 70), renewal, null, null);

        result.EpochAlarm.Should().BeTrue();
        result.Delta.Should().Be(100);
        result.NewTrafficUsed.Should().Be(5100);
        result.NewSeenCounter.Should().Be(100);
    }

    [Fact]
    public void ZeroDelta_DoesNotAdvanceLastActiveTime_AndEnsuresZeroRow()
    {
        var last_active = DateTime.Now.AddDays(-3);
        var renewal = Renewal(seen_counter: 100, last_poll: DateTime.Now.AddMinutes(-5), 100, last_active);

        var result = XuiTrafficAccounting.Compute(NasId, DateTime.Now, Stats(50, 50), renewal, null, null);

        result.Delta.Should().Be(0);
        result.NewLastActiveTime.Should().BeNull();
        result.Rows.Should().ContainSingle();
        result.Rows[0].TotalData.Should().Be(0);
        result.Rows[0].StartSession.Date.Should().Be(DateTime.Now.Date);
    }

    [Fact]
    public void Gap_DistributesDeltaAcrossMissingDays_WeightsSumEqualsTotal()
    {
        var now = new DateTime(2026, 10, 15, 12, 0, 0);
        var renewal = Renewal(seen_counter: 1000,
            last_poll: new DateTime(2026, 10, 12, 10, 0, 0), traffic_used: 1000);

        var result = XuiTrafficAccounting.Compute(NasId, now, Stats(1500, 1500), renewal, null, null);

        result.Delta.Should().Be(2000);

        var days = result.Rows.Select(row => row.StartSession.Date).ToList();
        days.Should().HaveCount(3);
        days.Should().BeInAscendingOrder();
        days[0].Should().Be(new DateTime(2026, 10, 13));
        days[1].Should().Be(new DateTime(2026, 10, 14));
        days[2].Should().Be(new DateTime(2026, 10, 15));

        result.Rows.Sum(row => row.TotalData).Should().Be(2000);
    }

    [Fact]
    public void Gap_UsesHistoricalDailyWeights_WhenAvailable()
    {
        var now = new DateTime(2026, 10, 15, 12, 0, 0);
        var renewal = Renewal(seen_counter: 1000,
            last_poll: new DateTime(2026, 10, 13, 10, 0, 0), traffic_used: 1000);

        var history = new Dictionary<DateTime, long>
        {
            [new DateTime(2026, 10, 14)] = 900,
            [new DateTime(2026, 10, 15)] = 100,
        };

        var result = XuiTrafficAccounting.Compute(NasId, now, Stats(2500, 500), renewal, null, null, history);

        result.Delta.Should().Be(2000);
        result.Rows.Should().HaveCount(2);

        var first_day = result.Rows[0];
        var second_day = result.Rows[1];

        first_day.TotalData.Should().Be(1800);
        second_day.TotalData.Should().Be(200);
        (first_day.TotalData + second_day.TotalData).Should().Be(2000);
    }

    [Fact]
    public void EpochDuringGap_WritesOnlyTodayRow_NoFabricatedHistory()
    {
        var now = new DateTime(2026, 10, 15, 12, 0, 0);
        var renewal = Renewal(seen_counter: 5000,
            last_poll: new DateTime(2026, 10, 12, 10, 0, 0), traffic_used: 5000);

        var result = XuiTrafficAccounting.Compute(NasId, now, Stats(300, 300), renewal, null, null);

        result.EpochAlarm.Should().BeTrue();
        result.Rows.Should().ContainSingle();
        result.Rows[0].StartSession.Date.Should().Be(now.Date);
        result.Rows[0].TotalData.Should().Be(600);
    }

    [Fact]
    public void MidDayRenewal_SessionIdsStayUniquePerRenewal()
    {
        var now = DateTime.Now;

        var old_row_session = XuiTrafficRow.SessionId(3, 7, now);
        var new_row_session = XuiTrafficRow.SessionId(3, 8, now);

        old_row_session.Should().NotBe(new_row_session);

        var old_result = XuiTrafficAccounting.Compute(NasId, now, Stats(10, 10), Renewal(traffic_used: 0), null, null);
        var new_renewal = Renewal();
        new_renewal.Id = 8;
        var new_result = XuiTrafficAccounting.Compute(NasId, now, Stats(5, 5), new_renewal, null, null);

        old_result.Rows[0].SessionId.Should().Be(old_row_session);
        new_result.Rows[0].SessionId.Should().Be(new_row_session);
    }

    [Fact]
    public void TodayRowUpsert_AccumulatesDayValueInPlace()
    {
        var now = new DateTime(2026, 10, 15, 12, 0, 0);
        var renewal = Renewal(seen_counter: 1000,
            last_poll: new DateTime(2026, 10, 15, 10, 0, 0), traffic_used: 1000);

        var today_row = new PhotonBypass.Domain.Plan.Entity.TrafficDataEntity
        {
            Id = 900,
            AccountId = 3,
            NasId = NasId,
            SessionId = XuiTrafficRow.SessionId(3, 7, now),
            StartSession = new DateTime(2026, 10, 15, 10, 0, 0),
            DataIn = 600,
            DataOut = 400,
        };

        var result = XuiTrafficAccounting.Compute(NasId, now, Stats(1000, 2000), renewal, today_row, null);

        var row = result.Rows.Should().ContainSingle().Subject;
        row.Id.Should().Be(900);
        row.TotalData.Should().Be(1000 + 2000);
        today_row.TotalData.Should().Be(1000, "the accounting must not mutate its input");
    }

    [Fact]
    public void ServerChange_SkipsDeltaAndRebasesAnchor()
    {
        var renewal = Renewal(seen_counter: 1000, last_poll: DateTime.Now.AddMinutes(-5), traffic_used: 1000);

        var result = XuiTrafficAccounting.Compute(60, DateTime.Now, Stats(2000, 2000), renewal, null, NasId);

        result.ServerChange.Should().BeTrue();
        result.Delta.Should().Be(0);
        result.NewTrafficUsed.Should().Be(1000);
        result.NewSeenCounter.Should().Be(4000);
        result.NewLastActiveTime.Should().BeNull();
        result.Rows.Should().ContainSingle().Which.TotalData.Should().Be(0);
    }
}

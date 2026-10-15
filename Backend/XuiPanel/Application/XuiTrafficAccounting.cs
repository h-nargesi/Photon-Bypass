using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Xui.Application;

public sealed class XuiAccountTrafficResult
{
    public required long Delta { get; init; }

    public required long NewTrafficUsed { get; init; }

    public required long NewSeenCounter { get; init; }

    public required DateTime? NewLastActiveTime { get; init; }

    public required bool EpochAlarm { get; init; }

    public required bool ServerChange { get; init; }

    public required List<TrafficDataEntity> Rows { get; init; }
}

public static class XuiTrafficAccounting
{
    public static XuiAccountTrafficResult Compute(
        int nas_id,
        DateTime now,
        XuiClientTrafficModel stats,
        RenewalEntity renewal,
        TrafficDataEntity? today_row,
        int? last_xui_nas_id,
        IReadOnlyDictionary<DateTime, long>? daily_history = null)
    {
        var current = stats.Total;
        var first_poll = renewal.LastSeenCounter is null;
        var server_change = last_xui_nas_id > 0 && last_xui_nas_id != nas_id;

        long delta;
        var epoch_alarm = false;

        if (server_change)
        {
            delta = 0;
        }
        else if (first_poll)
        {
            delta = current;
        }
        else if (current >= renewal.LastSeenCounter!.Value)
        {
            delta = current - renewal.LastSeenCounter.Value;
        }
        else
        {
            epoch_alarm = true;
            delta = current;
        }

        var rows = BuildRows(nas_id, now, renewal, today_row, stats, delta,
            single_day: first_poll || epoch_alarm || server_change, daily_history);

        return new XuiAccountTrafficResult
        {
            Delta = delta,
            NewTrafficUsed = renewal.TrafficUsed + delta,
            NewSeenCounter = current,
            NewLastActiveTime = delta > 0 ? now : null,
            EpochAlarm = epoch_alarm,
            ServerChange = server_change,
            Rows = rows,
        };
    }

    private static List<TrafficDataEntity> BuildRows(
        int nas_id,
        DateTime now,
        RenewalEntity renewal,
        TrafficDataEntity? today_row,
        XuiClientTrafficModel stats,
        long delta,
        bool single_day,
        IReadOnlyDictionary<DateTime, long>? daily_history)
    {
        var rows = new List<TrafficDataEntity>();
        var today = now.Date;

        var days = single_day || renewal.LastPollTime is not { } last_poll
            ? [today]
            : DistributeDays(last_poll, today);

        var shares = Distribute(delta, days, daily_history);

        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var (data_in, data_out) = SplitByDirection(shares[i], stats);

            if (day == today && today_row != null)
            {
                rows.Add(new TrafficDataEntity
                {
                    Id = today_row.Id,
                    AccountId = today_row.AccountId,
                    NasId = today_row.NasId,
                    SessionId = today_row.SessionId,
                    StartSession = today_row.StartSession,
                    EndSession = now,
                    DataIn = today_row.DataIn + data_in,
                    DataOut = today_row.DataOut + data_out,
                });
            }
            else
            {
                rows.Add(new TrafficDataEntity
                {
                    AccountId = renewal.AccountId,
                    NasId = nas_id,
                    SessionId = XuiTrafficRow.SessionId(renewal.AccountId, renewal.Id, day),
                    StartSession = day == today ? now : day,
                    EndSession = day == today ? now : day.AddDays(1),
                    DataIn = data_in,
                    DataOut = data_out,
                });
            }
        }

        return rows;
    }

    private static List<DateTime> DistributeDays(DateTime last_poll, DateTime today)
    {
        var days = new List<DateTime>();

        for (var day = last_poll.Date.AddDays(1); day <= today; day = day.AddDays(1))
        {
            days.Add(day);
        }

        return days.Count > 0 ? days : [today];
    }

    private static long[] Distribute(long delta, List<DateTime> days, IReadOnlyDictionary<DateTime, long>? daily_history)
    {
        var shares = new long[days.Count];

        if (delta == 0)
        {
            return shares;
        }

        var weights = BuildWeights(days, daily_history);
        var distributed = 0L;

        for (var i = 0; i < days.Count; i++)
        {
            if (i == days.Count - 1)
            {
                shares[i] = delta - distributed;
            }
            else
            {
                shares[i] = (long)Math.Round(delta * weights[i], MidpointRounding.AwayFromZero);
                distributed += shares[i];
            }
        }

        return shares;
    }

    private static double[] BuildWeights(List<DateTime> days, IReadOnlyDictionary<DateTime, long>? daily_history)
    {
        var positive_history = daily_history?
            .Where(pair => pair.Value > 0)
            .ToArray();

        if (positive_history is not { Length: > 0 })
        {
            return UniformWeights(days.Count);
        }

        var average = positive_history.Average(pair => pair.Value);
        var weights = days
            .Select(day => daily_history!.TryGetValue(day, out var usage) && usage > 0 ? usage : average)
            .ToArray();
        var total = weights.Sum();

        if (total <= 0)
        {
            return UniformWeights(days.Count);
        }

        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] /= total;
        }

        return weights;
    }

    private static double[] UniformWeights(int count)
    {
        var weights = new double[count];

        for (var i = 0; i < count; i++)
        {
            weights[i] = 1D / count;
        }

        return weights;
    }

    private static (long DataIn, long DataOut) SplitByDirection(long share, XuiClientTrafficModel stats)
    {
        var up_ratio = stats.Total > 0 ? (double)stats.Up / stats.Total : 0.5D;

        var data_out = (long)Math.Round(share * up_ratio, MidpointRounding.AwayFromZero);

        return (share - data_out, data_out);
    }
}

USE FastBypass;
GO

CREATE OR ALTER VIEW TotalPlanState AS

-- Docs/22 §2: the plan-state universe is keyed by VpnAccount — the stacking
-- windows, the traffic bands and the Id in the result are all per account;
-- RealmId/UserId/Status join in from VpnAccount (never NULL).

SELECT va.Id
	, va.UserId
	, va.Status
	, va.Username
	, va.RealmId
    , pl.Created
    -- Plan Info
    , pl.RowNumber
    , pl.SimultaneousUser
    , pl.TrafficLimit
    , pl.TimeLimitInDays
    , pl.RateLimitInMeg
    -- Last Usage Info
    , pl.LastConnectTime
    , pl.TrafficUsed
    -- Remains
    , pl.ExpirationDate
    , pl.TrafficLeft
FROM VpnAccount va
LEFT JOIN (
    SELECT rn.VpnAccountId
        , rn.RowNumber
        , rn.Created
        -- Plan Info
        , rn.SimultaneousUser
        , rn.TrafficLimit
        , rn.TimeLimitInDays
        , rn.RateLimitInMeg
        -- Last Usage Info
        , td.LastConnectTime
        , td.TotalTrafficUsed AS TrafficUsed
        , ROW_NUMBER() OVER(PARTITION BY rn.VpnAccountId, rn.Id ORDER BY td.TotalTrafficUsed DESC) AS TopTrafficData
        -- Remains
        , rn.ExpirationDate
        , rn.TrafficLimitRangeEnd - td.TotalTrafficUsed AS TrafficLeft
    FROM (
        SELECT rn.Id
            , rn.VpnAccountId
            , rn.RowNumber
            , rn.SimultaneousUser
            , rn.TrafficLimit
            , rn.TimeLimitInDays
            , rn.RateLimitInMeg
            , rn.TrafficLimitRangeStart
            , rn.TrafficLimit + rn.TrafficLimitRangeStart AS TrafficLimitRangeEnd
            , rn.ExpirationDate
            , rn.Created
        FROM (
            SELECT rn.Id
                , rn.VpnAccountId
                -- 1 = latest renewal (same convention as RenewalRepository.GetLatestByAccountIds / TrafficDataRepository.GetLastNasId); the one-row-per-account view keeps RowNumber = 1.
                , ROW_NUMBER() OVER(PARTITION BY rn.VpnAccountId ORDER BY rn.Id DESC) AS RowNumber
                , rn.SimultaneousUser
                , rn.TrafficLimit
                , rn.TimeLimitInDays
                , rn.RateLimitInMeg
                , ISNULL(SUM(TrafficLimit) OVER(PARTITION BY rn.VpnAccountId ORDER BY rn.Id 
                                                ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS TrafficLimitRangeStart
                -- D16: stacked expiry, frozen once at purchase by PlanApplication —
                -- no longer derived here from Created + TimeLimitInDays.
                , rn.ExpirationDate
                , rn.Created
            FROM Renewal rn
        ) rn
    ) rn
    LEFT JOIN (
        SELECT td.VpnAccountId
            , td.DataIn + td.DataOut + td.CumulativeTrafficUsed AS TotalTrafficUsed
            , td.CumulativeTrafficUsed
            , td.LastConnectTime
        FROM (
            SELECT td.VpnAccountId
                , td.DataIn
                , td.DataOut
                , ISNULL(SUM(DataIn + DataOut) OVER(PARTITION BY td.VpnAccountId ORDER BY td.Id 
                                                    ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS CumulativeTrafficUsed
                , MAX(CASE WHEN td.DataIn + td.DataOut > 0 THEN td.StartSession END) OVER(PARTITION BY td.VpnAccountId) AS LastConnectTime
            FROM TrafficData td
        ) td
    ) td
    ON td.VpnAccountId = rn.VpnAccountId AND td.CumulativeTrafficUsed >= rn.TrafficLimitRangeStart AND td.CumulativeTrafficUsed < rn.TrafficLimitRangeEnd
) pl ON pl.VpnAccountId = va.Id
WHERE ISNULL(pl.TopTrafficData, 1) = 1

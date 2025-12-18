USE FastBypass;
GO

CREATE OR ALTER VIEW PlanState AS

SELECT ac.Id
	, ac.IsActive
	, ac.Username
    -- Plan Info (Latest Renewal)
    , rn.RowNumber
    , rn.SimultaneousUser
    , rn.RestrictedRealmId
    , rn.TrafficLimit
    , rn.TimeLimitInDays
    -- Last Usage Info
    , td.LastConnectTime
    , td.TrafficUsed
    -- Remains
    , rn.ExpirationDate
    , rn.TrafficLeft
FROM Account ac
LEFT JOIN (
    SELECT rn.AccountId
        , rn.RowNumber
        -- Plan Info (Latest Renewal)
        , rn.SimultaneousUser
        , rn.RestrictedRealmId
        , rn.TrafficLimit
        , rn.TimeLimitInDays
        -- Last Usage Info
        , td.LastConnectTime
        , td.TotalTrafficUsed AS TrafficUsed
        , ROW_NUMBER() OVER(PARTITION BY rn.AccountId, rn.Id, ORDER BY td.TotalTrafficUsed DESC) AS TopTrafficData
        -- Remains
        , rn.ExpirationDate
        , rn.TrafficLimitRangeEnd - td.TotalTrafficUsed AS TrafficLeft
    FROM (
        SELECT rn.Id
            , rn.AccountId
            , rn.RowNumber
            , rn.SimultaneousUser
            , rn.RestrictedRealmId
            , rn.TimeLimitInDays
            , rn.TrafficLimit
            , rn.TrafficLimitRangeStart
            , rn.TrafficLimit + rn.TrafficLimitRangeStart AS TrafficLimitRangeEnd
            , rn.ExpirationDate
        FROM (
            SELECT rn.Id
                , rn.AccountId
                , ROW_NUMBER() OVER(PARTITION BY rn.AccountId ORDER BY rn.Id) AS RowNumber
                , rn.SimultaneousUser
                , rn.RestrictedRealmId
                , rn.TimeLimitInDays
                , rn.TrafficLimit
                , ISNULL(SUM(TrafficLimit) OVER(PARTITION BY rn.AccountId ORDER BY rn.Id 
                                                ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS TrafficLimitRangeStart
                , DATEADD(DAY, rn.TimeLimitInDays, rn.Created) AS ExpirationDate
            FROM Renewal rn
        ) rn
    ) rn
    LEFT JOIN (
        SELECT td.AccountId
            , td.DataIn + td.DataOut + td.CumulativeTrafficUsed AS TotalTrafficUsed
            , td.CumulativeTrafficUsed
            , td.LastConnectTime
        FROM (
            SELECT td.AccountId
                , td.DataIn
                , td.DataOut
                , ISNULL(SUM(DataIn + DataOut) OVER(PARTITION BY td.AccountId ORDER BY td.Id 
                                                    ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS CumulativeTrafficUsed
                , MAX(td.StartSession) OVER(PARTITION BY td.AccountId) AS LastConnectTime
            FROM TrafficData td
        ) td
    ) td
    ON td.AccountId = rn.AccountId AND td.CumulativeTrafficUsed >= rn.TrafficLimitRangeStart AND td.CumulativeTrafficUsed < rn.TrafficLimitRangeEnd
) plan
WHERE TopTrafficData = 1


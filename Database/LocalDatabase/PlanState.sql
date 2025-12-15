USE FastBypass;
GO

CREATE OR ALTER VIEW PlanState AS

SELECT ac.Id, ac.IsActive, ac.Username
    -- Plan Info (Latest Renewal)
    , rn.SimultaneousUser
    , rn.RestrictedRealmId
    , rn.TrafficLimit
    , rn.TimeLimitInDays
    -- Last Usage Info
    , td.LastConnectTime
    , td.TrafficUsed
    -- Remains
FROM Account ac
OUTER APPLY (
    SELECT ROW_NUMBER() OVER(PARTITION BY AccountId ORDER BY rn.Id DESC) AS RowNumber
        , rn.RestrictedRealmId
        , rn.SimultaneousUser
        , rn.RestrictedRealmId
        , rn.TrafficLimit
        , rn.TimeLimitInDays
    FROM Renewal rn
    WHERE rn.AccountId = ac.Id
) rn
OUTER APPLY (
    SELECT SUM(DataIn + DataOut) AS TrafficUsed
        , MAX(StartSession) AS LastConnectTime
    FROM TrafficData td
    WHERE td.AccountId = ac.Id
) td
GO
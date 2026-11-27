USE FastBypass;
GO

CREATE OR ALTER VIEW PlanState AS

-- One row per VpnAccount: the latest renewal (TotalPlanState.RowNumber = 1); an account without a renewal is still listed, with NULL plan columns.
SELECT Id
	, UserId
	, Status
	, Username
	, RealmId
    , Created
    -- Plan Info (Latest State)
    , SimultaneousUser
    , TrafficLimit
    , TimeLimitInDays
    , RateLimitInMeg
    -- Last Usage Info
    , LastConnectTime
    , TrafficUsed
    -- Remains
    , ExpirationDate
    , TrafficLeft
FROM TotalPlanState
WHERE ISNULL(RowNumber, 1) = 1

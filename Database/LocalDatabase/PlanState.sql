USE FastBypass;
GO

CREATE OR ALTER VIEW PlanState AS

SELECT Id
	, UserId
	, Status
	, Username
	, RealmId
    , Created
    , RowNumber
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

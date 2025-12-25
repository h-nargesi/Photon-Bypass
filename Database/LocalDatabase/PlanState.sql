USE FastBypass;
GO

CREATE OR ALTER VIEW PlanState AS

SELECT Id
	, IsActive
	, Username
    , Created
    -- Plan Info (Latest State)
    , SimultaneousUser
    , RestrictedRealmId
    , TrafficLimit
    , TimeLimitInDays
    -- Last Usage Info
    , LastConnectTime
    , TrafficUsed
    -- Remains
    , ExpirationDate
    , TrafficLeft
FROM TotalPlanState
WHERE ISNULL(RowNumber, 1) = 1

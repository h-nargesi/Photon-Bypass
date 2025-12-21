USE FastBypass;
GO

CREATE OR ALTER VIEW PlanState AS

SELECT Id
	, IsActive
	, Username
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
WHERE RowNumber IS NULL OR RowNumber = 1

USE FastBypass;
GO

CREATE OR ALTER VIEW TotalPlanState AS

SELECT Id
	, IsActive
	, Username
    -- Plan Info (Latest Renewal)
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


USE FastBypass;
GO

-- P1/C1 cleanup (docs/14-defects-audit.md): the old traffic merge-key bug inserted one
-- TrafficData row per sync cycle for every open session. This script removes the duplicates,
-- keeping the newest row (max Id) per (NasId, SessionId).
-- Dedup-only by design: historical usage sums behind TotalPlanState are NOT recalculated
-- (retroactive aggregation is irreversible; product-owner call).
-- MANUAL RUN on production, before the unique index script in this folder -- creating that
-- index fails while duplicates exist. Idempotent: reports the number of removed rows.

DECLARE @removed TABLE (Id INT);

;WITH duplicates AS (
    SELECT Id
        , ROW_NUMBER() OVER (PARTITION BY NasId, SessionId ORDER BY Id DESC) AS RowNum
    FROM TrafficData
)
DELETE FROM duplicates
OUTPUT deleted.Id INTO @removed (Id)
WHERE RowNum > 1;

SELECT COUNT(*) AS RemovedDuplicateRows FROM @removed;
GO

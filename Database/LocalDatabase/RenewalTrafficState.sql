USE FastBypass;
GO

-- x-ui traffic state (docs/17): D6/D14 columns on Renewal + widened TrafficData.SessionId.
-- Idempotent: safe to re-run. There is no migration framework -- deploy manually.

-- D6/D14: counter anchor and decision-truth columns on Renewal.
IF COL_LENGTH('Renewal', 'LastSeenCounter') IS NULL
    ALTER TABLE Renewal ADD LastSeenCounter BIGINT NULL;
GO

IF COL_LENGTH('Renewal', 'LastPollTime') IS NULL
    ALTER TABLE Renewal ADD LastPollTime DATETIME NULL;
GO

IF COL_LENGTH('Renewal', 'TrafficUsed') IS NULL
    ALTER TABLE Renewal ADD TrafficUsed BIGINT NOT NULL CONSTRAINT DF_Renewal_TrafficUsed DEFAULT 0;
GO

IF COL_LENGTH('Renewal', 'LastActiveTime') IS NULL
    ALTER TABLE Renewal ADD LastActiveTime DATETIME NULL;
GO

-- The x-ui collector writes synthetic day rows keyed 'xui:{accountId}:{renewalId}:{yyyyMMdd}'
-- (D7), which does not fit the original VARCHAR(8). The unique key exists either as the inline
-- table constraint (TrafficData.sql) or as the idempotent index (Indexes.sql): drop whichever
-- is present, widen, then recreate as the index form.
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'TrafficData' AND COLUMN_NAME = 'SessionId'
             AND CHARACTER_MAXIMUM_LENGTH < 64)
BEGIN
    IF OBJECT_ID('UK_TrafficData_NasId_SessionId', 'UQ') IS NOT NULL
        ALTER TABLE TrafficData DROP CONSTRAINT UK_TrafficData_NasId_SessionId;
    ELSE IF EXISTS (SELECT * FROM sys.indexes
                    WHERE name = 'UK_TrafficData_NasId_SessionId'
                      AND object_id = OBJECT_ID('TrafficData'))
        DROP INDEX UK_TrafficData_NasId_SessionId ON TrafficData;

    ALTER TABLE TrafficData ALTER COLUMN SessionId VARCHAR(64) NOT NULL;

    DECLARE @uk_traffic_sql NVARCHAR(MAX) =
        N'CREATE UNIQUE NONCLUSTERED INDEX UK_TrafficData_NasId_SessionId ON TrafficData (NasId, SessionId)';

    IF SERVERPROPERTY('EngineEdition') IN (3, 5, 8)
        SET @uk_traffic_sql += N' WITH (ONLINE = ON)';

    EXEC sp_executesql @uk_traffic_sql;
END
GO

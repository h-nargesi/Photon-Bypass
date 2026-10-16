USE FastBypass;
GO

IF OBJECT_ID('Realm') IS NULL
CREATE TABLE Realm (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_Realm_Id PRIMARY KEY,
	IsActive			BIT				NOT NULL	CONSTRAINT DF_Realm_IsActive DEFAULT 1,
	Name				NVARCHAR(15)	NOT NULL,
	LastTrafficSync		DATETIME			NULL,
	CapacityOverrideBytes	BIGINT			NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_Realm_Created DEFAULT GETDATE(),
)
GO

-- Idempotent: add the manual capacity-override column to databases created before T3.
-- NULL = capacity computed from Sum(BandWidth) of active NAS servers (default);
-- > 0  = absolute replacement capacity; 0 = realm closed (excluded from assignment/alarms).
IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('Realm')
      AND name = 'CapacityOverrideBytes'
)
    ALTER TABLE Realm ADD CapacityOverrideBytes BIGINT NULL;
GO

USE FastBypass;
GO

IF OBJECT_ID('Realm') IS NULL
CREATE TABLE Realm (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_Realm_Id PRIMARY KEY,
	IsActive			BIT				NOT NULL	CONSTRAINT DF_Realm_IsActive DEFAULT 1,
	Name				NVARCHAR(15)	NOT NULL,
	LastTrafficSync		DATETIME			NULL,
	-- NULL = capacity computed from Sum(BandWidth) of active NAS servers (default);
	-- > 0  = absolute replacement capacity; 0 = realm closed (excluded from assignment/alarms).
	CapacityOverrideBytes	BIGINT			NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_Realm_Created DEFAULT GETDATE()
)
GO

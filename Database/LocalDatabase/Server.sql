USE FastBypass;
GO

IF OBJECT_ID('Server') IS NULL
CREATE TABLE Server (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_Server_Id PRIMARY KEY,
	IsActive			BIT				NOT NULL	CONSTRAINT DF_Server_IsActive DEFAULT 1,
	RealmId				INT					NULL	CONSTRAINT FK_Server_RealmId FOREIGN KEY REFERENCES Realm (Id),
	IpAddress			VARCHAR(15)		NOT NULL,
	Name				NVARCHAR(15)	NOT NULL,
	DomainName			VARCHAR(255)	NOT NULL,
	-- 30-day sellable traffic in BYTES (Docs/24 D1/D2); the admin form takes gigabytes.
	-- Realm capacity = SUM(CapacityBytes) over the realm's active NAS servers.
	CapacityBytes		BIGINT			NOT NULL,
	-- T4 (Docs/16 §3): hard client-count cap for x-ui rows; NULL/0 = gate off.
	MaxClients			INT					NULL,
	OsType				TINYINT			NOT NULL,
	Features			INT				NOT NULL,
	-- Data Protection payload (base64) is larger than the plaintext JSON (Docs/14 M12).
	Config				VARCHAR(4000)		NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_Server_Created DEFAULT GETDATE()
)
GO

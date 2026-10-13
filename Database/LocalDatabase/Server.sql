USE FastBypass;
GO

IF OBJECT_ID('Server') IS NULL
CREATE TABLE Server (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_Server_Id PRIMARY KEY,
	IsActive			BIT				NOT NULL	CONSTRAINT DF_Server_IsActive DEFAULT 1,
	RealmId				INT					NULL	CONSTRAINT FK_Server_RealmId FOREIGN KEY REFERENCES Realm (Id),
	IpAddress			VARCHAR(15)		NOT NULL,
	Name				NVARCHAR(15)	NOT NULL,
	DomainName			VARCHAR(15)		NOT NULL,
	BandWidth			BIGINT			NOT NULL,
	OsType				TINYINT			NOT NULL,
	Features			INT				NOT NULL,
	-- P5/M12: Data Protection payload (base64) is larger than the plaintext JSON.
	Config				VARCHAR(4000)	NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_Server_Created DEFAULT GETDATE(),
)
GO

-- Idempotent: widen the column on databases created before P5/M12.
IF EXISTS (
    SELECT * FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('Server')
      AND c.name = 'Config'
      AND t.name = 'varchar'
      AND c.max_length < 4000
)
    ALTER TABLE Server ALTER COLUMN Config VARCHAR(4000) NULL;
GO

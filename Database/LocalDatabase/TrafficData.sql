USE FastBypass;
GO

IF OBJECT_ID('TrafficData') IS NULL
CREATE TABLE TrafficData (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_TrafficData_Id PRIMARY KEY,
	-- docs/22 §2: traffic rows belong to the VPN account.
	VpnAccountId		INT				NOT NULL	CONSTRAINT FK_TrafficData_VpnAccountId FOREIGN KEY REFERENCES VpnAccount (Id)
													ON DELETE CASCADE,
	NasId				INT					NULL	CONSTRAINT FK_TrafficData_NasId_Server FOREIGN KEY REFERENCES [Server] (Id),
	-- The x-ui collector writes synthetic day rows keyed 'xui:{vpnAccountId}:{renewalId}:{yyyyMMdd}'
	-- (docs/17 D7), so 8 characters are not enough.
	SessionId			VARCHAR(64)		NOT NULL,
	StartSession		DATETIME		NOT NULL,
	EndSession			DATETIME			NULL,
	DataIn				BIGINT			NOT NULL,
	DataOut				BIGINT			NOT NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_TrafficData_Created DEFAULT GETDATE(),

	CONSTRAINT UK_TrafficData_NasId_SessionId UNIQUE (NasId, SessionId)
)
GO

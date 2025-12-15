USE FastBypass;
GO

IF OBJECT_ID('Renewal') IS NULL
CREATE TABLE Renewal (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_Renewal_Id PRIMARY KEY,
	AccountId			INT				NOT NULL	CONSTRAINT FK_Renewal_AccountId FOREIGN KEY REFERENCES Account (Id)
													ON DELETE CASCADE,
	RestrictedRealmId	INT					NULL	CONSTRAINT FK_Renewal_RestrictedRealmId FOREIGN KEY REFERENCES Realm (Id),
	SimultaneousUser	TINYINT			NOT NULL,
	TrafficLimit		BIGINT				NULL,
	TimeLimitInDays		SMALLINT			NULL,
	RateLimitInMeg		TINYINT				NULL,
	Comment				NVARCHAR(255)		NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_Renewal_Created DEFAULT GETDATE(),
)
GO

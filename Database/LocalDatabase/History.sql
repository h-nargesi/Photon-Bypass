USE FastBypass;
GO

IF OBJECT_ID('History') IS NULL
CREATE TABLE History (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_History_Id PRIMARY KEY,
	-- Docs/22 D11: events anchor to the panel user (Target/Issuer) with an
	-- optional VpnAccountId for account-scoped events.
	Issuer				INT					NULL	CONSTRAINT FK_History_Issuer_Account FOREIGN KEY REFERENCES PanelUser (Id),
	Target				INT				NOT NULL	CONSTRAINT FK_History_Target_Account FOREIGN KEY REFERENCES PanelUser (Id),
	VpnAccountId		INT					NULL	CONSTRAINT FK_History_VpnAccountId FOREIGN KEY REFERENCES VpnAccount (Id),
	Category			TINYINT			NOT NULL,
	Type				TINYINT			NOT NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_History_Created DEFAULT GETDATE(),
	Title				NVARCHAR(15)	NOT NULL,
	Description			NVARCHAR(127)		NULL,
	Value				NVARCHAR(31)			NULL,
	Price				INT					NULL
)
GO

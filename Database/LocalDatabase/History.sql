USE FastBypass;
GO

IF OBJECT_ID('History') IS NULL
CREATE TABLE History (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_History_Id PRIMARY KEY,
	Issuer				INT					NULL	CONSTRAINT FK_History_Issuer_Account_Id FOREIGN KEY REFERENCES Account (Id),
	Target				INT				NOT NULL	CONSTRAINT FK_History_Target_Account_Id FOREIGN KEY REFERENCES Account (Id),
	Category			TINYINT			NOT NULL,
	Type				TINYINT			NOT NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_Histiry_Created DEFAULT GETDATE(),
	Title				NVARCHAR(15)	NOT NULL,
	Description			NVARCHAR(127)		NULL,
	Value				NVARCHAR(31)		NULL,
	Price				INT					NULL,
)
GO

USE FastBypass;
GO

IF OBJECT_ID('Account') IS NULL
CREATE TABLE Account (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_Account_Id PRIMARY KEY,
	IsActive			BIT				NOT NULL	CONSTRAINT DF_Account_IsActive DEFAULT 1,
	Username			VARCHAR(255)	NOT NULL	CONSTRAINT UK_Account_Username UNIQUE,
	Password			VARCHAR(88)		NOT NULL,
	Owner				INT					NULL	CONSTRAINT FK_Account_Owner_Account FOREIGN KEY REFERENCES Account (Id),
	Created				DATETIME		NOT NULL	CONSTRAINT DF_Account_Created DEFAULT GETDATE(),
	Name				NVARCHAR(15)		NULL,
	Surname				NVARCHAR(15)		NULL,
	Mobile				VARCHAR(13)			NULL	CONSTRAINT UK_Account_Mobile UNIQUE,
	Email				VARCHAR(320)		NULL	CONSTRAINT UK_Account_Email UNIQUE,
	IsMobileValid		BIT				NOT NULL	CONSTRAINT DF_Account_IsMobileValid DEFAULT 0,
	IsEmailValid		BIT				NOT NULL	CONSTRAINT DF_Account_IsEmailValid DEFAULT 0,
	Balance				INT				NOT NULL	CONSTRAINT DF_Account_Balance DEFAULT 0,
	CalculationMethod	INT					NULL	CONSTRAINT FK_Account_CalculationMethod_Price FOREIGN KEY REFERENCES Price (Id),
	UserType			INT				NOT NULL	CONSTRAINT DF_Account_UserType DEFAULT 0,
	VpnPassword			VARCHAR(32)			NULL,
	LastWarningTime		DATETIME			NULL,
	SendWarning			BIT				NOT NULL	CONSTRAINT DF_Account_SendWarning DEFAULT 1
)
GO

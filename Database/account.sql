IF OBJECT_ID('Account') IS NULL
CREATE TABLE Account (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_Account_Id PRIMARY KEY,
	Active				BIT				NOT NULL	CONSTRAINT DF_Account_Active DEFAULT 1,
	Username			VARCHAR(255)	NOT NULL	CONSTRAINT UK_Account_Username UNIQUE,
	Password			VARCHAR(88)		NOT NULL,
	Owner				INT					NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_Account_Created DEFAULT GETDATE(),
	Name				NVARCHAR(16)		NULL,
	Surname				NVARCHAR(16)		NULL,
	Mobile				VARCHAR(13)			NULL	CONSTRAINT UK_Account_Mobile UNIQUE,
	Email				VARCHAR(320)		NULL	CONSTRAINT UK_Account_Email UNIQUE,
	IsMobileValid		BIT				NOT NULL	CONSTRAINT DF_Account_IsMobileValid DEFAULT 0,
	IsEmailValid		BIT				NOT NULL	CONSTRAINT DF_Account_IsEmailValid DEFAULT 0,
	Balance				INT				NOT NULL	CONSTRAINT DF_Account_Balance DEFAULT 0,
	CalculationMethod	INT					NULL,
	UserType			INT				NOT NULL	CONSTRAINT DF_Account_UserType DEFAULT 0,
	VpnPassword			VARCHAR(32)			NULL,
	LastWarningTime		DATETIME			NULL,
	SendWarning			BIT				NOT NULL	CONSTRAINT DF_Account_SendWarning DEFAULT 1,

	CONSTRAINT FK_Account_Owner_Account_Id			FOREIGN KEY (Owner)	REFERENCES Account (Id),
)

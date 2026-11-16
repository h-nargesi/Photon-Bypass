USE FastBypass;
GO

IF OBJECT_ID('ResetPassword') IS NULL
CREATE TABLE ResetPassword (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_ResetPassword_Id PRIMARY KEY,
	-- docs/22 D11: reset codes are issued for the panel user.
	AccountId			INT				NOT NULL	CONSTRAINT FK_ResetPassword_AccountId FOREIGN KEY REFERENCES PanelUser (Id)
													ON DELETE CASCADE,
	ExpireDate			DATETIME		NOT NULL,
	HashCode			VARCHAR(88)		NOT NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_ResetPassword_Created DEFAULT GETDATE()
)
GO

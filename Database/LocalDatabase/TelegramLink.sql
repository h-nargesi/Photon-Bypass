USE FastBypass;
GO

IF OBJECT_ID('TelegramLink') IS NULL
CREATE TABLE TelegramLink (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_TelegramLink_Id PRIMARY KEY,
	AccountId			INT				NOT NULL	CONSTRAINT FK_TelegramLink_AccountId FOREIGN KEY REFERENCES Account (Id)
													ON DELETE CASCADE,
	ExpireDate			DATETIME		NOT NULL,
	-- one-time deep-link start parameter: Base64Url of ~32 random bytes
	-- (Telegram caps start params at 64 URL-safe characters)
	Code				VARCHAR(64)		NOT NULL	CONSTRAINT UK_TelegramLink_Code UNIQUE,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_TelegramLink_Created DEFAULT GETDATE()
)
GO

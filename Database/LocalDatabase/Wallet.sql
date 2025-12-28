USE FastBypass;
GO

IF OBJECT_ID('Wallet') IS NULL
CREATE TABLE Wallet (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_Wallet_Id PRIMARY KEY,
	AccountId			INT				NOT NULL	CONSTRAINT FK_Wallet_AccountId FOREIGN KEY REFERENCES Account (Id),
    Amount              INT             NOT NULL,
    Direction           SMALLINT        NOT NULL,
    Status              TINYINT         NOT NULL    CONSTRAINT DF_Wallet_Status DEFAULT 0,
    Description         NVARCHAR(127)   NOT NULL,
    InvoiceCode         INT                 NULL,
    ReferenceCode       VARCHAR(31)         NULL,
    Action              VARCHAR(16)         NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_Wallet_Created DEFAULT GETDATE(),
)
GO

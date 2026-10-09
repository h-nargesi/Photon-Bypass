USE FastBypass;
GO

IF OBJECT_ID('Invoice') IS NULL
CREATE TABLE Invoice (
	Code				INT				NOT NULL	CONSTRAINT PK_Invoice_Code PRIMARY KEY,
	AccountId			INT				NOT NULL	CONSTRAINT FK_Invoice_AccountId FOREIGN KEY REFERENCES Account (Id),
	Kind				TINYINT			NOT NULL	CONSTRAINT DF_Invoice_Kind DEFAULT 0,
	Title				NVARCHAR(127)	NOT NULL,
	TotalPrice			INT				NOT NULL,
	WalletDeduction		INT				NOT NULL	CONSTRAINT DF_Invoice_WalletDeduction DEFAULT 0,
	Payable				INT				NOT NULL	CONSTRAINT DF_Invoice_Payable DEFAULT 0,
	Action				NVARCHAR(400)		NULL,
	Status				TINYINT			NOT NULL	CONSTRAINT DF_Invoice_Status DEFAULT 0,
	ReceiptImage		VARBINARY(MAX)		NULL,
	ReceiptText			NVARCHAR(1000)		NULL,
	ReceiptAt			DATETIME2			NULL,
	Created				DATETIME2		NOT NULL	CONSTRAINT DF_Invoice_Created DEFAULT GETDATE(),
	CONSTRAINT CK_Invoice_ReceiptXor CHECK (NOT (ReceiptImage IS NOT NULL AND ReceiptText IS NOT NULL))
)
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Invoice_AccountId_Status')
CREATE INDEX IX_Invoice_AccountId_Status ON Invoice (AccountId, Status)
GO

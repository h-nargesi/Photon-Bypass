USE FastBypass;
GO

IF OBJECT_ID('Invoice') IS NULL
CREATE TABLE Invoice (
	Code				INT				NOT NULL	CONSTRAINT PK_Invoice_Code PRIMARY KEY,
	-- Docs/22 D8: the invoice belongs to the paying panel user.
	UserId				INT				NOT NULL	CONSTRAINT FK_Invoice_UserId FOREIGN KEY REFERENCES PanelUser (Id),
	-- Docs/22 §2: the VPN account a plan invoice renews; NULL on wallet-topup
	-- invoices and on new-account invoices that only resolve at settlement.
	VpnAccountId		INT					NULL	CONSTRAINT FK_Invoice_VpnAccountId FOREIGN KEY REFERENCES VpnAccount (Id),
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

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Invoice_UserId_Status')
CREATE INDEX IX_Invoice_UserId_Status ON Invoice (UserId, Status)
GO

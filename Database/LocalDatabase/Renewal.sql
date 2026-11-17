USE FastBypass;
GO

IF OBJECT_ID('Renewal') IS NULL
CREATE TABLE Renewal (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_Renewal_Id PRIMARY KEY,
	-- Docs/22 D5: renewals stack on one VPN account; the realm truth is
	-- VpnAccount.RealmId (the old RestrictedRealmId snapshot column is gone).
	VpnAccountId		INT				NOT NULL	CONSTRAINT FK_Renewal_VpnAccountId FOREIGN KEY REFERENCES VpnAccount (Id)
													ON DELETE CASCADE,
	SimultaneousUser	TINYINT			NOT NULL,
	TrafficLimit		BIGINT				NULL,
	TimeLimitInDays		SMALLINT			NULL,
	RateLimitInMeg		TINYINT			NULL,
	WalletDebit			INT				NOT NULL	CONSTRAINT FK_Renewal_WalletDebit FOREIGN KEY REFERENCES Wallet (Id),
	WalletCredit		INT					NULL	CONSTRAINT FK_Renewal_WalletCredit FOREIGN KEY REFERENCES Wallet (Id),
	Comment				NVARCHAR(255)		NULL,
	-- x-ui traffic state (Docs/17 D6/D14): counter anchor and decision-truth columns.
	LastSeenCounter		BIGINT				NULL,
	LastPollTime		DATETIME			NULL,
	TrafficUsed			BIGINT			NOT NULL	CONSTRAINT DF_Renewal_TrafficUsed DEFAULT 0,
	LastActiveTime		DATETIME			NULL,
	-- Stacked plan state (Docs/17 D16): frozen once at purchase time; sole writer
	-- is PlanApplication (the collector never writes them). NULL = no limit.
	ExpirationDate		DATETIME			NULL,
	CumulativeTrafficLeft BIGINT			NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_Renewal_Created DEFAULT GETDATE()
)
GO

USE FastBypass;
GO

IF OBJECT_ID('Renewal') IS NULL
CREATE TABLE Renewal (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_Renewal_Id PRIMARY KEY,
	-- Docs/22 D5: renewals stack on one VPN account; the realm truth is
	-- VpnAccount.RealmId (the old RestrictedRealmId snapshot column is gone).
	VpnAccountId		INT				NOT NULL	CONSTRAINT FK_Renewal_VpnAccountId FOREIGN KEY REFERENCES VpnAccount (Id)
													ON DELETE CASCADE,
	-- Docs/24 §2: NULL = unlimited concurrent connections (x-ui limitIp 0 /
	-- UM shared-users 0).
	SimultaneousUser	TINYINT			NULL,
	-- Docs/24 §2: the purchased product; NOT NULL (fresh-install set — every
	-- renewal is born from a Plan).
	-- (structure-sort dependency: Plan)
	PlanId				INT				NOT NULL	CONSTRAINT FK_Renewal_PlanId_Plans FOREIGN KEY REFERENCES Plans (Id),
	-- Docs/24 §2: who executed the purchase (audit; differs from the wallet
	-- owner exactly when an admin bought on behalf, D7).
	ActorId				INT				NULL	CONSTRAINT FK_Renewal_ActorId_PanelUser FOREIGN KEY REFERENCES PanelUser (Id),
	TrafficLimit		BIGINT				NULL,
	TimeLimitInDays		SMALLINT			NULL,
	RateLimitInMeg		TINYINT				NULL,
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

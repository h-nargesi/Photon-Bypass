USE FastBypass;
GO

IF OBJECT_ID('PanelUser') IS NULL BEGIN
	CREATE TABLE PanelUser (
		Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_PanelUser_Id PRIMARY KEY,
		-- Panel-only status (Docs/22 D12): 0=Active, 2=Deactivated (login blocked).
		-- VPN suspension is per-account on the VPN table, never here.
		Status				TINYINT			NOT NULL	CONSTRAINT DF_PanelUser_Status DEFAULT 0,
		Username			VARCHAR(255)	NOT NULL	CONSTRAINT UK_PanelUser_Username UNIQUE,
		Password			VARCHAR(200)	NOT NULL,
		Owner				INT					NULL	CONSTRAINT FK_PanelUser_Owner_PanelUser FOREIGN KEY REFERENCES PanelUser (Id),
		Created				DATETIME		NOT NULL	CONSTRAINT DF_PanelUser_Created DEFAULT GETDATE(),
		Name				NVARCHAR(15)		NULL,
		Surname				NVARCHAR(15)		NULL,
		Mobile				VARCHAR(13)			NULL,
		Email				VARCHAR(320)		NULL,
		IsMobileValid		BIT				NOT NULL	CONSTRAINT DF_PanelUser_IsMobileValid DEFAULT 0,
		IsEmailValid		BIT				NOT NULL	CONSTRAINT DF_PanelUser_IsEmailValid DEFAULT 0,
		-- Docs/22 D10: price calculation stays on the panel user (realm-independent).
		CalculationMethod	INT					NULL	CONSTRAINT FK_PanelUser_CalculationMethod_Price FOREIGN KEY REFERENCES Price (Id),
		UserType			INT				NOT NULL	CONSTRAINT DF_PanelUser_UserType DEFAULT 0,
		-- Docs/20 D2/D9: 0=User, 1=Manager, 2=Admin
		Role				TINYINT			NOT NULL	CONSTRAINT DF_PanelUser_Role DEFAULT 0,
		-- Telegram deep-link binding; the connection itself is the proof of
		-- validity, so no IsXxxValid flag exists for this column
		TelegramChatId		BIGINT				NULL
	)
	-- No admin seed: the first-release admin is created at deploy time with a
	-- real password by DeployCli (Scripts/deploy.sh — see Docs/19-deploy-runbook.md).
	-- The test-only admin/admin fixture lives in Backend/Tests/Data/Sql/DbScenario/init.sql.
END
GO

-- Filtered (NULLs excluded): Email and Mobile are optional, and an unfiltered
-- unique index would allow only one NULL row. These close the registration
-- CheckUniqueData -> INSERT TOCTOU window (Docs/14 L11).
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UK_PanelUser_Email')
CREATE UNIQUE NONCLUSTERED INDEX UK_PanelUser_Email
ON PanelUser(Email) WHERE Email IS NOT NULL;
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UK_PanelUser_Mobile')
CREATE UNIQUE NONCLUSTERED INDEX UK_PanelUser_Mobile
ON PanelUser(Mobile) WHERE Mobile IS NOT NULL;
GO

-- Same filtered pattern for the Telegram chat: optional column, but one chat
-- can belong to only one user (the bot's "already linked elsewhere" reply
-- comes from this index).
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UK_PanelUser_TelegramChatId')
CREATE UNIQUE NONCLUSTERED INDEX UK_PanelUser_TelegramChatId
ON PanelUser(TelegramChatId) WHERE TelegramChatId IS NOT NULL;
GO

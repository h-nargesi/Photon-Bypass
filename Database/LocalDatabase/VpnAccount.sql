USE FastBypass;
GO

IF OBJECT_ID('VpnAccount') IS NULL
CREATE TABLE VpnAccount (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_VpnAccount_Id PRIMARY KEY,
	UserId				INT				NOT NULL	CONSTRAINT FK_VpnAccount_UserId_PanelUser FOREIGN KEY REFERENCES PanelUser (Id)
													ON DELETE CASCADE,
	-- Docs/22 D2/D5: every account pins exactly one realm; renewal stacks
	-- in place and a stale account migrates (D6), so the column is NOT NULL
	-- and the bucket-0 fan-out is gone.
	RealmId				INT				NOT NULL	CONSTRAINT FK_VpnAccount_RealmId_Realm FOREIGN KEY REFERENCES Realm (Id),
	-- Docs/22 D3: globally-unique VPN username = owner panel username + optional
	-- suffix (system-suggested incremental number, user-editable); length <= 32.
	Username			VARCHAR(32)		NOT NULL	CONSTRAINT UK_VpnAccount_Username UNIQUE,
	VpnPassword			VARCHAR(200)	NOT NULL,
	-- Docs/22 D12: 0=Active, 1=Suspended (VPN blocked, portal alive), 2=Deactivated.
	Status				TINYINT			NOT NULL	CONSTRAINT DF_VpnAccount_Status DEFAULT 0,
	SendWarning			BIT				NOT NULL	CONSTRAINT DF_VpnAccount_SendWarning DEFAULT 1,
	LastWarningTime		DATETIME			NULL,
	-- Docs/16 D4: x-ui UUID-rotation throttle stamp
	LastCredentialRotateTime	DATETIME	NULL,
	Created				DATETIME		NOT NULL	CONSTRAINT DF_VpnAccount_Created DEFAULT GETDATE()
)
-- Docs/22 D9: an account is created only together with its first renewal
-- (one transaction in PlanApplication), never by registration.
GO

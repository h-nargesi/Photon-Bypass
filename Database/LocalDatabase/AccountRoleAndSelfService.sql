USE FastBypass;
GO

-- Admin-panel roles (docs/20 D2/D9/D11): three-valued Role on Account
-- (0=User, 1=Manager, 2=Admin; the single seeded admin gets 2) and the
-- AllowSelfService flag (UserTypes bit 4) marking accounts that may use their
-- own wallet/renewal. Existing accounts keep today's behavior = full
-- self-service. Idempotent: safe to re-run. There is no migration framework --
-- deploy manually (also on FastBypass_DbScenario).

IF COL_LENGTH('Account', 'Role') IS NULL
    ALTER TABLE Account ADD Role TINYINT NOT NULL CONSTRAINT DF_Account_Role DEFAULT 0;
GO

UPDATE Account SET Role = 2 WHERE Username = 'admin';
GO

UPDATE Account SET UserType = UserType | 4;
GO

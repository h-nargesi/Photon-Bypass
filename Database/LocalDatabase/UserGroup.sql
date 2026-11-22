USE FastBypass;
GO

-- Docs/24 D6: pricing groups. Titles are admin-facing only; users never see
-- the group itself, only its plans. The default group («وب سایت») receives
-- every self-registered member.
IF OBJECT_ID('UserGroup') IS NULL
CREATE TABLE UserGroup (
	Id			INT IDENTITY	NOT NULL	CONSTRAINT PK_UserGroup_Id PRIMARY KEY,
	Title		NVARCHAR(50)	NOT NULL	CONSTRAINT UK_UserGroup_Title UNIQUE,
	IsDefault	BIT				NOT NULL	CONSTRAINT DF_UserGroup_IsDefault DEFAULT 0,
	Created		DATETIME		NOT NULL	CONSTRAINT DF_UserGroup_Created DEFAULT GETDATE()
)
GO

-- At most one default group (filtered unique index — NULLs are not an issue
-- here because the column is NOT NULL, only the value 1 is constrained).
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UK_UserGroup_IsDefault')
CREATE UNIQUE NONCLUSTERED INDEX UK_UserGroup_IsDefault
ON UserGroup(IsDefault) WHERE IsDefault = 1;
GO

-- Seed: the default group must exist before any member row — the membership
-- column is NOT NULL and every creation path needs a fallback group (Docs/24 D6).
IF NOT EXISTS (SELECT * FROM UserGroup WHERE IsDefault = 1)
INSERT UserGroup (Title, IsDefault) VALUES (N'وب سایت', 1);
GO

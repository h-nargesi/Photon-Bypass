USE FastBypass;
GO

-- P5/L11 (docs/14-defects-audit.md): filtered unique indexes closing the
-- CheckUniqueData -> INSERT TOCTOU window in registration.
-- Idempotent: safe to re-run. There is no migration framework -- deploy manually.
-- NOTE: creation FAILS if duplicate rows already exist; the pre-checks below
--       report them first so they can be fixed before the indexes are created.

-- Pre-check: report existing duplicates (username).
IF EXISTS (
    SELECT Username FROM dbo.Account GROUP BY Username HAVING COUNT(*) > 1
)
BEGIN
    RAISERROR(N'Duplicate Account.Username rows exist; clean them before creating UX_Account_Username.', 16, 1);
    RETURN;
END

-- Pre-check: report existing duplicates (email, ignoring NULLs).
IF EXISTS (
    SELECT Email FROM dbo.Account WHERE Email IS NOT NULL GROUP BY Email HAVING COUNT(*) > 1
)
BEGIN
    RAISERROR(N'Duplicate Account.Email rows exist; clean them before creating UX_Account_Email.', 16, 1);
    RETURN;
END

-- Pre-check: report existing duplicates (mobile, ignoring NULLs).
IF EXISTS (
    SELECT Mobile FROM dbo.Account WHERE Mobile IS NOT NULL GROUP BY Mobile HAVING COUNT(*) > 1
)
BEGIN
    RAISERROR(N'Duplicate Account.Mobile rows exist; clean them before creating UX_Account_Mobile.', 16, 1);
    RETURN;
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Account_Username')
BEGIN
    DECLARE @ux_username_sql NVARCHAR(MAX) =
        N'CREATE UNIQUE NONCLUSTERED INDEX UX_Account_Username ON dbo.Account (Username)';

    IF SERVERPROPERTY('EngineEdition') IN (3, 5, 8)
        SET @ux_username_sql += N' WITH (ONLINE = ON)';

    EXEC sp_executesql @ux_username_sql;
END
GO

-- Filtered (NULLs excluded): Email and Mobile are optional, and an unfiltered
-- unique index would allow only one NULL row.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Account_Email')
BEGIN
    DECLARE @ux_email_sql NVARCHAR(MAX) =
        N'CREATE UNIQUE NONCLUSTERED INDEX UX_Account_Email ON dbo.Account (Email) WHERE Email IS NOT NULL';

    IF SERVERPROPERTY('EngineEdition') IN (3, 5, 8)
        SET @ux_email_sql += N' WITH (ONLINE = ON)';

    EXEC sp_executesql @ux_email_sql;
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Account_Mobile')
BEGIN
    DECLARE @ux_mobile_sql NVARCHAR(MAX) =
        N'CREATE UNIQUE NONCLUSTERED INDEX UX_Account_Mobile ON dbo.Account (Mobile) WHERE Mobile IS NOT NULL';

    IF SERVERPROPERTY('EngineEdition') IN (3, 5, 8)
        SET @ux_mobile_sql += N' WITH (ONLINE = ON)';

    EXEC sp_executesql @ux_mobile_sql;
END
GO

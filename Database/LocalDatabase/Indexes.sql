USE FastBypass;
GO

-- P1/H20 (docs/14-defects-audit.md): missing indexes for the hot query paths.
-- Idempotent: safe to re-run. There is no migration framework -- deploy manually.
-- Run TrafficDataDedup.sql first when duplicate (NasId, SessionId) rows exist,
-- otherwise the unique index creation fails.

-- TrafficData (NasId, SessionId) unique: relational backstop for the traffic merge key (C1).
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UK_TrafficData_NasId_SessionId')
BEGIN
    DECLARE @uk_traffic_sql NVARCHAR(MAX) =
        N'CREATE UNIQUE NONCLUSTERED INDEX UK_TrafficData_NasId_SessionId ON TrafficData (NasId, SessionId)';

    IF SERVERPROPERTY('EngineEdition') IN (3, 5, 8)
        SET @uk_traffic_sql += N' WITH (ONLINE = ON)';

    EXEC sp_executesql @uk_traffic_sql;
END
GO

-- TrafficData (AccountId, StartSession) INCLUDE (EndSession): Fetch / FetchOpen / chart reads.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TrafficData_AccountId_StartSession')
BEGIN
    DECLARE @ix_traffic_sql NVARCHAR(MAX) =
        N'CREATE NONCLUSTERED INDEX IX_TrafficData_AccountId_StartSession ON TrafficData (AccountId, StartSession) INCLUDE (EndSession)';

    IF SERVERPROPERTY('EngineEdition') IN (3, 5, 8)
        SET @ix_traffic_sql += N' WITH (ONLINE = ON)';

    EXEC sp_executesql @ix_traffic_sql;
END
GO

-- Renewal (AccountId): TotalPlanState view window functions.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Renewal_AccountId')
CREATE NONCLUSTERED INDEX IX_Renewal_AccountId
ON Renewal (AccountId);
GO

-- History (Target, Created): GetHistory filter.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_History_Target_Created')
CREATE NONCLUSTERED INDEX IX_History_Target_Created
ON History (Target, Created);
GO

-- Wallet (AccountId): GetBalance aggregate.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Wallet_AccountId')
CREATE NONCLUSTERED INDEX IX_Wallet_AccountId
ON Wallet (AccountId);
GO

-- Wallet (InvoiceCode): GetInvoice / GenerateNewInvoiceCode lookups. NOT unique on purpose:
-- one invoice spans several wallet rows (debit/credit items); code generation uniqueness is
-- guaranteed by the InvoiceSequence sequence (C4/P0), not by a row-level constraint.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Wallet_InvoiceCode')
CREATE NONCLUSTERED INDEX IX_Wallet_InvoiceCode
ON Wallet (InvoiceCode)
WHERE InvoiceCode IS NOT NULL;
GO

-- Account (Owner): GetActiveTargetArea on every non-admin login.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Account_Owner')
CREATE NONCLUSTERED INDEX IX_Account_Owner
ON Account (Owner);
GO

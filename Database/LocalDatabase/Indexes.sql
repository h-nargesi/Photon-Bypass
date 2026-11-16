USE FastBypass;
GO

-- Hot-path indexes (docs/14 H20, docs/22 §2). The unique TrafficData (NasId,
-- SessionId) key is the inline table constraint in TrafficData.sql. Run after
-- the table scripts.

-- TrafficData (VpnAccountId, StartSession) INCLUDE (EndSession): Fetch / FetchOpen / chart reads.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TrafficData_VpnAccountId_StartSession')
CREATE NONCLUSTERED INDEX IX_TrafficData_VpnAccountId_StartSession
ON TrafficData (VpnAccountId, StartSession) INCLUDE (EndSession);
GO

-- Renewal (VpnAccountId): TotalPlanState view window functions.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Renewal_VpnAccountId')
CREATE NONCLUSTERED INDEX IX_Renewal_VpnAccountId
ON Renewal (VpnAccountId);
GO

-- History (Target, Created): GetHistory filter.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_History_Target_Created')
CREATE NONCLUSTERED INDEX IX_History_Target_Created
ON History (Target, Created);
GO

-- History (VpnAccountId): account-scoped event drill-down (docs/22 D11).
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_History_VpnAccountId')
CREATE NONCLUSTERED INDEX IX_History_VpnAccountId
ON History (VpnAccountId)
WHERE VpnAccountId IS NOT NULL;
GO

-- Wallet-threshold state machine reads (docs/14 M33): GetLastByTitle groups
-- History by Target for a fixed Title on every wallet settlement and every
-- hourly run — without this index each read is a full table scan.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_History_Title_Target')
CREATE NONCLUSTERED INDEX IX_History_Title_Target
ON History (Title, Target);
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

-- PanelUser (Owner): GetActiveTargetArea on every non-admin login.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PanelUser_Owner')
CREATE NONCLUSTERED INDEX IX_PanelUser_Owner
ON PanelUser (Owner);
GO

-- VpnAccount (UserId): the account switcher / user-list nesting reads.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VpnAccount_UserId')
CREATE NONCLUSTERED INDEX IX_VpnAccount_UserId
ON VpnAccount (UserId);
GO

-- VpnAccount (RealmId): per-realm sweeps of the radius reconciliation.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VpnAccount_RealmId')
CREATE NONCLUSTERED INDEX IX_VpnAccount_RealmId
ON VpnAccount (RealmId);
GO

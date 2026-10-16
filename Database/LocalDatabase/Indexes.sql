USE FastBypass;
GO

-- Hot-path indexes (docs/14 H20). The unique TrafficData (NasId, SessionId) key
-- is the inline table constraint in TrafficData.sql. Run after the table scripts.

-- TrafficData (AccountId, StartSession) INCLUDE (EndSession): Fetch / FetchOpen / chart reads.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TrafficData_AccountId_StartSession')
CREATE NONCLUSTERED INDEX IX_TrafficData_AccountId_StartSession
ON TrafficData (AccountId, StartSession) INCLUDE (EndSession);
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

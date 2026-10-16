USE FastBypass;
GO

-- Invoice code source (docs/14 C4/P0): atomic sequence instead of MAX+1.
-- Fresh install starts at 10001; no legacy MAX(InvoiceCode) exists.
IF OBJECT_ID('InvoiceSequence', 'SO') IS NULL
	CREATE SEQUENCE InvoiceSequence AS INT START WITH 10001 INCREMENT BY 1;
GO

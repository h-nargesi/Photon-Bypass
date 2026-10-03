USE FastBypass;
GO

IF OBJECT_ID('InvoiceSequence', 'SO') IS NULL
BEGIN
    DECLARE @start_value INT = (SELECT ISNULL(MAX(InvoiceCode), 10000) + 1 FROM Wallet);
    DECLARE @sql NVARCHAR(MAX) = N'CREATE SEQUENCE InvoiceSequence AS INT START WITH ' + CAST(@start_value AS NVARCHAR(20)) + N' INCREMENT BY 1;';
    EXEC sp_executesql @sql;
END
GO

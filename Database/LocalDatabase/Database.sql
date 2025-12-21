-- Object: FastBypass
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FastBypass')
	CREATE DATABASE FastBypass
GO

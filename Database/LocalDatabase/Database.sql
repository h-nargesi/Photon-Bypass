-- Object: FastBypass
-- Fresh-install set (no release has shipped yet — there are no migration scripts):
-- every column added after the first drafts is folded into the CREATE TABLE below.
-- Run order: this file first, then the remaining .sql files in this folder
-- (foreign keys require the referenced table to exist; the test initializer
-- sorts the folder topologically by table-name references).
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FastBypass')
	CREATE DATABASE FastBypass
GO

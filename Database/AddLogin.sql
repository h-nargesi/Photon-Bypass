CREATE LOGIN test WITH PASSWORD = 'abc.123456';
GO

EXEC master..sp_addsrvrolemember @loginame = N'test', @rolename = N'sysadmin'
EXEC master..sp_addsrvrolemember @loginame = N'test', @rolename = N'dbcreator'
GO
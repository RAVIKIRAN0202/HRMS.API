IF COL_LENGTH('dbo.Users', 'MustChangePassword') IS NULL
    ALTER TABLE dbo.Users ADD MustChangePassword bit NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT 0;
IF COL_LENGTH('dbo.Users', 'SessionVersion') IS NULL
    ALTER TABLE dbo.Users ADD SessionVersion int NOT NULL CONSTRAINT DF_Users_SessionVersion DEFAULT 0;

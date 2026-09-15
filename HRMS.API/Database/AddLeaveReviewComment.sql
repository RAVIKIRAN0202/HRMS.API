-- Run against the HRMS database before starting the updated API.
IF COL_LENGTH('dbo.LeaveRequests', 'ReviewComment') IS NULL
    ALTER TABLE dbo.LeaveRequests ADD ReviewComment nvarchar(max) NULL;

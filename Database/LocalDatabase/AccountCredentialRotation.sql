USE FastBypass;
GO

-- x-ui UUID-rotation throttle (docs/16 D4): nullable stamp column on Account, same
-- pattern as Account.LastWarningTime. Idempotent: safe to re-run. There is no
-- migration framework -- deploy manually (also on FastBypass_DbScenario).

IF COL_LENGTH('Account', 'LastCredentialRotateTime') IS NULL
    ALTER TABLE Account ADD LastCredentialRotateTime DATETIME NULL;
GO

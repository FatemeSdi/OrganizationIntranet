-- Schema rollback is intentionally guarded: never discard enrollment, security state or configured providers.
-- Stop the new API before executing. An application rollback can normally keep these additive columns.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='002_AuthenticationSchema')
 BEGIN COMMIT; RETURN; END;
 IF EXISTS(SELECT 1 FROM Sec.AuthenticationChallenge)
   OR EXISTS(SELECT 1 FROM Sec.AuthenticationSettings WHERE Json<>N'{}' OR ProtectedSmsApiKey IS NOT NULL)
   OR EXISTS(SELECT 1 FROM Sec.[User] WHERE AuthenticatorSecret IS NOT NULL OR LastAuthenticatorStep<>-1 OR FailedLoginAttempts<>0 OR LockedUntil IS NOT NULL OR LastSecuritySmsAt IS NOT NULL)
   THROW 51000,'Authentication state has been used. Keep the additive schema; do not discard security data.',1;
 DROP TABLE Sec.AuthenticationChallenge;
 DROP TABLE Sec.AuthenticationSettings;
 ALTER TABLE Sec.[User] DROP CONSTRAINT CK_User_AuthenticationCounters,DF_User_SecurityStamp,DF_User_LastAuthenticatorStep,DF_User_FailedLoginAttempts,DF_User_AuthenticationVersion;
 ALTER TABLE Sec.[User] DROP COLUMN SecurityStamp,AuthenticatorSecret,LastAuthenticatorStep,FailedLoginAttempts,LockedUntil,LastSecuritySmsAt,AuthenticationVersion;
 DELETE dbo.SchemaMigration WHERE MigrationId='002_AuthenticationSchema';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

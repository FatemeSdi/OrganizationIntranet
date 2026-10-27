-- Rollback: stop the API first. Dropping the table forgets the per-application usernames; single sign-on then uses the intranet
-- username again for everyone, so users whose application username differs can no longer sign in that way until the mapping is re-entered.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='011_UserApplicationAccounts') BEGIN COMMIT; RETURN; END;
 DROP TABLE App.UserApplicationAccount;
 DELETE dbo.SchemaMigration WHERE MigrationId='011_UserApplicationAccounts';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

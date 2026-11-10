-- Rollback: INTERANET becomes active again (it was never public, so IsPublic stays 0).
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='019_HideHomeApplication') BEGIN COMMIT; RETURN; END;
 UPDATE App.Application SET IsActive = 1 WHERE ApplicationCode = 'INTERANET';
 DELETE dbo.SchemaMigration WHERE MigrationId='019_HideHomeApplication';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

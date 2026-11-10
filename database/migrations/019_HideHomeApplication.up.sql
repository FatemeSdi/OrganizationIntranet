-- Data: the application "اینترانت سازمانی" (code INTERANET) is not a system users open from the portal; it only owns the panel permissions and is the sender of administrator
-- notifications. It is made inactive so its card no longer appears in any list of systems. Nothing else changes: its permissions, role grants and the notifications that
-- name it as sender keep working (they do not depend on the application being active).
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='018_PublicApplications')
    THROW 51000,'Apply 018_PublicApplications first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='019_HideHomeApplication')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 UPDATE App.Application SET IsActive = 0, IsPublic = 0 WHERE ApplicationCode = 'INTERANET';
 INSERT dbo.SchemaMigration(MigrationId) VALUES('019_HideHomeApplication');
 COMMIT;
 SELECT 'Applied 019_HideHomeApplication' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

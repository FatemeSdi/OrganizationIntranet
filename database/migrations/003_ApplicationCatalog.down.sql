-- Rollback: stop the API first. Refuses while 004 is installed or while catalog configuration exists
-- (access grants, service registrations or non-default flags); export and reconcile those explicitly instead of losing them.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='003_ApplicationCatalog') BEGIN COMMIT; RETURN; END;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='004_AccessWorkflow') THROW 51000,'Roll back 004_AccessWorkflow first.',1;
 EXEC(N'IF EXISTS(SELECT 1 FROM App.ApplicationAccess) OR EXISTS(SELECT 1 FROM App.ApplicationService)
 OR EXISTS(SELECT 1 FROM App.Application WHERE RequiresLogin=0 OR IsInternetAccessible=1 OR IsPublic=1)
 THROW 51000,''Application catalog contains configuration. Export and explicitly reconcile before rollback.'',1;');
 DROP TABLE App.ApplicationService;
 DROP TABLE App.ApplicationAccess;
 ALTER TABLE App.Application DROP CONSTRAINT DF_Application_RequiresLogin, DF_Application_IsInternetAccessible, DF_Application_IsPublic;
 ALTER TABLE App.Application DROP COLUMN RequiresLogin, IsInternetAccessible, IsPublic;
 DELETE dbo.SchemaMigration WHERE MigrationId='003_ApplicationCatalog';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

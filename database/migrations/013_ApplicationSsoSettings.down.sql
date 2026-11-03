-- Rollback: stop the API first. The stored SSO paths and key hashes are lost; applications with SSO must then have their key and path in appsettings again
-- (the previous code reads ApplicationIntegrations:{CODE}:SsoKey/SsoPath).
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='013_ApplicationSsoSettings') BEGIN COMMIT; RETURN; END;
 DROP INDEX UX_Application_SsoKeyHash ON App.Application;
 ALTER TABLE App.Application DROP CONSTRAINT CK_Application_SsoPath;
 ALTER TABLE App.Application DROP COLUMN SsoPath, SsoKeyHash, SsoKeyChangedAt;
 DELETE dbo.SchemaMigration WHERE MigrationId='013_ApplicationSsoSettings';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

-- Rollback: stop the API first. The unit-based access settings are lost; applications that were unit-restricted become ordinary "request required" applications
-- (only their explicit user/role grants remain), so users who had access only through their unit lose it.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='014_ApplicationUnitAccess') BEGIN COMMIT; RETURN; END;
 DROP TABLE App.ApplicationUnitAccess;
 ALTER TABLE App.Application DROP CONSTRAINT CK_Application_AccessMode;
 ALTER TABLE App.Application DROP CONSTRAINT DF_Application_IsUnitRestricted;
 ALTER TABLE App.Application DROP COLUMN IsUnitRestricted;
 DELETE dbo.SchemaMigration WHERE MigrationId='014_ApplicationUnitAccess';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

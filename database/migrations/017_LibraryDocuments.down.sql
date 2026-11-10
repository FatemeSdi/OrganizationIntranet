-- Rollback: stop the API first. The library tables are DROPPED, so every document record and category is lost (the portal returns to its static catalog in the older code).
-- The uploaded FILES stay on the API server's disk (App_Data/library or Library:StoragePath); delete that folder by hand if you do not need them. The permission
-- LIBRARY_EDIT and its role grants are removed too.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='017_LibraryDocuments') BEGIN COMMIT; RETURN; END;
 DELETE rp FROM Sec.RolePermission rp JOIN Sec.Permission p ON p.PermissionId=rp.PermissionId WHERE p.PermissionCode='LIBRARY_EDIT';
 DELETE up FROM Sec.UserPermission up JOIN Sec.Permission p ON p.PermissionId=up.PermissionId WHERE p.PermissionCode='LIBRARY_EDIT';
 DELETE FROM Sec.Permission WHERE PermissionCode='LIBRARY_EDIT';
 DROP TABLE Lib.Document;
 DROP TABLE Lib.Category;
 IF SCHEMA_ID(N'Lib') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.objects WHERE schema_id=SCHEMA_ID(N'Lib')) EXEC(N'DROP SCHEMA Lib');
 DELETE dbo.SchemaMigration WHERE MigrationId='017_LibraryDocuments';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

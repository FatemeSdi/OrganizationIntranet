-- Rollback: stop the API and Admin first. The organization chart and every user's unit assignment are lost (the units can be re-imported
-- with tools/Import-OrganizationChartFromIts.ps1). The permissions ORG_VIEW and ORG_EDIT and their role grants are removed too.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='012_OrganizationChart') BEGIN COMMIT; RETURN; END;
 DELETE rp FROM Sec.RolePermission rp JOIN Sec.Permission p ON p.PermissionId=rp.PermissionId WHERE p.PermissionCode IN ('ORG_VIEW','ORG_EDIT');
 DELETE up FROM Sec.UserPermission up JOIN Sec.Permission p ON p.PermissionId=up.PermissionId WHERE p.PermissionCode IN ('ORG_VIEW','ORG_EDIT');
 DELETE FROM Sec.Permission WHERE PermissionCode IN ('ORG_VIEW','ORG_EDIT');
 DROP INDEX IX_User_UnitId ON Sec.[User];
 ALTER TABLE Sec.[User] DROP CONSTRAINT FK_User_OrgUnit;
 ALTER TABLE Sec.[User] DROP COLUMN UnitId;
 DROP TABLE Org.Unit;
 IF SCHEMA_ID(N'Org') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.objects WHERE schema_id=SCHEMA_ID(N'Org')) EXEC(N'DROP SCHEMA Org');
 DELETE dbo.SchemaMigration WHERE MigrationId='012_OrganizationChart';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

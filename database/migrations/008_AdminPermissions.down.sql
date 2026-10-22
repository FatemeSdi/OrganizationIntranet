-- Rollback: only together with the previous API/Admin build, which authorizes the Admin panel by the ADMIN role.
-- Running this under the current build locks everybody out of the Admin panel. Stop the new API and Admin first.
-- Removes the system permissions and their role grants (roles other than ADMIN that received them lose them too).
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='008_AdminPermissions')
 BEGIN COMMIT; RETURN; END;
 DECLARE @codes TABLE(Code varchar(100) PRIMARY KEY);
 INSERT @codes VALUES('INTRANET_ADMIN'),('USER_VIEW'),('USER_EDIT'),('ROLE_VIEW'),('ROLE_EDIT'),('PERMISSION_VIEW'),('PERMISSION_EDIT'),
  ('APPLICATION_VIEW'),('APPLICATION_EDIT'),('ACCESS_REQUEST_REVIEW'),('PUBLICATION_EDIT'),('AUTH_SETTINGS_EDIT');
 DELETE rp FROM Sec.RolePermission rp JOIN Sec.Permission p ON p.PermissionId=rp.PermissionId JOIN @codes c ON c.Code=p.PermissionCode;
 DELETE up FROM Sec.UserPermission up JOIN Sec.Permission p ON p.PermissionId=up.PermissionId JOIN @codes c ON c.Code=p.PermissionCode;
 DELETE p FROM Sec.Permission p JOIN @codes c ON c.Code=p.PermissionCode;
 DELETE dbo.SchemaMigration WHERE MigrationId='008_AdminPermissions';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

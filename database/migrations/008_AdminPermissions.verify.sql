SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='008_AdminPermissions') THROW 51000,'Admin permissions migration is not installed.',1;
DECLARE @codes TABLE(Code varchar(100) PRIMARY KEY);
INSERT @codes VALUES('INTRANET_ADMIN'),('USER_VIEW'),('USER_EDIT'),('ROLE_VIEW'),('ROLE_EDIT'),('PERMISSION_VIEW'),('PERMISSION_EDIT'),
 ('APPLICATION_VIEW'),('APPLICATION_EDIT'),('ACCESS_REQUEST_REVIEW'),('PUBLICATION_EDIT'),('AUTH_SETTINGS_EDIT');
IF EXISTS(SELECT 1 FROM @codes c WHERE NOT EXISTS(SELECT 1 FROM Sec.Permission p WHERE p.PermissionCode=c.Code AND p.IsActive=1))
 THROW 51000,'An Admin panel permission is missing or inactive.',1;
IF EXISTS(SELECT 1 FROM @codes c WHERE NOT EXISTS(SELECT 1 FROM Sec.Permission p JOIN Sec.RolePermission rp ON rp.PermissionId=p.PermissionId
          JOIN Sec.Role r ON r.RoleId=rp.RoleId WHERE p.PermissionCode=c.Code AND r.RoleCode='ADMIN' AND r.IsActive=1))
 THROW 51000,'Role ADMIN does not hold every Admin panel permission.',1;
-- Lock-out check: somebody must still be able to open the Admin panel.
SELECT COUNT(DISTINCT u.UserId) AS UsersWithAdminPanelAccess
FROM Sec.[User] u JOIN Sec.UserRole ur ON ur.UserId=u.UserId JOIN Sec.Role r ON r.RoleId=ur.RoleId AND r.IsActive=1
JOIN Sec.RolePermission rp ON rp.RoleId=r.RoleId JOIN Sec.Permission p ON p.PermissionId=rp.PermissionId AND p.IsActive=1 AND p.PermissionCode='INTRANET_ADMIN'
WHERE u.IsActive=1;
IF NOT EXISTS(SELECT 1 FROM Sec.[User] u JOIN Sec.UserRole ur ON ur.UserId=u.UserId JOIN Sec.Role r ON r.RoleId=ur.RoleId AND r.IsActive=1
 JOIN Sec.RolePermission rp ON rp.RoleId=r.RoleId JOIN Sec.Permission p ON p.PermissionId=rp.PermissionId AND p.IsActive=1 AND p.PermissionCode='INTRANET_ADMIN' WHERE u.IsActive=1)
 THROW 51000,'No active user can open the Admin panel.',1;
SELECT 'Admin panel permissions verified' AS VerificationStatus;

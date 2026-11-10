-- Rollback: stop the API first. Notifications sent from the panel (rows with an audience) are DELETED together with their delivery rows, because the old code has no
-- way to hide a withdrawn notification; notifications that came from other applications are kept. The permission NOTIFICATION_SEND and its role grants are removed too.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='015_AdminNotifications') BEGIN COMMIT; RETURN; END;
 EXEC(N'DELETE un FROM Notify.UserNotification un JOIN Notify.Notification n ON n.NotificationId=un.NotificationId WHERE n.AudienceType IS NOT NULL OR n.IsActive=0');
 EXEC(N'DELETE FROM Notify.Notification WHERE AudienceType IS NOT NULL OR IsActive=0');
 DELETE rp FROM Sec.RolePermission rp JOIN Sec.Permission p ON p.PermissionId=rp.PermissionId WHERE p.PermissionCode='NOTIFICATION_SEND';
 DELETE up FROM Sec.UserPermission up JOIN Sec.Permission p ON p.PermissionId=up.PermissionId WHERE p.PermissionCode='NOTIFICATION_SEND';
 DELETE FROM Sec.Permission WHERE PermissionCode='NOTIFICATION_SEND';
 DROP INDEX IX_Notification_AudienceUnitId ON Notify.Notification;
 DROP INDEX IX_Notification_AudienceUserId ON Notify.Notification;
 ALTER TABLE Notify.Notification DROP CONSTRAINT CK_Notification_Audience;
 ALTER TABLE Notify.Notification DROP CONSTRAINT FK_Notification_AudienceUnit;
 ALTER TABLE Notify.Notification DROP CONSTRAINT FK_Notification_AudienceUser;
 ALTER TABLE Notify.Notification DROP CONSTRAINT DF_Notification_IsActive;
 ALTER TABLE Notify.Notification DROP COLUMN IsActive, AudienceType, AudienceUserId, AudienceUnitId, RecipientCount, ChangedAt;
 DELETE dbo.SchemaMigration WHERE MigrationId='015_AdminNotifications';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='015_AdminNotifications') THROW 51000,'Admin notifications migration is not installed.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'Notify.Notification')
      AND ((name='IsActive' AND is_nullable=0) OR (name IN ('AudienceType','AudienceUserId','AudienceUnitId','RecipientCount','ChangedAt') AND is_nullable=1))) <> 6
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Notification_Audience' AND parent_object_id=OBJECT_ID(N'Notify.Notification') AND is_disabled=0 AND is_not_trusted=0)
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN ('FK_Notification_AudienceUser','FK_Notification_AudienceUnit') AND parent_object_id=OBJECT_ID(N'Notify.Notification') AND is_disabled=0 AND is_not_trusted=0) <> 2
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Notification_AudienceUserId' AND object_id=OBJECT_ID(N'Notify.Notification'))
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Notification_AudienceUnitId' AND object_id=OBJECT_ID(N'Notify.Notification'))
 THROW 51000,'Admin notification schema is incomplete.',1;
IF NOT EXISTS(SELECT 1 FROM Sec.Permission p JOIN Sec.RolePermission rp ON rp.PermissionId=p.PermissionId JOIN Sec.Role r ON r.RoleId=rp.RoleId
              WHERE p.PermissionCode='NOTIFICATION_SEND' AND p.IsActive=1 AND r.RoleCode='ADMIN')
 THROW 51000,'ADMIN does not hold NOTIFICATION_SEND.',1;
SELECT 'Admin notification schema verified' AS VerificationStatus;

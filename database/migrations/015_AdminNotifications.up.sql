-- Additive: notifications sent by an administrator from the panel (to one user, to an organizational unit with its sub-units, or to everyone).
-- They reuse Notify.Notification / Notify.UserNotification (one delivery row per recipient, with its own read state), so the bell and the notification center need no
-- second mechanism. New columns: IsActive (an admin can withdraw a notification and bring it back; withdrawn ones disappear from every recipient's list but keep
-- their read state), AudienceType/AudienceUserId/AudienceUnitId (who it was addressed to; NULL for notifications that come from other applications),
-- RecipientCount and ChangedAt. Also registers the permission NOTIFICATION_SEND (shared/Contracts/AdminPermissions.cs) and grants it to ADMIN.
-- Existing notifications are untouched (IsActive = 1, no audience).
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='014_ApplicationUnitAccess')
    THROW 51000,'Apply 014_ApplicationUnitAccess first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='015_AdminNotifications')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 IF COL_LENGTH(N'Notify.Notification',N'IsActive') IS NOT NULL OR COL_LENGTH(N'Notify.Notification',N'AudienceType') IS NOT NULL
    THROW 51000,'Admin notification columns already exist in part. Reconcile before applying.',1;
 DECLARE @adminRole int = (SELECT RoleId FROM Sec.Role WHERE RoleCode='ADMIN' AND IsActive=1);
 IF @adminRole IS NULL THROW 51000,'Role ADMIN does not exist or is inactive.',1;
 DECLARE @app int = (SELECT ApplicationId FROM App.Application WHERE ApplicationCode='INTERANET');
 IF @app IS NULL THROW 51000,'Application INTERANET does not exist (apply 008_AdminPermissions).',1;
 IF EXISTS(SELECT 1 FROM Sec.Permission WHERE PermissionCode='NOTIFICATION_SEND' AND ApplicationId<>@app)
    THROW 51000,'The permission code NOTIFICATION_SEND already exists for another application. Reconcile before applying.',1;

 ALTER TABLE Notify.Notification ADD
   IsActive bit NOT NULL CONSTRAINT DF_Notification_IsActive DEFAULT(1),
   AudienceType varchar(10) NULL,
   AudienceUserId bigint NULL,
   AudienceUnitId int NULL,
   RecipientCount int NULL,
   ChangedAt datetime2(7) NULL;
 EXEC(N'ALTER TABLE Notify.Notification ADD CONSTRAINT FK_Notification_AudienceUser FOREIGN KEY(AudienceUserId) REFERENCES Sec.[User](UserId)');
 EXEC(N'ALTER TABLE Notify.Notification ADD CONSTRAINT FK_Notification_AudienceUnit FOREIGN KEY(AudienceUnitId) REFERENCES Org.Unit(UnitId)');
 EXEC(N'ALTER TABLE Notify.Notification ADD CONSTRAINT CK_Notification_Audience CHECK(
     (AudienceType IS NULL AND AudienceUserId IS NULL AND AudienceUnitId IS NULL AND RecipientCount IS NULL)
  OR (AudienceType = ''All'' AND AudienceUserId IS NULL AND AudienceUnitId IS NULL AND CreatedBy IS NOT NULL AND RecipientCount >= 1)
  OR (AudienceType = ''User'' AND AudienceUserId IS NOT NULL AND AudienceUnitId IS NULL AND CreatedBy IS NOT NULL AND RecipientCount >= 1)
  OR (AudienceType = ''Unit'' AND AudienceUnitId IS NOT NULL AND AudienceUserId IS NULL AND CreatedBy IS NOT NULL AND RecipientCount >= 1))');
 EXEC(N'CREATE INDEX IX_Notification_AudienceUserId ON Notify.Notification(AudienceUserId) WHERE AudienceUserId IS NOT NULL');
 EXEC(N'CREATE INDEX IX_Notification_AudienceUnitId ON Notify.Notification(AudienceUnitId) WHERE AudienceUnitId IS NOT NULL');

 DECLARE @p TABLE(Code varchar(100) PRIMARY KEY, Name nvarchar(150) NOT NULL);
 INSERT @p(Code, Name) VALUES ('NOTIFICATION_SEND', N'ارسال و مدیریت اعلان‌های سازمانی');
 INSERT Sec.Permission(ApplicationId, PermissionName, PermissionCode, Description, IsActive)
 SELECT @app, p.Name, p.Code, N'مجوز سیستمی پنل مدیریت', 1 FROM @p p WHERE NOT EXISTS(SELECT 1 FROM Sec.Permission x WHERE x.PermissionCode=p.Code);
 UPDATE x SET IsActive=1 FROM Sec.Permission x JOIN @p p ON p.Code=x.PermissionCode WHERE x.IsActive=0;
 INSERT Sec.RolePermission(RoleId, PermissionId)
 SELECT @adminRole, x.PermissionId FROM Sec.Permission x JOIN @p p ON p.Code=x.PermissionCode
 WHERE NOT EXISTS(SELECT 1 FROM Sec.RolePermission rp WHERE rp.RoleId=@adminRole AND rp.PermissionId=x.PermissionId);

 INSERT dbo.SchemaMigration(MigrationId) VALUES('015_AdminNotifications');
 COMMIT;
 SELECT 'Applied 015_AdminNotifications' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

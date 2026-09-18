-- Restores legacy schema metadata, without deleting or rewriting business rows.
-- Deploy the previous application version together with this rollback.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @lock int;
    EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
    IF @lock<0 THROW 51000,'Cannot acquire migration lock.',1;
    IF OBJECT_ID(N'dbo.SchemaMigration',N'U') IS NULL
    BEGIN
        COMMIT;
        RETURN;
    END;
    IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='001_DatabaseConsistency')
    BEGIN
        COMMIT;
        RETURN;
    END;
    ALTER TABLE Sec.RolePermission DROP CONSTRAINT DF_RolePermission_CreatedAt;
    ALTER TABLE Sec.RolePermission ADD CONSTRAINT DF_RolePermission_CreatedAt DEFAULT GETDATE() FOR CreatedAt;
    ALTER TABLE Notify.UserNotification DROP CONSTRAINT CK_UserNotification_ReadState;
    DROP INDEX IX_UserNotification_UserId_IsRead_CreatedAt ON Notify.UserNotification;
    DROP INDEX IX_Notification_CreatedBy ON Notify.Notification;
    DROP INDEX IX_Notification_ApplicationId ON Notify.Notification;
    DROP INDEX IX_UserPermission_PermissionId ON Sec.UserPermission;
    CREATE UNIQUE INDEX UX_Application_ApplicationCode ON App.Application(ApplicationCode);
    CREATE UNIQUE INDEX UX_Role_RoleCode ON Sec.Role(RoleCode);
    EXEC sys.sp_rename N'App.UQ_Application_ApplicationCode',N'UQ_Application_Code',N'OBJECT';
    EXEC sys.sp_rename N'Sec.UQ_Role_RoleCode',N'UQ_Role_Code',N'OBJECT';
    EXEC sys.sp_rename N'Sec.UQ_Permission_ApplicationId_PermissionCode',N'UQ_Permission_Code',N'OBJECT';
    EXEC sys.sp_rename N'Sec.UQ_UserRole_UserId_RoleId',N'UQ_UserRole',N'OBJECT';
    EXEC sys.sp_rename N'Sec.UQ_UserPermission_UserId_PermissionId',N'UQ_UserPermission',N'OBJECT';
    EXEC sys.sp_rename N'Notify.UQ_UserNotification_NotificationId_UserId',N'UQ_UserNotification',N'OBJECT';
    EXEC sys.sp_rename N'Sec.RolePermission.UX_RolePermission_RoleId_PermissionId',N'UX_RolePermission_Role_Permission',N'INDEX';
    DELETE dbo.SchemaMigration WHERE MigrationId='001_DatabaseConsistency';
    COMMIT;
    SELECT 'Rolled back' AS MigrationStatus;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;

-- Stop the updated services before a rollback. Never discard workflow or integration history.
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='004_AccessWorkflow') BEGIN COMMIT; RETURN; END;
 EXEC(N'IF EXISTS(SELECT 1 FROM App.ApplicationAccessRequest) OR EXISTS(SELECT 1 FROM App.ApplicationSupervisionGroup)
 OR EXISTS(SELECT 1 FROM Notify.AccessRequestAlert) OR EXISTS(SELECT 1 FROM Notify.Notification WHERE ExternalId IS NOT NULL)
 OR EXISTS(SELECT 1 FROM App.Application WHERE AccessRequestNotificationChannels<>''InApp'')
 OR EXISTS(SELECT 1 FROM App.ApplicationService WHERE SyncCursor IS NOT NULL OR LastSyncedAt IS NOT NULL OR LastSyncError IS NOT NULL)
 THROW 51000,''Workflow contains configuration or history. Export and explicitly reconcile before rollback.'',1;');
 DROP TABLE Notify.AccessRequestAlert;
 DROP TABLE App.ApplicationSupervisor;
 DROP TABLE App.ApplicationSupervisionGroup;
 DROP TABLE App.ApplicationAccessRequest;
 DROP INDEX IX_Notification_ApplicationId_ExternalRecipientId_ExternalId ON Notify.Notification;
 DROP INDEX IX_Notification_ExternalRecipientId ON Notify.Notification;
 ALTER TABLE Notify.Notification DROP CONSTRAINT CK_Notification_ExternalIdentity,FK_Notification_User_ExternalRecipientId;
 ALTER TABLE Notify.Notification DROP COLUMN ExternalId,ExternalRecipientId,SourceUpdatedAt;
 ALTER TABLE App.ApplicationService DROP COLUMN SyncCursor,LastSyncedAt,LastSyncError;
 ALTER TABLE App.Application DROP CONSTRAINT CK_Application_AccessRequestChannels,DF_Application_AccessRequestNotificationChannels;
 ALTER TABLE App.Application DROP COLUMN AccessRequestNotificationChannels;
 DELETE dbo.SchemaMigration WHERE MigrationId='004_AccessWorkflow';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

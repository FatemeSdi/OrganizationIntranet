-- Catalog 003 is required. Schema only: no applications, permissions, or messages are seeded.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='003_ApplicationCatalog')
    THROW 51000,'Apply application catalog migration first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='004_AccessWorkflow')
 BEGIN COMMIT; SELECT 'Already applied' AS MigrationStatus; RETURN; END;
 IF OBJECT_ID(N'App.ApplicationAccessRequest',N'U') IS NOT NULL OR COL_LENGTH(N'Notify.Notification',N'ExternalId') IS NOT NULL
    OR COL_LENGTH(N'App.Application',N'AccessRequestNotificationChannels') IS NOT NULL OR COL_LENGTH(N'App.ApplicationService',N'SyncCursor') IS NOT NULL
    THROW 51000,'Partial workflow schema exists. Reconcile before applying.',1;
 ALTER TABLE App.Application ADD AccessRequestNotificationChannels varchar(10) NOT NULL CONSTRAINT DF_Application_AccessRequestNotificationChannels DEFAULT('InApp') WITH VALUES;
 EXEC(N'ALTER TABLE App.Application WITH CHECK ADD CONSTRAINT CK_Application_AccessRequestChannels CHECK(AccessRequestNotificationChannels IN (''InApp'',''Sms'',''Both''))');
 ALTER TABLE App.ApplicationService ADD SyncCursor nvarchar(1000) NULL, LastSyncedAt datetime2(7) NULL, LastSyncError nvarchar(500) NULL;
 ALTER TABLE Notify.Notification ADD ExternalId nvarchar(100) NULL, ExternalRecipientId bigint NULL, SourceUpdatedAt datetime2(7) NULL;
 EXEC(N'ALTER TABLE Notify.Notification WITH CHECK ADD CONSTRAINT CK_Notification_ExternalIdentity CHECK ((ExternalId IS NULL AND ExternalRecipientId IS NULL AND SourceUpdatedAt IS NULL) OR (ExternalId IS NOT NULL AND ExternalRecipientId IS NOT NULL AND SourceUpdatedAt IS NOT NULL))');
 EXEC(N'ALTER TABLE Notify.Notification ADD CONSTRAINT FK_Notification_User_ExternalRecipientId FOREIGN KEY(ExternalRecipientId) REFERENCES Sec.[User](UserId)');
 EXEC(N'CREATE UNIQUE INDEX IX_Notification_ApplicationId_ExternalRecipientId_ExternalId ON Notify.Notification(ApplicationId,ExternalRecipientId,ExternalId) WHERE ExternalId IS NOT NULL');
 EXEC(N'CREATE INDEX IX_Notification_ExternalRecipientId ON Notify.Notification(ExternalRecipientId)');
 CREATE TABLE App.ApplicationAccessRequest (
   ApplicationAccessRequestId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationAccessRequest PRIMARY KEY,
   ApplicationId int NOT NULL, UserId bigint NOT NULL, Reason nvarchar(1000) NOT NULL, Status varchar(20) NOT NULL,
   CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_ApplicationAccessRequest_CreatedAt DEFAULT(GETUTCDATE()),
   ResolvedAt datetime2(7) NULL, ReviewedBy bigint NULL, ReviewNote nvarchar(1000) NULL, Revision uniqueidentifier NOT NULL,
   CONSTRAINT FK_ApplicationAccessRequest_Application_ApplicationId FOREIGN KEY(ApplicationId) REFERENCES App.Application(ApplicationId),
   CONSTRAINT FK_ApplicationAccessRequest_User_UserId FOREIGN KEY(UserId) REFERENCES Sec.[User](UserId),
   CONSTRAINT FK_ApplicationAccessRequest_User_ReviewedBy FOREIGN KEY(ReviewedBy) REFERENCES Sec.[User](UserId),
   CONSTRAINT CK_ApplicationAccessRequest_Status CHECK(Status IN ('Pending','Approved','Rejected','Cancelled')),
   CONSTRAINT CK_ApplicationAccessRequest_Resolution CHECK((Status='Pending' AND ResolvedAt IS NULL AND ReviewedBy IS NULL) OR (Status='Cancelled' AND ResolvedAt IS NOT NULL AND ReviewedBy IS NULL) OR (Status IN ('Approved','Rejected') AND ResolvedAt IS NOT NULL AND ReviewedBy IS NOT NULL))
 );
 CREATE UNIQUE INDEX IX_ApplicationAccessRequest_UserId_ApplicationId ON App.ApplicationAccessRequest(UserId,ApplicationId) WHERE Status='Pending';
 CREATE INDEX IX_ApplicationAccessRequest_Status_CreatedAt_ApplicationAccessRequestId ON App.ApplicationAccessRequest(Status,CreatedAt,ApplicationAccessRequestId);
 CREATE INDEX IX_ApplicationAccessRequest_ApplicationId ON App.ApplicationAccessRequest(ApplicationId);
 CREATE INDEX IX_ApplicationAccessRequest_ReviewedBy ON App.ApplicationAccessRequest(ReviewedBy);
 CREATE TABLE App.ApplicationSupervisionGroup (
   ApplicationSupervisionGroupId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationSupervisionGroup PRIMARY KEY,
   ApplicationId int NOT NULL, Name nvarchar(150) NOT NULL, IsActive bit NOT NULL, Revision uniqueidentifier NOT NULL,
   CONSTRAINT FK_ApplicationSupervisionGroup_Application_ApplicationId FOREIGN KEY(ApplicationId) REFERENCES App.Application(ApplicationId)
 );
 CREATE UNIQUE INDEX IX_ApplicationSupervisionGroup_ApplicationId_Name ON App.ApplicationSupervisionGroup(ApplicationId,Name);
 CREATE TABLE App.ApplicationSupervisor (
   ApplicationSupervisorId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationSupervisor PRIMARY KEY,
   ApplicationSupervisionGroupId int NOT NULL, UserId bigint NULL, RoleId int NULL,
   CONSTRAINT CK_ApplicationSupervisor_Principal CHECK ((UserId IS NOT NULL AND RoleId IS NULL) OR (UserId IS NULL AND RoleId IS NOT NULL)),
   CONSTRAINT FK_ApplicationSupervisor_ApplicationSupervisionGroup_ApplicationSupervisionGroupId FOREIGN KEY(ApplicationSupervisionGroupId) REFERENCES App.ApplicationSupervisionGroup(ApplicationSupervisionGroupId),
   CONSTRAINT FK_ApplicationSupervisor_User_UserId FOREIGN KEY(UserId) REFERENCES Sec.[User](UserId),
   CONSTRAINT FK_ApplicationSupervisor_Role_RoleId FOREIGN KEY(RoleId) REFERENCES Sec.Role(RoleId)
 );
 CREATE UNIQUE INDEX IX_ApplicationSupervisor_ApplicationSupervisionGroupId_UserId ON App.ApplicationSupervisor(ApplicationSupervisionGroupId,UserId) WHERE UserId IS NOT NULL;
 CREATE UNIQUE INDEX IX_ApplicationSupervisor_ApplicationSupervisionGroupId_RoleId ON App.ApplicationSupervisor(ApplicationSupervisionGroupId,RoleId) WHERE RoleId IS NOT NULL;
 CREATE INDEX IX_ApplicationSupervisor_UserId ON App.ApplicationSupervisor(UserId);
 CREATE INDEX IX_ApplicationSupervisor_RoleId ON App.ApplicationSupervisor(RoleId);
 CREATE TABLE Notify.AccessRequestAlert (
   AccessRequestAlertId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AccessRequestAlert PRIMARY KEY,
   ApplicationAccessRequestId bigint NOT NULL, UserId bigint NOT NULL, InApp bit NOT NULL, IsRead bit NOT NULL,
   ReadAt datetime2(7) NULL, CreatedAt datetime2(7) NOT NULL, SmsStatus varchar(20) NOT NULL, SmsAttempts int NOT NULL,
   NextAttemptAt datetime2(7) NULL, SmsSentAt datetime2(7) NULL, SmsError nvarchar(500) NULL, Revision uniqueidentifier NOT NULL,
   CONSTRAINT FK_AccessRequestAlert_ApplicationAccessRequest_ApplicationAccessRequestId FOREIGN KEY(ApplicationAccessRequestId) REFERENCES App.ApplicationAccessRequest(ApplicationAccessRequestId),
   CONSTRAINT FK_AccessRequestAlert_User_UserId FOREIGN KEY(UserId) REFERENCES Sec.[User](UserId),
   CONSTRAINT CK_AccessRequestAlert_ReadState CHECK((IsRead=0 AND ReadAt IS NULL) OR (IsRead=1 AND ReadAt IS NOT NULL)),
   CONSTRAINT CK_AccessRequestAlert_SmsStatus CHECK(SmsStatus IN ('NotRequested','Pending','Processing','Sent','Failed','Cancelled'))
 );
 CREATE UNIQUE INDEX IX_AccessRequestAlert_ApplicationAccessRequestId_UserId ON Notify.AccessRequestAlert(ApplicationAccessRequestId,UserId);
 CREATE INDEX IX_AccessRequestAlert_UserId_InApp_IsRead ON Notify.AccessRequestAlert(UserId,InApp,IsRead);
 CREATE INDEX IX_AccessRequestAlert_SmsStatus_NextAttemptAt ON Notify.AccessRequestAlert(SmsStatus,NextAttemptAt);
 INSERT dbo.SchemaMigration(MigrationId) VALUES('004_AccessWorkflow');
 COMMIT;
 SELECT 'Applied 004_AccessWorkflow' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

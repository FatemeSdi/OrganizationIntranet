-- Fresh install only: builds an EMPTY database at the state after 002_AuthenticationSchema, then run 003..NNN in order
-- (database/migrations/README.md). Existing databases never run this file; they already have this schema.
-- Generated on 2026-10-22 from the reference database rolled back to 002 and checked against docs/database-catalog.md
-- (schema snapshot taken after 002). Includes the publication tables of tools/sql/001-002-publications.
-- Seeds only what later migrations require: the AuthenticationSettings singleton (as 002 does) and the ADMIN role (008).
-- No user is created: create the first administrator after 008 (see README).
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0 AND name<>'sysdiagrams')
    THROW 51000,'The database is not empty. The baseline is for fresh installs only; apply the numbered migrations instead.',1;
 IF SCHEMA_ID(N'App') IS NULL EXEC(N'CREATE SCHEMA [App]');
 IF SCHEMA_ID(N'Notify') IS NULL EXEC(N'CREATE SCHEMA [Notify]');
 IF SCHEMA_ID(N'Sec') IS NULL EXEC(N'CREATE SCHEMA [Sec]');
 EXEC(N'CREATE TABLE [App].[Application] (
  [ApplicationId] int IDENTITY(1,1) NOT NULL,
  [ApplicationName] nvarchar(150) NOT NULL,
  [ApplicationCode] varchar(50) NOT NULL,
  [BaseUrl] nvarchar(500) NULL,
  [Description] nvarchar(500) NULL,
  [Icon] nvarchar(200) NULL,
  [DisplayOrder] int NOT NULL CONSTRAINT [DF_Application_DisplayOrder] DEFAULT ((0)),
  [IsActive] bit NOT NULL CONSTRAINT [DF_Application_IsActive] DEFAULT ((1)),
  [CreatedAt] datetime NOT NULL CONSTRAINT [DF_Application_CreatedAt] DEFAULT (getutcdate()),
  CONSTRAINT [UQ_Application_ApplicationCode] UNIQUE NONCLUSTERED ([ApplicationCode]),
  CONSTRAINT [PK_Application] PRIMARY KEY CLUSTERED ([ApplicationId])
)');
 EXEC(N'CREATE TABLE [dbo].[SchemaMigration] (
  [MigrationId] varchar(150) NOT NULL,
  [AppliedAt] datetime2(7) NOT NULL CONSTRAINT [DF_SchemaMigration_AppliedAt] DEFAULT (sysutcdatetime()),
  CONSTRAINT [PK_SchemaMigration] PRIMARY KEY CLUSTERED ([MigrationId])
)');
 EXEC(N'CREATE TABLE [Notify].[Notification] (
  [NotificationId] bigint IDENTITY(1,1) NOT NULL,
  [Title] nvarchar(200) NOT NULL,
  [Message] nvarchar(max) NOT NULL,
  [NotificationType] varchar(50) NULL,
  [CreatedBy] bigint NULL,
  [CreatedAt] datetime NOT NULL CONSTRAINT [DF_Notification_CreatedAt] DEFAULT (getutcdate()),
  [ApplicationId] int NOT NULL,
  [TargetUrl] nvarchar(500) NULL,
  CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([NotificationId])
)');
 EXEC(N'CREATE TABLE [Notify].[Publication] (
  [PublicationId] bigint IDENTITY(1,1) NOT NULL,
  [Title] nvarchar(200) NOT NULL,
  [Summary] nvarchar(1000) NOT NULL,
  [Body] nvarchar(max) NOT NULL,
  [Kind] nvarchar(20) NOT NULL,
  [IsPublished] bit NOT NULL,
  [CreatedAt] datetime2(7) NOT NULL,
  [UpdatedAt] datetime2(7) NOT NULL,
  [PublishedAt] datetime2(7) NULL,
  [CreatedBy] bigint NOT NULL,
  [UpdatedBy] bigint NOT NULL,
  [Revision] bigint NOT NULL CONSTRAINT [DF_Publication_Revision] DEFAULT ((0)),
  CONSTRAINT [PK_Publication] PRIMARY KEY CLUSTERED ([PublicationId]),
  CONSTRAINT [CK_Publication_Kind] CHECK ([Kind]=''Circular'' OR [Kind]=''Announcement'' OR [Kind]=''News''),
  CONSTRAINT [CK_Publication_PublishState] CHECK ([IsPublished]=(0) AND [PublishedAt] IS NULL OR [IsPublished]=(1) AND [PublishedAt] IS NOT NULL)
)');
 EXEC(N'CREATE TABLE [Notify].[UserNotification] (
  [UserNotificationId] bigint IDENTITY(1,1) NOT NULL,
  [NotificationId] bigint NOT NULL,
  [UserId] bigint NOT NULL,
  [IsRead] bit NOT NULL CONSTRAINT [DF_UserNotification_IsRead] DEFAULT ((0)),
  [ReadAt] datetime NULL,
  [CreatedAt] datetime NOT NULL CONSTRAINT [DF_UserNotification_CreatedAt] DEFAULT (getutcdate()),
  CONSTRAINT [UQ_UserNotification_NotificationId_UserId] UNIQUE NONCLUSTERED ([NotificationId],[UserId]),
  CONSTRAINT [PK_UserNotification] PRIMARY KEY CLUSTERED ([UserNotificationId]),
  CONSTRAINT [CK_UserNotification_ReadState] CHECK ([IsRead]=(0) AND [ReadAt] IS NULL OR [IsRead]=(1) AND [ReadAt] IS NOT NULL)
)');
 EXEC(N'CREATE TABLE [Sec].[AuthenticationChallenge] (
  [Id] varchar(64) NOT NULL,
  [UserId] bigint NOT NULL,
  [Purpose] varchar(20) NOT NULL,
  [LoginMethod] varchar(20) NOT NULL,
  [SecurityStamp] varchar(32) NOT NULL,
  [SettingsRevision] uniqueidentifier NOT NULL,
  [ExpiresAt] datetime2(7) NOT NULL,
  [LastSentAt] datetime2(7) NULL,
  [CodeHash] varchar(64) NULL,
  [ProtectedSecret] nvarchar(2000) NULL,
  [Attempts] int NOT NULL,
  [Consumed] bit NOT NULL,
  [Version] uniqueidentifier NOT NULL,
  CONSTRAINT [PK_AuthenticationChallenge] PRIMARY KEY CLUSTERED ([Id]),
  CONSTRAINT [CK_AuthenticationChallenge_Attempts] CHECK ([Attempts]>=(0)),
  CONSTRAINT [CK_AuthenticationChallenge_LoginMethod] CHECK ([LoginMethod]=''ldap'' OR [LoginMethod]=''password''),
  CONSTRAINT [CK_AuthenticationChallenge_Purpose] CHECK ([Purpose]=''enroll'' OR [Purpose]=''reset'' OR [Purpose]=''login'')
)');
 EXEC(N'CREATE TABLE [Sec].[AuthenticationSettings] (
  [Id] int NOT NULL,
  [Json] nvarchar(max) NOT NULL,
  [ProtectedSmsApiKey] nvarchar(4000) NULL,
  [Revision] uniqueidentifier NOT NULL,
  CONSTRAINT [PK_AuthenticationSettings] PRIMARY KEY CLUSTERED ([Id]),
  CONSTRAINT [CK_AuthenticationSettings_Json] CHECK (isjson([Json])=(1) AND datalength([Json])<=(20000)),
  CONSTRAINT [CK_AuthenticationSettings_Singleton] CHECK ([Id]=(1))
)');
 EXEC(N'CREATE TABLE [Sec].[Permission] (
  [PermissionId] int IDENTITY(1,1) NOT NULL,
  [ApplicationId] int NOT NULL,
  [PermissionName] nvarchar(150) NOT NULL,
  [PermissionCode] varchar(100) NOT NULL,
  [Description] nvarchar(500) NULL,
  [IsActive] bit NOT NULL CONSTRAINT [DF_Permission_IsActive] DEFAULT ((1)),
  [CreatedAt] datetime NOT NULL CONSTRAINT [DF_Permission_CreatedAt] DEFAULT (getutcdate()),
  CONSTRAINT [UQ_Permission_ApplicationId_PermissionCode] UNIQUE NONCLUSTERED ([ApplicationId],[PermissionCode]),
  CONSTRAINT [PK_Permission] PRIMARY KEY CLUSTERED ([PermissionId])
)');
 EXEC(N'CREATE TABLE [Sec].[Role] (
  [RoleId] int IDENTITY(1,1) NOT NULL,
  [RoleName] nvarchar(100) NOT NULL,
  [RoleCode] varchar(50) NOT NULL,
  [IsActive] bit NOT NULL CONSTRAINT [DF_Role_IsActive] DEFAULT ((1)),
  [CreatedAt] datetime NOT NULL CONSTRAINT [DF_Role_CreatedAt] DEFAULT (getutcdate()),
  CONSTRAINT [UQ_Role_RoleCode] UNIQUE NONCLUSTERED ([RoleCode]),
  CONSTRAINT [PK_Role] PRIMARY KEY CLUSTERED ([RoleId])
)');
 EXEC(N'CREATE TABLE [Sec].[RolePermission] (
  [RolePermissionId] bigint IDENTITY(1,1) NOT NULL,
  [RoleId] int NOT NULL,
  [PermissionId] int NOT NULL,
  [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_RolePermission_CreatedAt] DEFAULT (getutcdate()),
  CONSTRAINT [PK_RolePermission] PRIMARY KEY CLUSTERED ([RolePermissionId])
)');
 EXEC(N'CREATE TABLE [Sec].[User] (
  [UserId] bigint IDENTITY(1,1) NOT NULL,
  [Username] nvarchar(100) NOT NULL,
  [NationalId] varchar(10) NULL,
  [Name] nvarchar(100) NOT NULL,
  [LastName] nvarchar(100) NOT NULL,
  [MobileNumber] varchar(20) NULL,
  [Email] nvarchar(200) NULL,
  [PasswordHash] nvarchar(500) NULL,
  [IsActive] bit NOT NULL CONSTRAINT [DF_User_IsActive] DEFAULT ((1)),
  [CreatedAt] datetime NOT NULL CONSTRAINT [DF_User_CreatedAt] DEFAULT (getutcdate()),
  [LastLogin] datetime NULL,
  [SecurityStamp] varchar(32) NOT NULL CONSTRAINT [DF_User_SecurityStamp] DEFAULT (replace(CONVERT([varchar](36),newid()),''-'','''')),
  [AuthenticatorSecret] nvarchar(2000) NULL,
  [LastAuthenticatorStep] bigint NOT NULL CONSTRAINT [DF_User_LastAuthenticatorStep] DEFAULT ((-1)),
  [FailedLoginAttempts] int NOT NULL CONSTRAINT [DF_User_FailedLoginAttempts] DEFAULT ((0)),
  [LockedUntil] datetime2(7) NULL,
  [LastSecuritySmsAt] datetime2(7) NULL,
  [AuthenticationVersion] uniqueidentifier NOT NULL CONSTRAINT [DF_User_AuthenticationVersion] DEFAULT (newid()),
  CONSTRAINT [UQ_User_Username] UNIQUE NONCLUSTERED ([Username]),
  CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([UserId]),
  CONSTRAINT [CK_User_AuthenticationCounters] CHECK ([FailedLoginAttempts]>=(0) AND [LastAuthenticatorStep]>=(-1))
)');
 EXEC(N'CREATE TABLE [Sec].[UserPermission] (
  [UserPermissionId] bigint IDENTITY(1,1) NOT NULL,
  [UserId] bigint NOT NULL,
  [PermissionId] int NOT NULL,
  [CreatedAt] datetime NOT NULL CONSTRAINT [DF_UserPermission_CreatedAt] DEFAULT (getutcdate()),
  CONSTRAINT [UQ_UserPermission_UserId_PermissionId] UNIQUE NONCLUSTERED ([UserId],[PermissionId]),
  CONSTRAINT [PK_UserPermission] PRIMARY KEY CLUSTERED ([UserPermissionId])
)');
 EXEC(N'CREATE TABLE [Sec].[UserRole] (
  [UserRoleId] bigint IDENTITY(1,1) NOT NULL,
  [UserId] bigint NOT NULL,
  [RoleId] int NOT NULL,
  [CreatedAt] datetime NOT NULL CONSTRAINT [DF_UserRole_CreatedAt] DEFAULT (getutcdate()),
  CONSTRAINT [UQ_UserRole_UserId_RoleId] UNIQUE NONCLUSTERED ([UserId],[RoleId]),
  CONSTRAINT [PK_UserRole] PRIMARY KEY CLUSTERED ([UserRoleId])
)');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_Application_DisplayOrder] ON [App].[Application] ([DisplayOrder])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_Notification_ApplicationId] ON [Notify].[Notification] ([ApplicationId])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_Notification_CreatedBy] ON [Notify].[Notification] ([CreatedBy])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_Publication_CreatedBy] ON [Notify].[Publication] ([CreatedBy])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_Publication_IsPublished_PublishedAt_PublicationId] ON [Notify].[Publication] ([IsPublished],[PublishedAt],[PublicationId])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_Publication_Kind] ON [Notify].[Publication] ([Kind])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_Publication_UpdatedBy] ON [Notify].[Publication] ([UpdatedBy])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_UserNotification_NotificationId] ON [Notify].[UserNotification] ([NotificationId])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_UserNotification_UserId] ON [Notify].[UserNotification] ([UserId])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_UserNotification_UserId_IsRead_CreatedAt] ON [Notify].[UserNotification] ([UserId],[IsRead],[CreatedAt])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_AuthenticationChallenge_ExpiresAt] ON [Sec].[AuthenticationChallenge] ([ExpiresAt])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_AuthenticationChallenge_UserId_Purpose] ON [Sec].[AuthenticationChallenge] ([UserId],[Purpose])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_Permission_ApplicationId] ON [Sec].[Permission] ([ApplicationId])');
 EXEC(N'CREATE UNIQUE NONCLUSTERED INDEX [UX_Permission_PermissionCode] ON [Sec].[Permission] ([PermissionCode])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_RolePermission_PermissionId] ON [Sec].[RolePermission] ([PermissionId])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_RolePermission_RoleId] ON [Sec].[RolePermission] ([RoleId])');
 EXEC(N'CREATE UNIQUE NONCLUSTERED INDEX [UX_RolePermission_RoleId_PermissionId] ON [Sec].[RolePermission] ([RoleId],[PermissionId])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_UserPermission_PermissionId] ON [Sec].[UserPermission] ([PermissionId])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_UserRole_RoleId] ON [Sec].[UserRole] ([RoleId])');
 EXEC(N'CREATE NONCLUSTERED INDEX [IX_UserRole_UserId] ON [Sec].[UserRole] ([UserId])');
 EXEC(N'ALTER TABLE [Notify].[Notification] WITH CHECK ADD CONSTRAINT [FK_Notification_Application] FOREIGN KEY ([ApplicationId]) REFERENCES [App].[Application] ([ApplicationId])');
 EXEC(N'ALTER TABLE [Notify].[Notification] WITH CHECK ADD CONSTRAINT [FK_Notification_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Sec].[User] ([UserId])');
 EXEC(N'ALTER TABLE [Notify].[Publication] WITH CHECK ADD CONSTRAINT [FK_Publication_User_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Sec].[User] ([UserId])');
 EXEC(N'ALTER TABLE [Notify].[Publication] WITH CHECK ADD CONSTRAINT [FK_Publication_User_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [Sec].[User] ([UserId])');
 EXEC(N'ALTER TABLE [Notify].[UserNotification] WITH CHECK ADD CONSTRAINT [FK_UserNotification_Notification] FOREIGN KEY ([NotificationId]) REFERENCES [Notify].[Notification] ([NotificationId])');
 EXEC(N'ALTER TABLE [Notify].[UserNotification] WITH CHECK ADD CONSTRAINT [FK_UserNotification_User] FOREIGN KEY ([UserId]) REFERENCES [Sec].[User] ([UserId])');
 EXEC(N'ALTER TABLE [Sec].[AuthenticationChallenge] WITH CHECK ADD CONSTRAINT [FK_AuthenticationChallenge_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [Sec].[User] ([UserId])');
 EXEC(N'ALTER TABLE [Sec].[Permission] WITH CHECK ADD CONSTRAINT [FK_Permission_Application] FOREIGN KEY ([ApplicationId]) REFERENCES [App].[Application] ([ApplicationId])');
 EXEC(N'ALTER TABLE [Sec].[RolePermission] WITH CHECK ADD CONSTRAINT [FK_RolePermission_Permission] FOREIGN KEY ([PermissionId]) REFERENCES [Sec].[Permission] ([PermissionId])');
 EXEC(N'ALTER TABLE [Sec].[RolePermission] WITH CHECK ADD CONSTRAINT [FK_RolePermission_Role] FOREIGN KEY ([RoleId]) REFERENCES [Sec].[Role] ([RoleId])');
 EXEC(N'ALTER TABLE [Sec].[UserPermission] WITH CHECK ADD CONSTRAINT [FK_UserPermission_Permission] FOREIGN KEY ([PermissionId]) REFERENCES [Sec].[Permission] ([PermissionId])');
 EXEC(N'ALTER TABLE [Sec].[UserPermission] WITH CHECK ADD CONSTRAINT [FK_UserPermission_User] FOREIGN KEY ([UserId]) REFERENCES [Sec].[User] ([UserId])');
 EXEC(N'ALTER TABLE [Sec].[UserRole] WITH CHECK ADD CONSTRAINT [FK_UserRole_Role] FOREIGN KEY ([RoleId]) REFERENCES [Sec].[Role] ([RoleId])');
 EXEC(N'ALTER TABLE [Sec].[UserRole] WITH CHECK ADD CONSTRAINT [FK_UserRole_User] FOREIGN KEY ([UserId]) REFERENCES [Sec].[User] ([UserId])');

 INSERT dbo.SchemaMigration(MigrationId) VALUES('001_DatabaseConsistency'),('002_AuthenticationSchema');
 -- Same defaults as AuthenticationSettingsDto: no unconfigured external provider is activated.
 INSERT Sec.AuthenticationSettings(Id,Json,Revision) VALUES(1,N'{}',NEWID());
 INSERT Sec.Role(RoleName,RoleCode,IsActive) VALUES(N'مدیر سامانه','ADMIN',1);
 COMMIT;
 SELECT 'Baseline installed (001, 002). Apply 003 and later migrations next.' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
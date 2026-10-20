SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='004_AccessWorkflow') THROW 51000,'Workflow migration is not installed.',1;
IF OBJECT_ID(N'App.ApplicationAccessRequest',N'U') IS NULL OR OBJECT_ID(N'App.ApplicationSupervisionGroup',N'U') IS NULL
 OR OBJECT_ID(N'App.ApplicationSupervisor',N'U') IS NULL OR OBJECT_ID(N'Notify.AccessRequestAlert',N'U') IS NULL
 OR COL_LENGTH(N'App.Application',N'AccessRequestNotificationChannels') IS NULL
 OR COL_LENGTH(N'App.ApplicationService',N'SyncCursor') IS NULL OR COL_LENGTH(N'Notify.Notification',N'ExternalId') IS NULL
 THROW 51000,'Workflow schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID(N'App.ApplicationAccessRequest'),OBJECT_ID(N'App.ApplicationSupervisionGroup'),OBJECT_ID(N'App.ApplicationSupervisor'),OBJECT_ID(N'Notify.AccessRequestAlert')) AND (is_disabled=1 OR is_not_trusted=1))
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id IN(OBJECT_ID(N'App.Application'),OBJECT_ID(N'App.ApplicationAccessRequest'),OBJECT_ID(N'App.ApplicationSupervisor'),OBJECT_ID(N'Notify.AccessRequestAlert'),OBJECT_ID(N'Notify.Notification')) AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51000,'Workflow constraints are disabled or untrusted.',1;
SELECT 'Workflow schema verified' AS VerificationStatus;

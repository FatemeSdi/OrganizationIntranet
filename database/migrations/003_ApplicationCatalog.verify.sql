SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='003_ApplicationCatalog') THROW 51000,'Application catalog migration is not installed.',1;
IF OBJECT_ID(N'App.ApplicationAccess',N'U') IS NULL OR OBJECT_ID(N'App.ApplicationService',N'U') IS NULL
 OR COL_LENGTH(N'App.Application',N'RequiresLogin') IS NULL OR COL_LENGTH(N'App.Application',N'IsInternetAccessible') IS NULL OR COL_LENGTH(N'App.Application',N'IsPublic') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_ApplicationAccess_Principal' AND parent_object_id=OBJECT_ID(N'App.ApplicationAccess'))
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'App.ApplicationAccess') AND delete_referential_action=0) <> 3
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ApplicationService_Application_ApplicationId' AND delete_referential_action=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ApplicationAccess_ApplicationId_UserId' AND is_unique=1 AND has_filter=1 AND object_id=OBJECT_ID(N'App.ApplicationAccess'))
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ApplicationAccess_ApplicationId_RoleId' AND is_unique=1 AND has_filter=1 AND object_id=OBJECT_ID(N'App.ApplicationAccess'))
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ApplicationService_ApplicationId_ServiceCode' AND is_unique=1 AND object_id=OBJECT_ID(N'App.ApplicationService'))
 THROW 51000,'Application catalog schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID(N'App.ApplicationAccess'),OBJECT_ID(N'App.ApplicationService')) AND (is_disabled=1 OR is_not_trusted=1))
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'App.ApplicationAccess') AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51000,'Application catalog constraints are disabled or untrusted.',1;
SELECT 'Application catalog schema verified' AS VerificationStatus;

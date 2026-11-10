SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='014_ApplicationUnitAccess') THROW 51000,'Unit access migration is not installed.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'App.Application') AND name='IsUnitRestricted' AND is_nullable=0)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Application_AccessMode' AND parent_object_id=OBJECT_ID(N'App.Application') AND is_disabled=0 AND is_not_trusted=0)
 OR OBJECT_ID(N'App.ApplicationUnitAccess',N'U') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_ApplicationUnitAccess_Application_Unit' AND is_unique=1 AND object_id=OBJECT_ID(N'App.ApplicationUnitAccess'))
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ApplicationUnitAccess_UnitId' AND object_id=OBJECT_ID(N'App.ApplicationUnitAccess'))
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'App.ApplicationUnitAccess') AND delete_referential_action=0 AND is_disabled=0 AND is_not_trusted=0) <> 2
 THROW 51000,'Unit access schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM App.Application WHERE IsPublic=1 AND IsUnitRestricted=1) THROW 51000,'An application is both public and unit-restricted.',1;
SELECT 'Unit access schema verified' AS VerificationStatus;

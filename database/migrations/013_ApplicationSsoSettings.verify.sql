SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='013_ApplicationSsoSettings') THROW 51000,'SSO settings migration is not installed.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'App.Application') AND is_nullable=1
      AND ((name='SsoPath' AND max_length=400) OR (name='SsoKeyHash' AND max_length=32 AND system_type_id=173) OR (name='SsoKeyChangedAt'))) <> 3
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_Application_SsoKeyHash' AND is_unique=1 AND has_filter=1 AND object_id=OBJECT_ID(N'App.Application'))
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Application_SsoPath' AND parent_object_id=OBJECT_ID(N'App.Application') AND is_disabled=0 AND is_not_trusted=0)
 THROW 51000,'SSO settings schema is incomplete.',1;
SELECT 'SSO settings schema verified' AS VerificationStatus;

SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='011_UserApplicationAccounts') THROW 51000,'User application account migration is not installed.',1;
IF OBJECT_ID(N'App.UserApplicationAccount',N'U') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_UserApplicationAccount_User_Application' AND is_unique=1 AND object_id=OBJECT_ID(N'App.UserApplicationAccount'))
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_UserApplicationAccount_Application_Name' AND is_unique=1 AND object_id=OBJECT_ID(N'App.UserApplicationAccount'))
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'App.UserApplicationAccount') AND delete_referential_action=0) <> 3
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_UserApplicationAccount_Name' AND parent_object_id=OBJECT_ID(N'App.UserApplicationAccount'))
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'App.UserApplicationAccount') AND name='ExternalUsername' AND is_nullable=0 AND max_length=200)
 THROW 51000,'User application account schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'App.UserApplicationAccount') AND (is_disabled=1 OR is_not_trusted=1))
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'App.UserApplicationAccount') AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51000,'User application account constraints are disabled or untrusted.',1;
SELECT 'User application account schema verified' AS VerificationStatus;

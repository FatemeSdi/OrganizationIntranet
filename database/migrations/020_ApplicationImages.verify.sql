SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='020_ApplicationImages') THROW 51000,'Application images migration is not installed.',1;
IF OBJECT_ID(N'App.ApplicationImage',N'U') IS NULL OR COL_LENGTH(N'App.Application',N'ImageUpdatedAt') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ApplicationImage_Application_ApplicationId' AND delete_referential_action=0)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_ApplicationImage_ContentType' AND parent_object_id=OBJECT_ID(N'App.ApplicationImage'))
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_ApplicationImage_Size' AND parent_object_id=OBJECT_ID(N'App.ApplicationImage'))
 THROW 51000,'Application image schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name IN ('CK_ApplicationImage_ContentType','CK_ApplicationImage_Size') AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51000,'Application image constraints are disabled or untrusted.',1;
-- The flag on Application and the image row must agree.
IF EXISTS(SELECT 1 FROM App.Application a LEFT JOIN App.ApplicationImage i ON i.ApplicationId=a.ApplicationId
          WHERE (a.ImageUpdatedAt IS NULL AND i.ApplicationId IS NOT NULL) OR (a.ImageUpdatedAt IS NOT NULL AND i.ApplicationId IS NULL))
 THROW 51000,'Application.ImageUpdatedAt and ApplicationImage disagree.',1;
SELECT 'Application images schema verified' AS VerificationStatus;

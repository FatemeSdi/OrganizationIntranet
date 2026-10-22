SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='007_PublicationImages') THROW 51000,'Publication images migration is not installed.',1;
IF OBJECT_ID(N'Notify.PublicationImage',N'U') IS NULL OR COL_LENGTH(N'Notify.Publication',N'ImageUpdatedAt') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_PublicationImage_Publication_PublicationId' AND delete_referential_action=0)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_PublicationImage_ContentType' AND parent_object_id=OBJECT_ID(N'Notify.PublicationImage'))
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_PublicationImage_Size' AND parent_object_id=OBJECT_ID(N'Notify.PublicationImage'))
 THROW 51000,'Publication image schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name IN ('CK_PublicationImage_ContentType','CK_PublicationImage_Size') AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51000,'Publication image constraints are disabled or untrusted.',1;
-- The flag on Publication and the image row must agree.
IF EXISTS(SELECT 1 FROM Notify.Publication p LEFT JOIN Notify.PublicationImage i ON i.PublicationId=p.PublicationId
          WHERE (p.ImageUpdatedAt IS NULL AND i.PublicationId IS NOT NULL) OR (p.ImageUpdatedAt IS NOT NULL AND i.PublicationId IS NULL))
 THROW 51000,'Publication.ImageUpdatedAt and PublicationImage disagree.',1;
SELECT 'Publication images schema verified' AS VerificationStatus;

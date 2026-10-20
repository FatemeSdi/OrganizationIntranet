SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='005_PublicPublications') THROW 51000,'Public publications migration is not installed.',1;
IF COL_LENGTH(N'Notify.Publication',N'IsPublic') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Publication_PublicRequiresPublished' AND parent_object_id=OBJECT_ID(N'Notify.Publication'))
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Publication_IsPublic_PublishedAt' AND object_id=OBJECT_ID(N'Notify.Publication'))
 THROW 51000,'Public publication schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Publication_PublicRequiresPublished' AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51000,'Public publication constraint is disabled or untrusted.',1;
IF EXISTS(SELECT 1 FROM Notify.Publication WHERE IsPublic=1 AND IsPublished=0) THROW 51000,'Unpublished content is marked public.',1;
SELECT 'Public publications schema verified' AS VerificationStatus;

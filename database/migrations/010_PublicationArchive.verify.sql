SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='010_PublicationArchive') THROW 51000,'Publication archive migration is not installed.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'Notify.Publication') AND name='IsArchived' AND is_nullable=0)
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'Notify.Publication') AND name='ArchivedAt' AND is_nullable=1)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Publication_ArchivedNotPublished' AND parent_object_id=OBJECT_ID(N'Notify.Publication'))
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Publication_IsArchived' AND object_id=OBJECT_ID(N'Notify.Publication'))
 THROW 51000,'Publication archive schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'Notify.Publication') AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51000,'Publication constraints are disabled or untrusted.',1;
IF EXISTS(SELECT 1 FROM Notify.Publication WHERE IsArchived=1 AND IsPublished=1) THROW 51000,'An archived publication is still published.',1;
SELECT 'Publication archive schema verified' AS VerificationStatus;

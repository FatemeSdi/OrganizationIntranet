SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='016_PublicationSlider') THROW 51000,'Publication slider migration is not installed.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'Notify.Publication') AND name IN ('ShowInSlider','SlideOrder') AND is_nullable=0) <> 2
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Publication_SlideOrder' AND parent_object_id=OBJECT_ID(N'Notify.Publication') AND is_disabled=0 AND is_not_trusted=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Publication_Slider' AND has_filter=1 AND object_id=OBJECT_ID(N'Notify.Publication'))
 THROW 51000,'Publication slider schema is incomplete.',1;
SELECT 'Publication slider schema verified' AS VerificationStatus;

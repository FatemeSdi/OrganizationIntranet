-- Additive: marks published content as visible to anonymous visitors. Nothing is published or made public by this script (default 0).
-- Requires 004_AccessWorkflow so the migration history stays linear.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF OBJECT_ID(N'Notify.Publication',N'U') IS NULL OR OBJECT_ID(N'dbo.SchemaMigration',N'U') IS NULL
    THROW 51000,'Apply the publications and baseline migrations first.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='004_AccessWorkflow')
    THROW 51000,'Apply 003_ApplicationCatalog and 004_AccessWorkflow first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='005_PublicPublications')
 BEGIN
   COMMIT;
   SELECT 'Already applied; run verification.' AS MigrationStatus;
   RETURN;
 END;
 -- Fail closed on a partial or independently installed change.
 IF COL_LENGTH(N'Notify.Publication',N'IsPublic') IS NOT NULL
    OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Publication_PublicRequiresPublished')
    OR EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Publication_IsPublic_PublishedAt' AND object_id=OBJECT_ID(N'Notify.Publication'))
    THROW 51000,'Public publication schema already exists in part. Reconcile before applying.',1;

 ALTER TABLE Notify.Publication ADD IsPublic bit NOT NULL CONSTRAINT DF_Publication_IsPublic DEFAULT(0) WITH VALUES;
 EXEC(N'ALTER TABLE Notify.Publication WITH CHECK ADD CONSTRAINT CK_Publication_PublicRequiresPublished CHECK ([IsPublic]=0 OR [IsPublished]=1);');
 EXEC(N'CREATE INDEX IX_Publication_IsPublic_PublishedAt ON Notify.Publication(IsPublic,PublishedAt,PublicationId);');

 INSERT dbo.SchemaMigration(MigrationId) VALUES('005_PublicPublications');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

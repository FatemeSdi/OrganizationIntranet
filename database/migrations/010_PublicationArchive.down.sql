-- Rollback: stop the API first. Archived publications are kept and simply become ordinary drafts again (they were already unpublished);
-- only the archive flag and its date are lost.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='010_PublicationArchive') BEGIN COMMIT; RETURN; END;
 DROP INDEX IX_Publication_IsArchived ON Notify.Publication;
 ALTER TABLE Notify.Publication DROP CONSTRAINT CK_Publication_ArchivedNotPublished;
 ALTER TABLE Notify.Publication DROP CONSTRAINT DF_Publication_IsArchived;
 ALTER TABLE Notify.Publication DROP COLUMN ArchivedAt, IsArchived;
 DELETE dbo.SchemaMigration WHERE MigrationId='010_PublicationArchive';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

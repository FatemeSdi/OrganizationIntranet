-- Guarded rollback: refuses to run while any content is public, because dropping the column would silently
-- change who can see it. Un-publish or un-mark the public items first, then stop the new API and run this.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='005_PublicPublications')
 BEGIN COMMIT; RETURN; END;
 IF EXISTS(SELECT 1 FROM Notify.Publication WHERE IsPublic=1)
   THROW 51000,'Some publications are public. Clear IsPublic deliberately before rolling back.',1;
 DROP INDEX IX_Publication_IsPublic_PublishedAt ON Notify.Publication;
 ALTER TABLE Notify.Publication DROP CONSTRAINT CK_Publication_PublicRequiresPublished;
 ALTER TABLE Notify.Publication DROP CONSTRAINT DF_Publication_IsPublic;
 ALTER TABLE Notify.Publication DROP COLUMN IsPublic;
 DELETE dbo.SchemaMigration WHERE MigrationId='005_PublicPublications';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

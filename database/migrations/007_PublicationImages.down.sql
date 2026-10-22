-- Guarded rollback: refuses to run while any publication still has an image, because dropping the table would silently delete them.
-- Remove the images in Admin (or back them up) first, then stop the new API and run this.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='007_PublicationImages')
 BEGIN COMMIT; RETURN; END;
 IF EXISTS(SELECT 1 FROM Notify.PublicationImage)
   THROW 51000,'Some publications still have images. Remove or back them up deliberately before rolling back.',1;
 DROP TABLE Notify.PublicationImage;
 ALTER TABLE Notify.Publication DROP COLUMN ImageUpdatedAt;
 DELETE dbo.SchemaMigration WHERE MigrationId='007_PublicationImages';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

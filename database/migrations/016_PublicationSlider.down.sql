-- Rollback: stop the API first. The slider choices (which items are shown and in which order) are lost; the old code shows the newest published items.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='016_PublicationSlider') BEGIN COMMIT; RETURN; END;
 DROP INDEX IX_Publication_Slider ON Notify.Publication;
 ALTER TABLE Notify.Publication DROP CONSTRAINT CK_Publication_SlideOrder;
 ALTER TABLE Notify.Publication DROP CONSTRAINT DF_Publication_SlideOrder;
 ALTER TABLE Notify.Publication DROP CONSTRAINT DF_Publication_ShowInSlider;
 ALTER TABLE Notify.Publication DROP COLUMN ShowInSlider, SlideOrder;
 DELETE dbo.SchemaMigration WHERE MigrationId='016_PublicationSlider';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

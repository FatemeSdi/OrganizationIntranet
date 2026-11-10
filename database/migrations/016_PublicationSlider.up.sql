-- Additive: control of the home-page news slider. Publication.ShowInSlider (default 1) decides whether a published item may appear in the slider and
-- Publication.SlideOrder (default 0, smaller first; ties keep the newest-first order) fixes its position. Existing items keep appearing exactly as before
-- (all in the slider, same order) because every row gets ShowInSlider = 1 and SlideOrder = 0.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='015_AdminNotifications')
    THROW 51000,'Apply 015_AdminNotifications first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='016_PublicationSlider')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 IF COL_LENGTH(N'Notify.Publication',N'ShowInSlider') IS NOT NULL OR COL_LENGTH(N'Notify.Publication',N'SlideOrder') IS NOT NULL
    THROW 51000,'Slider columns already exist in part. Reconcile before applying.',1;

 ALTER TABLE Notify.Publication ADD
   ShowInSlider bit NOT NULL CONSTRAINT DF_Publication_ShowInSlider DEFAULT(1),
   SlideOrder int NOT NULL CONSTRAINT DF_Publication_SlideOrder DEFAULT(0);
 EXEC(N'ALTER TABLE Notify.Publication ADD CONSTRAINT CK_Publication_SlideOrder CHECK(SlideOrder BETWEEN 0 AND 10000)');
 EXEC(N'CREATE INDEX IX_Publication_Slider ON Notify.Publication(ShowInSlider, SlideOrder, PublishedAt DESC) WHERE IsPublished = 1 AND IsArchived = 0');

 INSERT dbo.SchemaMigration(MigrationId) VALUES('016_PublicationSlider');
 COMMIT;
 SELECT 'Applied 016_PublicationSlider' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

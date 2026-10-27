-- Additive: soft delete ("archive") for publications. Archived rows stay in the table (restorable) but are never shown on the Portal
-- and are hidden from the default admin list. Archiving also unpublishes, enforced by CK_Publication_ArchivedNotPublished.
-- Existing rows are not archived. Requires 009_SsoTickets so the migration history stays linear.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='009_SsoTickets')
    THROW 51000,'Apply 009_SsoTickets first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='010_PublicationArchive')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 IF COL_LENGTH(N'Notify.Publication',N'IsArchived') IS NOT NULL OR COL_LENGTH(N'Notify.Publication',N'ArchivedAt') IS NOT NULL
    THROW 51000,'Publication archive schema already exists in part. Reconcile before applying.',1;

 ALTER TABLE Notify.Publication ADD IsArchived bit NOT NULL CONSTRAINT DF_Publication_IsArchived DEFAULT(0) WITH VALUES;
 ALTER TABLE Notify.Publication ADD ArchivedAt datetime2(7) NULL;
 EXEC(N'ALTER TABLE Notify.Publication WITH CHECK ADD CONSTRAINT CK_Publication_ArchivedNotPublished CHECK ([IsArchived]=0 OR [IsPublished]=0);');
 EXEC(N'CREATE INDEX IX_Publication_IsArchived ON Notify.Publication(IsArchived,PublishedAt,PublicationId);');

 INSERT dbo.SchemaMigration(MigrationId) VALUES('010_PublicationArchive');
 COMMIT;
 SELECT 'Applied 010_PublicationArchive' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

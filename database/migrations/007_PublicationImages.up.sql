-- Additive: feature image for publications (news carousel on the Portal). Existing rows get no image (ImageUpdatedAt NULL).
-- Requires 006_NationalIdLogin so the migration history stays linear.
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
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='006_NationalIdLogin')
    THROW 51000,'Apply 006_NationalIdLogin first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='007_PublicationImages')
 BEGIN
   COMMIT;
   SELECT 'Already applied; run verification.' AS MigrationStatus;
   RETURN;
 END;
 -- Fail closed on a partial or independently installed change.
 IF OBJECT_ID(N'Notify.PublicationImage',N'U') IS NOT NULL OR COL_LENGTH(N'Notify.Publication',N'ImageUpdatedAt') IS NOT NULL
    THROW 51000,'Publication image schema already exists in part. Reconcile before applying.',1;

 ALTER TABLE Notify.Publication ADD ImageUpdatedAt datetime2 NULL;
 CREATE TABLE Notify.PublicationImage(
   PublicationId bigint NOT NULL CONSTRAINT PK_PublicationImage PRIMARY KEY,
   ContentType varchar(20) NOT NULL,
   Data varbinary(max) NOT NULL,
   UpdatedAt datetime2 NOT NULL,
   CONSTRAINT FK_PublicationImage_Publication_PublicationId FOREIGN KEY(PublicationId) REFERENCES Notify.Publication(PublicationId),
   CONSTRAINT CK_PublicationImage_ContentType CHECK(ContentType IN ('image/jpeg','image/png','image/webp')),
   CONSTRAINT CK_PublicationImage_Size CHECK(DATALENGTH(Data) BETWEEN 1 AND 2097152)
 );

 INSERT dbo.SchemaMigration(MigrationId) VALUES('007_PublicationImages');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

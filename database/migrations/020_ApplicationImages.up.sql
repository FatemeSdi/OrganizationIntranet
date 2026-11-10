-- Additive: card image of an application (shown on the application card in the Portal). Existing applications get no image (ImageUpdatedAt NULL).
-- Same design as 007_PublicationImages: the bytes live in a separate table so application lists never load them.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF OBJECT_ID(N'App.Application',N'U') IS NULL OR OBJECT_ID(N'dbo.SchemaMigration',N'U') IS NULL
    THROW 51000,'Apply the baseline migrations first.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='019_HideHomeApplication')
    THROW 51000,'Apply 019_HideHomeApplication first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='020_ApplicationImages')
 BEGIN
   COMMIT;
   SELECT 'Already applied; run verification.' AS MigrationStatus;
   RETURN;
 END;
 -- Fail closed on a partial or independently installed change.
 IF OBJECT_ID(N'App.ApplicationImage',N'U') IS NOT NULL OR COL_LENGTH(N'App.Application',N'ImageUpdatedAt') IS NOT NULL
    THROW 51000,'Application image schema already exists in part. Reconcile before applying.',1;

 ALTER TABLE App.Application ADD ImageUpdatedAt datetime2 NULL;
 EXEC(N'CREATE TABLE App.ApplicationImage(
   ApplicationId int NOT NULL CONSTRAINT PK_ApplicationImage PRIMARY KEY,
   ContentType varchar(20) NOT NULL,
   Data varbinary(max) NOT NULL,
   UpdatedAt datetime2 NOT NULL,
   CONSTRAINT FK_ApplicationImage_Application_ApplicationId FOREIGN KEY(ApplicationId) REFERENCES App.Application(ApplicationId),
   CONSTRAINT CK_ApplicationImage_ContentType CHECK(ContentType IN (''image/jpeg'',''image/png'',''image/webp'')),
   CONSTRAINT CK_ApplicationImage_Size CHECK(DATALENGTH(Data) BETWEEN 1 AND 2097152)
 )');

 INSERT dbo.SchemaMigration(MigrationId) VALUES('020_ApplicationImages');
 COMMIT;
 SELECT 'Applied 020_ApplicationImages' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

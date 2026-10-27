-- Additive: maps an intranet user to the username of that user's account in an application (docs/sso.md). Single sign-on hands the
-- application this name, so the application needs no change and no table of its own. Without a row the intranet username is used.
-- An application account can belong to only one intranet user. Requires 010_PublicationArchive so the migration history stays linear.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='010_PublicationArchive')
    THROW 51000,'Apply 010_PublicationArchive first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='011_UserApplicationAccounts')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 IF OBJECT_ID(N'App.UserApplicationAccount',N'U') IS NOT NULL
    THROW 51000,'User application account schema already exists in part. Reconcile before applying.',1;

 CREATE TABLE App.UserApplicationAccount (
   UserApplicationAccountId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserApplicationAccount PRIMARY KEY,
   UserId bigint NOT NULL,
   ApplicationId int NOT NULL,
   ExternalUsername nvarchar(100) NOT NULL,
   CreatedAt datetime2(7) NOT NULL,
   UpdatedAt datetime2(7) NOT NULL,
   UpdatedBy bigint NOT NULL,
   CONSTRAINT CK_UserApplicationAccount_Name CHECK(LEN(LTRIM(RTRIM(ExternalUsername))) > 0),
   CONSTRAINT FK_UserApplicationAccount_User_UserId FOREIGN KEY(UserId) REFERENCES Sec.[User](UserId),
   CONSTRAINT FK_UserApplicationAccount_Application_ApplicationId FOREIGN KEY(ApplicationId) REFERENCES App.Application(ApplicationId),
   CONSTRAINT FK_UserApplicationAccount_User_UpdatedBy FOREIGN KEY(UpdatedBy) REFERENCES Sec.[User](UserId)
 );
 CREATE UNIQUE INDEX UX_UserApplicationAccount_User_Application ON App.UserApplicationAccount(UserId,ApplicationId);
 CREATE UNIQUE INDEX UX_UserApplicationAccount_Application_Name ON App.UserApplicationAccount(ApplicationId,ExternalUsername);
 CREATE INDEX IX_UserApplicationAccount_UpdatedBy ON App.UserApplicationAccount(UpdatedBy);

 INSERT dbo.SchemaMigration(MigrationId) VALUES('011_UserApplicationAccounts');
 COMMIT;
 SELECT 'Applied 011_UserApplicationAccounts' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

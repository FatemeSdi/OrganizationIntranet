-- Application catalog: login/network/public flags on App.Application, direct user/role access and service registrations.
-- Schema only: no application, URL, access grant or service is seeded; existing applications stay private and require login.
-- Requires 002_AuthenticationSchema so the migration history stays linear.
-- Reconstructed on 2026-10-22 from the database that recorded 003 (the original files were never committed); verified against
-- docs/database-catalog.md (schema after 002) and the live schema after 008 by Database/FreshInstallTests.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF OBJECT_ID(N'dbo.SchemaMigration',N'U') IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='002_AuthenticationSchema')
    THROW 51000,'Apply 002_AuthenticationSchema first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='003_ApplicationCatalog')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 -- Fail closed on a partial or independently installed catalog.
 IF OBJECT_ID(N'App.ApplicationAccess',N'U') IS NOT NULL OR OBJECT_ID(N'App.ApplicationService',N'U') IS NOT NULL
    OR COL_LENGTH(N'App.Application',N'RequiresLogin') IS NOT NULL OR COL_LENGTH(N'App.Application',N'IsInternetAccessible') IS NOT NULL
    OR COL_LENGTH(N'App.Application',N'IsPublic') IS NOT NULL
    THROW 51000,'Application catalog schema already exists in part. Reconcile before applying.',1;

 ALTER TABLE App.Application ADD
   RequiresLogin bit NOT NULL CONSTRAINT DF_Application_RequiresLogin DEFAULT(1) WITH VALUES,
   IsInternetAccessible bit NOT NULL CONSTRAINT DF_Application_IsInternetAccessible DEFAULT(0) WITH VALUES,
   IsPublic bit NOT NULL CONSTRAINT DF_Application_IsPublic DEFAULT(0) WITH VALUES;

 CREATE TABLE App.ApplicationAccess (
   ApplicationAccessId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationAccess PRIMARY KEY,
   ApplicationId int NOT NULL, UserId bigint NULL, RoleId int NULL,
   CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_ApplicationAccess_CreatedAt DEFAULT(GETUTCDATE()),
   CONSTRAINT CK_ApplicationAccess_Principal CHECK((UserId IS NOT NULL AND RoleId IS NULL) OR (UserId IS NULL AND RoleId IS NOT NULL)),
   CONSTRAINT FK_ApplicationAccess_Application_ApplicationId FOREIGN KEY(ApplicationId) REFERENCES App.Application(ApplicationId),
   CONSTRAINT FK_ApplicationAccess_User_UserId FOREIGN KEY(UserId) REFERENCES Sec.[User](UserId),
   CONSTRAINT FK_ApplicationAccess_Role_RoleId FOREIGN KEY(RoleId) REFERENCES Sec.Role(RoleId)
 );
 CREATE UNIQUE INDEX IX_ApplicationAccess_ApplicationId_UserId ON App.ApplicationAccess(ApplicationId,UserId) WHERE UserId IS NOT NULL;
 CREATE UNIQUE INDEX IX_ApplicationAccess_ApplicationId_RoleId ON App.ApplicationAccess(ApplicationId,RoleId) WHERE RoleId IS NOT NULL;
 CREATE INDEX IX_ApplicationAccess_UserId ON App.ApplicationAccess(UserId);
 CREATE INDEX IX_ApplicationAccess_RoleId ON App.ApplicationAccess(RoleId);

 CREATE TABLE App.ApplicationService (
   ApplicationServiceRegistrationId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationService PRIMARY KEY,
   ApplicationId int NOT NULL, ServiceCode varchar(50) NOT NULL, DisplayName nvarchar(150) NOT NULL, EndpointUrl nvarchar(500) NULL,
   IsActive bit NOT NULL CONSTRAINT DF_ApplicationService_IsActive DEFAULT(1),
   CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_ApplicationService_CreatedAt DEFAULT(GETUTCDATE()),
   CONSTRAINT FK_ApplicationService_Application_ApplicationId FOREIGN KEY(ApplicationId) REFERENCES App.Application(ApplicationId)
 );
 CREATE UNIQUE INDEX IX_ApplicationService_ApplicationId_ServiceCode ON App.ApplicationService(ApplicationId,ServiceCode);

 INSERT dbo.SchemaMigration(MigrationId) VALUES('003_ApplicationCatalog');
 COMMIT;
 SELECT 'Applied 003_ApplicationCatalog' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

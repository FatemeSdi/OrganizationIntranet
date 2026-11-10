-- Additive: third access mode for applications, based on the organizational chart (012).
-- App.Application.IsUnitRestricted = 1 means the application is available automatically to the users of the selected units (App.ApplicationUnitAccess) and
-- of all their sub-units; other users do not see it and cannot request it. Explicit user/role grants still work as exceptions. The mode is exclusive with IsPublic.
-- Existing applications keep their current behaviour (IsUnitRestricted = 0).
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='013_ApplicationSsoSettings')
    THROW 51000,'Apply 013_ApplicationSsoSettings first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='014_ApplicationUnitAccess')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 IF COL_LENGTH(N'App.Application',N'IsUnitRestricted') IS NOT NULL OR OBJECT_ID(N'App.ApplicationUnitAccess',N'U') IS NOT NULL
    THROW 51000,'Unit access schema already exists in part. Reconcile before applying.',1;

 ALTER TABLE App.Application ADD IsUnitRestricted bit NOT NULL CONSTRAINT DF_Application_IsUnitRestricted DEFAULT(0);
 EXEC(N'ALTER TABLE App.Application ADD CONSTRAINT CK_Application_AccessMode CHECK(NOT (IsPublic = 1 AND IsUnitRestricted = 1))');

 CREATE TABLE App.ApplicationUnitAccess (
   ApplicationUnitAccessId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationUnitAccess PRIMARY KEY,
   ApplicationId int NOT NULL,
   UnitId int NOT NULL,
   CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_ApplicationUnitAccess_CreatedAt DEFAULT(SYSUTCDATETIME()),
   CONSTRAINT FK_ApplicationUnitAccess_Application FOREIGN KEY(ApplicationId) REFERENCES App.Application(ApplicationId),
   CONSTRAINT FK_ApplicationUnitAccess_Unit FOREIGN KEY(UnitId) REFERENCES Org.Unit(UnitId)
 );
 CREATE UNIQUE INDEX UX_ApplicationUnitAccess_Application_Unit ON App.ApplicationUnitAccess(ApplicationId, UnitId);
 CREATE INDEX IX_ApplicationUnitAccess_UnitId ON App.ApplicationUnitAccess(UnitId);

 INSERT dbo.SchemaMigration(MigrationId) VALUES('014_ApplicationUnitAccess');
 COMMIT;
 SELECT 'Applied 014_ApplicationUnitAccess' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

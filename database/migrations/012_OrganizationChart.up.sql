-- Additive: organizational chart. Org.Unit is a tree (ParentId) of organizational units; Sec.User gets an optional UnitId.
-- It is the base for visibility of applications by unit and for sending notifications to a unit (later steps).
-- Also registers the two permissions ORG_VIEW and ORG_EDIT (shared/Contracts/AdminPermissions.cs) and grants them to the ADMIN role.
-- Existing rows are untouched: every user starts without a unit. Requires 011_UserApplicationAccounts so the migration history stays linear.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='011_UserApplicationAccounts')
    THROW 51000,'Apply 011_UserApplicationAccounts first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='012_OrganizationChart')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 IF OBJECT_ID(N'Org.Unit',N'U') IS NOT NULL OR COL_LENGTH(N'Sec.User',N'UnitId') IS NOT NULL
    THROW 51000,'Organization chart schema already exists in part. Reconcile before applying.',1;
 DECLARE @adminRole int = (SELECT RoleId FROM Sec.Role WHERE RoleCode='ADMIN' AND IsActive=1);
 IF @adminRole IS NULL THROW 51000,'Role ADMIN does not exist or is inactive.',1;
 DECLARE @app int = (SELECT ApplicationId FROM App.Application WHERE ApplicationCode='INTERANET');
 IF @app IS NULL THROW 51000,'Application INTERANET does not exist (apply 008_AdminPermissions).',1;
 IF EXISTS(SELECT 1 FROM Sec.Permission WHERE PermissionCode IN ('ORG_VIEW','ORG_EDIT') AND ApplicationId<>@app)
    THROW 51000,'An organization permission code already exists for another application. Reconcile before applying.',1;

 IF SCHEMA_ID(N'Org') IS NULL EXEC(N'CREATE SCHEMA Org');

 CREATE TABLE Org.Unit (
   UnitId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrgUnit PRIMARY KEY,
   ParentId int NULL,
   Title nvarchar(200) NOT NULL,
   FullPath nvarchar(1000) NOT NULL,
   DisplayOrder int NOT NULL CONSTRAINT DF_OrgUnit_DisplayOrder DEFAULT(0),
   IsActive bit NOT NULL CONSTRAINT DF_OrgUnit_IsActive DEFAULT(1),
   SourceId bigint NULL,
   CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_OrgUnit_CreatedAt DEFAULT(SYSUTCDATETIME()),
   UpdatedAt datetime2(7) NOT NULL CONSTRAINT DF_OrgUnit_UpdatedAt DEFAULT(SYSUTCDATETIME()),
   UpdatedBy bigint NULL,
   CONSTRAINT CK_OrgUnit_Title CHECK(LEN(LTRIM(RTRIM(Title))) > 0),
   CONSTRAINT CK_OrgUnit_NotOwnParent CHECK(ParentId IS NULL OR ParentId <> UnitId),
   CONSTRAINT FK_OrgUnit_Parent FOREIGN KEY(ParentId) REFERENCES Org.Unit(UnitId),
   CONSTRAINT FK_OrgUnit_UpdatedBy FOREIGN KEY(UpdatedBy) REFERENCES Sec.[User](UserId)
 );
 CREATE UNIQUE INDEX UX_OrgUnit_Parent_Title ON Org.Unit(ParentId,Title);
 CREATE UNIQUE INDEX UX_OrgUnit_SourceId ON Org.Unit(SourceId) WHERE SourceId IS NOT NULL;
 CREATE INDEX IX_OrgUnit_UpdatedBy ON Org.Unit(UpdatedBy);

 ALTER TABLE Sec.[User] ADD UnitId int NULL;
 ALTER TABLE Sec.[User] ADD CONSTRAINT FK_User_OrgUnit FOREIGN KEY(UnitId) REFERENCES Org.Unit(UnitId);
 CREATE INDEX IX_User_UnitId ON Sec.[User](UnitId);

 DECLARE @p TABLE(Code varchar(100) PRIMARY KEY, Name nvarchar(150) NOT NULL);
 INSERT @p(Code, Name) VALUES ('ORG_VIEW', N'مشاهده‌ی چارت سازمانی'), ('ORG_EDIT', N'ویرایش چارت سازمانی');
 INSERT Sec.Permission(ApplicationId, PermissionName, PermissionCode, Description, IsActive)
 SELECT @app, p.Name, p.Code, N'مجوز سیستمی پنل مدیریت', 1 FROM @p p WHERE NOT EXISTS(SELECT 1 FROM Sec.Permission x WHERE x.PermissionCode=p.Code);
 UPDATE x SET IsActive=1 FROM Sec.Permission x JOIN @p p ON p.Code=x.PermissionCode WHERE x.IsActive=0;
 INSERT Sec.RolePermission(RoleId, PermissionId)
 SELECT @adminRole, x.PermissionId FROM Sec.Permission x JOIN @p p ON p.Code=x.PermissionCode
 WHERE NOT EXISTS(SELECT 1 FROM Sec.RolePermission rp WHERE rp.RoleId=@adminRole AND rp.PermissionId=x.PermissionId);

 INSERT dbo.SchemaMigration(MigrationId) VALUES('012_OrganizationChart');
 COMMIT;
 SELECT 'Applied 012_OrganizationChart' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

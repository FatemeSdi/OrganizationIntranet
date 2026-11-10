-- Additive: managed library of organizational documents (laws and regulations, publications and support, forms) that replaces the static catalog on the portal.
-- Lib.Category groups documents inside one of the three fixed groups (rules, publications, forms); Lib.Document describes one uploaded file. The file itself is kept on
-- the API server's disk under an opaque name (StoredName), never in this database; only its name, type, size and SHA-256 are stored here.
-- Seeds the categories of the old static catalog (no documents yet) and registers the permission LIBRARY_EDIT for the ADMIN role.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='016_PublicationSlider')
    THROW 51000,'Apply 016_PublicationSlider first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='017_LibraryDocuments')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 IF OBJECT_ID(N'Lib.Category',N'U') IS NOT NULL OR OBJECT_ID(N'Lib.Document',N'U') IS NOT NULL
    THROW 51000,'Library schema already exists in part. Reconcile before applying.',1;
 DECLARE @adminRole int = (SELECT RoleId FROM Sec.Role WHERE RoleCode='ADMIN' AND IsActive=1);
 IF @adminRole IS NULL THROW 51000,'Role ADMIN does not exist or is inactive.',1;
 DECLARE @app int = (SELECT ApplicationId FROM App.Application WHERE ApplicationCode='INTERANET');
 IF @app IS NULL THROW 51000,'Application INTERANET does not exist (apply 008_AdminPermissions).',1;
 IF EXISTS(SELECT 1 FROM Sec.Permission WHERE PermissionCode='LIBRARY_EDIT' AND ApplicationId<>@app)
    THROW 51000,'The permission code LIBRARY_EDIT already exists for another application. Reconcile before applying.',1;

 IF SCHEMA_ID(N'Lib') IS NULL EXEC(N'CREATE SCHEMA Lib');

 CREATE TABLE Lib.Category (
   CategoryId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LibCategory PRIMARY KEY,
   GroupCode varchar(20) NOT NULL,
   Title nvarchar(200) NOT NULL,
   DisplayOrder int NOT NULL CONSTRAINT DF_LibCategory_DisplayOrder DEFAULT(0),
   IsActive bit NOT NULL CONSTRAINT DF_LibCategory_IsActive DEFAULT(1),
   CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_LibCategory_CreatedAt DEFAULT(SYSUTCDATETIME()),
   CONSTRAINT CK_LibCategory_Group CHECK(GroupCode IN ('rules','publications','forms')),
   CONSTRAINT CK_LibCategory_Title CHECK(LEN(LTRIM(RTRIM(Title))) > 0)
 );
 CREATE UNIQUE INDEX UX_LibCategory_Group_Title ON Lib.Category(GroupCode, Title);

 CREATE TABLE Lib.Document (
   DocumentId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_LibDocument PRIMARY KEY,
   CategoryId int NOT NULL,
   Title nvarchar(300) NOT NULL,
   Description nvarchar(1000) NULL,
   FileName nvarchar(260) NOT NULL,
   ContentType varchar(100) NOT NULL,
   SizeBytes bigint NOT NULL,
   StoredName char(32) NOT NULL,
   Sha256 binary(32) NOT NULL,
   IsActive bit NOT NULL CONSTRAINT DF_LibDocument_IsActive DEFAULT(1),
   CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_LibDocument_CreatedAt DEFAULT(SYSUTCDATETIME()),
   UpdatedAt datetime2(7) NOT NULL CONSTRAINT DF_LibDocument_UpdatedAt DEFAULT(SYSUTCDATETIME()),
   CreatedBy bigint NOT NULL,
   UpdatedBy bigint NOT NULL,
   CONSTRAINT CK_LibDocument_Title CHECK(LEN(LTRIM(RTRIM(Title))) > 0),
   CONSTRAINT CK_LibDocument_Size CHECK(SizeBytes > 0),
   CONSTRAINT FK_LibDocument_Category FOREIGN KEY(CategoryId) REFERENCES Lib.Category(CategoryId),
   CONSTRAINT FK_LibDocument_CreatedBy FOREIGN KEY(CreatedBy) REFERENCES Sec.[User](UserId),
   CONSTRAINT FK_LibDocument_UpdatedBy FOREIGN KEY(UpdatedBy) REFERENCES Sec.[User](UserId)
 );
 CREATE UNIQUE INDEX UX_LibDocument_StoredName ON Lib.Document(StoredName);
 CREATE INDEX IX_LibDocument_Category ON Lib.Document(CategoryId, IsActive);
 CREATE INDEX IX_LibDocument_CreatedBy ON Lib.Document(CreatedBy);
 CREATE INDEX IX_LibDocument_UpdatedBy ON Lib.Document(UpdatedBy);

 INSERT Lib.Category(GroupCode, Title, DisplayOrder) VALUES
  ('rules', N'کنوانسیون‌ها', 1), ('rules', N'مصوبات هیأت عامل', 2), ('rules', N'دستورالعمل‌ها', 3), ('rules', N'استانداردها', 4),
  ('rules', N'اداری و استخدامی', 5), ('rules', N'آموزش', 6), ('rules', N'آیین‌نامه‌ها', 7),
  ('publications', N'راهنمای سیستم مدیریت دانش', 1), ('publications', N'کتاب‌ها', 2), ('publications', N'آموزش ارسال فکس در اتوماسیون', 3),
  ('publications', N'آموزش ارسال پست الکترونیک', 4), ('publications', N'راهنمای کاربری درگاه واحد سازمان', 5),
  ('publications', N'راهنمای کاربری پایانه‌داران', 6), ('publications', N'راهنمای کاربری امور بندری', 7),
  ('forms', N'اداری و مالی', 1), ('forms', N'فناوری اطلاعات (فاوا)', 2), ('forms', N'اداره برق و تأسیسات', 3),
  ('forms', N'بیانیه خط‌مشی ISMS', 4), ('forms', N'گواهینامه استاندارد ISMS', 5),
  ('forms', N'فهرست گروه‌های تخصصی امنیت اطلاعات', 6), ('forms', N'آگاهی‌رسانی امنیت اطلاعات', 7);

 DECLARE @p TABLE(Code varchar(100) PRIMARY KEY, Name nvarchar(150) NOT NULL);
 INSERT @p(Code, Name) VALUES ('LIBRARY_EDIT', N'مدیریت اسناد و فرم‌ها');
 INSERT Sec.Permission(ApplicationId, PermissionName, PermissionCode, Description, IsActive)
 SELECT @app, p.Name, p.Code, N'مجوز سیستمی پنل مدیریت', 1 FROM @p p WHERE NOT EXISTS(SELECT 1 FROM Sec.Permission x WHERE x.PermissionCode=p.Code);
 UPDATE x SET IsActive=1 FROM Sec.Permission x JOIN @p p ON p.Code=x.PermissionCode WHERE x.IsActive=0;
 INSERT Sec.RolePermission(RoleId, PermissionId)
 SELECT @adminRole, x.PermissionId FROM Sec.Permission x JOIN @p p ON p.Code=x.PermissionCode
 WHERE NOT EXISTS(SELECT 1 FROM Sec.RolePermission rp WHERE rp.RoleId=@adminRole AND rp.PermissionId=x.PermissionId);

 INSERT dbo.SchemaMigration(MigrationId) VALUES('017_LibraryDocuments');
 COMMIT;
 SELECT 'Applied 017_LibraryDocuments' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

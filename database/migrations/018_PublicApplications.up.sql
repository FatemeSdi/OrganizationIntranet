-- Data: the ten systems of the old intranet's home page (Default.aspx) become PUBLIC applications of the portal, with the addresses and descriptions of that page.
-- Existing applications (matched by ApplicationCode) get the old page's BaseUrl and description and are made public and active; their name, icon, order and other flags are kept.
-- Two systems that were missing (GCOMS and BIJAKCONTROL) are inserted. Idempotent: running it again changes nothing more.
-- NOTE: public applications are shown, with their address, to anonymous visitors of the portal.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='017_LibraryDocuments')
    THROW 51000,'Apply 017_LibraryDocuments first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='018_PublicApplications')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;

 DECLARE @a TABLE(Code varchar(50) PRIMARY KEY, Name nvarchar(150) NOT NULL, Url nvarchar(500) NOT NULL, Descr nvarchar(500) NOT NULL, Icon varchar(30) NOT NULL);
 INSERT @a(Code, Name, Url, Descr, Icon) VALUES
  ('SALARYPAYMENTRECEIPT', N'مشاهده فیش حقوق', N'https://interanet.sez-hpa.ir:9091', N'مشاهده فیش حقوق پرسنلی بندر شهید رجایی', 'document'),
  ('FILESHARING', N'اشتراک فایل', N'https://10.2.77.40', N'سامانه اشتراک گذاری فایل ها بندر شهید رجایی', 'folder'),
  ('GATEWAY', N'درگاه واحد سازمان', N'https://sp.pmo.ir/', N'درگاه واحد خدمات الکترونیک سازمان بنادر و دریانوردی', 'building'),
  ('MOBIN', N'سامانه مبین', N'https://mobin.sez-hpa.ir', N'سامانه ثبت درخواست کارها مبین بندر شهید رجایی', 'message'),
  ('AUTOMATION', N'اتوماسیون اداری فرزین', N'http://10.2.78.76:91', N'سامانه اتوماسیون اداری فرزین بندر شهید رجایی', 'mail'),
  ('FILEUPLOAD', N'سامانه ورود فایل', N'https://10.2.245.106', N'سامانه ورود فایل بندر شهید رجایی', 'upload'),
  ('GCOMS', N'جیکامز', N'https://interanet.sez-hpa.ir:9092/', N'سامانه گزارش جیکامز قدیم بندر شهید رجایی', 'chart'),
  ('CUSTOMERS', N'اطلاعات مشتریان', N'https://interanet.sez-hpa.ir:8082/Account/Login', N'سامانه اطلاعات مشتریان بندر شهید رجایی', 'users'),
  ('BIJAKCONTROL', N'کنترل بیجک', N'https://interanet.sez-hpa.ir:8083/', N'سامانه کنترل بیجک بندر شهید رجایی', 'search'),
  ('DEMAND', N'دیماند', N'http://10.2.78.133:8081', N'دیماند بندر شهید رجایی', 'chart');

 -- A new code must not collide with an existing application of the same NAME (names are matched by the catalog import too).
 IF EXISTS(SELECT 1 FROM @a a JOIN App.Application x ON x.ApplicationName = a.Name AND x.ApplicationCode <> a.Code)
    THROW 51000,'An application with the same name but another code already exists. Reconcile before applying.',1;

 UPDATE x SET BaseUrl = a.Url, Description = a.Descr, IsPublic = 1, IsUnitRestricted = 0, IsActive = 1
 FROM App.Application x JOIN @a a ON a.Code = x.ApplicationCode;

 DECLARE @next int = ISNULL((SELECT MAX(DisplayOrder) FROM App.Application), 0);
 INSERT App.Application(ApplicationName, ApplicationCode, BaseUrl, Description, Icon, DisplayOrder, IsActive, RequiresLogin, IsInternetAccessible, IsPublic, CreatedAt)
 SELECT a.Name, a.Code, a.Url, a.Descr, a.Icon, @next + ROW_NUMBER() OVER (ORDER BY a.Code), 1, 1, 0, 1, SYSUTCDATETIME()
 FROM @a a WHERE NOT EXISTS(SELECT 1 FROM App.Application x WHERE x.ApplicationCode = a.Code);

 INSERT dbo.SchemaMigration(MigrationId) VALUES('018_PublicApplications');
 COMMIT;
 SELECT 'Applied 018_PublicApplications' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

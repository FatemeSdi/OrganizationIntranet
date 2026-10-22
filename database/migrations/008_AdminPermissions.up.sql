-- Data migration: system permissions of the Admin panel (shared/Contracts/AdminPermissions.cs), granted to the ADMIN role.
-- After this, Admin pages and api/admin endpoints are authorized by permission instead of by the ADMIN role.
-- Requires 007_PublicationImages so the migration history stays linear. Existing permissions and grants are left untouched.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='007_PublicationImages')
    THROW 51000,'Apply 007_PublicationImages first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='008_AdminPermissions')
 BEGIN
   COMMIT;
   SELECT 'Already applied; run verification.' AS MigrationStatus;
   RETURN;
 END;
 DECLARE @adminRole int = (SELECT RoleId FROM Sec.Role WHERE RoleCode='ADMIN');
 IF @adminRole IS NULL THROW 51000,'Role ADMIN does not exist; create it before granting the Admin panel permissions.',1;
 IF NOT EXISTS(SELECT 1 FROM Sec.Role WHERE RoleId=@adminRole AND IsActive=1) THROW 51000,'Role ADMIN is inactive; activate it first.',1;

 -- The intranet itself owns the Admin panel permissions (Permission.ApplicationId is required).
 DECLARE @app int = (SELECT ApplicationId FROM App.Application WHERE ApplicationCode='INTERANET');
 IF @app IS NULL
 BEGIN
   INSERT App.Application(ApplicationName, ApplicationCode, Description, DisplayOrder, IsActive)
   VALUES(N'اینترانت سازمان', 'INTERANET', N'پورتال داخلی و پنل مدیریت', 0, 1);
   SET @app = SCOPE_IDENTITY();
 END;

 DECLARE @p TABLE(Code varchar(100) PRIMARY KEY, Name nvarchar(150) NOT NULL);
 INSERT @p(Code, Name) VALUES
  ('INTRANET_ADMIN', N'ورود به پنل مدیریت اینترانت'),
  ('USER_VIEW', N'مشاهده‌ی کاربران'), ('USER_EDIT', N'ویرایش کاربران'),
  ('ROLE_VIEW', N'مشاهده‌ی نقش‌ها'), ('ROLE_EDIT', N'ویرایش نقش‌ها'),
  ('PERMISSION_VIEW', N'مشاهده‌ی مجوزها'), ('PERMISSION_EDIT', N'ویرایش و اختصاص مجوزها'),
  ('APPLICATION_VIEW', N'مشاهده‌ی سامانه‌ها'), ('APPLICATION_EDIT', N'ویرایش سامانه‌ها و دسترسی آن‌ها'),
  ('ACCESS_REQUEST_REVIEW', N'بررسی درخواست‌های دسترسی'),
  ('PUBLICATION_EDIT', N'مدیریت اطلاع‌رسانی سازمانی'),
  ('AUTH_SETTINGS_EDIT', N'تنظیمات ورود و امنیت');

 -- A code that already exists for another application would be ambiguous: stop instead of re-purposing it.
 IF EXISTS(SELECT 1 FROM Sec.Permission x JOIN @p p ON p.Code=x.PermissionCode WHERE x.ApplicationId<>@app)
    THROW 51000,'An Admin panel permission code already exists for another application. Reconcile before applying.',1;

 INSERT Sec.Permission(ApplicationId, PermissionName, PermissionCode, Description, IsActive)
 SELECT @app, p.Name, p.Code, N'مجوز سیستمی پنل مدیریت', 1 FROM @p p
 WHERE NOT EXISTS(SELECT 1 FROM Sec.Permission x WHERE x.PermissionCode=p.Code);
 UPDATE x SET IsActive=1 FROM Sec.Permission x JOIN @p p ON p.Code=x.PermissionCode WHERE x.IsActive=0;

 INSERT Sec.RolePermission(RoleId, PermissionId)
 SELECT @adminRole, x.PermissionId FROM Sec.Permission x JOIN @p p ON p.Code=x.PermissionCode
 WHERE NOT EXISTS(SELECT 1 FROM Sec.RolePermission rp WHERE rp.RoleId=@adminRole AND rp.PermissionId=x.PermissionId);

 INSERT dbo.SchemaMigration(MigrationId) VALUES('008_AdminPermissions');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

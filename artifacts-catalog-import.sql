SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @UserId bigint, @RoleId int;
 IF (SELECT COUNT(*) FROM Sec.[User] WHERE Username=N'fatemeh.sadeghi' AND IsActive=1)<>1 THROW 51000,'Expected active user.',1;
 IF (SELECT COUNT(*) FROM Sec.Role WHERE RoleCode='EXPERT' AND IsActive=1)<>1 THROW 51000,'Expected active role.',1;
 SELECT @UserId=UserId FROM Sec.[User] WHERE Username=N'fatemeh.sadeghi' AND IsActive=1;
 SELECT @RoleId=RoleId FROM Sec.Role WHERE RoleCode='EXPERT' AND IsActive=1;
 DECLARE @Items TABLE(Code varchar(50),Name nvarchar(150),Description nvarchar(500),Icon nvarchar(100),DisplayOrder int);
 INSERT @Items VALUES
(N'PAYSLIP',N'مشاهده فیش حقوق',N'مشاهده و دریافت فیش حقوقی پرسنل',N'document',1),
(N'ATTENDANCE',N'حضور و غیاب',N'ثبت و پیگیری حضور و غیاب پرسنل',N'clock',2),
(N'AUTOMATION',N'اتوماسیون اداری فرزین',N'گردش مکاتبات و نامه‌های اداری سازمان',N'mail',3),
(N'GATEWAY',N'درگاه واحد سازمان',N'خدمات الکترونیک واحدهای بندری و دریانوردی',N'building',4),
(N'MOBIN',N'سامانه مبین',N'ثبت و پیگیری درخواست کارها',N'message',5),
(N'CUSTOMERS',N'اطلاعات مشتریان',N'سامانه اطلاعات مشتریان بندر شهید رجایی',N'users',6),
(N'DOCINQUIRY',N'استعلام سند',N'استعلام و پیگیری وضعیت اسناد ثبت‌شده',N'search',7),
(N'FILEUPLOAD',N'سامانه ورود فایل',N'بارگذاری و ثبت فایل‌های ورودی سازمان',N'upload',8),
(N'FILESHARING',N'اشتراک فایل',N'اشتراک‌گذاری فایل میان واحدهای مختلف',N'folder',9),
(N'ARCHIVE',N'آرشیو الکترونیکی',N'بایگانی و جست‌وجوی اسناد الکترونیکی',N'archive',10),
(N'TASKS',N'مدیریت وظایف',N'ثبت و گزارش‌گیری از وظایف محوله',N'tasks',11),
(N'DEMAND',N'دیماند',N'مدیریت درخواست‌های عملیاتی بندر',N'chart',12);
 IF EXISTS(SELECT i.Code FROM @Items i JOIN App.Application a ON a.ApplicationCode=i.Code OR a.ApplicationName=i.Name GROUP BY i.Code HAVING COUNT(*)>1) THROW 51000,'Ambiguous catalog matches.',1;
 INSERT App.Application(ApplicationCode,ApplicationName,Description,Icon,DisplayOrder,IsActive,RequiresLogin,IsInternetAccessible,IsPublic,AccessRequestNotificationChannels,CreatedAt)
 SELECT i.Code,i.Name,i.Description,i.Icon,i.DisplayOrder,1,1,0,CASE WHEN i.Code='GATEWAY' THEN 1 ELSE 0 END,'InApp',GETUTCDATE()
 FROM @Items i WHERE NOT EXISTS(SELECT 1 FROM App.Application a WHERE a.ApplicationCode=i.Code OR a.ApplicationName=i.Name);
 DECLARE @Created int=@@ROWCOUNT;
 UPDATE a SET IsPublic=1 FROM App.Application a JOIN @Items i ON a.ApplicationCode=i.Code OR a.ApplicationName=i.Name WHERE i.Code='GATEWAY';
 INSERT App.ApplicationAccess(ApplicationId,UserId,RoleId,CreatedAt)
 SELECT a.ApplicationId,@UserId,NULL,GETUTCDATE() FROM App.Application a JOIN @Items i ON a.ApplicationCode=i.Code OR a.ApplicationName=i.Name
 WHERE i.Code='PAYSLIP' AND NOT EXISTS(SELECT 1 FROM App.ApplicationAccess g WHERE g.ApplicationId=a.ApplicationId AND g.UserId=@UserId);
 INSERT App.ApplicationAccess(ApplicationId,UserId,RoleId,CreatedAt)
 SELECT a.ApplicationId,NULL,@RoleId,GETUTCDATE() FROM App.Application a JOIN @Items i ON a.ApplicationCode=i.Code OR a.ApplicationName=i.Name
 WHERE i.Code='ATTENDANCE' AND NOT EXISTS(SELECT 1 FROM App.ApplicationAccess g WHERE g.ApplicationId=a.ApplicationId AND g.RoleId=@RoleId);
 IF NOT EXISTS(SELECT 1 FROM Sec.UserRole WHERE UserId=@UserId AND RoleId=@RoleId)
 INSERT Sec.UserRole(UserId,RoleId,CreatedAt) VALUES(@UserId,@RoleId,GETUTCDATE());
 IF (SELECT COUNT(*) FROM App.Application a JOIN @Items i ON a.ApplicationCode=i.Code OR a.ApplicationName=i.Name)<>12 THROW 51000,'Incomplete import.',1;
 COMMIT;
 SELECT @Created AS CreatedApplications;
 SELECT a.ApplicationCode,a.ApplicationName,a.IsPublic,a.DisplayOrder FROM App.Application a JOIN @Items i ON a.ApplicationCode=i.Code OR a.ApplicationName=i.Name ORDER BY a.DisplayOrder;
 SELECT a.ApplicationCode,u.Username,r.RoleCode FROM App.ApplicationAccess g JOIN App.Application a ON a.ApplicationId=g.ApplicationId LEFT JOIN Sec.[User] u ON u.UserId=g.UserId LEFT JOIN Sec.Role r ON r.RoleId=g.RoleId WHERE a.ApplicationCode IN ('PAYSLIP','ATTENDANCE');
 SELECT u.Username,r.RoleCode,r.RoleName FROM Sec.UserRole ur JOIN Sec.[User] u ON u.UserId=ur.UserId JOIN Sec.Role r ON r.RoleId=ur.RoleId WHERE ur.UserId=@UserId AND ur.RoleId=@RoleId;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

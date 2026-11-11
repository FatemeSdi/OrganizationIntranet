-- Data: the "حضور و غیاب" (attendance) system of the old intranet (found in the second copy of its home page) becomes a PUBLIC application of the portal, with its address.
-- Also: the card descriptions of DEMAND and GCOMS are updated to the texts chosen for their cards (they described the old page, not what the system does).
-- Idempotent: running it again changes nothing more. NOTE: public applications are shown, with their address, to anonymous visitors of the portal.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='020_ApplicationImages')
    THROW 51000,'Apply 020_ApplicationImages first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='021_AttendanceApplication')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;

 -- A new code must not collide with an existing application of the same NAME.
 IF EXISTS(SELECT 1 FROM App.Application WHERE ApplicationName = N'حضور و غیاب' AND ApplicationCode <> 'ATTENDANCE')
    THROW 51000,'An application with the same name but another code already exists. Reconcile before applying.',1;

 IF EXISTS(SELECT 1 FROM App.Application WHERE ApplicationCode = 'ATTENDANCE')
    UPDATE App.Application SET BaseUrl = N'http://10.2.28.95', Description = N'سامانه امور کارکنان', IsPublic = 1, IsUnitRestricted = 0, IsActive = 1 WHERE ApplicationCode = 'ATTENDANCE';
 ELSE
    INSERT App.Application(ApplicationName, ApplicationCode, BaseUrl, Description, Icon, DisplayOrder, IsActive, RequiresLogin, IsInternetAccessible, IsPublic, CreatedAt)
    VALUES(N'حضور و غیاب', 'ATTENDANCE', N'http://10.2.28.95', N'سامانه امور کارکنان', 'users', ISNULL((SELECT MAX(DisplayOrder) FROM App.Application), 0) + 1, 1, 1, 0, 1, SYSUTCDATETIME());

 UPDATE App.Application SET Description = N'محاسبه مدت زمان کارکرد خن شناورها' WHERE ApplicationCode = 'DEMAND';
 UPDATE App.Application SET Description = N'سامانه مدیریت کالاهای متفرقه و نفتی' WHERE ApplicationCode = 'GCOMS';

 INSERT dbo.SchemaMigration(MigrationId) VALUES('021_AttendanceApplication');
 COMMIT;
 SELECT 'Applied 021_AttendanceApplication' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

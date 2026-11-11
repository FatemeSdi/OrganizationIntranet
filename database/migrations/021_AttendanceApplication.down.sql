-- Rollback (approximate): the ATTENDANCE application is deleted (with its card image) when nothing else references it; otherwise it is only made non-public and inactive.
-- The descriptions of DEMAND and GCOMS are left as they are (their previous texts are not stored). Stop the API first.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='021_AttendanceApplication') BEGIN COMMIT; RETURN; END;
 DECLARE @id int = (SELECT ApplicationId FROM App.Application WHERE ApplicationCode = 'ATTENDANCE');
 IF @id IS NOT NULL
 BEGIN
   IF EXISTS(SELECT 1 FROM App.ApplicationAccess g WHERE g.ApplicationId=@id)
   OR EXISTS(SELECT 1 FROM Notify.Notification n WHERE n.ApplicationId=@id)
   OR EXISTS(SELECT 1 FROM App.ApplicationAccessRequest r WHERE r.ApplicationId=@id)
   OR EXISTS(SELECT 1 FROM App.ApplicationService s WHERE s.ApplicationId=@id)
   OR EXISTS(SELECT 1 FROM App.ApplicationUnitAccess u WHERE u.ApplicationId=@id)
   OR EXISTS(SELECT 1 FROM App.ApplicationSupervisionGroup g2 WHERE g2.ApplicationId=@id)
   OR EXISTS(SELECT 1 FROM Sec.Permission p WHERE p.ApplicationId=@id)
   OR EXISTS(SELECT 1 FROM App.UserApplicationAccount ua WHERE ua.ApplicationId=@id)
   OR EXISTS(SELECT 1 FROM Sec.SsoTicket t WHERE t.ApplicationId=@id)
     UPDATE App.Application SET IsPublic = 0, IsActive = 0 WHERE ApplicationId = @id;
   ELSE
   BEGIN
     DELETE App.ApplicationImage WHERE ApplicationId = @id;
     DELETE App.Application WHERE ApplicationId = @id;
   END
 END
 DELETE dbo.SchemaMigration WHERE MigrationId='021_AttendanceApplication';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

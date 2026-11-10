-- Rollback (approximate, because the previous values are not stored): the two applications this migration may have inserted (GCOMS, BIJAKCONTROL) are deleted when nothing
-- references them, and the ten systems are made NON-public again. Their addresses and descriptions are left as they are. Stop the API first.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='018_PublicApplications') BEGIN COMMIT; RETURN; END;
 UPDATE App.Application SET IsPublic = 0 WHERE ApplicationCode IN ('SALARYPAYMENTRECEIPT','FILESHARING','GATEWAY','MOBIN','AUTOMATION','FILEUPLOAD','GCOMS','CUSTOMERS','BIJAKCONTROL','DEMAND');
 DELETE x FROM App.Application x
 WHERE x.ApplicationCode IN ('GCOMS','BIJAKCONTROL')
   AND NOT EXISTS(SELECT 1 FROM App.ApplicationAccess g WHERE g.ApplicationId=x.ApplicationId)
   AND NOT EXISTS(SELECT 1 FROM Notify.Notification n WHERE n.ApplicationId=x.ApplicationId)
   AND NOT EXISTS(SELECT 1 FROM App.ApplicationAccessRequest r WHERE r.ApplicationId=x.ApplicationId)
   AND NOT EXISTS(SELECT 1 FROM App.ApplicationService s WHERE s.ApplicationId=x.ApplicationId)
   AND NOT EXISTS(SELECT 1 FROM App.ApplicationUnitAccess u WHERE u.ApplicationId=x.ApplicationId)
   AND NOT EXISTS(SELECT 1 FROM App.ApplicationSupervisionGroup g2 WHERE g2.ApplicationId=x.ApplicationId)
   AND NOT EXISTS(SELECT 1 FROM Sec.Permission p WHERE p.ApplicationId=x.ApplicationId)
   AND NOT EXISTS(SELECT 1 FROM App.UserApplicationAccount ua WHERE ua.ApplicationId=x.ApplicationId)
   AND NOT EXISTS(SELECT 1 FROM Sec.SsoTicket t WHERE t.ApplicationId=x.ApplicationId);
 DELETE dbo.SchemaMigration WHERE MigrationId='018_PublicApplications';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

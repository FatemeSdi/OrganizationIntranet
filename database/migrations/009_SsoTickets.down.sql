-- Rollback: stop the API first. Tickets are short-lived one-time credentials (60 seconds), so dropping them only cancels
-- sign-ins that are in progress; nothing permanent is lost.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='009_SsoTickets') BEGIN COMMIT; RETURN; END;
 DROP TABLE Sec.SsoTicket;
 DELETE dbo.SchemaMigration WHERE MigrationId='009_SsoTickets';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

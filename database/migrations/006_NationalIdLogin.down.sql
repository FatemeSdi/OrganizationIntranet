-- Rollback: stop the new API first and turn off «ورود با کد ملی و پیامک» in Admin/Authentication.
-- Pending national-ID challenges are short-lived (5 minutes) one-time codes; they are deleted so the old constraints can be restored.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='006_NationalIdLogin')
 BEGIN COMMIT; RETURN; END;
 DELETE Sec.AuthenticationChallenge WHERE Purpose='otp-login' OR LoginMethod='national-id-sms';
 DROP INDEX IX_User_NationalId ON Sec.[User];
 ALTER TABLE Sec.AuthenticationChallenge DROP CONSTRAINT CK_AuthenticationChallenge_Purpose;
 ALTER TABLE Sec.AuthenticationChallenge DROP CONSTRAINT CK_AuthenticationChallenge_LoginMethod;
 EXEC(N'ALTER TABLE Sec.AuthenticationChallenge WITH CHECK ADD CONSTRAINT CK_AuthenticationChallenge_Purpose CHECK ([Purpose] IN (''login'',''reset'',''enroll''));');
 EXEC(N'ALTER TABLE Sec.AuthenticationChallenge WITH CHECK ADD CONSTRAINT CK_AuthenticationChallenge_LoginMethod CHECK ([LoginMethod] IN (''password'',''ldap''));');
 DELETE dbo.SchemaMigration WHERE MigrationId='006_NationalIdLogin';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

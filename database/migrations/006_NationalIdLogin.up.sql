-- Widens the challenge CHECK constraints for national-ID + SMS login and adds a lookup index on Sec.User.NationalId.
-- No data is changed; the login method stays off until an administrator enables it in Admin/Authentication.
-- Requires 005_PublicPublications so the migration history stays linear.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF OBJECT_ID(N'Sec.AuthenticationChallenge',N'U') IS NULL OR OBJECT_ID(N'dbo.SchemaMigration',N'U') IS NULL
    THROW 51000,'Apply the authentication and baseline migrations first.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='005_PublicPublications')
    THROW 51000,'Apply 005_PublicPublications first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='006_NationalIdLogin')
 BEGIN
   COMMIT;
   SELECT 'Already applied; run verification.' AS MigrationStatus;
   RETURN;
 END;
 -- Fail closed on a partial or independently installed change.
 IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_User_NationalId' AND object_id=OBJECT_ID(N'Sec.[User]'))
    OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_AuthenticationChallenge_Purpose' AND parent_object_id=OBJECT_ID(N'Sec.AuthenticationChallenge'))
    OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_AuthenticationChallenge_LoginMethod' AND parent_object_id=OBJECT_ID(N'Sec.AuthenticationChallenge'))
    THROW 51000,'National-ID login schema exists in part or the 002 constraints are missing. Reconcile before applying.',1;

 ALTER TABLE Sec.AuthenticationChallenge DROP CONSTRAINT CK_AuthenticationChallenge_Purpose;
 ALTER TABLE Sec.AuthenticationChallenge DROP CONSTRAINT CK_AuthenticationChallenge_LoginMethod;
 EXEC(N'ALTER TABLE Sec.AuthenticationChallenge WITH CHECK ADD CONSTRAINT CK_AuthenticationChallenge_Purpose CHECK ([Purpose] IN (''login'',''reset'',''enroll'',''otp-login''));');
 EXEC(N'ALTER TABLE Sec.AuthenticationChallenge WITH CHECK ADD CONSTRAINT CK_AuthenticationChallenge_LoginMethod CHECK ([LoginMethod] IN (''password'',''ldap'',''national-id-sms''));');
 -- Not unique on purpose: existing data may hold duplicates; the login fails closed for an ambiguous national ID.
 EXEC(N'CREATE INDEX IX_User_NationalId ON Sec.[User](NationalId) WHERE NationalId IS NOT NULL;');

 INSERT dbo.SchemaMigration(MigrationId) VALUES('006_NationalIdLogin');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

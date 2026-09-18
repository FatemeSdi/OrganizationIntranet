-- Additive schema for the existing AuthenticationService. Does not enable LDAP/SMS/2FA.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF OBJECT_ID(N'Sec.User',N'U') IS NULL OR OBJECT_ID(N'dbo.SchemaMigration',N'U') IS NULL
    THROW 51000,'Apply the inspected baseline and consistency migration first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='002_AuthenticationSchema')
 BEGIN
   COMMIT;
   SELECT 'Already applied; run schema verification.' AS MigrationStatus;
   RETURN;
 END;
 -- Fail closed on a partial or independently installed schema; never overwrite settings/secrets.
 IF OBJECT_ID(N'Sec.AuthenticationSettings',N'U') IS NOT NULL OR OBJECT_ID(N'Sec.AuthenticationChallenge',N'U') IS NOT NULL
    OR EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'Sec.User') AND name IN
      ('SecurityStamp','AuthenticatorSecret','LastAuthenticatorStep','FailedLoginAttempts','LockedUntil','LastSecuritySmsAt','AuthenticationVersion'))
    THROW 51000,'Authentication schema already exists in part or has another owner. Reconcile before applying.',1;

 ALTER TABLE Sec.[User] ADD
   SecurityStamp varchar(32) NOT NULL CONSTRAINT DF_User_SecurityStamp DEFAULT(REPLACE(CONVERT(varchar(36),NEWID()),'-','')) WITH VALUES,
   AuthenticatorSecret nvarchar(2000) NULL,
   LastAuthenticatorStep bigint NOT NULL CONSTRAINT DF_User_LastAuthenticatorStep DEFAULT(-1) WITH VALUES,
   FailedLoginAttempts int NOT NULL CONSTRAINT DF_User_FailedLoginAttempts DEFAULT(0) WITH VALUES,
   LockedUntil datetime2(7) NULL,
   LastSecuritySmsAt datetime2(7) NULL,
   AuthenticationVersion uniqueidentifier NOT NULL CONSTRAINT DF_User_AuthenticationVersion DEFAULT(NEWID()) WITH VALUES;
 EXEC(N'ALTER TABLE Sec.[User] WITH CHECK ADD CONSTRAINT CK_User_AuthenticationCounters CHECK (FailedLoginAttempts >= 0 AND LastAuthenticatorStep >= -1);');

 CREATE TABLE Sec.AuthenticationSettings (
   Id int NOT NULL CONSTRAINT PK_AuthenticationSettings PRIMARY KEY,
   Json nvarchar(max) NOT NULL,
   ProtectedSmsApiKey nvarchar(4000) NULL,
   Revision uniqueidentifier NOT NULL,
   CONSTRAINT CK_AuthenticationSettings_Singleton CHECK (Id=1),
   CONSTRAINT CK_AuthenticationSettings_Json CHECK (ISJSON(Json)=1 AND DATALENGTH(Json)<=20000)
 );
 -- Same defaults as AuthenticationSettingsDto: no unconfigured external provider is activated.
 INSERT Sec.AuthenticationSettings(Id,Json,Revision) VALUES(1,N'{}',NEWID());
 CREATE TABLE Sec.AuthenticationChallenge (
   Id varchar(64) NOT NULL CONSTRAINT PK_AuthenticationChallenge PRIMARY KEY,
   UserId bigint NOT NULL,
   Purpose varchar(20) NOT NULL,
   LoginMethod varchar(20) NOT NULL,
   SecurityStamp varchar(32) NOT NULL,
   SettingsRevision uniqueidentifier NOT NULL,
   ExpiresAt datetime2(7) NOT NULL,
   LastSentAt datetime2(7) NULL,
   CodeHash varchar(64) NULL,
   ProtectedSecret nvarchar(2000) NULL,
   Attempts int NOT NULL,
   Consumed bit NOT NULL,
   Version uniqueidentifier NOT NULL,
   CONSTRAINT FK_AuthenticationChallenge_User_UserId FOREIGN KEY(UserId) REFERENCES Sec.[User](UserId),
   CONSTRAINT CK_AuthenticationChallenge_Attempts CHECK(Attempts>=0),
   CONSTRAINT CK_AuthenticationChallenge_Purpose CHECK(Purpose IN ('login','reset','enroll')),
   CONSTRAINT CK_AuthenticationChallenge_LoginMethod CHECK(LoginMethod IN ('password','ldap'))
 );
 CREATE INDEX IX_AuthenticationChallenge_ExpiresAt ON Sec.AuthenticationChallenge(ExpiresAt);
 CREATE INDEX IX_AuthenticationChallenge_UserId_Purpose ON Sec.AuthenticationChallenge(UserId,Purpose);
 INSERT dbo.SchemaMigration(MigrationId) VALUES('002_AuthenticationSchema');
 COMMIT;
 SELECT 'Applied 002_AuthenticationSchema' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

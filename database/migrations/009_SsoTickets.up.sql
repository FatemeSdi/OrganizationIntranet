-- Additive: one-time single sign-on tickets (docs/sso.md). Only the SHA-256 of a ticket is stored; tickets live 60 seconds.
-- Per-application SSO keys and landing URLs stay in the API secure configuration, never in the database.
-- Requires 008_AdminPermissions so the migration history stays linear.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='008_AdminPermissions')
    THROW 51000,'Apply 008_AdminPermissions first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='009_SsoTickets')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 IF OBJECT_ID(N'Sec.SsoTicket',N'U') IS NOT NULL
    THROW 51000,'SSO ticket schema already exists in part. Reconcile before applying.',1;

 CREATE TABLE Sec.SsoTicket (
   SsoTicketId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SsoTicket PRIMARY KEY,
   TicketHash binary(32) NOT NULL,
   UserId bigint NOT NULL,
   ApplicationId int NOT NULL,
   CreatedAt datetime2(7) NOT NULL,
   ExpiresAt datetime2(7) NOT NULL,
   RedeemedAt datetime2(7) NULL,
   CONSTRAINT CK_SsoTicket_Lifetime CHECK(ExpiresAt > CreatedAt),
   CONSTRAINT FK_SsoTicket_User_UserId FOREIGN KEY(UserId) REFERENCES Sec.[User](UserId),
   CONSTRAINT FK_SsoTicket_Application_ApplicationId FOREIGN KEY(ApplicationId) REFERENCES App.Application(ApplicationId)
 );
 CREATE UNIQUE INDEX IX_SsoTicket_TicketHash ON Sec.SsoTicket(TicketHash);
 CREATE INDEX IX_SsoTicket_ExpiresAt ON Sec.SsoTicket(ExpiresAt);
 CREATE INDEX IX_SsoTicket_UserId ON Sec.SsoTicket(UserId);
 CREATE INDEX IX_SsoTicket_ApplicationId ON Sec.SsoTicket(ApplicationId);

 INSERT dbo.SchemaMigration(MigrationId) VALUES('009_SsoTickets');
 COMMIT;
 SELECT 'Applied 009_SsoTickets' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

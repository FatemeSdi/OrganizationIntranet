SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='019_HideHomeApplication') THROW 51000,'Hide home application migration is not installed.',1;
IF EXISTS(SELECT 1 FROM App.Application WHERE ApplicationCode='INTERANET' AND (IsActive=1 OR IsPublic=1)) THROW 51000,'INTERANET is still active or public.',1;
IF NOT EXISTS(SELECT 1 FROM Sec.Permission p JOIN App.Application a ON a.ApplicationId=p.ApplicationId WHERE a.ApplicationCode='INTERANET' AND p.PermissionCode='INTRANET_ADMIN' AND p.IsActive=1)
 THROW 51000,'The panel permissions of INTERANET must stay in place.',1;
SELECT 'Home application hidden' AS VerificationStatus;

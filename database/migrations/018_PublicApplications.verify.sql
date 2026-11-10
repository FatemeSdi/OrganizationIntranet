SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='018_PublicApplications') THROW 51000,'Public applications migration is not installed.',1;
IF (SELECT COUNT(*) FROM App.Application WHERE IsActive=1 AND IsPublic=1 AND IsUnitRestricted=0 AND BaseUrl IS NOT NULL AND Description IS NOT NULL
      AND ApplicationCode IN ('SALARYPAYMENTRECEIPT','FILESHARING','GATEWAY','MOBIN','AUTOMATION','FILEUPLOAD','GCOMS','CUSTOMERS','BIJAKCONTROL','DEMAND')) <> 10
 THROW 51000,'The ten public applications are not all present, active, public and addressed.',1;
IF EXISTS(SELECT 1 FROM App.Application WHERE ApplicationCode IN ('SALARYPAYMENTRECEIPT','FILESHARING','GATEWAY','MOBIN','AUTOMATION','FILEUPLOAD','GCOMS','CUSTOMERS','BIJAKCONTROL','DEMAND')
      AND (BaseUrl NOT LIKE 'http://%' AND BaseUrl NOT LIKE 'https://%'))
 THROW 51000,'A public application has an address that is not http(s).',1;
SELECT 'Public applications verified' AS VerificationStatus;

SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='021_AttendanceApplication') THROW 51000,'Attendance application migration is not installed.',1;
IF NOT EXISTS(SELECT 1 FROM App.Application WHERE ApplicationCode='ATTENDANCE' AND IsActive=1 AND IsPublic=1 AND IsUnitRestricted=0 AND BaseUrl IS NOT NULL AND BaseUrl LIKE 'http%://%' AND Description IS NOT NULL)
 THROW 51000,'ATTENDANCE is not present, active, public and addressed.',1;
IF (SELECT COUNT(*) FROM App.Application WHERE ApplicationCode IN ('DEMAND','GCOMS') AND Description IS NOT NULL AND LEN(Description) > 0) <> 2
 THROW 51000,'DEMAND and GCOMS must have a card description.',1;
SELECT 'Attendance application verified' AS VerificationStatus;

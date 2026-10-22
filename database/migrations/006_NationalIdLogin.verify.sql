SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='006_NationalIdLogin') THROW 51000,'National-ID login migration is not installed.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_User_NationalId' AND object_id=OBJECT_ID(N'Sec.[User]'))
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_AuthenticationChallenge_Purpose' AND parent_object_id=OBJECT_ID(N'Sec.AuthenticationChallenge') AND definition LIKE '%otp-login%')
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_AuthenticationChallenge_LoginMethod' AND parent_object_id=OBJECT_ID(N'Sec.AuthenticationChallenge') AND definition LIKE '%national-id-sms%')
 THROW 51000,'National-ID login schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name IN ('CK_AuthenticationChallenge_Purpose','CK_AuthenticationChallenge_LoginMethod') AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51000,'Challenge constraints are disabled or untrusted.',1;
-- Informational: national IDs shared by several users cannot sign in with national ID + SMS (fail closed).
SELECT COUNT(*) AS DuplicateNationalIds FROM (SELECT NationalId FROM Sec.[User] WHERE NationalId IS NOT NULL AND NationalId<>'' GROUP BY NationalId HAVING COUNT(*)>1) d;
SELECT 'National-ID login schema verified' AS VerificationStatus;

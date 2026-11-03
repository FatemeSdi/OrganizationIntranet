-- Additive: per-application SSO settings move from appsettings (ApplicationIntegrations:{CODE}:SsoPath/SsoKey) into App.Application so the admin panel manages them.
-- SsoPath is the relative landing path (the host comes from BaseUrl). The SSO key is stored only as a SHA-256 hash: the intranet just verifies the key an
-- application presents, so the plain key is never needed again after it has been shown once. A hash can be set on only one application (filtered unique index).
-- The three known applications get their (non-secret) landing path; their keys are entered in the panel (or copied by a one-off script) because a migration cannot know them.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,'Cannot acquire schema migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='012_OrganizationChart')
    THROW 51000,'Apply 012_OrganizationChart first.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='013_ApplicationSsoSettings')
 BEGIN COMMIT; SELECT 'Already applied; run verification.' AS MigrationStatus; RETURN; END;
 IF COL_LENGTH(N'App.Application',N'SsoPath') IS NOT NULL OR COL_LENGTH(N'App.Application',N'SsoKeyHash') IS NOT NULL OR COL_LENGTH(N'App.Application',N'SsoKeyChangedAt') IS NOT NULL
    THROW 51000,'SSO settings columns already exist in part. Reconcile before applying.',1;

 ALTER TABLE App.Application ADD SsoPath nvarchar(200) NULL, SsoKeyHash binary(32) NULL, SsoKeyChangedAt datetime2(7) NULL;
 EXEC(N'ALTER TABLE App.Application ADD CONSTRAINT CK_Application_SsoPath CHECK(SsoPath IS NULL OR LEN(LTRIM(RTRIM(SsoPath))) > 0)');
 EXEC(N'CREATE UNIQUE INDEX UX_Application_SsoKeyHash ON App.Application(SsoKeyHash) WHERE SsoKeyHash IS NOT NULL');
 EXEC(N'UPDATE App.Application SET SsoPath = CASE ApplicationCode WHEN ''TTMS'' THEN N''Security/Account/Sso'' WHEN ''ACCESSREPORT'' THEN N''Security/Account/Sso'' WHEN ''ITSERVICEDESK'' THEN N''Account/Sso'' END WHERE ApplicationCode IN (''TTMS'',''ACCESSREPORT'',''ITSERVICEDESK'')');

 INSERT dbo.SchemaMigration(MigrationId) VALUES('013_ApplicationSsoSettings');
 COMMIT;
 SELECT 'Applied 013_ApplicationSsoSettings' AS MigrationStatus;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

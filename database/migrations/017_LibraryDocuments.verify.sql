SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='017_LibraryDocuments') THROW 51000,'Library migration is not installed.',1;
IF OBJECT_ID(N'Lib.Category',N'U') IS NULL OR OBJECT_ID(N'Lib.Document',N'U') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_LibCategory_Group_Title' AND is_unique=1 AND object_id=OBJECT_ID(N'Lib.Category'))
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_LibDocument_StoredName' AND is_unique=1 AND object_id=OBJECT_ID(N'Lib.Document'))
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN (OBJECT_ID(N'Lib.Category'),OBJECT_ID(N'Lib.Document')) AND is_disabled=0 AND is_not_trusted=0) <> 4
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'Lib.Document') AND delete_referential_action=0 AND is_disabled=0 AND is_not_trusted=0) <> 3
 THROW 51000,'Library schema is incomplete.',1;
IF NOT EXISTS(SELECT 1 FROM Sec.Permission p JOIN Sec.RolePermission rp ON rp.PermissionId=p.PermissionId JOIN Sec.Role r ON r.RoleId=rp.RoleId
              WHERE p.PermissionCode='LIBRARY_EDIT' AND p.IsActive=1 AND r.RoleCode='ADMIN')
 THROW 51000,'ADMIN does not hold LIBRARY_EDIT.',1;
SELECT 'Library schema verified' AS VerificationStatus;

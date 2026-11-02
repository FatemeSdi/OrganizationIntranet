SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='012_OrganizationChart') THROW 51000,'Organization chart migration is not installed.',1;
IF OBJECT_ID(N'Org.Unit',N'U') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_OrgUnit_Parent_Title' AND is_unique=1 AND object_id=OBJECT_ID(N'Org.Unit'))
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_OrgUnit_SourceId' AND is_unique=1 AND has_filter=1 AND object_id=OBJECT_ID(N'Org.Unit'))
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'Org.Unit') AND delete_referential_action=0) <> 2
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_OrgUnit_Title' AND parent_object_id=OBJECT_ID(N'Org.Unit'))
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_OrgUnit_NotOwnParent' AND parent_object_id=OBJECT_ID(N'Org.Unit'))
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'Sec.User') AND name='UnitId' AND is_nullable=1)
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_User_OrgUnit' AND parent_object_id=OBJECT_ID(N'Sec.User') AND delete_referential_action=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_User_UnitId' AND object_id=OBJECT_ID(N'Sec.User'))
 THROW 51000,'Organization chart schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id=OBJECT_ID(N'Org.Unit') AND (is_disabled=1 OR is_not_trusted=1))
 OR EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'Org.Unit') AND (is_disabled=1 OR is_not_trusted=1))
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'Org.Unit') AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51000,'Organization chart constraints are disabled or untrusted.',1;
IF (SELECT COUNT(*) FROM Sec.Permission p JOIN Sec.RolePermission rp ON rp.PermissionId=p.PermissionId JOIN Sec.Role r ON r.RoleId=rp.RoleId
     WHERE p.PermissionCode IN ('ORG_VIEW','ORG_EDIT') AND p.IsActive=1 AND r.RoleCode='ADMIN') <> 2
 THROW 51000,'ADMIN does not hold both organization permissions.',1;
IF EXISTS(SELECT 1 FROM Org.Unit u LEFT JOIN Org.Unit p ON p.UnitId=u.ParentId WHERE u.ParentId IS NOT NULL AND p.UnitId IS NULL) THROW 51000,'An organization unit points to a missing parent.',1;
SELECT 'Organization chart schema verified' AS VerificationStatus;

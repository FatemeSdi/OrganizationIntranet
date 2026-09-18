SET NOCOUNT ON;
SET LOCK_TIMEOUT 5000;
IF OBJECT_ID(N'Sec.AuthenticationSettings',N'U') IS NULL OR OBJECT_ID(N'Sec.AuthenticationChallenge',N'U') IS NULL
 THROW 51000,'Authentication tables missing.',1;
IF COL_LENGTH(N'Notify.Publication',N'Revision') IS NULL
 THROW 51000,'Publication revision missing.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'Sec.User') AND name IN
 ('SecurityStamp','AuthenticatorSecret','LastAuthenticatorStep','FailedLoginAttempts','LockedUntil','LastSecuritySmsAt','AuthenticationVersion'))<>7
 THROW 51000,'User authentication columns missing.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 THROW 51000,'Untrusted or disabled foreign key.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 THROW 51000,'Untrusted or disabled check constraint.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE delete_referential_action<>0)
 THROW 51000,'Unexpected cascading delete.',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Publication_Kind') OR
   NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Publication_PublishState') OR
   NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_AuthenticationSettings_Json')
 THROW 51000,'Required integrity constraints missing.',1;
IF (SELECT COUNT(*) FROM Sec.AuthenticationSettings WHERE Id=1)<>1
 THROW 51000,'Authentication singleton missing.',1;
SELECT 'User' TableName,COUNT_BIG(*) Rows FROM Sec.[User]
UNION ALL SELECT 'Role',COUNT_BIG(*) FROM Sec.Role
UNION ALL SELECT 'UserRole',COUNT_BIG(*) FROM Sec.UserRole
UNION ALL SELECT 'Application',COUNT_BIG(*) FROM App.Application
UNION ALL SELECT 'Permission',COUNT_BIG(*) FROM Sec.Permission
UNION ALL SELECT 'RolePermission',COUNT_BIG(*) FROM Sec.RolePermission
UNION ALL SELECT 'UserPermission',COUNT_BIG(*) FROM Sec.UserPermission
UNION ALL SELECT 'Notification',COUNT_BIG(*) FROM Notify.Notification
UNION ALL SELECT 'UserNotification',COUNT_BIG(*) FROM Notify.UserNotification
UNION ALL SELECT 'Publication',COUNT_BIG(*) FROM Notify.Publication
UNION ALL SELECT 'AuthenticationSettings',COUNT_BIG(*) FROM Sec.AuthenticationSettings
UNION ALL SELECT 'AuthenticationChallenge',COUNT_BIG(*) FROM Sec.AuthenticationChallenge;
SELECT COUNT(*) ForeignKeys, SUM(CONVERT(int,is_disabled)) Disabled, SUM(CONVERT(int,is_not_trusted)) Untrusted FROM sys.foreign_keys;
SELECT COUNT(*) CheckConstraints FROM sys.check_constraints;
DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;
SELECT 'Schema verification passed; DBCC output above must contain no violations.' Result;

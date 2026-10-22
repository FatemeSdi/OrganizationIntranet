SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='009_SsoTickets') THROW 51000,'SSO ticket migration is not installed.',1;
IF OBJECT_ID(N'Sec.SsoTicket',N'U') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_SsoTicket_TicketHash' AND is_unique=1 AND object_id=OBJECT_ID(N'Sec.SsoTicket'))
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'Sec.SsoTicket') AND delete_referential_action=0) <> 2
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_SsoTicket_Lifetime' AND parent_object_id=OBJECT_ID(N'Sec.SsoTicket'))
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'Sec.SsoTicket') AND name='TicketHash' AND max_length=32 AND is_nullable=0)
 THROW 51000,'SSO ticket schema is incomplete.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'Sec.SsoTicket') AND (is_disabled=1 OR is_not_trusted=1))
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'Sec.SsoTicket') AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51000,'SSO ticket constraints are disabled or untrusted.',1;
SELECT 'SSO ticket schema verified' AS VerificationStatus;

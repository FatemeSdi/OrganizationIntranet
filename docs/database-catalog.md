# فهرست فنی بانک اطلاعاتی
برداشت مستقیم از SQL Server در 2026-09-17؛ فقط metadata و آمار تجمیعی، بدون داده شخصی یا اسرار. max_length بر حسب بایت است؛ برای nvarchar بر دو تقسیم شود و مقدار -1 یعنی max. شماره بخش‌ها با result set اسکریپت architecture-audit.readonly.sql تطابق دارد.

## Result 1

| DatabaseName | ProductVersion | compatibility_level | collation_name | recovery_model_desc | is_read_committed_snapshot_on | is_auto_close_on | is_auto_shrink_on |
| --- | --- | --- | --- | --- | --- | --- | --- |
| OrganizationIntranet | 15.0.4420.2 | 150 | SQL_Latin1_General_CP1_CI_AS | FULL | False | False | False |

## Result 2

| CanViewDefinition | CanViewDatabaseState |
| --- | --- |
| 1 | 1 |

## Result 3

| SchemaName | TableName | ApproxRows |
| --- | --- | --- |
| App | Application | 1 |
| dbo | SchemaMigration | 2 |
| dbo | sysdiagrams | 0 |
| Notify | Notification | 0 |
| Notify | Publication | 0 |
| Notify | UserNotification | 0 |
| Sec | AuthenticationChallenge | 0 |
| Sec | AuthenticationSettings | 1 |
| Sec | Permission | 6 |
| Sec | Role | 2 |
| Sec | RolePermission | 6 |
| Sec | User | 3 |
| Sec | UserPermission | 0 |
| Sec | UserRole | 3 |

## Result 4

| SchemaName | TableName | column_id | ColumnName | TypeName | max_length | precision | scale | is_nullable | is_identity | is_computed | DefaultDefinition |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| App | Application | 1 | ApplicationId | int | 4 | 10 | 0 | False | True | False |  |
| App | Application | 2 | ApplicationName | nvarchar | 300 | 0 | 0 | False | False | False |  |
| App | Application | 3 | ApplicationCode | varchar | 50 | 0 | 0 | False | False | False |  |
| App | Application | 4 | BaseUrl | nvarchar | 1000 | 0 | 0 | True | False | False |  |
| App | Application | 5 | Description | nvarchar | 1000 | 0 | 0 | True | False | False |  |
| App | Application | 6 | Icon | nvarchar | 400 | 0 | 0 | True | False | False |  |
| App | Application | 7 | DisplayOrder | int | 4 | 10 | 0 | False | False | False | ((0)) |
| App | Application | 8 | IsActive | bit | 1 | 1 | 0 | False | False | False | ((1)) |
| App | Application | 9 | CreatedAt | datetime | 8 | 23 | 3 | False | False | False | (getutcdate()) |
| dbo | SchemaMigration | 1 | MigrationId | varchar | 150 | 0 | 0 | False | False | False |  |
| dbo | SchemaMigration | 2 | AppliedAt | datetime2 | 8 | 27 | 7 | False | False | False | (sysutcdatetime()) |
| dbo | sysdiagrams | 1 | name | sysname | 256 | 0 | 0 | False | False | False |  |
| dbo | sysdiagrams | 2 | principal_id | int | 4 | 10 | 0 | False | False | False |  |
| dbo | sysdiagrams | 3 | diagram_id | int | 4 | 10 | 0 | False | True | False |  |
| dbo | sysdiagrams | 4 | version | int | 4 | 10 | 0 | True | False | False |  |
| dbo | sysdiagrams | 5 | definition | varbinary | -1 | 0 | 0 | True | False | False |  |
| Notify | Notification | 1 | NotificationId | bigint | 8 | 19 | 0 | False | True | False |  |
| Notify | Notification | 2 | Title | nvarchar | 400 | 0 | 0 | False | False | False |  |
| Notify | Notification | 3 | Message | nvarchar | -1 | 0 | 0 | False | False | False |  |
| Notify | Notification | 4 | NotificationType | varchar | 50 | 0 | 0 | True | False | False |  |
| Notify | Notification | 5 | CreatedBy | bigint | 8 | 19 | 0 | True | False | False |  |
| Notify | Notification | 6 | CreatedAt | datetime | 8 | 23 | 3 | False | False | False | (getutcdate()) |
| Notify | Notification | 7 | ApplicationId | int | 4 | 10 | 0 | False | False | False |  |
| Notify | Notification | 8 | TargetUrl | nvarchar | 1000 | 0 | 0 | True | False | False |  |
| Notify | Publication | 1 | PublicationId | bigint | 8 | 19 | 0 | False | True | False |  |
| Notify | Publication | 2 | Title | nvarchar | 400 | 0 | 0 | False | False | False |  |
| Notify | Publication | 3 | Summary | nvarchar | 2000 | 0 | 0 | False | False | False |  |
| Notify | Publication | 4 | Body | nvarchar | -1 | 0 | 0 | False | False | False |  |
| Notify | Publication | 5 | Kind | nvarchar | 40 | 0 | 0 | False | False | False |  |
| Notify | Publication | 6 | IsPublished | bit | 1 | 1 | 0 | False | False | False |  |
| Notify | Publication | 7 | CreatedAt | datetime2 | 8 | 27 | 7 | False | False | False |  |
| Notify | Publication | 8 | UpdatedAt | datetime2 | 8 | 27 | 7 | False | False | False |  |
| Notify | Publication | 9 | PublishedAt | datetime2 | 8 | 27 | 7 | True | False | False |  |
| Notify | Publication | 10 | CreatedBy | bigint | 8 | 19 | 0 | False | False | False |  |
| Notify | Publication | 11 | UpdatedBy | bigint | 8 | 19 | 0 | False | False | False |  |
| Notify | Publication | 12 | Revision | bigint | 8 | 19 | 0 | False | False | False | ((0)) |
| Notify | UserNotification | 1 | UserNotificationId | bigint | 8 | 19 | 0 | False | True | False |  |
| Notify | UserNotification | 2 | NotificationId | bigint | 8 | 19 | 0 | False | False | False |  |
| Notify | UserNotification | 3 | UserId | bigint | 8 | 19 | 0 | False | False | False |  |
| Notify | UserNotification | 4 | IsRead | bit | 1 | 1 | 0 | False | False | False | ((0)) |
| Notify | UserNotification | 5 | ReadAt | datetime | 8 | 23 | 3 | True | False | False |  |
| Notify | UserNotification | 6 | CreatedAt | datetime | 8 | 23 | 3 | False | False | False | (getutcdate()) |
| Sec | AuthenticationChallenge | 1 | Id | varchar | 64 | 0 | 0 | False | False | False |  |
| Sec | AuthenticationChallenge | 2 | UserId | bigint | 8 | 19 | 0 | False | False | False |  |
| Sec | AuthenticationChallenge | 3 | Purpose | varchar | 20 | 0 | 0 | False | False | False |  |
| Sec | AuthenticationChallenge | 4 | LoginMethod | varchar | 20 | 0 | 0 | False | False | False |  |
| Sec | AuthenticationChallenge | 5 | SecurityStamp | varchar | 32 | 0 | 0 | False | False | False |  |
| Sec | AuthenticationChallenge | 6 | SettingsRevision | uniqueidentifier | 16 | 0 | 0 | False | False | False |  |
| Sec | AuthenticationChallenge | 7 | ExpiresAt | datetime2 | 8 | 27 | 7 | False | False | False |  |
| Sec | AuthenticationChallenge | 8 | LastSentAt | datetime2 | 8 | 27 | 7 | True | False | False |  |
| Sec | AuthenticationChallenge | 9 | CodeHash | varchar | 64 | 0 | 0 | True | False | False |  |
| Sec | AuthenticationChallenge | 10 | ProtectedSecret | nvarchar | 4000 | 0 | 0 | True | False | False |  |
| Sec | AuthenticationChallenge | 11 | Attempts | int | 4 | 10 | 0 | False | False | False |  |
| Sec | AuthenticationChallenge | 12 | Consumed | bit | 1 | 1 | 0 | False | False | False |  |
| Sec | AuthenticationChallenge | 13 | Version | uniqueidentifier | 16 | 0 | 0 | False | False | False |  |
| Sec | AuthenticationSettings | 1 | Id | int | 4 | 10 | 0 | False | False | False |  |
| Sec | AuthenticationSettings | 2 | Json | nvarchar | -1 | 0 | 0 | False | False | False |  |
| Sec | AuthenticationSettings | 3 | ProtectedSmsApiKey | nvarchar | 8000 | 0 | 0 | True | False | False |  |
| Sec | AuthenticationSettings | 4 | Revision | uniqueidentifier | 16 | 0 | 0 | False | False | False |  |
| Sec | Permission | 1 | PermissionId | int | 4 | 10 | 0 | False | True | False |  |
| Sec | Permission | 2 | ApplicationId | int | 4 | 10 | 0 | False | False | False |  |
| Sec | Permission | 3 | PermissionName | nvarchar | 300 | 0 | 0 | False | False | False |  |
| Sec | Permission | 4 | PermissionCode | varchar | 100 | 0 | 0 | False | False | False |  |
| Sec | Permission | 5 | Description | nvarchar | 1000 | 0 | 0 | True | False | False |  |
| Sec | Permission | 6 | IsActive | bit | 1 | 1 | 0 | False | False | False | ((1)) |
| Sec | Permission | 7 | CreatedAt | datetime | 8 | 23 | 3 | False | False | False | (getutcdate()) |
| Sec | Role | 1 | RoleId | int | 4 | 10 | 0 | False | True | False |  |
| Sec | Role | 2 | RoleName | nvarchar | 200 | 0 | 0 | False | False | False |  |
| Sec | Role | 3 | RoleCode | varchar | 50 | 0 | 0 | False | False | False |  |
| Sec | Role | 4 | IsActive | bit | 1 | 1 | 0 | False | False | False | ((1)) |
| Sec | Role | 5 | CreatedAt | datetime | 8 | 23 | 3 | False | False | False | (getutcdate()) |
| Sec | RolePermission | 1 | RolePermissionId | bigint | 8 | 19 | 0 | False | True | False |  |
| Sec | RolePermission | 2 | RoleId | int | 4 | 10 | 0 | False | False | False |  |
| Sec | RolePermission | 3 | PermissionId | int | 4 | 10 | 0 | False | False | False |  |
| Sec | RolePermission | 4 | CreatedAt | datetime2 | 8 | 27 | 7 | False | False | False | (getutcdate()) |
| Sec | User | 1 | UserId | bigint | 8 | 19 | 0 | False | True | False |  |
| Sec | User | 2 | Username | nvarchar | 200 | 0 | 0 | False | False | False |  |
| Sec | User | 3 | NationalId | varchar | 10 | 0 | 0 | True | False | False |  |
| Sec | User | 4 | Name | nvarchar | 200 | 0 | 0 | False | False | False |  |
| Sec | User | 5 | LastName | nvarchar | 200 | 0 | 0 | False | False | False |  |
| Sec | User | 6 | MobileNumber | varchar | 20 | 0 | 0 | True | False | False |  |
| Sec | User | 7 | Email | nvarchar | 400 | 0 | 0 | True | False | False |  |
| Sec | User | 8 | PasswordHash | nvarchar | 1000 | 0 | 0 | True | False | False |  |
| Sec | User | 9 | IsActive | bit | 1 | 1 | 0 | False | False | False | ((1)) |
| Sec | User | 10 | CreatedAt | datetime | 8 | 23 | 3 | False | False | False | (getutcdate()) |
| Sec | User | 11 | LastLogin | datetime | 8 | 23 | 3 | True | False | False |  |
| Sec | User | 12 | SecurityStamp | varchar | 32 | 0 | 0 | False | False | False | (replace(CONVERT([varchar](36),newid()),'-','')) |
| Sec | User | 13 | AuthenticatorSecret | nvarchar | 4000 | 0 | 0 | True | False | False |  |
| Sec | User | 14 | LastAuthenticatorStep | bigint | 8 | 19 | 0 | False | False | False | ((-1)) |
| Sec | User | 15 | FailedLoginAttempts | int | 4 | 10 | 0 | False | False | False | ((0)) |
| Sec | User | 16 | LockedUntil | datetime2 | 8 | 27 | 7 | True | False | False |  |
| Sec | User | 17 | LastSecuritySmsAt | datetime2 | 8 | 27 | 7 | True | False | False |  |
| Sec | User | 18 | AuthenticationVersion | uniqueidentifier | 16 | 0 | 0 | False | False | False | (newid()) |
| Sec | UserPermission | 1 | UserPermissionId | bigint | 8 | 19 | 0 | False | True | False |  |
| Sec | UserPermission | 2 | UserId | bigint | 8 | 19 | 0 | False | False | False |  |
| Sec | UserPermission | 3 | PermissionId | int | 4 | 10 | 0 | False | False | False |  |
| Sec | UserPermission | 4 | CreatedAt | datetime | 8 | 23 | 3 | False | False | False | (getutcdate()) |
| Sec | UserRole | 1 | UserRoleId | bigint | 8 | 19 | 0 | False | True | False |  |
| Sec | UserRole | 2 | UserId | bigint | 8 | 19 | 0 | False | False | False |  |
| Sec | UserRole | 3 | RoleId | int | 4 | 10 | 0 | False | False | False |  |
| Sec | UserRole | 4 | CreatedAt | datetime | 8 | 23 | 3 | False | False | False | (getutcdate()) |

## Result 5

| SchemaName | TableName | IndexName | type_desc | is_unique | is_primary_key | is_disabled | filter_definition | key_ordinal | is_included_column | ColumnName |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| App | Application | IX_Application_DisplayOrder | NONCLUSTERED | False | False | False |  | 1 | False | DisplayOrder |
| App | Application | PK_Application | CLUSTERED | True | True | False |  | 1 | False | ApplicationId |
| App | Application | UQ_Application_ApplicationCode | NONCLUSTERED | True | False | False |  | 1 | False | ApplicationCode |
| dbo | SchemaMigration | PK_SchemaMigration | CLUSTERED | True | True | False |  | 1 | False | MigrationId |
| dbo | sysdiagrams | PK__sysdiagr__C2B05B615B8A38F5 | CLUSTERED | True | True | False |  | 1 | False | diagram_id |
| dbo | sysdiagrams | UK_principal_name | NONCLUSTERED | True | False | False |  | 1 | False | principal_id |
| dbo | sysdiagrams | UK_principal_name | NONCLUSTERED | True | False | False |  | 2 | False | name |
| Notify | Notification | IX_Notification_ApplicationId | NONCLUSTERED | False | False | False |  | 1 | False | ApplicationId |
| Notify | Notification | IX_Notification_CreatedBy | NONCLUSTERED | False | False | False |  | 1 | False | CreatedBy |
| Notify | Notification | PK_Notification | CLUSTERED | True | True | False |  | 1 | False | NotificationId |
| Notify | Publication | IX_Publication_CreatedBy | NONCLUSTERED | False | False | False |  | 1 | False | CreatedBy |
| Notify | Publication | IX_Publication_IsPublished_PublishedAt_PublicationId | NONCLUSTERED | False | False | False |  | 1 | False | IsPublished |
| Notify | Publication | IX_Publication_IsPublished_PublishedAt_PublicationId | NONCLUSTERED | False | False | False |  | 2 | False | PublishedAt |
| Notify | Publication | IX_Publication_IsPublished_PublishedAt_PublicationId | NONCLUSTERED | False | False | False |  | 3 | False | PublicationId |
| Notify | Publication | IX_Publication_Kind | NONCLUSTERED | False | False | False |  | 1 | False | Kind |
| Notify | Publication | IX_Publication_UpdatedBy | NONCLUSTERED | False | False | False |  | 1 | False | UpdatedBy |
| Notify | Publication | PK_Publication | CLUSTERED | True | True | False |  | 1 | False | PublicationId |
| Notify | UserNotification | IX_UserNotification_NotificationId | NONCLUSTERED | False | False | False |  | 1 | False | NotificationId |
| Notify | UserNotification | IX_UserNotification_UserId | NONCLUSTERED | False | False | False |  | 1 | False | UserId |
| Notify | UserNotification | IX_UserNotification_UserId_IsRead_CreatedAt | NONCLUSTERED | False | False | False |  | 1 | False | UserId |
| Notify | UserNotification | IX_UserNotification_UserId_IsRead_CreatedAt | NONCLUSTERED | False | False | False |  | 2 | False | IsRead |
| Notify | UserNotification | IX_UserNotification_UserId_IsRead_CreatedAt | NONCLUSTERED | False | False | False |  | 3 | False | CreatedAt |
| Notify | UserNotification | PK_UserNotification | CLUSTERED | True | True | False |  | 1 | False | UserNotificationId |
| Notify | UserNotification | UQ_UserNotification_NotificationId_UserId | NONCLUSTERED | True | False | False |  | 1 | False | NotificationId |
| Notify | UserNotification | UQ_UserNotification_NotificationId_UserId | NONCLUSTERED | True | False | False |  | 2 | False | UserId |
| Sec | AuthenticationChallenge | IX_AuthenticationChallenge_ExpiresAt | NONCLUSTERED | False | False | False |  | 1 | False | ExpiresAt |
| Sec | AuthenticationChallenge | IX_AuthenticationChallenge_UserId_Purpose | NONCLUSTERED | False | False | False |  | 1 | False | UserId |
| Sec | AuthenticationChallenge | IX_AuthenticationChallenge_UserId_Purpose | NONCLUSTERED | False | False | False |  | 2 | False | Purpose |
| Sec | AuthenticationChallenge | PK_AuthenticationChallenge | CLUSTERED | True | True | False |  | 1 | False | Id |
| Sec | AuthenticationSettings | PK_AuthenticationSettings | CLUSTERED | True | True | False |  | 1 | False | Id |
| Sec | Permission | IX_Permission_ApplicationId | NONCLUSTERED | False | False | False |  | 1 | False | ApplicationId |
| Sec | Permission | PK_Permission | CLUSTERED | True | True | False |  | 1 | False | PermissionId |
| Sec | Permission | UQ_Permission_ApplicationId_PermissionCode | NONCLUSTERED | True | False | False |  | 1 | False | ApplicationId |
| Sec | Permission | UQ_Permission_ApplicationId_PermissionCode | NONCLUSTERED | True | False | False |  | 2 | False | PermissionCode |
| Sec | Permission | UX_Permission_PermissionCode | NONCLUSTERED | True | False | False |  | 1 | False | PermissionCode |
| Sec | Role | PK_Role | CLUSTERED | True | True | False |  | 1 | False | RoleId |
| Sec | Role | UQ_Role_RoleCode | NONCLUSTERED | True | False | False |  | 1 | False | RoleCode |
| Sec | RolePermission | IX_RolePermission_PermissionId | NONCLUSTERED | False | False | False |  | 1 | False | PermissionId |
| Sec | RolePermission | IX_RolePermission_RoleId | NONCLUSTERED | False | False | False |  | 1 | False | RoleId |
| Sec | RolePermission | PK_RolePermission | CLUSTERED | True | True | False |  | 1 | False | RolePermissionId |
| Sec | RolePermission | UX_RolePermission_RoleId_PermissionId | NONCLUSTERED | True | False | False |  | 1 | False | RoleId |
| Sec | RolePermission | UX_RolePermission_RoleId_PermissionId | NONCLUSTERED | True | False | False |  | 2 | False | PermissionId |
| Sec | User | PK_User | CLUSTERED | True | True | False |  | 1 | False | UserId |
| Sec | User | UQ_User_Username | NONCLUSTERED | True | False | False |  | 1 | False | Username |
| Sec | UserPermission | IX_UserPermission_PermissionId | NONCLUSTERED | False | False | False |  | 1 | False | PermissionId |
| Sec | UserPermission | PK_UserPermission | CLUSTERED | True | True | False |  | 1 | False | UserPermissionId |
| Sec | UserPermission | UQ_UserPermission_UserId_PermissionId | NONCLUSTERED | True | False | False |  | 1 | False | UserId |
| Sec | UserPermission | UQ_UserPermission_UserId_PermissionId | NONCLUSTERED | True | False | False |  | 2 | False | PermissionId |
| Sec | UserRole | IX_UserRole_RoleId | NONCLUSTERED | False | False | False |  | 1 | False | RoleId |
| Sec | UserRole | IX_UserRole_UserId | NONCLUSTERED | False | False | False |  | 1 | False | UserId |
| Sec | UserRole | PK_UserRole | CLUSTERED | True | True | False |  | 1 | False | UserRoleId |
| Sec | UserRole | UQ_UserRole_UserId_RoleId | NONCLUSTERED | True | False | False |  | 1 | False | UserId |
| Sec | UserRole | UQ_UserRole_UserId_RoleId | NONCLUSTERED | True | False | False |  | 2 | False | RoleId |

## Result 6

| name | SchemaName | TableName | ColumnName | ReferencedSchema | ReferencedTable | ReferencedColumn | delete_referential_action_desc | is_disabled | is_not_trusted |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FK_Notification_Application | Notify | Notification | ApplicationId | App | Application | ApplicationId | NO_ACTION | False | False |
| FK_Notification_CreatedBy | Notify | Notification | CreatedBy | Sec | User | UserId | NO_ACTION | False | False |
| FK_Publication_User_CreatedBy | Notify | Publication | CreatedBy | Sec | User | UserId | NO_ACTION | False | False |
| FK_Publication_User_UpdatedBy | Notify | Publication | UpdatedBy | Sec | User | UserId | NO_ACTION | False | False |
| FK_UserNotification_Notification | Notify | UserNotification | NotificationId | Notify | Notification | NotificationId | NO_ACTION | False | False |
| FK_UserNotification_User | Notify | UserNotification | UserId | Sec | User | UserId | NO_ACTION | False | False |
| FK_AuthenticationChallenge_User_UserId | Sec | AuthenticationChallenge | UserId | Sec | User | UserId | NO_ACTION | False | False |
| FK_Permission_Application | Sec | Permission | ApplicationId | App | Application | ApplicationId | NO_ACTION | False | False |
| FK_RolePermission_Permission | Sec | RolePermission | PermissionId | Sec | Permission | PermissionId | NO_ACTION | False | False |
| FK_RolePermission_Role | Sec | RolePermission | RoleId | Sec | Role | RoleId | NO_ACTION | False | False |
| FK_UserPermission_Permission | Sec | UserPermission | PermissionId | Sec | Permission | PermissionId | NO_ACTION | False | False |
| FK_UserPermission_User | Sec | UserPermission | UserId | Sec | User | UserId | NO_ACTION | False | False |
| FK_UserRole_Role | Sec | UserRole | RoleId | Sec | Role | RoleId | NO_ACTION | False | False |
| FK_UserRole_User | Sec | UserRole | UserId | Sec | User | UserId | NO_ACTION | False | False |

## Result 7

| SchemaName | TableName | name | definition | is_disabled | is_not_trusted |
| --- | --- | --- | --- | --- | --- |
| Notify | Publication | CK_Publication_PublishState | ([IsPublished]=(0) AND [PublishedAt] IS NULL OR [IsPublished]=(1) AND [PublishedAt] IS NOT NULL) | False | False |
| Sec | User | CK_User_AuthenticationCounters | ([FailedLoginAttempts]>=(0) AND [LastAuthenticatorStep]>=(-1)) | False | False |
| Sec | AuthenticationSettings | CK_AuthenticationSettings_Singleton | ([Id]=(1)) | False | False |
| Sec | AuthenticationSettings | CK_AuthenticationSettings_Json | (isjson([Json])=(1) AND datalength([Json])<=(20000)) | False | False |
| Sec | AuthenticationChallenge | CK_AuthenticationChallenge_Attempts | ([Attempts]>=(0)) | False | False |
| Sec | AuthenticationChallenge | CK_AuthenticationChallenge_Purpose | ([Purpose]='enroll' OR [Purpose]='reset' OR [Purpose]='login') | False | False |
| Sec | AuthenticationChallenge | CK_AuthenticationChallenge_LoginMethod | ([LoginMethod]='ldap' OR [LoginMethod]='password') | False | False |
| Notify | UserNotification | CK_UserNotification_ReadState | ([IsRead]=(0) AND [ReadAt] IS NULL OR [IsRead]=(1) AND [ReadAt] IS NOT NULL) | False | False |
| Notify | Publication | CK_Publication_Kind | ([Kind]='Circular' OR [Kind]='Announcement' OR [Kind]='News') | False | False |

## Result 8

| SchemaName | name | type_desc |
| --- | --- | --- |
| dbo | fn_diagramobjects | SQL_SCALAR_FUNCTION |
| dbo | sp_alterdiagram | SQL_STORED_PROCEDURE |
| dbo | sp_creatediagram | SQL_STORED_PROCEDURE |
| dbo | sp_dropdiagram | SQL_STORED_PROCEDURE |
| dbo | sp_helpdiagramdefinition | SQL_STORED_PROCEDURE |
| dbo | sp_helpdiagrams | SQL_STORED_PROCEDURE |
| dbo | sp_renamediagram | SQL_STORED_PROCEDURE |
| dbo | sp_upgraddiagrams | SQL_STORED_PROCEDURE |

## Result 9

| SchemaName | ObjectName | referenced_schema_name | referenced_entity_name | is_schema_bound_reference |
| --- | --- | --- | --- | --- |
| dbo | sp_upgraddiagrams | dbo | dtproperties | False |
| dbo | sp_helpdiagrams |  | sysdiagrams | False |
| dbo | sp_upgraddiagrams | dbo | sysdiagrams | False |
| dbo | sp_helpdiagramdefinition | dbo | sysdiagrams | False |
| dbo | sp_creatediagram | dbo | sysdiagrams | False |
| dbo | sp_renamediagram | dbo | sysdiagrams | False |
| dbo | sp_alterdiagram | dbo | sysdiagrams | False |
| dbo | sp_dropdiagram | dbo | sysdiagrams | False |
| Sec | CK_AuthenticationSettings_Singleton | Sec | AuthenticationSettings | True |
| Sec | CK_AuthenticationSettings_Json | Sec | AuthenticationSettings | True |
| Sec | CK_AuthenticationChallenge_Purpose | Sec | AuthenticationChallenge | True |
| Sec | CK_AuthenticationChallenge_LoginMethod | Sec | AuthenticationChallenge | True |
| Sec | CK_AuthenticationChallenge_Attempts | Sec | AuthenticationChallenge | True |
| Notify | CK_UserNotification_ReadState | Notify | UserNotification | True |
| Notify | CK_UserNotification_ReadState | Notify | UserNotification | True |
| Sec | CK_User_AuthenticationCounters | Sec | User | True |
| Sec | CK_User_AuthenticationCounters | Sec | User | True |
| Notify | CK_Publication_Kind | Notify | Publication | True |
| Notify | CK_Publication_PublishState | Notify | Publication | True |
| Notify | CK_Publication_PublishState | Notify | Publication | True |

## Result 10

| type_desc | permission_name | state_desc | PermissionEntries |
| --- | --- | --- | --- |
| DATABASE_ROLE | EXECUTE | GRANT | 7 |
| DATABASE_ROLE | SELECT | GRANT | 186 |
| DATABASE_ROLE | VIEW ANY COLUMN ENCRYPTION KEY DEFINITION | GRANT | 1 |
| DATABASE_ROLE | VIEW ANY COLUMN MASTER KEY DEFINITION | GRANT | 1 |
| SQL_USER | EXECUTE | DENY | 7 |
| WINDOWS_USER | CONNECT | GRANT | 1 |

## Result 11

| Metric | Total |
| --- | --- |
| UsersWithoutPassword | 0 |
| InactiveUsers | 0 |
| InactiveRoleAssignments | 0 |
| InvalidNotificationReadState | 0 |

## Result 12

| Metric | GroupsFound |
| --- | --- |
| DuplicateTrimmedUsernames | 0 |
| DuplicateNonEmptyNationalIds | 0 |
| DuplicateNonEmptyMobiles | 0 |

## Result 13

| name | type_desc | SizeMB | is_percent_growth | growth |
| --- | --- | --- | --- | --- |
| OrganizationIntranet | ROWS | 8 | False | 8192 |
| OrganizationIntranet_log | LOG | 8 | False | 8192 |

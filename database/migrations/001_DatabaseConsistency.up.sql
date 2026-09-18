-- Upgrade the inspected legacy schema. No business rows are changed or removed.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @lock int;
    EXEC @lock=sys.sp_getapplock @Resource=N'OrganizationIntranet.SchemaMigration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
    IF @lock<0 THROW 51000,'Cannot acquire migration lock.',1;
    IF OBJECT_ID(N'dbo.SchemaMigration',N'U') IS NULL
        CREATE TABLE dbo.SchemaMigration (
            MigrationId varchar(150) NOT NULL CONSTRAINT PK_SchemaMigration PRIMARY KEY,
            AppliedAt datetime2(7) NOT NULL CONSTRAINT DF_SchemaMigration_AppliedAt DEFAULT SYSUTCDATETIME()
        );
    IF EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId='001_DatabaseConsistency')
    BEGIN
        COMMIT;
        SELECT 'Already applied' AS MigrationStatus;
        RETURN;
    END;

    DECLARE @renames TABLE (SchemaName sysname,TableName sysname,OldName sysname,NewName sysname,ObjectType varchar(10));
    INSERT @renames VALUES
    ('App','Application','UQ_Application_Code','UQ_Application_ApplicationCode','OBJECT'),
    ('Sec','Role','UQ_Role_Code','UQ_Role_RoleCode','OBJECT'),
    ('Sec','Permission','UQ_Permission_Code','UQ_Permission_ApplicationId_PermissionCode','OBJECT'),
    ('Sec','UserRole','UQ_UserRole','UQ_UserRole_UserId_RoleId','OBJECT'),
    ('Sec','UserPermission','UQ_UserPermission','UQ_UserPermission_UserId_PermissionId','OBJECT'),
    ('Notify','UserNotification','UQ_UserNotification','UQ_UserNotification_NotificationId_UserId','OBJECT'),
    ('Sec','RolePermission','UX_RolePermission_Role_Permission','UX_RolePermission_RoleId_PermissionId','INDEX');
    DECLARE @schema sysname,@table sysname,@old sysname,@new sysname,@type varchar(10),@qualified nvarchar(776);
    DECLARE names CURSOR LOCAL FAST_FORWARD FOR SELECT * FROM @renames;
    OPEN names;
    FETCH NEXT FROM names INTO @schema,@table,@old,@new,@type;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(QUOTENAME(@schema)+'.'+QUOTENAME(@table)) AND name=@old)
            THROW 51001,'Expected legacy index/constraint is missing. Inspect schema drift before retrying.',1;
        SET @qualified=QUOTENAME(@schema)+'.'+CASE WHEN @type='INDEX' THEN QUOTENAME(@table)+'.' ELSE '' END+QUOTENAME(@old);
        EXEC sys.sp_rename @objname=@qualified,@newname=@new,@objtype=@type;
        FETCH NEXT FROM names INTO @schema,@table,@old,@new,@type;
    END;
    CLOSE names;
    DEALLOCATE names;

    -- Drop only exact duplicate indexes; never remove a referenced key or an index with extra columns/filter.
    DECLARE @duplicates TABLE (SchemaName sysname,TableName sysname,IndexName sysname,ColumnName sysname,ConstraintName sysname);
    INSERT @duplicates VALUES
        ('App','Application','UX_Application_ApplicationCode','ApplicationCode','UQ_Application_ApplicationCode'),
        ('Sec','Role','UX_Role_RoleCode','RoleCode','UQ_Role_RoleCode');
    DECLARE @column sysname,@constraint sysname,@objectId int,@indexId int,@sql nvarchar(max);
    DECLARE duplicates CURSOR LOCAL FAST_FORWARD FOR SELECT * FROM @duplicates;
    OPEN duplicates;
    FETCH NEXT FROM duplicates INTO @schema,@table,@old,@column,@constraint;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @objectId=OBJECT_ID(QUOTENAME(@schema)+'.'+QUOTENAME(@table));
        SET @indexId=NULL;
        SELECT @indexId=index_id FROM sys.indexes WHERE object_id=@objectId AND name=@old AND is_unique=1 AND is_unique_constraint=0 AND has_filter=0 AND is_disabled=0 AND type=2;
        IF @indexId IS NULL OR (SELECT COUNT(*) FROM sys.index_columns WHERE object_id=@objectId AND index_id=@indexId)<>1
            THROW 51002,'Duplicate index definition differs from reviewed schema.',1;
        IF NOT EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=@objectId AND ic.index_id=@indexId AND ic.key_ordinal=1 AND c.name=@column)
            THROW 51002,'Duplicate index has an unexpected key.',1;
        IF NOT EXISTS (SELECT 1 FROM sys.key_constraints k JOIN sys.indexes i ON i.object_id=k.parent_object_id AND i.index_id=k.unique_index_id JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE k.parent_object_id=@objectId AND k.name=@constraint AND k.type='UQ' AND i.is_disabled=0 AND ic.key_ordinal=1 AND c.name=@column AND (SELECT COUNT(*) FROM sys.index_columns x WHERE x.object_id=i.object_id AND x.index_id=i.index_id)=1)
            THROW 51002,'Replacement unique constraint is not valid.',1;
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id=@objectId AND key_index_id=@indexId)
            THROW 51003,'Duplicate index is referenced by a foreign key.',1;
        SET @sql=N'DROP INDEX '+QUOTENAME(@old)+N' ON '+QUOTENAME(@schema)+N'.'+QUOTENAME(@table);
        EXEC sys.sp_executesql @sql;
        FETCH NEXT FROM duplicates INTO @schema,@table,@old,@column,@constraint;
    END;
    CLOSE duplicates;
    DEALLOCATE duplicates;

    CREATE INDEX IX_UserPermission_PermissionId ON Sec.UserPermission(PermissionId);
    CREATE INDEX IX_Notification_ApplicationId ON Notify.Notification(ApplicationId);
    CREATE INDEX IX_Notification_CreatedBy ON Notify.Notification(CreatedBy);
    CREATE INDEX IX_UserNotification_UserId_IsRead_CreatedAt ON Notify.UserNotification(UserId,IsRead,CreatedAt);
    ALTER TABLE Notify.UserNotification WITH CHECK ADD CONSTRAINT CK_UserNotification_ReadState
        CHECK ((IsRead=0 AND ReadAt IS NULL) OR (IsRead=1 AND ReadAt IS NOT NULL));
    ALTER TABLE Sec.RolePermission DROP CONSTRAINT DF_RolePermission_CreatedAt;
    ALTER TABLE Sec.RolePermission ADD CONSTRAINT DF_RolePermission_CreatedAt DEFAULT GETUTCDATE() FOR CreatedAt;
    INSERT dbo.SchemaMigration(MigrationId) VALUES ('001_DatabaseConsistency');
    COMMIT;
    SELECT 'Applied' AS MigrationStatus;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;

SELECT CASE WHEN OBJECT_ID(N'Notify.Publication', N'U') IS NOT NULL THEN 1 ELSE 0 END AS TableExists;
SELECT COUNT(*) AS ColumnCount FROM sys.columns WHERE object_id = OBJECT_ID(N'Notify.Publication');
SELECT COUNT(*) AS ForeignKeyCount FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'Notify.Publication');
SELECT COUNT(*) AS SecondaryIndexCount FROM sys.indexes WHERE object_id = OBJECT_ID(N'Notify.Publication') AND is_primary_key = 0 AND index_id > 0;
SELECT COUNT_BIG(*) AS PublicationCount FROM [Notify].[Publication];
SELECT name, is_disabled, is_not_trusted FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'Notify.Publication');
SELECT name, is_nullable FROM sys.columns WHERE object_id = OBJECT_ID(N'Notify.Publication') AND name = N'Revision';

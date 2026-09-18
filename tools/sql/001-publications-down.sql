-- Deploy the preceding application version first. Export any publication data before rollback.
-- By default refuse to remove a table containing content.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'Notify.Publication', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM [Notify].[Publication])
        THROW 50001, 'Publication contains data. Export and explicitly remove the data before rollback.', 1;
    DROP TABLE [Notify].[Publication];
END;
COMMIT;

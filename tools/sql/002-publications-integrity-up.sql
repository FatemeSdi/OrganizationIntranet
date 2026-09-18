-- Apply after 001-publications-up.sql, before deploying the updated API and Admin.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'Notify.Publication', N'U') IS NULL
    THROW 51000, 'Apply publication migration 001 first.', 1;
IF COL_LENGTH(N'Notify.Publication', N'Revision') IS NULL
    ALTER TABLE [Notify].[Publication] ADD [Revision] bigint NOT NULL
        CONSTRAINT [DF_Publication_Revision] DEFAULT (0) WITH VALUES;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'Notify.Publication') AND name = N'CK_Publication_Kind')
    ALTER TABLE [Notify].[Publication] WITH CHECK ADD CONSTRAINT [CK_Publication_Kind]
        CHECK ([Kind] IN ('News', 'Announcement', 'Circular'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'Notify.Publication') AND name = N'CK_Publication_PublishState')
    ALTER TABLE [Notify].[Publication] WITH CHECK ADD CONSTRAINT [CK_Publication_PublishState]
        CHECK (([IsPublished] = 0 AND [PublishedAt] IS NULL) OR ([IsPublished] = 1 AND [PublishedAt] IS NOT NULL));
COMMIT;

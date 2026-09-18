-- Restore the previous application version before running. Publication content is retained.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'Notify.CK_Publication_PublishState', N'C') IS NOT NULL
    ALTER TABLE [Notify].[Publication] DROP CONSTRAINT [CK_Publication_PublishState];
IF OBJECT_ID(N'Notify.CK_Publication_Kind', N'C') IS NOT NULL
    ALTER TABLE [Notify].[Publication] DROP CONSTRAINT [CK_Publication_Kind];
IF OBJECT_ID(N'Notify.DF_Publication_Revision', N'D') IS NOT NULL
    ALTER TABLE [Notify].[Publication] DROP CONSTRAINT [DF_Publication_Revision];
IF COL_LENGTH(N'Notify.Publication', N'Revision') IS NOT NULL
    ALTER TABLE [Notify].[Publication] DROP COLUMN [Revision];
COMMIT;

-- Run against the existing OrganizationIntranet database before deploying the updated API/UI.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF SCHEMA_ID(N'Notify') IS NULL EXEC(N'CREATE SCHEMA [Notify]');
IF OBJECT_ID(N'Notify.Publication', N'U') IS NULL
BEGIN
    CREATE TABLE [Notify].[Publication] (
        [PublicationId] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Publication] PRIMARY KEY,
        [Title] nvarchar(200) NOT NULL,
        [Summary] nvarchar(1000) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [IsPublished] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [PublishedAt] datetime2 NULL,
        [CreatedBy] bigint NOT NULL,
        [UpdatedBy] bigint NOT NULL,
        CONSTRAINT [FK_Publication_User_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Sec].[User]([UserId]),
        CONSTRAINT [FK_Publication_User_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [Sec].[User]([UserId])
    );
    CREATE INDEX [IX_Publication_IsPublished_PublishedAt_PublicationId] ON [Notify].[Publication] ([IsPublished], [PublishedAt], [PublicationId]);
    CREATE INDEX [IX_Publication_Kind] ON [Notify].[Publication] ([Kind]);
    CREATE INDEX [IX_Publication_CreatedBy] ON [Notify].[Publication] ([CreatedBy]);
    CREATE INDEX [IX_Publication_UpdatedBy] ON [Notify].[Publication] ([UpdatedBy]);
END;
COMMIT;

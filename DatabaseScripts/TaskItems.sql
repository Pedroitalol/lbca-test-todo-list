CREATE TABLE [TaskItems] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [DueDate] datetime2 NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Priority] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_TaskItems] PRIMARY KEY ([Id])
);
GO

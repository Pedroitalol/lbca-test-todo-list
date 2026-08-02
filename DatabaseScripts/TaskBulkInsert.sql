-- ===========================================================================
-- File:    TaskBulkInsert.sql
-- Purpose: SQL Server objects required for the high-performance batch import.
--
-- Objects created / updated:
--   1. dbo.TaskImportType         — User-Defined Table Type (TVP schema)
--   2. dbo.sp_InsertTaskBatch     — Stored Procedure: set-based INSERT with
--                                   WHERE NOT EXISTS duplicate guard
--   3. IX_TaskItems_Title_Unique  — Unique non-clustered index on Title
--                                   (enforces uniqueness at the DB layer,
--                                    fixes race conditions between concurrent imports)
--
-- Execution: Run once on the target database. Re-run is safe (IF NOT EXISTS
-- guards are used). No data is modified by running this script.
-- ===========================================================================

USE [YOUR_DATABASE_NAME]; -- ← replace with your actual database name
GO

-- ---------------------------------------------------------------------------
-- 1. User-Defined Table Type: dbo.TaskImportType
--    Matches exactly the columns sent by TaskRepository.BuildTaskDataTable().
-- ---------------------------------------------------------------------------
IF TYPE_ID(N'dbo.TaskImportType') IS NULL
BEGIN
    CREATE TYPE dbo.TaskImportType AS TABLE
    (
        Id          UNIQUEIDENTIFIER NOT NULL,
        Title       NVARCHAR(100)    NOT NULL,
        Description NVARCHAR(500)    NULL,
        DueDate     DATETIME2        NOT NULL,
        [Status]    NVARCHAR(50)     NOT NULL,
        Priority    NVARCHAR(50)     NOT NULL
    );
    PRINT 'Created type: dbo.TaskImportType';
END
ELSE
BEGIN
    PRINT 'Type dbo.TaskImportType already exists — skipped.';
END
GO

-- ---------------------------------------------------------------------------
-- 2. Stored Procedure: dbo.sp_InsertTaskBatch
--    Receives a TVP of type TaskImportType and performs a single set-based
--    INSERT into TaskItems using WHERE NOT EXISTS as a final duplicate guard.
--
--    This INSERT ... WHERE NOT EXISTS is the last line of defence against:
--      a) race conditions between two concurrent imports;
--      b) rows that slipped past the in-application duplicate check.
--
--    The caller (TaskRepository.BulkInsertBatchAsync) enrols this call
--    inside an external SqlTransaction, so this SP does NOT commit or
--    manage its own transaction.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.sp_InsertTaskBatch', 'P') IS NULL
BEGIN
    EXEC('CREATE PROCEDURE dbo.sp_InsertTaskBatch AS BEGIN SET NOCOUNT ON; END');
    PRINT 'Stub created: dbo.sp_InsertTaskBatch (will be replaced below)';
END
GO

ALTER PROCEDURE dbo.sp_InsertTaskBatch
    @TaskRows dbo.TaskImportType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    -- INSERT ... WHERE NOT EXISTS: set-based duplicate guard.
    -- If another import committed a row with the same Title between the
    -- application-level check and this INSERT, the conflicting row is
    -- silently skipped instead of raising a PK/UNIQUE violation.
    INSERT INTO dbo.TaskItems
        (Id, Title, Description, DueDate, [Status], Priority)
    SELECT
        tr.Id,
        tr.Title,
        tr.Description,
        tr.DueDate,
        tr.[Status],
        tr.Priority
    FROM
        @TaskRows AS tr
    WHERE NOT EXISTS (
        SELECT 1
        FROM   dbo.TaskItems existing
        WHERE  existing.Title = tr.Title COLLATE Latin1_General_CI_AS
    );
END;
GO

PRINT 'Procedure dbo.sp_InsertTaskBatch created/updated successfully.';
GO

-- ---------------------------------------------------------------------------
-- 3. Unique index on TaskItems.Title
--    Enforces uniqueness at the database level regardless of which path
--    the insert came from (import, API, direct SQL).
--    Run AFTER any existing duplicates have been cleaned up.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  object_id = OBJECT_ID(N'dbo.TaskItems')
    AND    name      = N'IX_TaskItems_Title_Unique'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_TaskItems_Title_Unique
        ON dbo.TaskItems (Title ASC);
    PRINT 'Created index: IX_TaskItems_Title_Unique';
END
ELSE
BEGIN
    PRINT 'Index IX_TaskItems_Title_Unique already exists — skipped.';
END
GO
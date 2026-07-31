-- 1. Cria a estrutura (TVP) idêntica às colunas da aplicação
CREATE TYPE dbo.TaskImportType AS TABLE
(
    Id          UNIQUEIDENTIFIER NOT NULL,
    Title       NVARCHAR(100)    NOT NULL,
    Description NVARCHAR(500)    NULL,
    DueDate     DATETIME2        NOT NULL,
    [Status]    NVARCHAR(50)     NOT NULL,
    Priority    NVARCHAR(50)     NOT NULL
);
GO

-- 2. Stored Procedure que recebe a matriz de 10.000 linhas via memória do SQL e as descarrega na tabela
CREATE PROCEDURE dbo.sp_InsertTaskBatch
    @TaskRows dbo.TaskImportType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.TaskItems
        (Id, Title, Description, DueDate, Status, Priority)
    SELECT
        tr.Id, tr.Title, tr.Description, tr.DueDate, tr.[Status], tr.Priority
    FROM
        @TaskRows AS tr;
END;
GO
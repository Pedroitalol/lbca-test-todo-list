using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.Domain.Entities;
using TbcaTest.Domain.Enums;
using TbcaTest.Infra.Contexts;
using TaskStatus = TbcaTest.Domain.Enums.TaskStatus;

namespace TbcaTest.Infra.Data.Repository
{
    public class TaskRepository : ITaskRepository
    {
        private readonly TbcaTestContext _context;

        public TaskRepository(TbcaTestContext context)
        {
            _context = context;
        }

        public async Task<TaskItem?> GetByIdAsync(Guid id)
        {
            return await _context.TaskItems.FindAsync(id);
        }

        public async Task<IEnumerable<TaskItem>> GetPagedTasksAsync(int pageNumber, int pageSize)
        {
            return await _context.TaskItems
                .AsNoTracking()
                .OrderBy(t => t.DueDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<bool> ExistsByTitleAsync(string title)
        {
            return await _context.TaskItems.AnyAsync(t => t.Title == title);
        }

        public async Task<IEnumerable<string>> GetExistingTitlesFromBatchAsync(IEnumerable<string> titles)
        {
            var titleList = titles.ToList();
            if (titleList.Count == 0)
                return Enumerable.Empty<string>();

            if (_context.Database.IsRelational() && _context.Database.GetDbConnection() is SqlConnection sqlConn)
            {
                if (sqlConn.State != System.Data.ConnectionState.Open)
                    await sqlConn.OpenAsync();

                var existingTitles = new List<string>();
                foreach (var chunk in titleList.Chunk(2000))
                {
                    var titlesInChunk = await sqlConn.QueryAsync<string>(
                        "SELECT Title FROM dbo.TaskItems WHERE Title IN @Titles",
                        new { Titles = chunk });
                    existingTitles.AddRange(titlesInChunk);
                }
                return existingTitles;
            }

            return await _context.TaskItems
                .AsNoTracking()
                .Where(t => titleList.Contains(t.Title))
                .Select(t => t.Title)
                .ToListAsync();
        }

        public async Task AddAsync(TaskItem task)
        {
            await _context.TaskItems.AddAsync(task);
        }

        public Task UpdateAsync(TaskItem task)
        {
            _context.TaskItems.Update(task);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(TaskItem task)
        {
            _context.TaskItems.Remove(task);
            return Task.CompletedTask;
        }

        public async Task BulkInsertBatchAsync(IEnumerable<TaskImportRow> rows, IDbTransaction? transaction)
        {
            if (transaction?.Connection is not SqlConnection sqlConnection)
            {
                var tasks = rows.Select(r =>
                {
                    var task = new TaskItem(r.Title, r.Description, r.DueDate, r.Priority);
                    typeof(TaskItem).GetProperty(nameof(TaskItem.Id))!.SetValue(task, r.Id);
                    if (Enum.TryParse<TaskStatus>(r.Status, out var status))
                        task.UpdateStatus(status);
                    return task;
                });
                await _context.TaskItems.AddRangeAsync(tasks);
                await _context.SaveChangesAsync();
                return;
            }

            var table = BuildTaskDataTable(rows);

            var parameters = new DynamicParameters();
            parameters.Add(
                "@TaskRows",
                table.AsTableValuedParameter("dbo.TaskImportType"),
                DbType.Object);

            await sqlConnection.ExecuteAsync(
                "dbo.sp_InsertTaskBatch",
                parameters,
                transaction: transaction,
                commandType: CommandType.StoredProcedure);
        }

        private static DataTable BuildTaskDataTable(IEnumerable<TaskImportRow> rows)
        {
            var table = new DataTable("TaskImportType");
            table.Columns.Add("Id",          typeof(Guid));
            table.Columns.Add("Title",       typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("DueDate",     typeof(DateTime));
            table.Columns.Add("Status",      typeof(string));
            table.Columns.Add("Priority",    typeof(string));

            foreach (var row in rows)
            {
                table.Rows.Add(
                    row.Id,
                    row.Title,
                    row.Description is null ? (object)DBNull.Value : row.Description,
                    row.DueDate,
                    row.Status,
                    row.Priority.ToString());
            }

            return table;
        }
    }
}

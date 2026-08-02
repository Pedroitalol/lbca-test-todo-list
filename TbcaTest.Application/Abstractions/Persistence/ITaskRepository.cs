using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Application.Abstractions.Persistence
{
    public interface ITaskRepository
    {
        Task<TaskItem?> GetByIdAsync(Guid id);
        Task<IEnumerable<TaskItem>> GetPagedTasksAsync(int pageNumber, int pageSize);
        Task<bool> ExistsByTitleAsync(string title);

        Task<IEnumerable<string>> GetExistingTitlesFromBatchAsync(IEnumerable<string> titles);
        Task AddAsync(TaskItem task);
        Task UpdateAsync(TaskItem task);
        Task DeleteAsync(TaskItem task);
        Task BulkInsertBatchAsync(IEnumerable<TaskImportRow> rows, IDbTransaction? transaction);
    }
}

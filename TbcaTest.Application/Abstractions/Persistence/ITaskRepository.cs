using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Application.Abstractions.Persistence
{
    public interface ITaskRepository
    {
        Task<TaskItem?> GetByIdAsync(Guid id);
        Task<IEnumerable<TaskItem>> GetPagedTasksAsync(int pageNumber, int pageSize);
        Task<bool> ExistsByTitleAsync(string title);
        Task AddAsync(TaskItem task);
        Task UpdateAsync(TaskItem task);
        Task DeleteAsync(TaskItem task);
    }
}

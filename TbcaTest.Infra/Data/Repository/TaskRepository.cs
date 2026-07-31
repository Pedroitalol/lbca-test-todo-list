using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Domain.Entities;
using TbcaTest.Infra.Contexts;

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
    }
}

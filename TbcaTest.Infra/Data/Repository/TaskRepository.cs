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

        public async Task AddAsync(TaskItem task)
        {
            await _context.TaskItems.AddAsync(task);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(TaskItem task)
        {
            _context.TaskItems.Update(task);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(TaskItem task)
        {
            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();
        }
    }
}

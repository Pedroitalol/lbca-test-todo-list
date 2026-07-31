using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly ITaskRepository _taskRepository;

        public TaskService(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }

        public async Task<IEnumerable<TaskResponse>> GetPagedTasksAsync(int pageNumber, int pageSize)
        {
            var tasks = await _taskRepository.GetPagedTasksAsync(pageNumber, pageSize);

            return tasks.Select(t => new TaskResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                DueDate = t.DueDate,
                Status = t.Status,
                Priority = t.Priority
            });
        }

        public async Task<TaskResponse?> GetByIdAsync(Guid id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task == null) return null;

            return new TaskResponse
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                DueDate = task.DueDate,
                Status = task.Status,
                Priority = task.Priority
            };
        }

        public async Task<TaskResponse> CreateTaskAsync(CreateTaskRequest request)
        {
            var task = new TaskItem(request.Title, request.Description, request.DueDate, request.Priority);

            await _taskRepository.AddAsync(task);

            return new TaskResponse
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                DueDate = task.DueDate,
                Status = task.Status,
                Priority = task.Priority
            };
        }

        public async Task UpdateTaskAsync(Guid id, UpdateTaskRequest request)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task != null)
            {
                task.Update(request.Title, request.Description, request.DueDate, request.Status, request.Priority);
                await _taskRepository.UpdateAsync(task);
            }
        }

        public async Task DeleteTaskAsync(Guid id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task != null)
            {
                await _taskRepository.DeleteAsync(task);
            }
        }
    }
}

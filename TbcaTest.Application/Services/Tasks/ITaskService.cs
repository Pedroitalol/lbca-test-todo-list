using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TbcaTest.Application.DTOs.Tasks;

namespace TbcaTest.Application.Services
{
    public interface ITaskService
    {
        Task<TaskResponse?> GetByIdAsync(Guid id);
        Task<IEnumerable<TaskResponse>> GetPagedTasksAsync(int pageNumber, int pageSize);
        Task<TaskResponse> CreateTaskAsync(CreateTaskRequest request);
        Task UpdateTaskAsync(Guid id, UpdateTaskRequest request);
        Task DeleteTaskAsync(Guid id);
    }
}

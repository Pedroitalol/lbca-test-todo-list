using System;
using TbcaTest.Domain.Entities;
using TbcaTest.Domain.Enums;
using TaskStatus = TbcaTest.Domain.Enums.TaskStatus;

namespace TbcaTest.Application.DTOs.Tasks
{
    public class UpdateTaskRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DueDate { get; set; }
        public TaskStatus Status { get; set; }
        public TaskPriority Priority { get; set; }
    }
}

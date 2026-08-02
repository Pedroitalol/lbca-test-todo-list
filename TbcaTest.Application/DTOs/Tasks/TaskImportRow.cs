using System;
using TbcaTest.Domain.Enums;

namespace TbcaTest.Application.DTOs.Tasks
{
    public sealed class TaskImportRow
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public int RowNumber { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public DateTime DueDate { get; init; }
        public TaskPriority Priority { get; init; }
        public string Status { get; init; } = "Pending";
    }
}

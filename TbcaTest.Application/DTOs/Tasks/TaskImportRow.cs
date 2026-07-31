using System;
using TbcaTest.Domain.Enums;

namespace TbcaTest.Application.DTOs.Tasks
{
    /// <summary>
    /// Lightweight, validated row data produced by the Excel parser.
    /// Lives only in memory between validation and DB insertion — avoids
    /// creating full <see cref="TbcaTest.Domain.Entities.TaskItem"/> entities
    /// until the row is actually committed.
    /// </summary>
    public sealed class TaskImportRow
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public DateTime DueDate { get; init; }
        public TaskPriority Priority { get; init; }
        public string Status { get; init; } = "Pending"; // Always "Pending" on import
    }
}

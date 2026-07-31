using System;
using TbcaTest.Domain.Enums;
using TaskStatus = TbcaTest.Domain.Enums.TaskStatus;

namespace TbcaTest.Domain.Entities;


public class TaskItem
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime DueDate { get; private set; }
    public TaskStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }

    public TaskItem() { }

    public TaskItem(string title, string? description, DateTime dueDate, TaskPriority priority)
    {
        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        DueDate = dueDate;
        Priority = priority;
        Status = TaskStatus.Pending;
    }

    public void Update(string title, string? description, DateTime dueDate, TaskStatus status, TaskPriority priority)
    {
        Title = title;
        Description = description;
        DueDate = dueDate;
        Status = status;
        Priority = priority;
    }

    public void UpdateStatus(TaskStatus newStatus)
    {
        Status = newStatus;
    }
}
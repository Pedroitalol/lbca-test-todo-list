using System;
using TbcaTest.Domain.Entities;
using TbcaTest.Domain.Enums;

namespace TbcaTest.Tests.TestHelpers;

public class TaskItemBuilder
{
    private string _title = "Tarefa de Teste";
    private string? _description = "Descrição de teste automatizado";
    private DateTime _dueDate = DateTime.UtcNow.AddDays(7);
    private TaskPriority _priority = TaskPriority.Medium;

    public TaskItemBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TaskItemBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public TaskItemBuilder WithDueDate(DateTime dueDate)
    {
        _dueDate = dueDate;
        return this;
    }

    public TaskItemBuilder WithPriority(TaskPriority priority)
    {
        _priority = priority;
        return this;
    }

    public TaskItem Build()
    {
        return new TaskItem(_title, _description, _dueDate, _priority);
    }
}

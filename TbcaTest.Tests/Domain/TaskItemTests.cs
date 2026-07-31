using System;
using FluentAssertions;
using TbcaTest.Domain.Entities;
using TbcaTest.Domain.Enums;
using TbcaTest.Tests.TestHelpers;
using Xunit;
using TaskStatus = TbcaTest.Domain.Enums.TaskStatus;

namespace TbcaTest.Tests.Domain;

public class TaskItemTests
{
    // ─────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldGenerateNonEmptyId()
    {
        var task = new TaskItemBuilder().Build();
        task.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueIds_AcrossMultipleInstances()
    {
        var task1 = new TaskItemBuilder().Build();
        var task2 = new TaskItemBuilder().Build();
        task1.Id.Should().NotBe(task2.Id);
    }

    [Fact]
    public void Constructor_ShouldSetDefaultStatusToPending()
    {
        var task = new TaskItemBuilder().Build();
        task.Status.Should().Be(TaskStatus.Pending);
    }

    [Fact]
    public void Constructor_ShouldPreserveTitle()
    {
        var task = new TaskItemBuilder().WithTitle("My Task Title").Build();
        task.Title.Should().Be("My Task Title");
    }

    [Fact]
    public void Constructor_ShouldPreserveDescription()
    {
        var task = new TaskItemBuilder().WithDescription("A detailed description").Build();
        task.Description.Should().Be("A detailed description");
    }

    [Fact]
    public void Constructor_ShouldAllowNullDescription()
    {
        var task = new TaskItemBuilder().WithDescription(null).Build();
        task.Description.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldPreserveDueDate()
    {
        var dueDate = new DateTime(2030, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var task = new TaskItemBuilder().WithDueDate(dueDate).Build();
        task.DueDate.Should().Be(dueDate);
    }

    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Medium)]
    [InlineData(TaskPriority.High)]
    public void Constructor_ShouldSetCorrectPriority(TaskPriority priority)
    {
        var task = new TaskItemBuilder().WithPriority(priority).Build();
        task.Priority.Should().Be(priority);
    }

    // ─────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void Update_ShouldChangeTitle()
    {
        var task = new TaskItemBuilder().WithTitle("Old Title").Build();
        task.Update("New Title", null, DateTime.UtcNow.AddDays(1), TaskStatus.Pending, TaskPriority.Low);
        task.Title.Should().Be("New Title");
    }

    [Fact]
    public void Update_ShouldChangeDescription()
    {
        var task = new TaskItemBuilder().WithDescription("Old Desc").Build();
        task.Update("Title", "New Desc", DateTime.UtcNow.AddDays(1), TaskStatus.Pending, TaskPriority.Low);
        task.Description.Should().Be("New Desc");
    }

    [Fact]
    public void Update_ShouldAllowClearingDescriptionToNull()
    {
        var task = new TaskItemBuilder().WithDescription("Has a description").Build();
        task.Update("Title", null, DateTime.UtcNow.AddDays(1), TaskStatus.Pending, TaskPriority.Low);
        task.Description.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldChangeDueDate()
    {
        var task = new TaskItemBuilder().Build();
        var newDate = new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        task.Update("Title", null, newDate, TaskStatus.Pending, TaskPriority.Low);
        task.DueDate.Should().Be(newDate);
    }

    [Theory]
    [InlineData(TaskStatus.Pending)]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Completed)]
    public void Update_ShouldChangeStatus(TaskStatus newStatus)
    {
        var task = new TaskItemBuilder().Build();
        task.Update("Title", null, DateTime.UtcNow.AddDays(1), newStatus, TaskPriority.Low);
        task.Status.Should().Be(newStatus);
    }

    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Medium)]
    [InlineData(TaskPriority.High)]
    public void Update_ShouldChangePriority(TaskPriority newPriority)
    {
        var task = new TaskItemBuilder().Build();
        task.Update("Title", null, DateTime.UtcNow.AddDays(1), TaskStatus.Pending, newPriority);
        task.Priority.Should().Be(newPriority);
    }

    [Fact]
    public void Update_ShouldNotChangeId()
    {
        var task = new TaskItemBuilder().Build();
        var originalId = task.Id;
        task.Update("New Title", "New Desc", DateTime.UtcNow.AddDays(10), TaskStatus.InProgress, TaskPriority.High);
        task.Id.Should().Be(originalId);
    }

    [Fact]
    public void Update_ShouldModifyAllFields_InOneCall()
    {
        var task = new TaskItemBuilder()
            .WithTitle("Old Title")
            .WithDescription("Old Desc")
            .WithDueDate(DateTime.UtcNow.AddDays(1))
            .WithPriority(TaskPriority.Low)
            .Build();

        var newDate = DateTime.UtcNow.AddDays(10);
        task.Update("New Title", "New Desc", newDate, TaskStatus.Completed, TaskPriority.High);

        task.Title.Should().Be("New Title");
        task.Description.Should().Be("New Desc");
        task.DueDate.Should().Be(newDate);
        task.Status.Should().Be(TaskStatus.Completed);
        task.Priority.Should().Be(TaskPriority.High);
    }

    // ─────────────────────────────────────────────────────────
    // UpdateStatus
    // ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskStatus.Pending)]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Completed)]
    public void UpdateStatus_ShouldSetExactlyTheGivenStatus(TaskStatus newStatus)
    {
        var task = new TaskItemBuilder().Build();
        task.UpdateStatus(newStatus);
        task.Status.Should().Be(newStatus);
    }

    [Fact]
    public void UpdateStatus_ShouldNotModifyOtherFields()
    {
        var dueDate = DateTime.UtcNow.AddDays(5);
        var task = new TaskItemBuilder()
            .WithTitle("My Title")
            .WithDescription("My Description")
            .WithDueDate(dueDate)
            .WithPriority(TaskPriority.High)
            .Build();

        var originalId = task.Id;

        task.UpdateStatus(TaskStatus.Completed);

        task.Id.Should().Be(originalId);
        task.Title.Should().Be("My Title");
        task.Description.Should().Be("My Description");
        task.DueDate.Should().Be(dueDate);
        task.Priority.Should().Be(TaskPriority.High);
    }

    [Fact]
    public void UpdateStatus_CalledTwice_ShouldReflectLatestStatus()
    {
        var task = new TaskItemBuilder().Build();
        task.UpdateStatus(TaskStatus.InProgress);
        task.UpdateStatus(TaskStatus.Completed);
        task.Status.Should().Be(TaskStatus.Completed);
    }
}

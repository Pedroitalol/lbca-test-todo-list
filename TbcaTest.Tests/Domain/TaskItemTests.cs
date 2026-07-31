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
    [Fact]
    public void Constructor_ShouldInitializeTaskWithDefaultPendingStatus()
    {
        // Arrange & Act
        var task = new TaskItemBuilder().Build();

        // Assert
        task.Id.Should().NotBeEmpty();
        task.Title.Should().NotBeNullOrWhiteSpace();
        task.Status.Should().Be(TaskStatus.Pending);
    }

    [Fact]
    public void Constructor_ShouldAllowNullDescription()
    {
        // Arrange & Act
        var task = new TaskItemBuilder()
            .WithDescription(null)
            .Build();

        // Assert
        task.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Medium)]
    [InlineData(TaskPriority.High)]
    public void Constructor_ShouldSetCorrectPriority(TaskPriority priority)
    {
        // Arrange & Act
        var task = new TaskItemBuilder()
            .WithPriority(priority)
            .Build();

        // Assert
        task.Priority.Should().Be(priority);
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueId_ForMultipleInstances()
    {
        // Arrange & Act
        var task1 = new TaskItemBuilder().Build();
        var task2 = new TaskItemBuilder().Build();

        // Assert
        task1.Id.Should().NotBe(task2.Id);
    }

    [Fact]
    public void Update_ShouldModifyAllFields_AndKeepSameId()
    {
        // Arrange
        var task = new TaskItemBuilder().Build();
        var originalId = task.Id;

        var newTitle = "Título Atualizado";
        var newDescription = "Nova Descrição Detalhada";
        var newDueDate = DateTime.UtcNow.AddDays(10);
        var newStatus = TaskStatus.InProgress;
        var newPriority = TaskPriority.High;

        // Act
        task.Update(newTitle, newDescription, newDueDate, newStatus, newPriority);

        // Assert
        task.Id.Should().Be(originalId);
        task.Title.Should().Be(newTitle);
        task.Description.Should().Be(newDescription);
        task.DueDate.Should().Be(newDueDate);
        task.Status.Should().Be(newStatus);
        task.Priority.Should().Be(newPriority);
    }

    [Fact]
    public void Update_ShouldAllowSettingNullDescription()
    {
        // Arrange
        var task = new TaskItemBuilder()
            .WithDescription("Descrição Inicial")
            .Build();

        // Act
        task.Update("Título", null, DateTime.UtcNow.AddDays(1), TaskStatus.InProgress, TaskPriority.Low);

        // Assert
        task.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(TaskStatus.Pending)]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Completed)]
    public void UpdateStatus_ShouldChangeStatusToGivenState(TaskStatus newStatus)
    {
        // Arrange
        var task = new TaskItemBuilder().Build();

        // Act
        task.UpdateStatus(newStatus);

        // Assert
        task.Status.Should().Be(newStatus);
    }
}

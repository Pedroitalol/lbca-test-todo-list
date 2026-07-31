using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.Application.Services;
using TbcaTest.Domain.Entities;
using TbcaTest.Domain.Enums;
using TbcaTest.Domain.Exceptions;
using Xunit;
using TaskStatus = TbcaTest.Domain.Enums.TaskStatus;

namespace TbcaTest.Tests.Services;

public class TaskServiceTests
{
    private readonly Mock<ITaskRepository> _taskRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly TaskService _taskService;

    public TaskServiceTests()
    {
        _taskRepositoryMock = new Mock<ITaskRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _taskService = new TaskService(_taskRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task CreateTaskAsync_WhenTitleIsUnique_ShouldCreateSuccessfullyAndCommit()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            Title = "Unique Title",
            Description = "A valid description",
            DueDate = DateTime.UtcNow.AddDays(2),
            Priority = TaskPriority.High
        };

        _taskRepositoryMock.Setup(repo => repo.ExistsByTitleAsync(request.Title))
            .ReturnsAsync(false);

        // Act
        var result = await _taskService.CreateTaskAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(request.Title);

        _taskRepositoryMock.Verify(repo => repo.AddAsync(It.IsAny<TaskItem>()), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_WhenTitleAlreadyExists_ShouldThrowDomainValidationExceptionAndNotCommit()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            Title = "Existing Title",
            Description = "A valid description",
            DueDate = DateTime.UtcNow.AddDays(2),
            Priority = TaskPriority.High
        };

        _taskRepositoryMock.Setup(repo => repo.ExistsByTitleAsync(request.Title))
            .ReturnsAsync(true);

        // Act
        Func<Task> act = async () => await _taskService.CreateTaskAsync(request);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("A task with this title already exists.");

        _taskRepositoryMock.Verify(repo => repo.AddAsync(It.IsAny<TaskItem>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTaskExists_ShouldReturnTaskResponse()
    {
        // Arrange
        var id = Guid.NewGuid();
        var task = new TaskItem("Title", "Desc", DateTime.UtcNow.AddDays(1), TaskPriority.Medium);
        _taskRepositoryMock.Setup(repo => repo.GetByIdAsync(id)).ReturnsAsync(task);

        // Act
        var result = await _taskService.GetByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Title");
    }

    [Fact]
    public async Task UpdateTaskAsync_WhenTaskExists_ShouldUpdateAndCommit()
    {
        // Arrange
        var id = Guid.NewGuid();
        var task = new TaskItem("Old Title", "Old Desc", DateTime.UtcNow.AddDays(1), TaskPriority.Low);
        _taskRepositoryMock.Setup(repo => repo.GetByIdAsync(id)).ReturnsAsync(task);

        var request = new UpdateTaskRequest
        {
            Title = "New Title",
            Description = "New Desc",
            DueDate = DateTime.UtcNow.AddDays(2),
            Priority = TaskPriority.High,
            Status = TaskStatus.InProgress
        };

        // Act
        await _taskService.UpdateTaskAsync(id, request);

        // Assert
        task.Title.Should().Be("New Title");
        task.Status.Should().Be(TaskStatus.InProgress);
        _taskRepositoryMock.Verify(repo => repo.UpdateAsync(task), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_WhenTaskExists_ShouldUpdateStatusAndCommit()
    {
        // Arrange
        var id = Guid.NewGuid();
        var task = new TaskItem("Title", "Desc", DateTime.UtcNow.AddDays(1), TaskPriority.Medium);
        _taskRepositoryMock.Setup(repo => repo.GetByIdAsync(id)).ReturnsAsync(task);

        var request = new UpdateTaskStatusRequest
        {
            Status = TaskStatus.Completed
        };

        // Act
        await _taskService.UpdateTaskStatusAsync(id, request);

        // Assert
        task.Status.Should().Be(TaskStatus.Completed);
        _taskRepositoryMock.Verify(repo => repo.UpdateAsync(task), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Once);
    }
}

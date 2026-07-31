using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.Application.Services;
using TbcaTest.Domain.Entities;
using TbcaTest.Domain.Enums;
using TbcaTest.Domain.Exceptions;
using TbcaTest.Tests.TestHelpers;
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

    // ─────────────────────────────────────────────────────────
    // GetByIdAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenTaskExists_ShouldReturnMappedResponse()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dueDate = DateTime.UtcNow.AddDays(5);
        var task = new TaskItemBuilder()
            .WithTitle("My Task")
            .WithDescription("My Description")
            .WithDueDate(dueDate)
            .WithPriority(TaskPriority.High)
            .Build();

        _taskRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(task);

        // Act
        var result = await _taskService.GetByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("My Task");
        result.Description.Should().Be("My Description");
        result.DueDate.Should().Be(dueDate);
        result.Priority.Should().Be(TaskPriority.High);
        result.Status.Should().Be(TaskStatus.Pending); // default status
    }

    [Fact]
    public async Task GetByIdAsync_WhenTaskDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _taskRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((TaskItem?)null);

        // Act
        var result = await _taskService.GetByIdAsync(id);

        // Assert
        result.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────
    // GetPagedTasksAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPagedTasksAsync_ShouldReturnMappedDTOs_WithCorrectCount()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItemBuilder().WithTitle("Task A").WithPriority(TaskPriority.Low).Build(),
            new TaskItemBuilder().WithTitle("Task B").WithPriority(TaskPriority.High).Build(),
        };
        _taskRepositoryMock.Setup(r => r.GetPagedTasksAsync(1, 10)).ReturnsAsync(tasks);

        // Act
        var result = (await _taskService.GetPagedTasksAsync(1, 10)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Task A");
        result[0].Priority.Should().Be(TaskPriority.Low);
        result[1].Title.Should().Be("Task B");
        result[1].Priority.Should().Be(TaskPriority.High);
    }

    [Fact]
    public async Task GetPagedTasksAsync_WhenRepositoryReturnsEmpty_ShouldReturnEmptyCollection()
    {
        // Arrange
        _taskRepositoryMock.Setup(r => r.GetPagedTasksAsync(1, 10)).ReturnsAsync(new List<TaskItem>());

        // Act
        var result = await _taskService.GetPagedTasksAsync(1, 10);

        // Assert
        result.Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────
    // CreateTaskAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTaskAsync_WhenTitleIsUnique_ShouldReturnMappedResponseAndCommit()
    {
        // Arrange
        var dueDate = DateTime.UtcNow.AddDays(2);
        var request = new CreateTaskRequest
        {
            Title = "Unique Title",
            Description = "A valid description",
            DueDate = dueDate,
            Priority = TaskPriority.High
        };

        _taskRepositoryMock.Setup(r => r.ExistsByTitleAsync(request.Title)).ReturnsAsync(false);

        // Act
        var result = await _taskService.CreateTaskAsync(request);

        // Assert – output DTO is fully mapped
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be("Unique Title");
        result.Description.Should().Be("A valid description");
        result.DueDate.Should().Be(dueDate);
        result.Priority.Should().Be(TaskPriority.High);
        result.Status.Should().Be(TaskStatus.Pending); // always starts as Pending

        _taskRepositoryMock.Verify(r => r.AddAsync(It.Is<TaskItem>(t => t.Title == "Unique Title")), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_WhenTitleAlreadyExists_ShouldThrowDomainValidationExceptionAndNotPersist()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            Title = "Existing Title",
            Description = "A valid description",
            DueDate = DateTime.UtcNow.AddDays(2),
            Priority = TaskPriority.High
        };

        _taskRepositoryMock.Setup(r => r.ExistsByTitleAsync(request.Title)).ReturnsAsync(true);

        // Act
        Func<Task> act = async () => await _taskService.CreateTaskAsync(request);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("A task with this title already exists.");

        _taskRepositoryMock.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateTaskAsync_WhenDescriptionIsNull_ShouldSucceed()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            Title = "No Description Task",
            Description = null,
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Low
        };

        _taskRepositoryMock.Setup(r => r.ExistsByTitleAsync(request.Title)).ReturnsAsync(false);

        // Act
        var result = await _taskService.CreateTaskAsync(request);

        // Assert
        result.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Medium)]
    [InlineData(TaskPriority.High)]
    public async Task CreateTaskAsync_ShouldPreservePriority_ForAllPriorityValues(TaskPriority priority)
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            Title = $"Task with priority {priority}",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = priority
        };

        _taskRepositoryMock.Setup(r => r.ExistsByTitleAsync(It.IsAny<string>())).ReturnsAsync(false);

        // Act
        var result = await _taskService.CreateTaskAsync(request);

        // Assert
        result.Priority.Should().Be(priority);
    }

    // ─────────────────────────────────────────────────────────
    // UpdateTaskAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTaskAsync_WhenTaskExists_ShouldApplyAllFieldsAndCommit()
    {
        // Arrange
        var id = Guid.NewGuid();
        var task = new TaskItemBuilder().WithTitle("Old Title").Build();
        _taskRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(task);

        var newDueDate = DateTime.UtcNow.AddDays(10);
        var request = new UpdateTaskRequest
        {
            Title = "New Title",
            Description = "New Desc",
            DueDate = newDueDate,
            Priority = TaskPriority.High,
            Status = TaskStatus.InProgress
        };

        // Act
        await _taskService.UpdateTaskAsync(id, request);

        // Assert – all fields were mutated on the entity
        task.Title.Should().Be("New Title");
        task.Description.Should().Be("New Desc");
        task.DueDate.Should().Be(newDueDate);
        task.Priority.Should().Be(TaskPriority.High);
        task.Status.Should().Be(TaskStatus.InProgress);

        _taskRepositoryMock.Verify(r => r.UpdateAsync(task), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateTaskAsync_WhenTaskDoesNotExist_ShouldNotCallUpdateNorCommit()
    {
        // Arrange
        var id = Guid.NewGuid();
        _taskRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((TaskItem?)null);

        var request = new UpdateTaskRequest
        {
            Title = "Irrelevant",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Low,
            Status = TaskStatus.Pending
        };

        // Act
        await _taskService.UpdateTaskAsync(id, request);

        // Assert – nothing was persisted
        _taskRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
    }

    [Theory]
    [InlineData(TaskStatus.Pending)]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Completed)]
    public async Task UpdateTaskAsync_ShouldApplyStatus_ForAllValidStatuses(TaskStatus status)
    {
        // Arrange
        var id = Guid.NewGuid();
        var task = new TaskItemBuilder().Build();
        _taskRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(task);

        var request = new UpdateTaskRequest
        {
            Title = "Title",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Medium,
            Status = status
        };

        // Act
        await _taskService.UpdateTaskAsync(id, request);

        // Assert
        task.Status.Should().Be(status);
    }

    // ─────────────────────────────────────────────────────────
    // UpdateTaskStatusAsync
    // ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskStatus.Pending)]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Completed)]
    public async Task UpdateTaskStatusAsync_WhenTaskExists_ShouldUpdateStatusAndCommit(TaskStatus newStatus)
    {
        // Arrange
        var id = Guid.NewGuid();
        var task = new TaskItemBuilder().Build();
        _taskRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(task);

        var request = new UpdateTaskStatusRequest { Status = newStatus };

        // Act
        await _taskService.UpdateTaskStatusAsync(id, request);

        // Assert
        task.Status.Should().Be(newStatus);
        _taskRepositoryMock.Verify(r => r.UpdateAsync(task), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_ShouldOnlyChangeStatus_NotOtherFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dueDate = DateTime.UtcNow.AddDays(3);
        var task = new TaskItemBuilder()
            .WithTitle("Original Title")
            .WithDescription("Original Desc")
            .WithDueDate(dueDate)
            .WithPriority(TaskPriority.Low)
            .Build();

        _taskRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(task);

        // Act
        await _taskService.UpdateTaskStatusAsync(id, new UpdateTaskStatusRequest { Status = TaskStatus.Completed });

        // Assert – only Status changed
        task.Title.Should().Be("Original Title");
        task.Description.Should().Be("Original Desc");
        task.DueDate.Should().Be(dueDate);
        task.Priority.Should().Be(TaskPriority.Low);
        task.Status.Should().Be(TaskStatus.Completed);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_WhenTaskDoesNotExist_ShouldNotCallUpdateNorCommit()
    {
        // Arrange
        var id = Guid.NewGuid();
        _taskRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((TaskItem?)null);

        // Act
        await _taskService.UpdateTaskStatusAsync(id, new UpdateTaskStatusRequest { Status = TaskStatus.Completed });

        // Assert
        _taskRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
    }

    // ─────────────────────────────────────────────────────────
    // DeleteTaskAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTaskAsync_WhenTaskExists_ShouldCallDeleteAndCommit()
    {
        // Arrange
        var id = Guid.NewGuid();
        var task = new TaskItemBuilder().Build();
        _taskRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(task);

        // Act
        await _taskService.DeleteTaskAsync(id);

        // Assert
        _taskRepositoryMock.Verify(r => r.DeleteAsync(task), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteTaskAsync_WhenTaskDoesNotExist_ShouldNotCallDeleteNorCommit()
    {
        // Arrange
        var id = Guid.NewGuid();
        _taskRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((TaskItem?)null);

        // Act
        await _taskService.DeleteTaskAsync(id);

        // Assert
        _taskRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<TaskItem>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
    }

    // ─────────────────────────────────────────────────────────
    // ImportTasksFromExcelAsync
    // ─────────────────────────────────────────────────────────

    private static System.IO.Stream BuildExcelStream(Action<ClosedXML.Excel.IXLWorksheet> configureSheet)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Tasks");
        ws.Cell(1, 1).Value = "Title";
        ws.Cell(1, 2).Value = "Description";
        ws.Cell(1, 3).Value = "DueDate";
        ws.Cell(1, 4).Value = "Priority";
        configureSheet(ws);

        var stream = new System.IO.MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task ImportTasksFromExcelAsync_WhenAllRowsValid_ShouldImportAllAndCommitOnce()
    {
        // Arrange
        _taskRepositoryMock.Setup(r => r.GetAllTitlesAsync()).ReturnsAsync(new List<string>());

        using var stream = BuildExcelStream(ws =>
        {
            ws.Cell(2, 1).Value = "Task A";
            ws.Cell(2, 2).Value = "Desc A";
            ws.Cell(2, 3).Value = DateTime.UtcNow.AddDays(1).ToString("O");
            ws.Cell(2, 4).Value = "High";

            ws.Cell(3, 1).Value = "Task B";
            ws.Cell(3, 2).Value = "Desc B";
            ws.Cell(3, 3).Value = DateTime.UtcNow.AddDays(2).ToString("O");
            ws.Cell(3, 4).Value = "Low";
        });

        // Act
        var response = await _taskService.ImportTasksFromExcelAsync(stream);

        // Assert
        response.Should().NotBeNull();
        response.TotalRowsProcessed.Should().Be(2);
        response.SuccessfulImports.Should().Be(2);
        response.FailedImports.Should().Be(0);
        response.Errors.Should().BeEmpty();

        _taskRepositoryMock.Verify(r => r.BulkInsertBatchAsync(It.IsAny<IEnumerable<TaskImportRow>>(), It.IsAny<System.Data.IDbTransaction>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never); // Using BulkInsert so CommitAsync is bypassed on UnitOfWork (Transaction committed instead)
    }

    [Fact]
    public async Task ImportTasksFromExcelAsync_WhenAllRowsInvalid_ShouldNotCommitAtAll()
    {
        // Arrange
        _taskRepositoryMock.Setup(r => r.GetAllTitlesAsync()).ReturnsAsync(new List<string>());

        using var stream = BuildExcelStream(ws =>
        {
            // Row with blank title
            ws.Cell(2, 1).Value = "";
            ws.Cell(2, 2).Value = "Desc";
            ws.Cell(2, 3).Value = DateTime.UtcNow.AddDays(1).ToString("O");
            ws.Cell(2, 4).Value = "High";

            // Row with past date
            ws.Cell(3, 1).Value = "Another Task";
            ws.Cell(3, 2).Value = "Desc";
            ws.Cell(3, 3).Value = DateTime.UtcNow.AddDays(-1).ToString("O");
            ws.Cell(3, 4).Value = "Low";
        });

        // Act
        var result = await _taskService.ImportTasksFromExcelAsync(stream);

        // Assert
        result.TotalRowsProcessed.Should().Be(2);
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(2);

        _taskRepositoryMock.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task ImportTasksFromExcelAsync_WhenTitleAlreadyExistsInDb_ShouldMarkRowAsFailed()
    {
        // Arrange
        _taskRepositoryMock.Setup(r => r.GetAllTitlesAsync()).ReturnsAsync(new List<string> { "Existing Task" });

        using var stream = BuildExcelStream(ws =>
        {
            ws.Cell(2, 1).Value = "Existing Task";
            ws.Cell(2, 2).Value = "Desc";
            ws.Cell(2, 3).Value = DateTime.UtcNow.AddDays(1).ToString("O");
            ws.Cell(2, 4).Value = "High";
        });

        // Act
        var result = await _taskService.ImportTasksFromExcelAsync(stream);

        // Assert
        result.FailedImports.Should().Be(1);
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("Existing Task");
    }

    [Fact]
    public async Task ImportTasksFromExcelAsync_WhenDuplicateTitleWithinSameFile_ShouldMarkSecondAsFailed()
    {
        // Arrange – DB has no duplicates, but the same title appears twice in the sheet
        _taskRepositoryMock.Setup(r => r.GetAllTitlesAsync()).ReturnsAsync(new List<string>());

        using var stream = BuildExcelStream(ws =>
        {
            ws.Cell(2, 1).Value = "Duplicate Title";
            ws.Cell(2, 2).Value = "Desc";
            ws.Cell(2, 3).Value = DateTime.UtcNow.AddDays(1).ToString("O");
            ws.Cell(2, 4).Value = "Low";

            ws.Cell(3, 1).Value = "Duplicate Title"; // same title again
            ws.Cell(3, 2).Value = "Desc2";
            ws.Cell(3, 3).Value = DateTime.UtcNow.AddDays(2).ToString("O");
            ws.Cell(3, 4).Value = "Medium";
        });

        // Act
        var result = await _taskService.ImportTasksFromExcelAsync(stream);

        // Assert
        result.SuccessfulImports.Should().Be(1);
        result.FailedImports.Should().Be(1);
        result.Errors[0].RowNumber.Should().Be(3); // second occurrence fails
    }

    [Fact]
    public async Task ImportTasksFromExcelAsync_WhenInvalidPriority_ShouldRecordErrorWithRowNumber()
    {
        // Arrange
        _taskRepositoryMock.Setup(r => r.GetAllTitlesAsync()).ReturnsAsync(new List<string>());

        using var stream = BuildExcelStream(ws =>
        {
            ws.Cell(2, 1).Value = "Valid Title";
            ws.Cell(2, 2).Value = "Desc";
            ws.Cell(2, 3).Value = DateTime.UtcNow.AddDays(1).ToString("O");
            ws.Cell(2, 4).Value = "InvalidPriority";
        });

        // Act
        var result = await _taskService.ImportTasksFromExcelAsync(stream);

        // Assert
        result.FailedImports.Should().Be(1);
        result.Errors.Should().ContainSingle()
            .Which.RowNumber.Should().Be(2);
        result.Errors[0].Message.Should().Contain("InvalidPriority");
    }

    [Fact]
    public async Task ImportTasksFromExcelAsync_WhenMixOfValidAndInvalid_ShouldSaveOnlyValidOnes()
    {
        // Arrange
        _taskRepositoryMock.Setup(r => r.GetAllTitlesAsync()).ReturnsAsync(new List<string> { "Existing" });

        using var stream = BuildExcelStream(ws =>
        {
            // valid
            ws.Cell(2, 1).Value = "New Task";
            ws.Cell(2, 2).Value = "Desc";
            ws.Cell(2, 3).Value = DateTime.UtcNow.AddDays(1).ToString("O");
            ws.Cell(2, 4).Value = "Medium";

            // invalid: duplicate in DB
            ws.Cell(3, 1).Value = "Existing";
            ws.Cell(3, 2).Value = "Desc";
            ws.Cell(3, 3).Value = DateTime.UtcNow.AddDays(1).ToString("O");
            ws.Cell(3, 4).Value = "High";

            // invalid: past date + wrong priority
            ws.Cell(4, 1).Value = "Another Task";
            ws.Cell(4, 2).Value = "Desc";
            ws.Cell(4, 3).Value = DateTime.UtcNow.AddDays(-1).ToString("O");
            ws.Cell(4, 4).Value = "Unknown";
        });

        // Act
        var response = await _taskService.ImportTasksFromExcelAsync(stream);

        // Assert
        response.Should().NotBeNull();
        response.TotalRowsProcessed.Should().Be(3);
        response.SuccessfulImports.Should().Be(1);
        response.FailedImports.Should().Be(2);
        response.Errors.Should().HaveCount(2);

        _taskRepositoryMock.Verify(r => r.BulkInsertBatchAsync(It.IsAny<IEnumerable<TaskImportRow>>(), It.IsAny<System.Data.IDbTransaction>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task ImportTasksFromExcelAsync_WhenHeaderOnlySheet_ShouldReturnEmptyReport()
    {
        // Arrange
        _taskRepositoryMock.Setup(r => r.GetAllTitlesAsync()).ReturnsAsync(new List<string>());

        // Sheet with only the header row
        using var stream = BuildExcelStream(_ => { });

        // Act
        var result = await _taskService.ImportTasksFromExcelAsync(stream);

        // Assert
        result.TotalRowsProcessed.Should().Be(0);
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(0);
        result.Errors.Should().BeEmpty();

        _taskRepositoryMock.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
    }
}

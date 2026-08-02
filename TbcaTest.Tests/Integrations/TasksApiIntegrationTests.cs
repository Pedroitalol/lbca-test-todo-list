using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.Domain.Enums;
using TbcaTest.Infra.Contexts;
using Xunit;
using TaskStatus = TbcaTest.Domain.Enums.TaskStatus;

namespace TbcaTest.Tests.Integrations;

// ─────────────────────────────────────────────────────────────────────────────
// Infrastructure: Auth handler + Factory
// ─────────────────────────────────────────────────────────────────────────────

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("DatabaseStartup__ApplyMigrationsOnStartup", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("AppSecurity:RateLimiting:DefaultRequestsPerSecond", "1000000"),
                new KeyValuePair<string, string?>("AppSecurity:RateLimiting:AuthRequestsPerSecond", "1000000"),
                new KeyValuePair<string, string?>("DatabaseStartup:ApplyMigrationsOnStartup", "false")
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            });

            services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
                options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AllowAnonymousFilter()));
        });

        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TbcaTestContext>));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            var dbConnectionDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbConnection));
            if (dbConnectionDescriptor != null) services.Remove(dbConnectionDescriptor);

            // Unique in-memory DB per factory instance prevents cross-test pollution
            var dbName = Guid.NewGuid().ToString();
            services.AddDbContext<TbcaTestContext>((_, options) =>
                options.UseInMemoryDatabase(dbName));

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            scope.ServiceProvider.GetRequiredService<TbcaTestContext>().Database.EnsureCreated();
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Test helper: creates a fresh factory + client per test class instance so
// every test class starts with a clean database.
// ─────────────────────────────────────────────────────────────────────────────

public abstract class TasksApiTestBase : IDisposable
{
    protected readonly HttpClient Client;
    private readonly CustomWebApplicationFactory _factory;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    protected TasksApiTestBase()
    {
        _factory = new CustomWebApplicationFactory();
        Client = _factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
    }

    // Creates a task and returns the full response DTO
    protected async Task<TaskResponse> CreateTaskAsync(
        string title = "Integration Task",
        string? description = "A test description",
        int dueDays = 3,
        TaskPriority priority = TaskPriority.Medium)
    {
        var request = new CreateTaskRequest
        {
            Title = title,
            Description = description,
            DueDate = DateTime.UtcNow.AddDays(dueDays),
            Priority = priority
        };
        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "seed task creation must succeed");
        return (await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions))!;
    }

    // Builds a .xlsx in-memory stream with a header row + the provided data rows
    protected static System.IO.Stream BuildExcelStream(IEnumerable<(string title, string desc, DateTime dueDate, string priority)> rows)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Tasks");
        ws.Cell(1, 1).Value = "Title";
        ws.Cell(1, 2).Value = "Description";
        ws.Cell(1, 3).Value = "DueDate";
        ws.Cell(1, 4).Value = "Priority";

        int rowNum = 2;
        foreach (var (title, desc, dueDate, p) in rows)
        {
            ws.Cell(rowNum, 1).Value = title;
            ws.Cell(rowNum, 2).Value = desc;
            ws.Cell(rowNum, 3).Value = dueDate.ToString("O");
            ws.Cell(rowNum, 4).Value = p;
            rowNum++;
        }

        var stream = new System.IO.MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GET /api/tasks
// ─────────────────────────────────────────────────────────────────────────────

public class GetTasksIntegrationTests : TasksApiTestBase
{
    [Fact]
    public async Task GetTasks_WhenNoTasksExist_ShouldReturnEmptyList()
    {
        var response = await Client.GetAsync("/api/tasks?page=1&size=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tasks = await response.Content.ReadFromJsonAsync<IEnumerable<TaskResponse>>(JsonOptions);
        tasks.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetTasks_AfterCreation_ShouldReturnCreatedTasks()
    {
        await CreateTaskAsync("Task Alpha");
        await CreateTaskAsync("Task Beta");

        var response = await Client.GetAsync("/api/tasks?page=1&size=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tasks = (await response.Content.ReadFromJsonAsync<IEnumerable<TaskResponse>>(JsonOptions))!.ToList();
        tasks.Should().HaveCountGreaterOrEqualTo(2);
        tasks.Select(t => t.Title).Should().Contain("Task Alpha").And.Contain("Task Beta");
    }

    [Fact]
    public async Task GetTasks_ShouldRespectPageSize()
    {
        for (int i = 1; i <= 5; i++)
            await CreateTaskAsync($"Paged Task {i}");

        var response = await Client.GetAsync("/api/tasks?page=1&size=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tasks = (await response.Content.ReadFromJsonAsync<IEnumerable<TaskResponse>>(JsonOptions))!.ToList();
        tasks.Should().HaveCountLessOrEqualTo(2);
    }

    [Fact]
    public async Task GetTasks_ShouldReturnAllFieldsInResponse()
    {
        var dueDate = DateTime.UtcNow.AddDays(7);
        await CreateTaskAsync("Field Check Task", "Full description", 7, TaskPriority.High);

        var response = await Client.GetAsync("/api/tasks?page=1&size=10");
        var tasks = (await response.Content.ReadFromJsonAsync<IEnumerable<TaskResponse>>(JsonOptions))!.ToList();
        var task = tasks.First(t => t.Title == "Field Check Task");

        task.Id.Should().NotBeEmpty();
        task.Description.Should().Be("Full description");
        task.Priority.Should().Be(TaskPriority.High);
        task.Status.Should().Be(TaskStatus.Pending);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GET /api/tasks/{id}
// ─────────────────────────────────────────────────────────────────────────────

public class GetTaskByIdIntegrationTests : TasksApiTestBase
{
    [Fact]
    public async Task GetTaskById_WhenExists_ShouldReturnTask_WithAllFields()
    {
        var created = await CreateTaskAsync("Details Task", "My description", 5, TaskPriority.Low);

        var response = await Client.GetAsync($"/api/tasks/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        task.Should().NotBeNull();
        task!.Id.Should().Be(created.Id);
        task.Title.Should().Be("Details Task");
        task.Description.Should().Be("My description");
        task.Priority.Should().Be(TaskPriority.Low);
        task.Status.Should().Be(TaskStatus.Pending);
    }

    [Fact]
    public async Task GetTaskById_WhenDoesNotExist_ShouldReturn404()
    {
        var response = await Client.GetAsync($"/api/tasks/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Task not found");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// POST /api/tasks
// ─────────────────────────────────────────────────────────────────────────────

public class CreateTaskIntegrationTests : TasksApiTestBase
{
    [Fact]
    public async Task CreateTask_WithValidData_ShouldReturn201AndPersist()
    {
        var request = new CreateTaskRequest
        {
            Title = "Create Integration Test",
            Description = "Valid description",
            DueDate = DateTime.UtcNow.AddDays(2),
            Priority = TaskPriority.High
        };

        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        task.Should().NotBeNull();
        task!.Id.Should().NotBeEmpty();
        task.Title.Should().Be("Create Integration Test");
        task.Description.Should().Be("Valid description");
        task.Priority.Should().Be(TaskPriority.High);
        task.Status.Should().Be(TaskStatus.Pending);

        // Confirm it was persisted via GET
        var getResponse = await Client.GetAsync($"/api/tasks/{task.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateTask_ShouldReturn201WithLocationHeader()
    {
        var request = new CreateTaskRequest
        {
            Title = "Location Header Task",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Medium
        };

        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("Created response must contain a Location header");
    }

    [Fact]
    public async Task CreateTask_WithEmptyTitle_ShouldReturn400WithValidationError()
    {
        var request = new CreateTaskRequest
        {
            Title = string.Empty,
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Low
        };

        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Title is required");
    }

    [Fact]
    public async Task CreateTask_WithTitleExceeding100Chars_ShouldReturn400()
    {
        var request = new CreateTaskRequest
        {
            Title = new string('A', 101),
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Low
        };

        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("100");
    }

    [Fact]
    public async Task CreateTask_WithDescriptionExceeding500Chars_ShouldReturn400()
    {
        var request = new CreateTaskRequest
        {
            Title = "Valid Title",
            Description = new string('D', 501),
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Low
        };

        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Description must not exceed 500 characters");
    }

    [Fact]
    public async Task CreateTask_WithPastDueDate_ShouldReturn400()
    {
        var request = new CreateTaskRequest
        {
            Title = "Past Date Task",
            DueDate = DateTime.UtcNow.AddDays(-1),
            Priority = TaskPriority.Low
        };

        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("DueDate");
    }

    [Fact]
    public async Task CreateTask_WithInvalidPriority_ShouldReturn400()
    {
        var request = new CreateTaskRequest
        {
            Title = "Invalid Priority Task",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = (TaskPriority)999
        };

        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid priority level");
    }

    [Fact]
    public async Task CreateTask_WithDuplicateTitle_ShouldReturn400()
    {
        await CreateTaskAsync("Duplicate Title");

        var request = new CreateTaskRequest
        {
            Title = "Duplicate Title",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Medium
        };

        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("A task with this title already exists.");
    }

    [Fact]
    public async Task CreateTask_WithNullDescription_ShouldSucceedAndPersistNullDescription()
    {
        var request = new CreateTaskRequest
        {
            Title = "No Description Task",
            Description = null,
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Low
        };

        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        task!.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Medium)]
    [InlineData(TaskPriority.High)]
    public async Task CreateTask_ShouldPreservePriority_ForAllValidValues(TaskPriority priority)
    {
        var request = new CreateTaskRequest
        {
            Title = $"Priority Test {priority} {Guid.NewGuid()}",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = priority
        };

        var response = await Client.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        task!.Priority.Should().Be(priority);
    }

    [Fact]
    public async Task CreateTask_TwentyTimes_AllShouldSucceedAndBeRetrievable()
    {
        var createdIds = new List<Guid>();

        for (int i = 1; i <= 20; i++)
        {
            var task = await CreateTaskAsync($"Batch Task {i} {Guid.NewGuid()}", dueDays: i);
            createdIds.Add(task.Id);
            await Task.Delay(55); // respect rate limiter
        }

        var getResponse = await Client.GetAsync("/api/tasks?page=1&size=100");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var all = (await getResponse.Content.ReadFromJsonAsync<IEnumerable<TaskResponse>>(JsonOptions))!;
        var retrievedIds = all.Select(t => t.Id).ToList();

        foreach (var id in createdIds)
            retrievedIds.Should().Contain(id);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PUT /api/tasks/{id}
// ─────────────────────────────────────────────────────────────────────────────

public class UpdateTaskIntegrationTests : TasksApiTestBase
{
    [Fact]
    public async Task UpdateTask_WithValidData_ShouldReturn204AndPersistChanges()
    {
        var created = await CreateTaskAsync("Original Title", "Original Desc", 2, TaskPriority.Low);

        var updateRequest = new UpdateTaskRequest
        {
            Title = "Updated Title",
            Description = "Updated Description",
            DueDate = DateTime.UtcNow.AddDays(5),
            Priority = TaskPriority.High,
            Status = TaskStatus.InProgress
        };

        var putResponse = await Client.PutAsJsonAsync($"/api/tasks/{created.Id}", updateRequest);
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify persistence via GET detail
        var getResponse = await Client.GetAsync($"/api/tasks/{created.Id}");
        var task = await getResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);

        task!.Title.Should().Be("Updated Title");
        task.Description.Should().Be("Updated Description");
        task.Priority.Should().Be(TaskPriority.High);
        task.Status.Should().Be(TaskStatus.InProgress);
    }

    [Fact]
    public async Task UpdateTask_WhenTaskDoesNotExist_ShouldReturn404()
    {
        var request = new UpdateTaskRequest
        {
            Title = "Irrelevant",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Low,
            Status = TaskStatus.Pending
        };

        var response = await Client.PutAsJsonAsync($"/api/tasks/{Guid.NewGuid()}", request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Task not found");
    }

    [Fact]
    public async Task UpdateTask_WithEmptyTitle_ShouldReturn400()
    {
        var created = await CreateTaskAsync("Task To Update");

        var request = new UpdateTaskRequest
        {
            Title = string.Empty,
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Low,
            Status = TaskStatus.Pending
        };

        var response = await Client.PutAsJsonAsync($"/api/tasks/{created.Id}", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTask_WithPastDueDate_ShouldReturn400()
    {
        var created = await CreateTaskAsync("Task To Update DueDate");

        var request = new UpdateTaskRequest
        {
            Title = "New Title",
            DueDate = DateTime.UtcNow.AddDays(-1),
            Priority = TaskPriority.Low,
            Status = TaskStatus.Pending
        };

        var response = await Client.PutAsJsonAsync($"/api/tasks/{created.Id}", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(TaskStatus.Pending)]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Completed)]
    public async Task UpdateTask_ShouldAcceptAllValidStatuses(TaskStatus status)
    {
        var created = await CreateTaskAsync($"Task Status Update {status} {Guid.NewGuid()}");

        var request = new UpdateTaskRequest
        {
            Title = $"Updated {status}",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.Medium,
            Status = status
        };

        var response = await Client.PutAsJsonAsync($"/api/tasks/{created.Id}", request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.GetAsync($"/api/tasks/{created.Id}");
        var task = await getResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        task!.Status.Should().Be(status);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PATCH /api/tasks/{id}/status
// ─────────────────────────────────────────────────────────────────────────────

public class UpdateTaskStatusIntegrationTests : TasksApiTestBase
{
    [Theory]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Completed)]
    [InlineData(TaskStatus.Pending)]
    public async Task UpdateTaskStatus_ShouldReturn204AndPersistNewStatus(TaskStatus newStatus)
    {
        var created = await CreateTaskAsync($"Status Task {newStatus} {Guid.NewGuid()}");

        var request = new UpdateTaskStatusRequest { Status = newStatus };
        var response = await Client.PatchAsJsonAsync($"/api/tasks/{created.Id}/status", request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.GetAsync($"/api/tasks/{created.Id}");
        var task = await getResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        task!.Status.Should().Be(newStatus);
    }

    [Fact]
    public async Task UpdateTaskStatus_ShouldNotChangeOtherFields()
    {
        var created = await CreateTaskAsync("Immutable Fields Task", "My Desc", 4, TaskPriority.High);

        var request = new UpdateTaskStatusRequest { Status = TaskStatus.Completed };
        await Client.PatchAsJsonAsync($"/api/tasks/{created.Id}/status", request);

        var getResponse = await Client.GetAsync($"/api/tasks/{created.Id}");
        var task = await getResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);

        task!.Title.Should().Be("Immutable Fields Task");
        task.Description.Should().Be("My Desc");
        task.Priority.Should().Be(TaskPriority.High);
        task.Status.Should().Be(TaskStatus.Completed);
    }

    [Fact]
    public async Task UpdateTaskStatus_WhenTaskDoesNotExist_ShouldReturn404()
    {
        var request = new UpdateTaskStatusRequest { Status = TaskStatus.InProgress };
        var response = await Client.PatchAsJsonAsync($"/api/tasks/{Guid.NewGuid()}/status", request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Task not found");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// POST /api/tasks/import
// ─────────────────────────────────────────────────────────────────────────────

public class ImportTasksIntegrationTests : TasksApiTestBase
{
    private static MultipartFormDataContent BuildMultipart(System.IO.Stream stream, string filename = "tasks.xlsx")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", filename);
        return content;
    }

    [Fact]
    public async Task ImportTasks_WithValidRows_ShouldReturn200AndReportSuccess()
    {
        using var stream = BuildExcelStream(new[]
        {
            ("Import Task 1", "Desc 1", DateTime.UtcNow.AddDays(1), "High"),
            ("Import Task 2", "Desc 2", DateTime.UtcNow.AddDays(2), "Low"),
        });

        var response = await Client.PostAsync("/api/tasks/import", BuildMultipart(stream));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await response.Content.ReadFromJsonAsync<ImportTaskResponse>(JsonOptions);
        report.Should().NotBeNull();
        report!.TotalRowsProcessed.Should().Be(2);
        report.SuccessfulImports.Should().Be(2);
        report.FailedImports.Should().Be(0);
        report.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportTasks_ValidRows_ShouldBePersisted_AndRetrievableViaGet()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];
        using var stream = BuildExcelStream(new[]
        {
            ($"Persisted Import {uniqueSuffix}", "Desc", DateTime.UtcNow.AddDays(3), "Medium"),
        });

        await Client.PostAsync("/api/tasks/import", BuildMultipart(stream));

        var getResponse = await Client.GetAsync("/api/tasks?page=1&size=100");
        var tasks = (await getResponse.Content.ReadFromJsonAsync<IEnumerable<TaskResponse>>(JsonOptions))!;
        tasks.Should().Contain(t => t.Title == $"Persisted Import {uniqueSuffix}");
    }

    [Fact]
    public async Task ImportTasks_WithInvalidRows_ShouldReturn200WithErrorsAndNotPersistBadRows()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];
        using var stream = BuildExcelStream(new[]
        {
            ($"Valid Import {uniqueSuffix}", "Desc", DateTime.UtcNow.AddDays(1), "High"),   // valid
            ("",                             "Desc", DateTime.UtcNow.AddDays(1), "Low"),    // blank title
            ("Past Date Task",               "Desc", DateTime.UtcNow.AddDays(-1), "Low"),   // past date
        });

        var response = await Client.PostAsync("/api/tasks/import", BuildMultipart(stream));
        response.StatusCode.Should().Be(HttpStatusCode.OK); // never 500

        var report = await response.Content.ReadFromJsonAsync<ImportTaskResponse>(JsonOptions);
        report!.TotalRowsProcessed.Should().Be(3);
        report.SuccessfulImports.Should().Be(1);
        report.FailedImports.Should().Be(2);
        report.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportTasks_WhenRowHasDuplicateTitleFromDb_ShouldFailThatRow()
    {
        await CreateTaskAsync("Already Exists Task");

        using var stream = BuildExcelStream(new[]
        {
            ("Already Exists Task", "Desc", DateTime.UtcNow.AddDays(1), "Medium"),
        });

        var response = await Client.PostAsync("/api/tasks/import", BuildMultipart(stream));
        var report = await response.Content.ReadFromJsonAsync<ImportTaskResponse>(JsonOptions);

        report!.FailedImports.Should().Be(1);
        report.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("Already Exists Task");
    }

    [Fact]
    public async Task ImportTasks_WithInvalidPriority_ShouldFailWithDescriptiveError()
    {
        using var stream = BuildExcelStream(new[]
        {
            ("Priority Error Task", "Desc", DateTime.UtcNow.AddDays(1), "Urgente"),
        });

        var response = await Client.PostAsync("/api/tasks/import", BuildMultipart(stream));
        var report = await response.Content.ReadFromJsonAsync<ImportTaskResponse>(JsonOptions);

        report!.FailedImports.Should().Be(1);
        report.Errors[0].RowNumber.Should().Be(2); // row 2 (header is row 1)
        report.Errors[0].Message.Should().Contain("Urgente");
    }

    [Fact]
    public async Task ImportTasks_WithWrongFileExtension_ShouldReturn400()
    {
        var content = new MultipartFormDataContent();
        var fileContent = new StringContent("not-an-excel-file");
        content.Add(fileContent, "file", "tasks.csv");

        var response = await Client.PostAsync("/api/tasks/import", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(".xlsx");
    }

    [Fact]
    public async Task ImportTasks_WithNoFile_ShouldReturn400()
    {
        var content = new MultipartFormDataContent(); // empty — no file attached

        var response = await Client.PostAsync("/api/tasks/import", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

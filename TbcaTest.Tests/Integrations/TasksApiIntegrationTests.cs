using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.Domain.Enums;
using TbcaTest.Infra.Contexts;
using Xunit;
using TaskStatus = TbcaTest.Domain.Enums.TaskStatus;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TbcaTest.Tests.Integrations;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, 
        ILoggerFactory logger, UrlEncoder encoder) 
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { new Claim(ClaimTypes.Name, "TestUser"), new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
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

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("AppSecurity:ApiKey", "test-key"),
                new KeyValuePair<string, string?>("AppSecurity:ApiSecret", "test-secret"),
                new KeyValuePair<string, string?>("AppSecurity:RateLimiting:DefaultRequestsPerSecond", "1000000"),
                new KeyValuePair<string, string?>("AppSecurity:RateLimiting:AuthRequestsPerSecond", "1000000")
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            });

            services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
            {
                options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AllowAnonymousFilter());
            });
        });
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("DatabaseStartup:ApplyMigrationsOnStartup", "false")
            });
        });

        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TbcaTestContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var dbConnectionDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbConnection));

            if (dbConnectionDescriptor != null)
            {
                services.Remove(dbConnectionDescriptor);
            }

            // Create a unique DB name to prevent cross-test contamination
            var dbName = Guid.NewGuid().ToString();

            services.AddDbContext<TbcaTestContext>((container, options) =>
            {
                options.UseInMemoryDatabase(dbName);
            });

            // Ensure the database is created
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TbcaTestContext>();
            db.Database.EnsureCreated();
        });
    }
}

public class TasksApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TasksApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        _client.DefaultRequestHeaders.Add("X-API-KEY", "test-key");
        _client.DefaultRequestHeaders.Add("X-API-SECRET", "test-secret");
    }

    [Fact]
    public async Task CreateTask_WhenCalledTwentyTimes_ShouldPersistAndReturnSuccessfully()
    {
        // Arrange
        int tasksCount = 20;
        var createdTaskIds = new List<Guid>();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());

        // Execute sequentially to avoid 429 Too Many Requests (Rate Limiter)
        for (int i = 1; i <= tasksCount; i++)
        {
            var request = new CreateTaskRequest
            {
                Title = $"Automated Test Task {i}",
                Description = $"Description for automated task number {i}",
                DueDate = DateTime.UtcNow.AddDays(i),
                Priority = TaskPriority.High
            };

            var response = await _client.PostAsJsonAsync("/api/tasks", request);
            await Task.Delay(55); // Keep requests < 20 per second
            
            // Assert creation
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var createdTask = await response.Content.ReadFromJsonAsync<TaskResponse>(options);
            createdTask.Should().NotBeNull();
            createdTask!.Title.Should().Be(request.Title);
            
            createdTaskIds.Add(createdTask.Id);
        }

        // Assert - Retrieve tasks using GET endpoint
        var getResponse = await _client.GetAsync($"/api/tasks?page=1&size=100");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrievedTasks = await getResponse.Content.ReadFromJsonAsync<IEnumerable<TaskResponse>>(options);
        
        retrievedTasks.Should().NotBeNull();
        retrievedTasks.Should().HaveCountGreaterOrEqualTo(tasksCount);

        // Ensure all our created IDs are present in the response
        var retrievedIds = retrievedTasks!.Select(t => t.Id).ToList();
        foreach (var id in createdTaskIds)
        {
            retrievedIds.Should().Contain(id);
        }
    }

    [Fact]
    public async Task CreateTask_WithExistingTitle_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            Title = "Unique Title Test",
            Description = "First task",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskPriority.High
        };

        // Act - Create first task
        var response1 = await _client.PostAsJsonAsync("/api/tasks", request);
        response1.EnsureSuccessStatusCode();

        // Act - Create second task with same title
        var request2 = new CreateTaskRequest
        {
            Title = "Unique Title Test",
            Description = "Second task with same title",
            DueDate = DateTime.UtcNow.AddDays(2),
            Priority = TaskPriority.Medium
        };
        var response2 = await _client.PostAsJsonAsync("/api/tasks", request2);

        // Assert
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorContent = await response2.Content.ReadAsStringAsync();
        errorContent.Should().Contain("A task with this title already exists.");
    }

    [Fact]
    public async Task CreateTask_WithMissingRequiredFields_ShouldReturnBadRequest()
    {
        // Arrange - Invalid title, past date, description too long, invalid enum
        var request = new CreateTaskRequest
        {
            Title = string.Empty, // Invalid title (Required)
            Description = new string('A', 501), // Invalid description (MaxLength 500)
            DueDate = DateTime.UtcNow.AddDays(-1), // Invalid date (Must be in future)
            Priority = (TaskPriority)999 // Invalid enum
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("Title is required");
        errorContent.Should().Contain("Description must not exceed 500 characters");
        errorContent.Should().Contain("Invalid priority level");
    }
}

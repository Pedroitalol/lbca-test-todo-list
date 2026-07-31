using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.Application.Services;

namespace TbcaTest.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks([FromQuery] int page = 1, [FromQuery] int size = 10)
    {
        var tasks = await _taskService.GetPagedTasksAsync(page, size);

        return Ok(tasks);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTask(Guid id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task == null)
        {
            return NotFound(new { errors = new[] { "Task not found" } });
        }
        return Ok(task);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        var task = await _taskService.CreateTaskAsync(request);
        return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(Guid id, [FromBody] UpdateTaskRequest request)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task == null)
        {
            return NotFound(new { errors = new[] { "Task not found" } });
        }

        await _taskService.UpdateTaskAsync(id, request);
        return NoContent();
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateTaskStatus(Guid id, [FromBody] UpdateTaskStatusRequest request)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task == null)
        {
            return NotFound(new { errors = new[] { "Task not found" } });
        }

        await _taskService.UpdateTaskStatusAsync(id, request);
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportTasks(Microsoft.AspNetCore.Http.IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "No file uploaded." } });
        }

        var extension = System.IO.Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { errors = new[] { "Invalid file format. Please upload a .xlsx file." } });
        }

        using var stream = file.OpenReadStream();
        var report = await _taskService.ImportTasksFromExcelAsync(stream);

        return Ok(report);
    }
}
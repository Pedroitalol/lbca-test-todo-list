using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.Application.Services;

namespace TbcaTest.Api.Controllers;

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

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        var task = await _taskService.CreateTaskAsync(request);
        return CreatedAtAction(nameof(GetTasks), new { id = task.Id }, task);
    }
}
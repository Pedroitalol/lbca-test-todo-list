using System.ComponentModel.DataAnnotations;
using TbcaTest.Domain.Enums;
using TaskStatus = TbcaTest.Domain.Enums.TaskStatus;

namespace TbcaTest.Application.DTOs.Tasks
{
    public class UpdateTaskStatusRequest
    {
        [EnumDataType(typeof(TaskStatus), ErrorMessage = "Invalid status")]
        public TaskStatus Status { get; set; }
    }
}

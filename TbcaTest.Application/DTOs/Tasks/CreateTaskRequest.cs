using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TbcaTest.Domain.Entities;
using TbcaTest.Domain.Enums;

namespace TbcaTest.Application.DTOs.Tasks
{
    public class CreateTaskRequest : IValidatableObject
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title must not exceed 100 characters")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
        public string? Description { get; set; }

        public DateTime DueDate { get; set; }

        [EnumDataType(typeof(TaskPriority), ErrorMessage = "Invalid priority level")]
        public TaskPriority Priority { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DueDate <= DateTime.UtcNow)
            {
                yield return new ValidationResult("DueDate must be greater than the current date.", new[] { nameof(DueDate) });
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.CrossCutting.Configuration;
using TbcaTest.Domain.Entities;
using TbcaTest.Domain.Exceptions;

namespace TbcaTest.Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly AppSecurityOptions _securityOptions;

        public TaskService(ITaskRepository taskRepository, IUnitOfWork unitOfWork, IOptions<AppSecurityOptions> securityOptions)
        {
            _taskRepository = taskRepository;
            _unitOfWork = unitOfWork;
            _securityOptions = securityOptions.Value;
        }

        public async Task<IEnumerable<TaskResponse>> GetPagedTasksAsync(int pageNumber, int pageSize)
        {
            var tasks = await _taskRepository.GetPagedTasksAsync(pageNumber, pageSize);

            return tasks.Select(t => new TaskResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                DueDate = t.DueDate,
                Status = t.Status,
                Priority = t.Priority
            });
        }

        public async Task<TaskResponse?> GetByIdAsync(Guid id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task == null) return null;

            return new TaskResponse
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                DueDate = task.DueDate,
                Status = task.Status,
                Priority = task.Priority
            };
        }

        public async Task<TaskResponse> CreateTaskAsync(CreateTaskRequest request)
        {
            var exists = await _taskRepository.ExistsByTitleAsync(request.Title);
            if (exists)
            {
                throw new DomainValidationException("A task with this title already exists.");
            }

            var task = new TaskItem(request.Title, request.Description, request.DueDate, request.Priority);

            await _taskRepository.AddAsync(task);
            await _unitOfWork.CommitAsync();

            return new TaskResponse
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                DueDate = task.DueDate,
                Status = task.Status,
                Priority = task.Priority
            };
        }

        public async Task UpdateTaskAsync(Guid id, UpdateTaskRequest request)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task != null)
            {
                task.Update(request.Title, request.Description, request.DueDate, request.Status, request.Priority);
                await _taskRepository.UpdateAsync(task);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task UpdateTaskStatusAsync(Guid id, UpdateTaskStatusRequest request)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task != null)
            {
                task.UpdateStatus(request.Status);
                await _taskRepository.UpdateAsync(task);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task DeleteTaskAsync(Guid id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task != null)
            {
                await _taskRepository.DeleteAsync(task);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task<ImportTaskResponse> ImportTasksFromExcelAsync(System.IO.Stream excelStream)
        {
            const int BatchSize = 10_000;
            var response = new ImportTaskResponse();
            int maxReportedErrors = _securityOptions.ImportMaxReportedErrors;

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var seenTitlesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentBatch = new List<TaskImportRow>(BatchSize);

            void AddError(int rowNumber, string message)
            {
                response.FailedImports++;
                if (response.Errors.Count < maxReportedErrors)
                    response.Errors.Add(new ImportTaskError { RowNumber = rowNumber, Message = message });
                else
                    response.TruncatedErrors++;
            }

            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(excelStream);

            if (reader.FieldCount == 0)
            {
                AddError(0, "No worksheets found or file is empty.");
                return response;
            }

            bool hasHeader = reader.Read();
            if (!hasHeader) return response;

            int rowNumber = 1;

            async Task FlushBatchAsync(List<TaskImportRow> batch)
            {
                if (batch.Count == 0) return;

                var existingInDb = new HashSet<string>(
                    await _taskRepository.GetExistingTitlesFromBatchAsync(batch.Select(r => r.Title)),
                    StringComparer.OrdinalIgnoreCase);

                var insertable = batch.Where(r => !existingInDb.Contains(r.Title)).ToList();
                var duplicates = batch.Count - insertable.Count;

                if (duplicates > 0)
                {
                    response.SuccessfulImports -= duplicates;

                    foreach (var dup in batch.Where(r => existingInDb.Contains(r.Title)))
                        AddError(dup.RowNumber, $"Duplicate detected at insert time: title '{dup.Title}' already exists in the database.");
                }

                if (insertable.Count == 0) return;

                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    await _taskRepository.BulkInsertBatchAsync(insertable, transaction);
                    transaction?.Commit();
                }
                catch (Exception ex)
                {
                    transaction?.Rollback();
                    response.SuccessfulImports -= insertable.Count;
                    response.FailedImports += (insertable.Count - 1);
                    AddError(insertable.First().RowNumber, $"Database error inserting batch: {ex.Message}");
                }
            }

            while (reader.Read())
            {
                rowNumber++;
                response.TotalRowsProcessed++;

                try
                {
                    string title       = reader.GetValue(0)?.ToString()?.Trim() ?? string.Empty;
                    string description = reader.GetValue(1)?.ToString()?.Trim() ?? string.Empty;
                    string dueDateStr  = reader.GetValue(2)?.ToString() ?? string.Empty;
                    string priorityStr = reader.GetValue(3)?.ToString()?.Trim() ?? string.Empty;

                    var rowErrors = new List<string>();

                    if (string.IsNullOrWhiteSpace(title))
                        rowErrors.Add("Title is required.");
                    else if (title.Length > 100)
                        rowErrors.Add("Title must not exceed 100 characters.");
                    else if (seenTitlesInFile.Contains(title))
                        rowErrors.Add($"Duplicate within this file: title '{title}' appears more than once.");

                    if (description.Length > 500)
                        rowErrors.Add("Description must not exceed 500 characters.");

                    DateTime dueDate = default;
                    if (!DateTime.TryParse(dueDateStr, out dueDate))
                        rowErrors.Add("DueDate is invalid or in an incorrect format.");
                    else if (dueDate <= DateTime.UtcNow)
                        rowErrors.Add("DueDate must be greater than the current date.");

                    TbcaTest.Domain.Enums.TaskPriority priority = default;
                    if (!Enum.TryParse<TbcaTest.Domain.Enums.TaskPriority>(priorityStr, ignoreCase: true, out priority))
                        rowErrors.Add($"Priority '{priorityStr}' is invalid. Allowed: {string.Join(", ", Enum.GetNames(typeof(TbcaTest.Domain.Enums.TaskPriority)))}.");

                    if (rowErrors.Count > 0)
                    {
                        foreach (var err in rowErrors)
                            AddError(rowNumber, err);
                        response.FailedImports -= (rowErrors.Count - 1);
                    }
                    else
                    {
                        seenTitlesInFile.Add(title);
                        currentBatch.Add(new TaskImportRow
                        {
                            RowNumber   = rowNumber,
                            Title       = title,
                            Description = string.IsNullOrEmpty(description) ? null : description,
                            DueDate     = dueDate,
                            Priority    = priority
                        });
                        response.SuccessfulImports++;

                        if (currentBatch.Count >= BatchSize)
                        {
                            await FlushBatchAsync(currentBatch);
                            currentBatch.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddError(rowNumber, $"Unexpected error processing row: {ex.Message}");
                }
            }

            if (currentBatch.Count > 0)
            {
                await FlushBatchAsync(currentBatch);
                currentBatch.Clear();
            }

            return response;
        }
    }
}

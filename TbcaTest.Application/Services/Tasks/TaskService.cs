using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Application.DTOs.Tasks;
using TbcaTest.Domain.Entities;
using TbcaTest.Domain.Exceptions;

namespace TbcaTest.Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IUnitOfWork _unitOfWork;

        public TaskService(ITaskRepository taskRepository, IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _unitOfWork = unitOfWork;
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

            // Register CodePages encoding provider required by ExcelDataReader
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            // 1. Pre-load existing titles (set-based O(1) lookup)
            var existingTitles = new HashSet<string>(
                await _taskRepository.GetAllTitlesAsync(),
                StringComparer.OrdinalIgnoreCase);

            var seenTitlesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentBatch = new List<TaskImportRow>(BatchSize);

            // Begin single transaction for atomic bulk inserts
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(excelStream);
                
                // Check if the dataset has at least one sheet
                if (reader.FieldCount == 0)
                {
                    response.Errors.Add(new ImportTaskError
                    {
                        RowNumber = 0,
                        Message = "No worksheets found or file is empty."
                    });
                    return response;
                }

                // Read header row (skip it)
                bool hasHeader = reader.Read();
                if (!hasHeader) return response;

                int rowNumber = 1; // 1 was header

                while (reader.Read())
                {
                    rowNumber++;
                    response.TotalRowsProcessed++;

                    try
                    {
                        // Safely get string values from reader
                        string title       = reader.GetValue(0)?.ToString()?.Trim() ?? string.Empty;
                        string description = reader.GetValue(1)?.ToString()?.Trim() ?? string.Empty;
                        string dueDateStr  = reader.GetValue(2)?.ToString() ?? string.Empty;
                        string priorityStr = reader.GetValue(3)?.ToString()?.Trim() ?? string.Empty;

                        var rowErrors = new List<string>();

                        // Validations
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            rowErrors.Add("Title is required.");
                        }
                        else if (title.Length > 100)
                        {
                            rowErrors.Add("Title must not exceed 100 characters.");
                        }
                        else if (existingTitles.Contains(title) || seenTitlesInFile.Contains(title))
                        {
                            rowErrors.Add($"A task with the title '{title}' already exists.");
                        }

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
                            response.FailedImports++;
                            response.Errors.Add(new ImportTaskError
                            {
                                RowNumber = rowNumber,
                                Message   = string.Join(" ", rowErrors)
                            });
                        }
                        else
                        {
                            seenTitlesInFile.Add(title);
                            currentBatch.Add(new TaskImportRow
                            {
                                Title       = title,
                                Description = string.IsNullOrEmpty(description) ? null : description,
                                DueDate     = dueDate,
                                Priority    = priority
                            });
                            response.SuccessfulImports++;

                            // If we hit the batch size, insert and clear memory
                            if (currentBatch.Count >= BatchSize)
                            {
                                await _taskRepository.BulkInsertBatchAsync(currentBatch, transaction);
                                currentBatch.Clear(); // Limpa a memória para o próximo lote
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        response.FailedImports++;
                        response.Errors.Add(new ImportTaskError
                        {
                            RowNumber = rowNumber,
                            Message   = $"Unexpected error processing row: {ex.Message}"
                        });
                    }
                }

                // Insert any remaining valid rows that didn't fill a complete batch
                if (currentBatch.Count > 0)
                {
                    await _taskRepository.BulkInsertBatchAsync(currentBatch, transaction);
                    currentBatch.Clear();
                }

                // If no exception occurred up to this point, commit the transaction
                transaction.Commit();
            }
            catch (Exception ex)
            {
                // Any single batch database insertion failure rolls back ALL batches
                transaction?.Rollback();

                // Mark ALL valid rows as failed and wipe the success counter
                response.FailedImports += response.SuccessfulImports;
                response.SuccessfulImports = 0;
                
                response.Errors.Add(new ImportTaskError
                {
                    RowNumber = 0,
                    Message   = $"Database error during batch insertion. All batches rolled back. Reason: {ex.Message}"
                });
            }

            return response;
        }
    }
}

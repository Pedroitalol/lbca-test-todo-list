using System.Collections.Generic;

namespace TbcaTest.Application.DTOs.Tasks
{
    public class ImportTaskResponse
    {
        public int TotalRowsProcessed { get; set; }
        public int SuccessfulImports { get; set; }
        public int FailedImports { get; set; }
        public List<ImportTaskError> Errors { get; set; } = new List<ImportTaskError>();
    }
}

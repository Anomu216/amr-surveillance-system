namespace AMRSurveillance.Imports.Models;

public class ImportResult<TEntity>
    where TEntity : class
{
    public List<TEntity> Records { get; set; } = new();

    // Validation errors that prevent a row from being imported.
    public List<ImportRowError> Errors { get; set; } = new();

    // Data quality warnings that do not prevent a row from being imported.
    public List<ImportRowError> Warnings { get; set; } = new();

    public ImportSummary Summary
    {
        get
        {
            var totalRows =
                Records.Count +
                Errors
                    .Select(e => e.RowNumber)
                    .Where(r => r > 0)
                    .Distinct()
                    .Count();

            var failedRows =
                Errors
                    .Select(e => e.RowNumber)
                    .Where(r => r > 0)
                    .Distinct()
                    .Count();

            return new ImportSummary
            {
                TotalRows = totalRows,
                SuccessfulRows = Records.Count,
                FailedRows = failedRows
            };
        }
    }
}
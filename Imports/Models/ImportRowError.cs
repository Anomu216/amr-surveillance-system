namespace AMRSurveillance.Imports.Models;

public class ImportRowError
{
    public int RowNumber { get; set; }

    public string ColumnName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
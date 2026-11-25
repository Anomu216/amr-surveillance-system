namespace AMRSurveillance.Imports.Models;

public class ImportSummary
{
    public int TotalRows { get; set; }

    public int SuccessfulRows { get; set; }

    public int FailedRows { get; set; }

    public int ErrorCount =>
        FailedRows;
}
namespace AMRSurveillance.ViewModels;

public class WorksheetImportResult
{
    public int TotalRows { get; set; }

    public int SuccessfulRows { get; set; }

    public int FailedRows { get; set; }

    public List<WorksheetImportError> Errors { get; set; }
        = new();
}

public class WorksheetImportError
{
    public int RowNumber { get; set; }

    public string IsolateID { get; set; }
        = string.Empty;

    public string ErrorMessage { get; set; }
        = string.Empty;
}
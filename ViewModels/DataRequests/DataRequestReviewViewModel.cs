namespace AMRSurveillance.ViewModels.DataRequests;

public class DataRequestReviewViewModel
{
    public int DataRequestID { get; set; }

    public string RequesterName { get; set; } =
        string.Empty;

    public string Reason { get; set; } =
        string.Empty;

    public DateTime RequestedAt { get; set; }

    public string Status { get; set; } =
        string.Empty;


    // ============================================================
    // REQUESTED DATA
    // ============================================================

    public List<DataRequestDatasetViewModel> SelectedDatasets { get; set; } = [];


    // ============================================================
    // IDENTIFIER FILTER
    // ============================================================

    public string? IdentifierType { get; set; }

    public List<string> IdentifierValues
    { get; set; } = [];


    // ============================================================
    // DATE FILTER
    // ============================================================

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }


    // ============================================================
    // OTHER FILTERS
    // ============================================================

    public List<string> FacilityIDs { get; set; } = [];

    public List<string> OrganismIDs { get; set; } = [];


    // ============================================================
    // REVIEW DETAILS
    // ============================================================

    public string? ReviewComments { get; set; }
}
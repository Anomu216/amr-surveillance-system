using Microsoft.AspNetCore.Http;

namespace AMRSurveillance.ViewModels.DataRequests;

public class DataRequestViewModel
{
    // ============================================================
    // REQUEST DETAILS
    // ============================================================

    public string Reason { get; set; } = string.Empty;


    // ============================================================
    // DATA SELECTION
    // ============================================================

    // Logical datasets and their selectable columns.
    public List<DataRequestDatasetViewModel> AvailableDatasets { get; set; } = [];

    // Maps each active AST card name (e.g., "GN83") to its antibiotic codes.
    public Dictionary<string, List<string>> AstCardAntibiotics { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);


    // ============================================================
    // FILTERS
    // ============================================================

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public List<string> FacilityIDs { get; set; } = [];

    public List<string> OrganismIDs { get; set; } = [];


    // ============================================================
    // AVAILABLE FILTER OPTIONS
    // ============================================================

    public List<DataRequestOptionViewModel> Facilities { get; set; } = [];

    public List<DataRequestOptionViewModel> Organisms { get; set; } = [];


    // ============================================================
    // IDENTIFIER UPLOAD
    // ============================================================

    // The type of identifier contained in the uploaded Excel file.
    // Examples: PatientID, IsolateID, KENID, MRSNID.
    public string? IdentifierType { get; set; }

    // Available identifier types for the dropdown.
    public List<DataRequestOptionViewModel> IdentifierTypes { get; set; } = [];

    // Excel file containing the requested identifiers.
    public IFormFile? IdentifierFile { get; set; }


    // ============================================================
    // IDENTIFIER VALIDATION
    // ============================================================

    // Results of validating uploaded identifiers.
    public List<DataRequestIdentifierValidationViewModel>
        IdentifierValidationResults
    { get; set; } = [];

    // Number of identifiers that do not match the required format.
    public int InvalidIdentifierCount =>
        IdentifierValidationResults.Count(
            x => !x.IsPatternValid);

    // Number of identifiers with a valid format that were not found
    // in the database.
    public int MissingIdentifierCount =>
        IdentifierValidationResults.Count(
            x => x.IsPatternValid &&
                 !x.ExistsInDatabase);

    // True when one or more uploaded identifiers have an invalid
    // format. These should prevent the request from being submitted.
    public bool HasIdentifierValidationErrors =>
        InvalidIdentifierCount > 0;


    // ============================================================
    // REQUEST STATUS / FEEDBACK
    // ============================================================

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }
}


// ================================================================
// DATASET
// ================================================================

public class DataRequestDatasetViewModel
{
    // Logical dataset name displayed to the user.
    public string Name { get; set; } = string.Empty;

    // Internal dataset identifier.
    public string Value { get; set; } = string.Empty;

    // Columns available for selection.
    public List<DataRequestColumnViewModel> Columns { get; set; } = [];
}


// ================================================================
// COLUMN
// ================================================================

public class DataRequestColumnViewModel
{
    // Actual column/property name used by the export service.
    public string Value { get; set; } = string.Empty;

    // Friendly column name displayed to the user.
    public string Text { get; set; } = string.Empty;

    // Whether the user selected this column for the request.
    public bool IsSelected { get; set; }
}


// ================================================================
// DROPDOWN / FILTER OPTION
// ================================================================

public class DataRequestOptionViewModel
{
    public string Value { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}


// ================================================================
// IDENTIFIER VALIDATION RESULT
// ================================================================

public class DataRequestIdentifierValidationViewModel
{
    // Identifier supplied by the user.
    public string Identifier { get; set; } = string.Empty;

    // Indicates whether the identifier matches the expected pattern
    // for the selected identifier type.
    public bool IsPatternValid { get; set; }

    // Indicates whether a valid-format identifier exists in the
    // relevant database table or logical relationship.
    public bool ExistsInDatabase { get; set; }

    // Indicates whether this identifier can be used to resolve
    // related datasets.
    public bool CanResolveRelationships { get; set; }

    // Error or warning message returned to the user.
    public string? Message { get; set; }
}
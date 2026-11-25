using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.ViewModels;

public class WorksheetViewModel
{
    // =====================================================
    // Worksheet / Isolate Information
    // =====================================================

    [Required]
    [StringLength(20)]
    public string PatientID { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string IsolateID { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string CultureResult { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ManuscriptID { get; set; }

    // =====================================================
    // Organism Identification
    // =====================================================

    [StringLength(100)]
    public string? AcceptedID { get; set; }

    [StringLength(100)]
    public string? WHONETID { get; set; }

    /// Available WHONET IDs from the Organism reference table.
    public IEnumerable<SelectListItem> WHONETIDs { get; set; }
        = [];

    // MALDIID belongs to Shipment, not Worksheet.

    // =====================================================
    // Laboratory Characteristics
    // =====================================================

    [StringLength(20)]
    public string? GramReaction { get; set; }

    [StringLength(50)]
    public string? Morphology { get; set; }

    [StringLength(20)]
    public string? Hemolysis { get; set; }

    [StringLength(20)]
    public string? Catalase { get; set; }

    [StringLength(20)]
    public string? Oxidase { get; set; }

    [StringLength(20)]
    public string? Coagulase { get; set; }

    [StringLength(20)]
    public string? LactoseFermentation { get; set; }

    [StringLength(500)]
    public string? Comments { get; set; }

    // =====================================================
    // AST Cards & Organism Mapping
    // =====================================================

    /// Multiple AST cards can be selected in the UI.
    /// The database stores them as a semicolon-delimited string, e.g., GN83;N808.
    public List<string> ASTCards { get; set; } = [];

    /// Available AST cards populated from ASTCards / Antibiotic configuration.
    public IEnumerable<SelectListItem> AvailableASTCards { get; set; }
        = [];

    /// Optional map of normalized ASTCard code -> mapped WHONETIDs from ASTCardOrganisms.
    public Dictionary<string, List<string>> CardOrganisms { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // =====================================================
    // AST Findings
    // =====================================================

    /// Manually entered by the laboratory user. Valid values: POS, NEG.
    [StringLength(10)]
    public string? ESBL { get; set; }

    /// Automatically calculated.
    /// Displayed for QC only; not entered by the user.
    public bool? MDR { get; set; }

    /// Automatically calculated.
    /// Displayed for QC only; not entered by the user.
    public bool? Carbapenemase { get; set; }

    /// Automatically calculated.
    /// Displayed for QC only; not entered by the user.
    public bool? MRSA { get; set; }

    /// Cefoxitin screening result.
    /// Valid values: POS, NEG.
    [StringLength(10)]
    public string? CefoxitinScreen { get; set; }

    /// Inducible clindamycin resistance result.
    /// Valid values: POS, NEG.
    [StringLength(10)]
    public string? InducibleClindamycinResistance { get; set; }

    [StringLength(500)]
    public string? ASTComments { get; set; }

    // =====================================================
    // Dynamic AST Results
    // =====================================================

    public List<WorksheetAntibioticViewModel> Antibiotics { get; set; } = [];
}
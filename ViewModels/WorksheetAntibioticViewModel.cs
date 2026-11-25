using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.ViewModels;

public class WorksheetAntibioticViewModel
{
    // =====================================================
    // Antibiotic Identification
    // =====================================================

    public string AntibioticCode { get; set; } = string.Empty;

    public string AntibioticName { get; set; } = string.Empty;

    public string AntibioticClassName { get; set; } = string.Empty;

    public string ASTCard { get; set; } = string.Empty;

    // Supports antibiotics present on multiple selected cards (e.g., Meropenem on GN83 and XN28)
    public List<string> ASTCards { get; set; } = new();

    // Preserves hard-copy worksheet ordering per AST card
    public int CardDisplayOrder { get; set; } = int.MaxValue;


    // =====================================================
    // AST Configuration
    // =====================================================

    public bool MICEnabled { get; set; }

    public bool InterpretationEnabled { get; set; }

    // True when this antibiotic is intrinsically resistant
    // for the selected organism.
    public bool IsIntrinsicallyResistant { get; set; }


    // =====================================================
    // Worksheet Results
    // =====================================================

    [Display(Name = "MIC")]
    public string? MIC { get; set; }

    [Display(Name = "Interpretation")]
    public string? Interpretation { get; set; }
}
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.ViewModels;

public class AntibioticViewModel
{
    // =============================================
    // Antibiotic
    // =============================================

    [Required]
    [Display(Name = "Antibiotic Code")]
    [StringLength(20)]
    public string AntibioticCode { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Antibiotic Name")]
    [StringLength(200)]
    public string AntibioticName { get; set; } = string.Empty;

    // =============================================
    // Antibiotic Class
    // =============================================

    [Required]
    [Display(Name = "Antibiotic Class")]
    public string AntibioticClassName { get; set; } = string.Empty;

    // =============================================
    // AST Configuration
    // =============================================

    [Required]
    [Display(Name = "AST Card")]
    [StringLength(500)]
    public string ASTCard { get; set; } = string.Empty;

    [Display(Name = "MIC Enabled")]
    public bool MICEnabled { get; set; } = true;

    [Display(Name = "Interpretation Enabled")]
    public bool InterpretationEnabled { get; set; } = true;

    [Display(Name = "Active")]
    public bool Active { get; set; } = true;

    [Display(Name = "Include in Monthly Report (High-Consequence / Reserve)")]
    public bool IncludeInMonthlyReport { get; set; } = false;

    // =============================================
    // Lookup
    // =============================================

    public IEnumerable<SelectListItem> AntibioticClasses { get; set; }
        = Enumerable.Empty<SelectListItem>();
}
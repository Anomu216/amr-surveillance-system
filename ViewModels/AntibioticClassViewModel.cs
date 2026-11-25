using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.ViewModels;

public class AntibioticClassViewModel
{
    [Required]
    [Display(Name = "Antibiotic Class")]
    [StringLength(100)]
    public string AntibioticClassName { get; set; } = string.Empty;

    // =============================================
    // Display Properties
    // =============================================

    public int AntibioticCount { get; set; }
    
}
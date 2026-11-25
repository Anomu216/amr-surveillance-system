using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.ViewModels;

public class OrganismViewModel
{
    // =============================================
    // WHONET ID
    // =============================================

    [Required]
    [Display(Name = "WHONET ID")]
    [StringLength(100)]
    public string WHONETID { get; set; } = string.Empty;

    // =============================================
    // Statistics
    // =============================================

    public int IntrinsicResistanceCount { get; set; }
}
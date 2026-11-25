using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AMRSurveillance.Models;

public class IntrinsicResistance
{
    // =============================================
    // Composite Primary Key
    // =============================================

    [Required]
    [StringLength(100)]
    public string WHONETID { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string AntibioticCode { get; set; } = string.Empty;

    // =============================================
    // Navigation Properties
    // =============================================

    [ForeignKey(nameof(WHONETID))]
    public Organism Organism { get; set; } = null!;

    [ForeignKey(nameof(AntibioticCode))]
    public Antibiotic Antibiotic { get; set; } = null!;
}
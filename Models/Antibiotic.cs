using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AMRSurveillance.Models;

public class Antibiotic
{
    // =============================================
    // Primary Key
    // =============================================

    [Key]
    [StringLength(20)]
    public string AntibioticCode { get; set; } = string.Empty;

    // =============================================
    // Antibiotic Information
    // =============================================

    [Required]
    [StringLength(200)]
    public string AntibioticName { get; set; } = string.Empty;

    // =============================================
    // Antibiotic Class
    // =============================================

    [Required]
    [StringLength(100)]
    public string AntibioticClassName { get; set; } = string.Empty;

    [ForeignKey(nameof(AntibioticClassName))]
    public AntibioticClass AntibioticClass { get; set; } = null!;

    // =============================================
    // AST Card Configuration
    // =============================================

    [Required]
    [StringLength(500)]
    public string ASTCard { get; set; } = string.Empty;

    // =============================================
    // AST Configuration
    // =============================================

    public bool MICEnabled { get; set; } = true;

    public bool InterpretationEnabled { get; set; } = true;

    public bool Active { get; set; } = true;

    public bool IncludeInMonthlyReport { get; set; } = false;

    // =============================================
    // Navigation Properties
    // =============================================

    public ICollection<IntrinsicResistance> IntrinsicResistances { get; set; }
        = new List<IntrinsicResistance>();
}
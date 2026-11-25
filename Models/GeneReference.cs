
using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.Models;

public class GeneReference
{
    // =====================================================
    // Primary Key
    // =====================================================

    [Key]
    [Required]
    [StringLength(100)]
    public string GeneName { get; set; } = string.Empty;

    // =====================================================
    // Gene Information
    // =====================================================

    [StringLength(255)]
    public string? GeneDescription { get; set; }

    [StringLength(50)]
    public string? GeneCategory { get; set; }

    [StringLength(100)]
    public string? ApplicableOrganism { get; set; }

    // =====================================================
    // Status
    // =====================================================

    public bool IsActive { get; set; } = true;
}

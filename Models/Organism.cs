using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.Models;

public class Organism
{
    // =============================================
    // Primary Key
    // =============================================

    [Key]
    [StringLength(100)]
    public string WHONETID { get; set; } = string.Empty;

    // =============================================
    // Navigation Properties
    // =============================================

    public ICollection<Worksheet> Worksheets { get; set; }
        = [];

    public ICollection<IntrinsicResistance> IntrinsicResistances { get; set; }
        = [];

    public ICollection<ASTCardOrganism> ASTCardOrganisms { get; set; }
        = [];
}
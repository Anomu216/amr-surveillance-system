using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.Models;

public class ASTCardDefinition
{
    [Key]
    [StringLength(20)]
    public string ASTCard { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ASTCardOrganism> ASTCardOrganisms { get; set; } = [];
    public ICollection<ASTCardAntibiotic> ASTCardAntibiotics { get; set; } = [];
}
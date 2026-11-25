using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.Models;

public class ASTCardAntibiotic
{
    public int Id { get; set; }

    [StringLength(20)]
    public string ASTCard { get; set; } = string.Empty;

    [StringLength(20)]
    public string AntibioticCode { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public ASTCardDefinition Card { get; set; } = null!;
}
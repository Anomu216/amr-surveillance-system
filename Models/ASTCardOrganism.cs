using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.Models;

public class ASTCardOrganism
{
    [StringLength(20)]
    public string ASTCard { get; set; } = string.Empty;

    [StringLength(100)]
    public string WHONETID { get; set; } = string.Empty;

    public ASTCardDefinition Card { get; set; } = null!;
    public Organism Organism { get; set; } = null!;
}
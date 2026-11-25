using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.Models;

public class AntibioticClass
{
    // =============================================
    // Primary Key
    // =============================================

    [Key]
    [StringLength(100)]
    public string AntibioticClassName { get; set; } = string.Empty;

    // =============================================
    // Navigation Properties
    // =============================================

    public ICollection<Antibiotic> Antibiotics { get; set; }
        = new List<Antibiotic>();
}
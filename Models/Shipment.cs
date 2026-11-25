using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AMRSurveillance.Models;

public class Shipment
{
    // =====================================================
    // Primary Key
    // =====================================================

    [Key]
    [StringLength(150)]
    public string KENID { get; set; } = string.Empty;

    // =====================================================
    // Worksheet Link
    // =====================================================

    [Required]
    [StringLength(20)]
    public string IsolateID { get; set; } = string.Empty;

    // =====================================================
    // Shipment Information
    // =====================================================

    [StringLength(150)]
    public string? MALDIID { get; set; }

    [StringLength(150)]
    public string? MRSNID { get; set; }

    public DateTime? ShipmentDate { get; set; }

    [StringLength(100)]
    public string? ShipmentNumber { get; set; }

    [StringLength(100)]
    public string? BoxNumber { get; set; }

    // =====================================================
    // Navigation Property
    // =====================================================

    [ForeignKey(nameof(IsolateID))]
    public Worksheet Worksheet { get; set; } = null!;
}
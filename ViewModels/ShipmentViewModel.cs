using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.ViewModels;

public class ShipmentViewModel
{
    // =====================================================
    // Shipment Identifier
    // =====================================================

    [Required]
    [StringLength(150)]
    public string KENID { get; set; } = string.Empty;

    // =====================================================
    // Worksheet / Isolate
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
}
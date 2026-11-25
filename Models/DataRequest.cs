using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AMRSurveillance.Models;

public class DataRequest
{
    // ============================================================
    // PRIMARY KEY
    // ============================================================

    [Key]
    public int DataRequestID { get; set; }


    // ============================================================
    // REQUEST INFORMATION
    // ============================================================

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Pending";


    // ============================================================
    // REQUESTED DATA CONFIGURATION
    // ============================================================

    // Stores the selected tables and columns as JSON.
    [Column(TypeName = "nvarchar(max)")]
    public string? SelectedData { get; set; }

    // Stores filter settings such as date range, organism,
    // facility, and other supported filters as JSON.
    [Column(TypeName = "nvarchar(max)")]
    public string? Filters { get; set; }


    // ============================================================
    // ID-BASED REQUEST CONFIGURATION
    // ============================================================

    // Examples: PatientID, IsolateID, MRSNID, KENID.
    [StringLength(50)]
    public string? IdentifierType { get; set; }

    // Stores the uploaded IDs or a reference to the uploaded file.
    [Column(TypeName = "nvarchar(max)")]
    public string? IdentifierValues { get; set; }


    // ============================================================
    // REQUEST AUDIT INFORMATION
    // ============================================================

    [Required]
    [StringLength(450)]
    public string RequestedByUserID { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;


    // ============================================================
    // APPROVAL INFORMATION
    // ============================================================

    [StringLength(450)]
    public string? ReviewedByUserID { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [StringLength(1000)]
    public string? ReviewComments { get; set; }


    // ============================================================
    // EXPORT INFORMATION
    // ============================================================

    public DateTime? ExportedAt { get; set; }
}
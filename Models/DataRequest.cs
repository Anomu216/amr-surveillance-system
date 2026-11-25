using AMRSurveillance.Security;
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
    // EXPORT & EXPIRATION INFORMATION
    // ============================================================

    public DateTime? ExportedAt { get; set; }

    // Stamped once on the very first download; never overwritten on re-downloads.
    public DateTime? FirstDownloadedAt { get; set; }

    // Set to FirstDownloadedAt + 72 hours upon first download.
    public DateTime? ExpiresAt { get; set; }

    [NotMapped]
    public bool IsExpired =>
            string.Equals(Status, "Expired", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
             ExpiresAt.HasValue &&
             EastAfricaTime.Now >= ExpiresAt.Value);

    [NotMapped]
    public bool IsHiddenFromUser =>
        string.Equals(Status, "Rejected", StringComparison.OrdinalIgnoreCase) &&
        ReviewedAt.HasValue &&
        EastAfricaTime.Now >= ReviewedAt.Value.AddHours(72);
}
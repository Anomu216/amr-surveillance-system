using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AMRSurveillance.Models;

public class Worksheet
{
    // ============================================================
    // PRIMARY KEY
    // ============================================================

    [Key]
    [Required]
    [StringLength(20)]
    public string IsolateID { get; set; } = string.Empty;

    // ============================================================
    // ISOLATE INFORMATION
    // ============================================================

    [Required]
    [StringLength(20)]
    public string PatientID { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string CultureResult { get; set; } = string.Empty;

    [StringLength(100)]
    public string? AcceptedID { get; set; }

    [StringLength(100)]
    public string? WHONETID { get; set; }

    // ============================================================
    // ISOLATE CHARACTERISTICS
    // ============================================================

    [StringLength(20)]
    public string? GramReaction { get; set; }

    [StringLength(50)]
    public string? Morphology { get; set; }

    [StringLength(20)]
    public string? Hemolysis { get; set; }

    [StringLength(20)]
    public string? Catalase { get; set; }

    [StringLength(20)]
    public string? Oxidase { get; set; }

    [StringLength(20)]
    public string? Coagulase { get; set; }

    [StringLength(20)]
    public string? LactoseFermentation { get; set; }

    [StringLength(500)]
    public string? Comments { get; set; }

    // ============================================================
    // AST INFORMATION
    // ============================================================

    // Stored as one database field. UI will allow multiple card selections, e.g. "XN28;XN05;GN83"
    [StringLength(500)]
    public string? ASTCard { get; set; }

    // ============================================================
    // AST / LABORATORY FINDINGS
    // ============================================================

    // Automatically calculated from AST results, antibiotic categories and intrinsic resistance rules.
    public bool? MDR { get; set; }

    // Manually entered by the user as POS / NEG.
    [StringLength(10)]
    public string? ESBL { get; set; }

    // Automatically calculated.
    public bool? Carbapenemase { get; set; }

    // Automatically calculated.
    public bool? MRSA { get; set; }

    // Manual laboratory screening result.
    [StringLength(10)]
    public string? CefoxitinScreen { get; set; }

    // Manual laboratory result.
    [StringLength(10)]
    public string? InducibleClindamycinResistance { get; set; }

    [StringLength(500)]
    public string? ASTComments { get; set; }

    // ============================================================
    // PUBLICATION / MANUSCRIPT TRACKING
    // ============================================================

    [StringLength(100)]
    public string? ManuscriptID { get; set; }

    // ============================================================
    // NAVIGATION PROPERTIES
    // ============================================================

    public Patient Patient { get; set; } = null!;

    public Organism? Organism { get; set; }

    public ICollection<Shipment> Shipments { get; set; } = [];
}
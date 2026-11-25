
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AMRSurveillance.Models;

public class Genomics
{
    // =====================================================
    // Primary Key One genomics record per isolate
    // =====================================================

    [Key]
    [Required]
    [StringLength(20)]
    public string IsolateID { get; set; } = string.Empty;

    // =====================================================
    // Shipment Identification 
     // =====================================================

    [StringLength(150)]
    public string? KENID { get; set; }

    [StringLength(150)]
    public string? MRSNID { get; set; }

    // =====================================================
    // Sample and Sequencing Information
    // =====================================================

    [StringLength(100)]
    public string? DNASampleID { get; set; }

    [StringLength(100)]
    public string? GenomicONTID { get; set; }

    [StringLength(100)]
    public string? GenomicILMID { get; set; }

    [StringLength(100)]
    public string? ProjectCode { get; set; }

    [StringLength(100)]
    public string? SeqRunID { get; set; }

    [Column(TypeName = "date")]
    public DateTime? SequencingDate { get; set; }

    [StringLength(255)]
    public string? SubmittingProtocol { get; set; }

    // =====================================================
    // Organism Identification
    // =====================================================

    [StringLength(100)]
    public string? WHONETID { get; set; }

    [StringLength(100)]
    public string? WGSID { get; set; }

    // =====================================================
    // Genome Assembly Metrics
    // =====================================================

    public long? GenomeLength { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? GCPercent { get; set; }

    public int? NoOfContigs { get; set; }

    public long? AssemblyN50 { get; set; }

    public bool? CompleteGenome { get; set; }

    public int? NoOfPlasmids { get; set; }

    // =====================================================
    // Strain Typing
    // =====================================================

    [StringLength(50)]
    public string? StrainType { get; set; }

    // Escherichia coli

    [StringLength(50)]
    public string? Phylogroup { get; set; }

    [StringLength(100)]
    public string? Serotype { get; set; }

    [StringLength(100)]
    public string? Fimtype { get; set; }

    // Staphylococcus aureus

    [StringLength(50)]
    public string? SCCmecType { get; set; }

    [StringLength(50)]
    public string? SpaType { get; set; }

    // Klebsiella pneumoniae and Acinetobacter baumannii

    [StringLength(50)]
    public string? OLocus { get; set; }

    [StringLength(50)]
    public string? KLocus { get; set; }

    // Klebsiella pneumoniae

    [Column(TypeName = "decimal(8,2)")]
    public decimal? VirulenceScore { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal? ResistanceScore { get; set; }

    // =====================================================
    // Navigation Properties
    // =====================================================

    [ForeignKey(nameof(IsolateID))]
    public Worksheet Worksheet { get; set; } = null!;
    public Shipment? Shipment { get; set; }
    public Organism? Organism { get; set; }
}

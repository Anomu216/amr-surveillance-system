
using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.ViewModels;

public class GenomicsViewModel
{
    // =====================================================
    // Isolate and Shipment Identification
    // =====================================================

    [Required(ErrorMessage = "Select an isolate.")]
    [StringLength(20)]
    public string IsolateID { get; set; } = string.Empty;

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

    [Range(1, long.MaxValue,
        ErrorMessage = "Genome length must be greater than zero.")]
    public long? GenomeLength { get; set; }

    [Range(typeof(decimal), "0", "100",
        ErrorMessage = "GC percentage must be between 0 and 100.")]
    public decimal? GCPercent { get; set; }

    [Range(0, int.MaxValue,
        ErrorMessage = "Number of contigs cannot be negative.")]
    public int? NoOfContigs { get; set; }

    [Range(1, long.MaxValue,
        ErrorMessage = "Assembly N50 must be greater than zero.")]
    public long? AssemblyN50 { get; set; }

    public bool? CompleteGenome { get; set; }

    [Range(0, int.MaxValue,
        ErrorMessage = "Number of plasmids cannot be negative.")]
    public int? NoOfPlasmids { get; set; }

    // =====================================================
    // General Strain Typing
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

    // Klebsiella pneumoniae and
    // Acinetobacter baumannii

    [StringLength(50)]
    public string? OLocus { get; set; }

    [StringLength(50)]
    public string? KLocus { get; set; }

    // Klebsiella pneumoniae

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal? VirulenceScore { get; set; }

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal? ResistanceScore { get; set; }
}

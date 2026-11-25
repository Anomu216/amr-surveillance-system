
namespace AMRSurveillance.ViewModels;

/// Represents one Genomics record, including its fixed fields
/// and dynamically registered gene results.
public class GenomicsRecordViewModel : GenomicsViewModel
{
    /// Gene name mapped to its nullable detection result.
    /// true  = gene detected (1)
    /// false = gene not detected (0)
    /// null  = unknown or not tested (NULL)
    public Dictionary<string, bool?> GeneValues { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

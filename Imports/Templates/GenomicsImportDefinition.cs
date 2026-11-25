
using AMRSurveillance.Imports.Models;

namespace AMRSurveillance.Imports.Templates;

public static class GenomicsImportDefinition
{
    public static IReadOnlyList<ImportFieldDefinition> BuildFields(
        IEnumerable<string> registeredGeneNames)
    {
        var fields = new List<ImportFieldDefinition>
        {
            // Required isolate identifier
            new()
            {
                Name = "IsolateID",
                Type = ImportFieldType.String,
                Required = true
            },

            // Identifiers and project metadata
            new() { Name = "KENID", Type = ImportFieldType.String },
            new() { Name = "MRSNID", Type = ImportFieldType.String },
            new() { Name = "DNASampleID", Type = ImportFieldType.String },
            new() { Name = "GenomicONTID", Type = ImportFieldType.String },
            new() { Name = "GenomicILMID", Type = ImportFieldType.String },
            new() { Name = "ProjectCode", Type = ImportFieldType.String },
            new() { Name = "SeqRunID", Type = ImportFieldType.String },
            new() { Name = "SubmittingProtocol", Type = ImportFieldType.String },
            new() { Name = "WHONETID", Type = ImportFieldType.String },
            new() { Name = "WGSID", Type = ImportFieldType.String },

            // Sequencing and assembly metrics
            new() { Name = "SequencingDate", Type = ImportFieldType.Date },
            new() { Name = "GenomeLength", Type = ImportFieldType.Integer },
            new() { Name = "GCPercent", Type = ImportFieldType.Decimal },
            new() { Name = "NoOfContigs", Type = ImportFieldType.Integer },
            new() { Name = "AssemblyN50", Type = ImportFieldType.Integer },
            new() { Name = "CompleteGenome", Type = ImportFieldType.String },
            new() { Name = "NoOfPlasmids", Type = ImportFieldType.Integer },

            // Typing information
            new() { Name = "StrainType", Type = ImportFieldType.String },
            new() { Name = "Phylogroup", Type = ImportFieldType.String },
            new() { Name = "Serotype", Type = ImportFieldType.String },
            new() { Name = "Fimtype", Type = ImportFieldType.String },
            new() { Name = "SCCmecType", Type = ImportFieldType.String },
            new() { Name = "SpaType", Type = ImportFieldType.String },
            new() { Name = "OLocus", Type = ImportFieldType.String },
            new() { Name = "KLocus", Type = ImportFieldType.String },

            // Scores
            new() { Name = "VirulenceScore", Type = ImportFieldType.Decimal },
            new() { Name = "ResistanceScore", Type = ImportFieldType.Decimal }
        };

        var genes = registeredGeneNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        foreach (var geneName in genes)
        {
            fields.Add(new ImportFieldDefinition
            {
                Name = geneName,
                Type = ImportFieldType.String,
                Required = false
            });
        }

        return fields;
    }
}

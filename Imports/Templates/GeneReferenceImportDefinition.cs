
using AMRSurveillance.Imports.Models;

namespace AMRSurveillance.Imports.Templates;

public static class GeneReferenceImportDefinition
{
    public static readonly IReadOnlyList<ImportFieldDefinition> Fields =
        new List<ImportFieldDefinition>
        {
            // Gene name is required and is the reference table's primary key.
            new()
            {
                Name = "GeneName",
                Type = ImportFieldType.String,
                Required = true
            },

            // Optional gene description.
            new()
            {
                Name = "GeneDescription",
                Type = ImportFieldType.String,
                Required = false
            },

            // Optional classification, e.g. resistance mechanism/category.
            new()
            {
                Name = "GeneCategory",
                Type = ImportFieldType.String,
                Required = false
            },

            // Optional organism to which the gene applies.
            new()
            {
                Name = "ApplicableOrganism",
                Type = ImportFieldType.String,
                Required = false
            }
        };
}

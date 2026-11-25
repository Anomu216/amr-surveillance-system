using AMRSurveillance.Imports.Models;
using AMRSurveillance.Models;

namespace AMRSurveillance.Imports.Templates;

public static class FacilityImportDefinition
{
    public static readonly IReadOnlyList<ImportFieldDefinition> Fields =
        new List<ImportFieldDefinition>
        {
            new()
            {
                Name = nameof(Facility.FacilityID),
                Type = ImportFieldType.String,
                Required = true
            },

            new()
            {
                Name = nameof(Facility.FacilityName),
                Type = ImportFieldType.String,
                Required = true
            },

            new()
            {
                Name = nameof(Facility.County),
                Type = ImportFieldType.String,
                Required = true
            },

            new()
            {
                Name = nameof(Facility.SubCounty),
                Type = ImportFieldType.String,
                Required = true
            },

            new()
            {
                Name = nameof(Facility.FacilityLevel),
                Type = ImportFieldType.String,
                Required = true
            },

            new()
            {
                Name = nameof(Facility.Ownership),
                Type = ImportFieldType.String,
                Required = true
            }
        };
}
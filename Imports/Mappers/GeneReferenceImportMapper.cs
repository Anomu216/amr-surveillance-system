
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Models;
using ClosedXML.Excel;

namespace AMRSurveillance.Imports.Mappers;

public class GeneReferenceImportMapper : IImportMapper<GeneReference>
{
    public GeneReference Map(
        IXLWorksheet worksheet,
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap)
    {
        string? ReadString(string columnName)
        {
            if (!headerMap.TryGetValue(columnName, out var columnNumber))
                return null;

            var value = row.Cell(columnNumber).GetString();

            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        return new GeneReference
        {
            GeneName = ReadString("GeneName") ?? string.Empty,
            GeneDescription = ReadString("GeneDescription"),
            GeneCategory = ReadString("GeneCategory"),
            ApplicableOrganism = ReadString("ApplicableOrganism"),
            IsActive = true
        };
    }
}

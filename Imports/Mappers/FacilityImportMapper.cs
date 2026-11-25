using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Models;
using ClosedXML.Excel;

namespace AMRSurveillance.Imports.Mappers;

public class FacilityImportMapper : IImportMapper<Facility>
{
    public Facility Map(
        IXLWorksheet worksheet,
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap)
    {
        return new Facility
        {
            FacilityID = row.Cell(headerMap["FacilityID"])
                .GetString()
                .Trim(),

            FacilityName = row.Cell(headerMap["FacilityName"])
                .GetString()
                .Trim(),

            County = row.Cell(headerMap["County"])
                .GetString()
                .Trim(),

            SubCounty = row.Cell(headerMap["SubCounty"])
                .GetString()
                .Trim(),

            FacilityLevel = row.Cell(headerMap["FacilityLevel"])
                .GetString()
                .Trim(),

            Ownership = row.Cell(headerMap["Ownership"])
                .GetString()
                .Trim()
        };
    }
}
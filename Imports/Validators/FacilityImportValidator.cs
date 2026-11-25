using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Models;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using ClosedXML.Excel;

namespace AMRSurveillance.Imports.Validators;

public class FacilityImportValidator
    : IEntityImportValidator<Facility>
{
    private readonly IFacilityService _facilityService;

    private readonly IImportMapper<Facility> _mapper;

    public FacilityImportValidator(
        IFacilityService facilityService,
        IImportMapper<Facility> mapper)
    {
        _facilityService =
            facilityService;

        _mapper =
            mapper;
    }

    public async Task<ImportResult<Facility>> ValidateAsync(
        IXLWorksheet worksheet)
    {
        var result =
            new ImportResult<Facility>();

        var headerMap =
            worksheet
                .Row(1)
                .CellsUsed()
                .ToDictionary(
                    c => c.GetString().Trim(),
                    c => c.Address.ColumnNumber,
                    StringComparer.OrdinalIgnoreCase);

        var importedFacilityIDs =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            int rowNumber =
                row.RowNumber();

            // -----------------------------------------
            // Map Excel row
            // -----------------------------------------

            var facility =
                _mapper.Map(
                    worksheet,
                    row,
                    headerMap);

            bool valid = true;


            // -----------------------------------------
            // Facility ID
            // -----------------------------------------

            if (string.IsNullOrWhiteSpace(
                facility.FacilityID))
            {
                result.Errors.Add(
                    new ImportRowError
                    {
                        RowNumber = rowNumber,
                        ColumnName =
                            nameof(Facility.FacilityID),
                        Message =
                            "Facility ID is required."
                    });

                valid = false;
            }
            else
            {
                if (facility.FacilityID !=
                    facility.FacilityID.ToUpperInvariant())
                {
                    result.Errors.Add(
                        new ImportRowError
                        {
                            RowNumber = rowNumber,
                            ColumnName =
                                nameof(Facility.FacilityID),
                            Message =
                                "Facility ID must be uppercase."
                        });

                    valid = false;
                }


                if (!importedFacilityIDs.Add(
                    facility.FacilityID))
                {
                    result.Errors.Add(
                        new ImportRowError
                        {
                            RowNumber = rowNumber,
                            ColumnName =
                                nameof(Facility.FacilityID),
                            Message =
                                "Duplicate Facility ID in uploaded file."
                        });

                    valid = false;
                }


                if (await _facilityService
                    .ExistsAsync(facility.FacilityID))
                {
                    result.Errors.Add(
                        new ImportRowError
                        {
                            RowNumber = rowNumber,
                            ColumnName =
                                nameof(Facility.FacilityID),
                            Message =
                                "Facility already exists."
                        });

                    valid = false;
                }
            }


            // -----------------------------------------
            // Facility Name
            // -----------------------------------------

            if (string.IsNullOrWhiteSpace(
                facility.FacilityName))
            {
                result.Errors.Add(
                    new ImportRowError
                    {
                        RowNumber = rowNumber,
                        ColumnName =
                            nameof(Facility.FacilityName),
                        Message =
                            "Facility Name is required."
                    });

                valid = false;
            }


            // -----------------------------------------
            // County
            // -----------------------------------------

            if (string.IsNullOrWhiteSpace(
                facility.County))
            {
                result.Errors.Add(
                    new ImportRowError
                    {
                        RowNumber = rowNumber,
                        ColumnName =
                            nameof(Facility.County),
                        Message =
                            "County is required."
                    });

                valid = false;
            }


            // -----------------------------------------
            // SubCounty
            // -----------------------------------------

            if (string.IsNullOrWhiteSpace(
                facility.SubCounty))
            {
                result.Errors.Add(
                    new ImportRowError
                    {
                        RowNumber = rowNumber,
                        ColumnName =
                            nameof(Facility.SubCounty),
                        Message =
                            "SubCounty is required."
                    });

                valid = false;
            }


            // -----------------------------------------
            // Facility Level
            // -----------------------------------------

            if (string.IsNullOrWhiteSpace(
                facility.FacilityLevel))
            {
                result.Errors.Add(
                    new ImportRowError
                    {
                        RowNumber = rowNumber,
                        ColumnName =
                            nameof(Facility.FacilityLevel),
                        Message =
                            "Facility Level is required."
                    });

                valid = false;
            }


            // -----------------------------------------
            // Ownership
            // -----------------------------------------

            if (string.IsNullOrWhiteSpace(
                facility.Ownership))
            {
                result.Errors.Add(
                    new ImportRowError
                    {
                        RowNumber = rowNumber,
                        ColumnName =
                            nameof(Facility.Ownership),
                        Message =
                            "Ownership is required."
                    });

                valid = false;
            }


            // -----------------------------------------
            // Add valid record
            // -----------------------------------------

            if (valid)
            {
                result.Records.Add(
                    facility);
            }
        }

        return result;
    }
}
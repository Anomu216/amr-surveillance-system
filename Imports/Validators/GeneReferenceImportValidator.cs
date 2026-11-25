
using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Models;
using AMRSurveillance.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Imports.Validators;

public class GeneReferenceImportValidator
    : IEntityImportValidator<GeneReference>
{
    private readonly ApplicationDbContext _context;
    private readonly IImportMapper<GeneReference> _mapper;

    public GeneReferenceImportValidator(
        ApplicationDbContext context,
        IImportMapper<GeneReference> mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<ImportResult<GeneReference>> ValidateAsync(
        IXLWorksheet worksheet)
    {
        var result = new ImportResult<GeneReference>();

        var headerMap = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        var headerRow = worksheet.FirstRowUsed();

        if (headerRow == null)
        {
            result.Errors.Add(new ImportRowError
            {
                RowNumber = 1,
                ColumnName = string.Empty,
                Message = "The worksheet is empty."
            });

            return result;
        }

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();

            if (!string.IsNullOrWhiteSpace(header))
                headerMap[header] = cell.Address.ColumnNumber;
        }

        var requiredHeaders = new[]
        {
            "GeneName",
            "GeneDescription",
            "GeneCategory",
            "ApplicableOrganism"
        };

        foreach (var requiredHeader in requiredHeaders)
        {
            if (!headerMap.ContainsKey(requiredHeader))
            {
                result.Errors.Add(new ImportRowError
                {
                    RowNumber = headerRow.RowNumber(),
                    ColumnName = requiredHeader,
                    Message = $"Required column '{requiredHeader}' is missing."
                });
            }
        }

        if (result.Errors.Any())
            return result;

        var existingNames = await _context.GeneReferences
            .AsNoTracking()
            .Select(g => g.GeneName)
            .ToListAsync();

        var knownNames = new HashSet<string>(
            existingNames,
            StringComparer.OrdinalIgnoreCase);

        var fileNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var lastRow = worksheet.LastRowUsed()?.RowNumber()
                      ?? headerRow.RowNumber();

        for (var rowNumber = headerRow.RowNumber() + 1;
             rowNumber <= lastRow;
             rowNumber++)
        {
            var row = worksheet.Row(rowNumber);

            if (row.IsEmpty())
                continue;

            var gene = _mapper.Map(worksheet, row, headerMap);
            var rowErrors = new List<ImportRowError>();

            if (string.IsNullOrWhiteSpace(gene.GeneName))
            {
                rowErrors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    ColumnName = "GeneName",
                    Message = "Gene name is required."
                });
            }
            else
            {
                if (gene.GeneName.Length > 100)
                {
                    rowErrors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        ColumnName = "GeneName",
                        Message = "Gene name cannot exceed 100 characters."
                    });
                }

                if (knownNames.Contains(gene.GeneName))
                {
                    rowErrors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        ColumnName = "GeneName",
                        Message = "This gene already exists in Gene Reference."
                    });
                }

                if (!fileNames.Add(gene.GeneName))
                {
                    rowErrors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        ColumnName = "GeneName",
                        Message = "This gene name appears more than once in the uploaded file."
                    });
                }
            }

            if (gene.GeneDescription?.Length > 255)
            {
                rowErrors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    ColumnName = "GeneDescription",
                    Message = "Description cannot exceed 255 characters."
                });
            }

            if (gene.GeneCategory?.Length > 50)
            {
                rowErrors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    ColumnName = "GeneCategory",
                    Message = "Gene category cannot exceed 50 characters."
                });
            }

            if (gene.ApplicableOrganism?.Length > 100)
            {
                rowErrors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    ColumnName = "ApplicableOrganism",
                    Message = "Applicable organism cannot exceed 100 characters."
                });
            }

            if (rowErrors.Count == 0)
            {
                result.Records.Add(gene);
                knownNames.Add(gene.GeneName);
            }
            else
            {
                result.Errors.AddRange(rowErrors);
            }
        }

        return result;
    }
}

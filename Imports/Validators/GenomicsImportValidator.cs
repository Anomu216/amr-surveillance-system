
using System.Globalization;
using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Models;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Imports.Validators;

public class GenomicsImportValidator
    : IEntityImportValidator<GenomicsRecordViewModel>
{
    private readonly ApplicationDbContext _context;
    private readonly IImportMapper<GenomicsRecordViewModel> _mapper;
    private readonly IGeneReferenceSchemaService _schemaService;

    public GenomicsImportValidator(
        ApplicationDbContext context,
        IImportMapper<GenomicsRecordViewModel> mapper,
        IGeneReferenceSchemaService schemaService)
    {
        _context = context;
        _mapper = mapper;
        _schemaService = schemaService;
    }

    public async Task<ImportResult<GenomicsRecordViewModel>> ValidateAsync(
        IXLWorksheet worksheet)
    {
        var result = new ImportResult<GenomicsRecordViewModel>();

        var headerRow = worksheet.FirstRowUsed();

        if (headerRow == null)
        {
            AddError(result, 1, string.Empty, "The worksheet is empty.");
            return result;
        }

        // ------------------------------------------------------------
        // 1. Read and validate headers
        // ------------------------------------------------------------

        var headerMap = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();

            if (string.IsNullOrWhiteSpace(header))
                continue;

            if (headerMap.ContainsKey(header))
            {
                AddError(
                    result,
                    headerRow.RowNumber(),
                    header,
                    $"Duplicate column header '{header}'.");

                continue;
            }

            headerMap[header] = cell.Address.ColumnNumber;
        }

        var registeredGenes = await _context.GeneReferences
            .AsNoTracking()
            .Where(g => g.IsActive)
            .Select(g => g.GeneName)
            .ToListAsync();

        var geneSet = new HashSet<string>(
            registeredGenes,
            StringComparer.OrdinalIgnoreCase);

        if (!headerMap.ContainsKey("IsolateID"))
        {
            AddError(
                result,
                headerRow.RowNumber(),
                "IsolateID",
                "Required column 'IsolateID' is missing.");
        }

        var fixedHeaders = new HashSet<string>(
            FixedFields,
            StringComparer.OrdinalIgnoreCase);

        foreach (var header in headerMap.Keys)
        {
            if (fixedHeaders.Contains(header))
                continue;

            if (!geneSet.Contains(header))
            {
                AddError(
                    result,
                    headerRow.RowNumber(),
                    header,
                    $"'{header}' is not a registered active gene name.");
            }
        }

        if (result.Errors.Any())
            return result;

        // ------------------------------------------------------------
        // 2. Validate each row's local values
        // ------------------------------------------------------------

        var lastRow = worksheet.LastRowUsed()?.RowNumber()
                      ?? headerRow.RowNumber();

        var seenIsolates = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var isolateIds = new List<(int RowNumber, string IsolateID)>();

        for (var rowNumber = headerRow.RowNumber() + 1;
             rowNumber <= lastRow;
             rowNumber++)
        {
            var row = worksheet.Row(rowNumber);

            if (row.IsEmpty())
                continue;

            var isolateId = ReadCell(row, headerMap, "IsolateID");

            if (string.IsNullOrWhiteSpace(isolateId))
            {
                AddError(
                    result,
                    rowNumber,
                    "IsolateID",
                    "IsolateID is required.");

                continue;
            }

            if (isolateId.Length > 20)
            {
                AddError(
                    result,
                    rowNumber,
                    "IsolateID",
                    "IsolateID cannot exceed 20 characters.");

                continue;
            }

            if (!seenIsolates.Add(isolateId))
            {
                AddError(
                    result,
                    rowNumber,
                    "IsolateID",
                    $"IsolateID '{isolateId}' appears more than once in this workbook.");

                continue;
            }

            isolateIds.Add((rowNumber, isolateId));

            ValidateRowValues(
                result,
                row,
                rowNumber,
                headerMap,
                geneSet);
        }

        if (result.Errors.Any())
            return result;

        if (isolateIds.Count == 0)
        {
            AddError(
                result,
                headerRow.RowNumber(),
                "IsolateID",
                "The worksheet contains no data rows to import.");

            return result;
        }

        // ------------------------------------------------------------
        // 3. Check isolates against Worksheets and Genomics
        // ------------------------------------------------------------

        var ids = isolateIds
            .Select(x => x.IsolateID)
            .ToList();

        var worksheetRecords = await _context.Set<Worksheet>()
            .AsNoTracking()
            .Where(w => ids.Contains(w.IsolateID))
            .Select(w => new
            {
                w.IsolateID,
                w.WHONETID
            })
            .ToListAsync();

        var worksheetByIsolate = worksheetRecords
            .GroupBy(
                w => w.IsolateID,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var existingGenomicsIds = await _context.Genomics
            .AsNoTracking()
            .Where(g => ids.Contains(g.IsolateID))
            .Select(g => g.IsolateID)
            .ToListAsync();

        var genomicsIdSet = new HashSet<string>(
            existingGenomicsIds,
            StringComparer.OrdinalIgnoreCase);

        // Collect nonblank WHONETIDs from the workbook for one lookup.
        var submittedWHONETIDs = isolateIds
            .Select(item =>
            {
                var row = worksheet.Row(item.RowNumber);
                return ReadCell(row, headerMap, "WHONETID");
            })
            .Where(value => value != null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingOrganismWHONETIDs = submittedWHONETIDs.Count == 0
            ? new List<string>()
            : await _context.Organisms
                .AsNoTracking()
                .Where(o => submittedWHONETIDs.Contains(o.WHONETID!))
                .Select(o => o.WHONETID!)
                .Distinct()
                .ToListAsync();

        var organismWHONETIDSet = new HashSet<string>(
            existingOrganismWHONETIDs
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in isolateIds)
        {
            if (!worksheetByIsolate.TryGetValue(
                    item.IsolateID,
                    out var worksheetRecord))
            {
                AddError(
                    result,
                    item.RowNumber,
                    "IsolateID",
                    $"Isolate '{item.IsolateID}' does not exist in Worksheets.");

                // Cannot compare WHONETID without the corresponding
                // Worksheets record.
                continue;
            }

            if (genomicsIdSet.Contains(item.IsolateID))
            {
                AddError(
                    result,
                    item.RowNumber,
                    "IsolateID",
                    $"Isolate '{item.IsolateID}' already has a Genomics record.");
            }

            // --------------------------------------------------------
            // 4. WHONETID must match the same isolate's Worksheets row
            // --------------------------------------------------------

            var row = worksheet.Row(item.RowNumber);
            var submittedWHONETID = NormalizeOptionalString(
                ReadCell(row, headerMap, "WHONETID"));

            var worksheetWHONETID = NormalizeOptionalString(
                worksheetRecord.WHONETID);

            if (!string.Equals(
                    submittedWHONETID,
                    worksheetWHONETID,
                    StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    result,
                    item.RowNumber,
                    "WHONETID",
                    $"WHONETID for isolate '{item.IsolateID}' must match " +
                    "the WHONETID recorded for that isolate in Worksheets.");

                continue;
            }

            // A blank WHONETID is valid only when the Worksheets value
            // is also blank.
            if (submittedWHONETID == null)
                continue;

            // A nonblank WHONETID must also exist in Organisms.
            if (!organismWHONETIDSet.Contains(submittedWHONETID))
            {
                AddError(
                    result,
                    item.RowNumber,
                    "WHONETID",
                    $"WHONETID '{submittedWHONETID}' does not exist in Organisms.");
            }
        }

        if (result.Errors.Any())
            return result;

        // ------------------------------------------------------------
        // 5. Build records only after every row passes validation
        // ------------------------------------------------------------

        foreach (var item in isolateIds)
        {
            var row = worksheet.Row(item.RowNumber);
            var model = _mapper.Map(worksheet, row, headerMap);

            model.IsolateID = item.IsolateID;

            // Keep only active, registered genes in the dictionary.
            // An omitted gene remains absent from this dictionary and
            // is stored as NULL by the import service.
            var validGeneValues = new Dictionary<string, bool?>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var geneName in registeredGenes)
            {
                if (!headerMap.TryGetValue(
                        geneName,
                        out var columnNumber))
                {
                    continue;
                }

                validGeneValues[geneName] =
                    ParseGeneValue(row.Cell(columnNumber));
            }

            model.GeneValues = validGeneValues;

            result.Records.Add(model);
        }

        return result;
    }

    // ================================================================
    // FIXED IMPORT FIELDS
    // ================================================================

    private static readonly string[] FixedFields =
    {
        "IsolateID",
        "KENID",
        "MRSNID",
        "DNASampleID",
        "GenomicONTID",
        "GenomicILMID",
        "ProjectCode",
        "SeqRunID",
        "SubmittingProtocol",
        "WHONETID",
        "WGSID",
        "SequencingDate",
        "GenomeLength",
        "GCPercent",
        "NoOfContigs",
        "AssemblyN50",
        "CompleteGenome",
        "NoOfPlasmids",
        "StrainType",
        "Phylogroup",
        "Serotype",
        "Fimtype",
        "SCCmecType",
        "SpaType",
        "OLocus",
        "KLocus",
        "VirulenceScore",
        "ResistanceScore"
    };

    // ================================================================
    // ROW VALIDATION
    // ================================================================

    private static void ValidateRowValues(
        ImportResult<GenomicsRecordViewModel> result,
        IXLRow row,
        int rowNumber,
        IReadOnlyDictionary<string, int> headerMap,
        HashSet<string> registeredGenes)
    {
        ValidateDate(
            result, row, rowNumber, headerMap, "SequencingDate");

        ValidateLong(
            result, row, rowNumber, headerMap, "GenomeLength", 1);

        ValidateLong(
            result, row, rowNumber, headerMap, "AssemblyN50", 1);

        ValidateInt(
            result, row, rowNumber, headerMap, "NoOfContigs", 0);

        ValidateInt(
            result, row, rowNumber, headerMap, "NoOfPlasmids", 0);

        ValidateDecimal(
            result, row, rowNumber, headerMap,
            "GCPercent", 0m, 100m);

        ValidateDecimal(
            result, row, rowNumber, headerMap,
            "VirulenceScore", 0m, 999999.99m);

        ValidateDecimal(
            result, row, rowNumber, headerMap,
            "ResistanceScore", 0m, 999999.99m);

        ValidateBoolean(
            result, row, rowNumber, headerMap, "CompleteGenome");

        foreach (var geneName in registeredGenes)
        {
            if (!headerMap.TryGetValue(
                    geneName,
                    out var columnNumber))
            {
                continue;
            }

            var cell = row.Cell(columnNumber);
            var rawValue = cell.GetString().Trim();

            if (!cell.IsEmpty() &&
                !string.IsNullOrWhiteSpace(rawValue) &&
                ParseGeneValue(cell) == null &&
                !IsUnknownGeneValue(rawValue))
            {
                AddError(
                    result,
                    rowNumber,
                    geneName,
                    "Use Detected, Not detected, Unknown, Not tested, " +
                    "Not available, or N/A.");
            }
        }

        ValidateStringLengths(result, row, rowNumber, headerMap);
    }

    private static void ValidateDate(
        ImportResult<GenomicsRecordViewModel> result,
        IXLRow row,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        string column)
    {
        if (!headers.TryGetValue(column, out var index))
            return;

        var cell = row.Cell(index);

        if (cell.IsEmpty() ||
            string.IsNullOrWhiteSpace(cell.GetString()))
        {
            return;
        }

        if (cell.TryGetValue<DateTime>(out _))
            return;

        if (DateTime.TryParse(
                cell.GetString().Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            return;
        }

        AddError(result, rowNumber, column, "Enter a valid date.");
    }

    private static void ValidateLong(
        ImportResult<GenomicsRecordViewModel> result,
        IXLRow row,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        string column,
        long minimum)
    {
        if (!headers.TryGetValue(column, out var index))
            return;

        var cell = row.Cell(index);

        if (cell.IsEmpty() ||
            string.IsNullOrWhiteSpace(cell.GetString()))
        {
            return;
        }

        if (long.TryParse(
                cell.GetString().Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value) &&
            value >= minimum)
        {
            return;
        }

        AddError(
            result,
            rowNumber,
            column,
            $"Enter a whole number greater than or equal to {minimum}.");
    }

    private static void ValidateInt(
        ImportResult<GenomicsRecordViewModel> result,
        IXLRow row,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        string column,
        int minimum)
    {
        if (!headers.TryGetValue(column, out var index))
            return;

        var cell = row.Cell(index);

        if (cell.IsEmpty() ||
            string.IsNullOrWhiteSpace(cell.GetString()))
        {
            return;
        }

        if (int.TryParse(
                cell.GetString().Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value) &&
            value >= minimum)
        {
            return;
        }

        AddError(
            result,
            rowNumber,
            column,
            $"Enter a whole number greater than or equal to {minimum}.");
    }

    private static void ValidateDecimal(
        ImportResult<GenomicsRecordViewModel> result,
        IXLRow row,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        string column,
        decimal minimum,
        decimal maximum)
    {
        if (!headers.TryGetValue(column, out var index))
            return;

        var cell = row.Cell(index);

        if (cell.IsEmpty() ||
            string.IsNullOrWhiteSpace(cell.GetString()))
        {
            return;
        }

        if (decimal.TryParse(
                cell.GetString().Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value) &&
            value >= minimum &&
            value <= maximum)
        {
            return;
        }

        AddError(
            result,
            rowNumber,
            column,
            $"Enter a number between {minimum} and {maximum}.");
    }

    private static void ValidateBoolean(
        ImportResult<GenomicsRecordViewModel> result,
        IXLRow row,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        string column)
    {
        if (!headers.TryGetValue(column, out var index))
            return;

        var cell = row.Cell(index);

        if (cell.IsEmpty() ||
            string.IsNullOrWhiteSpace(cell.GetString()))
        {
            return;
        }

        var value = cell.GetString().Trim();

        if (value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("No", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("False", StringComparison.OrdinalIgnoreCase) ||
            value == "1" ||
            value == "0")
        {
            return;
        }

        AddError(
            result,
            rowNumber,
            column,
            "Use Yes, No, True, False, 1, or 0.");
    }

    // ================================================================
    // STRING LENGTHS — ALIGNED WITH GenomicsViewModel
    // ================================================================

    private static void ValidateStringLengths(
        ImportResult<GenomicsRecordViewModel> result,
        IXLRow row,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers)
    {
        var limits = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["IsolateID"] = 20,
            ["KENID"] = 150,
            ["MRSNID"] = 150,
            ["DNASampleID"] = 100,
            ["GenomicONTID"] = 100,
            ["GenomicILMID"] = 100,
            ["ProjectCode"] = 100,
            ["SeqRunID"] = 100,
            ["SubmittingProtocol"] = 255,
            ["WHONETID"] = 100,
            ["WGSID"] = 100,
            ["StrainType"] = 50,
            ["Phylogroup"] = 50,
            ["Serotype"] = 100,
            ["Fimtype"] = 100,
            ["SCCmecType"] = 50,
            ["SpaType"] = 50,
            ["OLocus"] = 50,
            ["KLocus"] = 50
        };

        foreach (var limit in limits)
        {
            if (!headers.TryGetValue(limit.Key, out var index))
                continue;

            var value = row.Cell(index).GetString().Trim();

            if (value.Length > limit.Value)
            {
                AddError(
                    result,
                    rowNumber,
                    limit.Key,
                    $"Value cannot exceed {limit.Value} characters.");
            }
        }
    }

    // ================================================================
    // GENE RESULT PARSING
    // ================================================================

    private static bool? ParseGeneValue(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        var value = cell.GetString().Trim();

        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (value.Equals("Detected", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Positive", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Present", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
            value == "1")
        {
            return true;
        }

        if (value.Equals("Not detected", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Negative", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Absent", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("False", StringComparison.OrdinalIgnoreCase) ||
            value == "0")
        {
            return false;
        }

        return null;
    }

    private static bool IsUnknownGeneValue(string value)
    {
        return value.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Not tested", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Not available", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("N/A", StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // SHARED HELPERS
    // ================================================================

    private static string? ReadCell(
        IXLRow row,
        IReadOnlyDictionary<string, int> headers,
        string column)
    {
        if (!headers.TryGetValue(column, out var index))
            return null;

        var value = row.Cell(index).GetString().Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? NormalizeOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static void AddError(
        ImportResult<GenomicsRecordViewModel> result,
        int rowNumber,
        string column,
        string message)
    {
        result.Errors.Add(new ImportRowError
        {
            RowNumber = rowNumber,
            ColumnName = column,
            Message = message
        });
    }
}

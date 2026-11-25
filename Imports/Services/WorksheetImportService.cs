using System.Data;
using System.Text.RegularExpressions;
using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Mappers;
using AMRSurveillance.Imports.Templates;
using AMRSurveillance.Imports.Validators;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AMRSurveillance.Imports.Services;

public partial class WorksheetImportService(
    ApplicationDbContext context,
    WorksheetImportMapper mapper,
    WorksheetImportValidator validator,
    WorksheetTemplateGenerator templateGenerator,
    IASTSchemaService astSchemaService,
    IWorksheetService worksheetService) : IWorksheetImportService
{
    [GeneratedRegex(@"^[A-Z0-9_]+$")]
    private static partial Regex AntibioticCodeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_]+$")]
    private static partial Regex ColumnNameRegex();

    // =====================================================
    // Generate Template
    // =====================================================

    public async Task<byte[]> GenerateTemplateAsync()
    {
        return await templateGenerator.GenerateAsync();
    }

    // =====================================================
    // Import Worksheet Excel File
    // =====================================================

    public async Task<WorksheetImportResult> ImportAsync(
        Stream fileStream)
    {
        var result = new WorksheetImportResult();

        if (fileStream == null ||
            !fileStream.CanRead ||
            (fileStream.CanSeek && fileStream.Length == 0))
        {
            AddFileError(
                result,
                "The uploaded file is empty or cannot be read.");

            return result;
        }

        XLWorkbook workbook;

        try
        {
            workbook = new XLWorkbook(fileStream);
        }
        catch (Exception)
        {
            AddFileError(
                result,
                "The uploaded file is not a valid Excel workbook.");

            return result;
        }

        using (workbook)
        {
            // -------------------------------------------------
            // Find Worksheet sheet before making schema changes
            // -------------------------------------------------

            var excelWorksheet = workbook.Worksheets
                .FirstOrDefault(w =>
                    string.Equals(
                        w.Name,
                        "Worksheet",
                        StringComparison.OrdinalIgnoreCase));

            if (excelWorksheet == null)
            {
                AddFileError(
                    result,
                    "The Excel file does not contain a worksheet named 'Worksheet'.");

                return result;
            }

            // -------------------------------------------------
            // Build and validate the header map
            // -------------------------------------------------

            Dictionary<string, int> columnMap;

            try
            {
                columnMap = mapper.BuildColumnMap(excelWorksheet);
            }
            catch (Exception)
            {
                AddFileError(
                    result,
                    "The column headers could not be read. Check for duplicate or invalid headers.");

                return result;
            }

            if (columnMap.Count == 0)
            {
                AddFileError(
                    result,
                    "No column headers were found in row 1 of the Worksheet sheet.");

                return result;
            }

            string[] requiredColumns =
            [
                "IsolateID",
                "PatientID",
                "CultureResult"
            ];

            var missingColumns = requiredColumns
                .Where(required =>
                    !columnMap.Keys.Any(header =>
                        string.Equals(
                            header,
                            required,
                            StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missingColumns.Count > 0)
            {
                AddFileError(
                    result,
                    $"Required column(s) missing: {string.Join(", ", missingColumns)}.");

                return result;
            }

            // -------------------------------------------------
            // Ensure dynamic AST columns only after basic workbook structure has passed validation
            // -------------------------------------------------

            try
            {
                await astSchemaService.EnsureASTColumnsAsync();
            }
            catch (Exception)
            {
                AddFileError(
                    result,
                    "The dynamic AST columns could not be verified or created. No worksheet rows were imported.");

                return result;
            }

            // -------------------------------------------------
            // Process data rows
            // -------------------------------------------------

            HashSet<string> importedIsolateIDs = new(StringComparer.OrdinalIgnoreCase);

            var lastDataRow =
                excelWorksheet.LastRowUsed()?.RowNumber() ?? 1;

            for (var rowNumber = 2;
                 rowNumber <= lastDataRow;
                 rowNumber++)
            {
                var excelRow = excelWorksheet.Row(rowNumber);

                if (!excelRow.CellsUsed().Any())
                    continue;

                result.TotalRows++;

                WorksheetImportRow mappedRow;

                try
                {
                    mappedRow = mapper.Map(
                        excelRow,
                        columnMap);
                }
                catch (Exception)
                {
                    AddRowError(
                        result,
                        rowNumber,
                        string.Empty,
                        "The row could not be read. Check the cell values and column headers.");

                    continue;
                }

                var isolateID = Clean(mappedRow.IsolateID)?.ToUpperInvariant() ?? string.Empty;
                var patientID = Clean(mappedRow.PatientID)?.ToUpperInvariant() ?? string.Empty;
                var cultureResult = Clean(mappedRow.CultureResult) ?? string.Empty;

                // -------------------------------------------------
                // Validate row using the existing validator
                // -------------------------------------------------

                List<string> errors;

                try
                {
                    errors = await validator.ValidateAsync(
                        mappedRow,
                        importedIsolateIDs);
                }
                catch (Exception)
                {
                    AddRowError(
                        result,
                        rowNumber,
                        isolateID,
                        "Row validation failed because of a database or validation error.");

                    continue;
                }

                if (errors.Count > 0)
                {
                    AddRowError(
                        result,
                        rowNumber,
                        isolateID,
                        string.Join(" ", errors));

                    continue;
                }

                // -------------------------------------------------
                // Build one Worksheet entity and normalize growth/AST fields
                // -------------------------------------------------

                bool isNoGrowth = string.Equals(
                    cultureResult,
                    "No Growth",
                    StringComparison.OrdinalIgnoreCase);

                var whonetID = isNoGrowth ? null : Clean(mappedRow.WHONETID);
                var cefoxitinScreen = isNoGrowth ? null : Clean(mappedRow.CefoxitinScreen);
                var astResults = isNoGrowth
                    ? []
                    : BuildASTResults(mappedRow);

                var normalizedCards = isNoGrowth
                    ? []
                    : NormalizeASTCards(mappedRow.ASTCard);

                var interpretationsByCode = isNoGrowth
                    ? null
                    : mappedRow.InterpretationResults.ToDictionary(
                        kv => kv.Key,
                        kv => (string?)kv.Value,
                        StringComparer.OrdinalIgnoreCase);

                var derived = isNoGrowth
                    ? new DerivedWorksheetClassification(null, null, null)
                    : await worksheetService.CalculateDerivedResultsAsync(
                        whonetID,
                        cefoxitinScreen,
                        interpretationsByCode);

                var worksheetEntity = new Worksheet
                {
                    IsolateID = isolateID,
                    PatientID = patientID,
                    CultureResult = cultureResult,
                    ManuscriptID = Clean(mappedRow.ManuscriptID),

                    AcceptedID = isNoGrowth ? null : Clean(mappedRow.AcceptedID),
                    WHONETID = whonetID,

                    GramReaction = isNoGrowth ? null : Clean(mappedRow.GramReaction),
                    Morphology = isNoGrowth ? null : Clean(mappedRow.Morphology),
                    Hemolysis = isNoGrowth ? null : Clean(mappedRow.Hemolysis),
                    Catalase = isNoGrowth ? null : Clean(mappedRow.Catalase),
                    Oxidase = isNoGrowth ? null : Clean(mappedRow.Oxidase),
                    Coagulase = isNoGrowth ? null : Clean(mappedRow.Coagulase),
                    LactoseFermentation = isNoGrowth ? null : Clean(mappedRow.LactoseFermentation),

                    Comments = Clean(mappedRow.Comments),
                    ASTCard = normalizedCards.Count > 0
                        ? string.Join(";", normalizedCards)
                        : null,

                    ESBL = isNoGrowth ? null : Clean(mappedRow.ESBL),
                    CefoxitinScreen = cefoxitinScreen,
                    InducibleClindamycinResistance =
                        isNoGrowth ? null : Clean(mappedRow.InducibleClindamycinResistance),

                    ASTComments = isNoGrowth ? null : Clean(mappedRow.ASTComments),

                    MDR = derived.MDR,
                    MRSA = derived.MRSA,
                    Carbapenemase = derived.Carbapenemase
                };

                // -------------------------------------------------
                // Save the complete row within one transaction
                // -------------------------------------------------

                try
                {
                    await using var transaction =
                        await context.Database.BeginTransactionAsync();

                    context.Worksheets.Add(worksheetEntity);

                    await context.SaveChangesAsync();

                    await SaveASTResultsInCurrentTransactionAsync(
                        isolateID,
                        astResults);

                    await transaction.CommitAsync();

                    importedIsolateIDs.Add(isolateID);
                    result.SuccessfulRows++;
                }
                catch (Exception)
                {
                    // EF may still track an entity whose insert was
                    // rolled back. Detach it before processing another row.
                    var trackedEntry =
                        context.Entry(worksheetEntity);

                    if (trackedEntry.State != EntityState.Detached)
                    {
                        trackedEntry.State = EntityState.Detached;
                    }

                    AddRowError(
                        result,
                        rowNumber,
                        isolateID,
                        "The record could not be saved. Its fixed fields and AST results were rolled back.");
                }
            }
        }

        return result;
    }

    // =====================================================
    // Save Dynamic AST Values Within Current Transaction
    // =====================================================

    private async Task SaveASTResultsInCurrentTransactionAsync(
        string isolateID,
        List<WorksheetAntibioticViewModel> astResults)
    {
        if (astResults.Count == 0)
            return;

        var connection = context.Database.GetDbConnection();

        var transaction =
            context.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "An active database transaction is required to save AST results.");

        var dbTransaction = transaction.GetDbTransaction();

        List<string> assignments = [];

        List<(string Name, object Value)> parameters = [];

        var index = 0;

        foreach (var antibiotic in astResults)
        {
            if (antibiotic == null)
                continue;

            var code = NormalizeAntibioticCode(
                antibiotic.AntibioticCode);

            if (code == null)
                continue;

            var micColumn = $"{code}_MIC";
            var interpretationColumn = $"{code}_INT";

            if (!IsValidColumnName(micColumn) ||
                !IsValidColumnName(interpretationColumn))
            {
                throw new InvalidOperationException(
                    $"Invalid AST column for antibiotic '{code}'.");
            }

            var micParameter = $"@MIC{index}";
            var interpretationParameter = $"@INT{index}";

            assignments.Add(
                $"{QuoteIdentifier(micColumn)} = {micParameter}");

            assignments.Add(
                $"{QuoteIdentifier(interpretationColumn)} = {interpretationParameter}");

            parameters.Add((
                micParameter,
                (object?)Clean(antibiotic.MIC) ?? DBNull.Value));

            parameters.Add((
                interpretationParameter,
                (object?)Clean(antibiotic.Interpretation) ?? DBNull.Value));

            index++;
        }

        if (assignments.Count == 0)
            return;

        var sql = $"""
            UPDATE [dbo].[Worksheet]
            SET {string.Join(", ", assignments)}
            WHERE [IsolateID] = @IsolateID;
            """;

        await using var command = connection.CreateCommand();

        command.Transaction = dbTransaction;
        command.CommandText = sql;

        var isolateParameter = command.CreateParameter();
        isolateParameter.ParameterName = "@IsolateID";
        isolateParameter.Value = isolateID;
        command.Parameters.Add(isolateParameter);

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        var affectedRows = await command.ExecuteNonQueryAsync();

        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Expected to update one Worksheet row for isolate '{isolateID}', but updated {affectedRows}.");
        }
    }

    // =====================================================
    // Build Dynamic AST ViewModel Collection
    // =====================================================

    private static List<WorksheetAntibioticViewModel> BuildASTResults(
        WorksheetImportRow row)
    {
        var codes = row.MICResults.Keys
            .Union(
                row.InterpretationResults.Keys,
                StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return codes
            .Select(code => new WorksheetAntibioticViewModel
            {
                AntibioticCode = code,

                MIC = row.MICResults.TryGetValue(
                    code,
                    out var mic)
                    ? mic
                    : null,

                Interpretation = row.InterpretationResults.TryGetValue(
                    code,
                    out var interpretation)
                    ? interpretation
                    : null
            })
            .ToList();
    }

    // =====================================================
    // Validation and SQL Identifier Helpers
    // =====================================================

    private static List<string> NormalizeASTCards(string? astCard)
    {
        if (string.IsNullOrWhiteSpace(astCard))
            return [];

        return astCard
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeAntibioticCode(string? antibioticCode)
    {
        if (string.IsNullOrWhiteSpace(antibioticCode))
            return null;

        var code = antibioticCode.Trim().ToUpperInvariant();

        if (!AntibioticCodeRegex().IsMatch(code))
        {
            throw new InvalidOperationException(
                $"Invalid antibiotic code: {antibioticCode}");
        }

        return code;
    }

    private static bool IsValidColumnName(string columnName)
    {
        return ColumnNameRegex().IsMatch(columnName);
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (!IsValidColumnName(identifier))
        {
            throw new InvalidOperationException(
                $"Invalid SQL identifier: {identifier}");
        }

        return $"[{identifier}]";
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static void AddFileError(
        WorksheetImportResult result,
        string message)
    {
        result.FailedRows++;

        result.Errors.Add(new WorksheetImportError
        {
            RowNumber = 0,
            IsolateID = string.Empty,
            ErrorMessage = message
        });
    }

    private static void AddRowError(
        WorksheetImportResult result,
        int rowNumber,
        string? isolateID,
        string message)
    {
        result.FailedRows++;

        result.Errors.Add(new WorksheetImportError
        {
            RowNumber = rowNumber,
            IsolateID = isolateID ?? string.Empty,
            ErrorMessage = message
        });
    }
}
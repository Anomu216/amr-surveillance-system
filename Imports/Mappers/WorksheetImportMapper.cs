using System.Text.RegularExpressions;
using ClosedXML.Excel;
using AMRSurveillance.ViewModels;

namespace AMRSurveillance.Imports.Mappers;

public class WorksheetImportMapper
{
    private static readonly Regex MicColumnRegex = new(
        @"^([A-Za-z0-9_]+)_MIC$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InterpretationColumnRegex = new(
        @"^([A-Za-z0-9_]+)_INT$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // =====================================================
    // Map Excel Row
    // =====================================================

    public WorksheetImportRow Map(
        IXLRow row,
        IReadOnlyDictionary<string, int> columnMap)
    {
        var result = new WorksheetImportRow
        {
            // -------------------------------------------------
            // Worksheet / Isolate Information
            // -------------------------------------------------

            IsolateID =
                GetValue(
                    row,
                    columnMap,
                    "IsolateID"),

            PatientID =
                GetValue(
                    row,
                    columnMap,
                    "PatientID"),

            CultureResult =
                GetValue(
                    row,
                    columnMap,
                    "CultureResult"),

            ManuscriptID =
                GetNullableValue(
                    row,
                    columnMap,
                    "ManuscriptID"),

            // -------------------------------------------------
            // Organism Identification
            // -------------------------------------------------

            AcceptedID =
                GetNullableValue(
                    row,
                    columnMap,
                    "AcceptedID"),

            WHONETID =
                GetNullableValue(
                    row,
                    columnMap,
                    "WHONETID"),

            // -------------------------------------------------
            // Laboratory Characteristics
            // -------------------------------------------------

            GramReaction =
                GetNullableValue(
                    row,
                    columnMap,
                    "GramReaction"),

            Morphology =
                GetNullableValue(
                    row,
                    columnMap,
                    "Morphology"),

            Hemolysis =
                GetNullableValue(
                    row,
                    columnMap,
                    "Hemolysis"),

            Catalase =
                GetNullableValue(
                    row,
                    columnMap,
                    "Catalase"),

            Oxidase =
                GetNullableValue(
                    row,
                    columnMap,
                    "Oxidase"),

            Coagulase =
                GetNullableValue(
                    row,
                    columnMap,
                    "Coagulase"),

            LactoseFermentation =
                GetNullableValue(
                    row,
                    columnMap,
                    "LactoseFermentation"),

            Comments =
                GetNullableValue(
                    row,
                    columnMap,
                    "Comments"),

            // -------------------------------------------------
            // AST Findings
            // -------------------------------------------------

            ASTCard =
                GetNullableValue(
                    row,
                    columnMap,
                    "ASTCard"),

            ESBL =
                GetNullableValue(
                    row,
                    columnMap,
                    "ESBL"),

            CefoxitinScreen =
                GetNullableValue(
                    row,
                    columnMap,
                    "CefoxitinScreen"),

            InducibleClindamycinResistance =
                GetNullableValue(
                    row,
                    columnMap,
                    "InducibleClindamycinResistance"),

            ASTComments =
                GetNullableValue(
                    row,
                    columnMap,
                    "ASTComments")
        };

        // =====================================================
        // Dynamic AST Columns
        // =====================================================

        foreach (var column in columnMap)
        {
            var columnName =
                column.Key;

            // ---------------------------------------------
            // MIC
            // ---------------------------------------------

            var micMatch =
                MicColumnRegex.Match(columnName);

            if (micMatch.Success)
            {
                var antibioticCode =
                    micMatch.Groups[1]
                        .Value
                        .ToUpperInvariant();

                result.MICResults[antibioticCode] =
                    GetNullableValue(
                        row,
                        columnMap,
                        columnName);
            }

            // ---------------------------------------------
            // Interpretation
            // ---------------------------------------------

            var interpretationMatch =
                InterpretationColumnRegex.Match(columnName);

            if (interpretationMatch.Success)
            {
                var antibioticCode =
                    interpretationMatch.Groups[1]
                        .Value
                        .ToUpperInvariant();

                result.InterpretationResults[
                    antibioticCode] =
                    GetNullableValue(
                        row,
                        columnMap,
                        columnName);
            }
        }

        return result;
    }

    // =====================================================
    // Build Column Map
    // =====================================================

    public Dictionary<string, int> BuildColumnMap(
        IXLWorksheet worksheet)
    {
        var columnMap =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        var headerRow =
            worksheet.FirstRowUsed();

        if (headerRow == null)
            return columnMap;

        foreach (var cell in headerRow.CellsUsed())
        {
            var header =
                cell.GetString().Trim();

            if (string.IsNullOrWhiteSpace(header))
                continue;

            if (!columnMap.ContainsKey(header))
            {
                columnMap.Add(
                    header,
                    cell.Address.ColumnNumber);
            }
        }

        return columnMap;
    }

    // =====================================================
    // Get Required Value
    // =====================================================

    private static string GetValue(
        IXLRow row,
        IReadOnlyDictionary<string, int> columnMap,
        string columnName)
    {
        if (!columnMap.TryGetValue(
                columnName,
                out var columnNumber))
        {
            return string.Empty;
        }

        return row
            .Cell(columnNumber)
            .GetString()
            .Trim();
    }

    // =====================================================
    // Get Nullable Value
    // =====================================================

    private static string? GetNullableValue(
        IXLRow row,
        IReadOnlyDictionary<string, int> columnMap,
        string columnName)
    {
        var value =
            GetValue(
                row,
                columnMap,
                columnName);

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }
}
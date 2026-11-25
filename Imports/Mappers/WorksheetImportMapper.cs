using System.Text.RegularExpressions;
using ClosedXML.Excel;
using AMRSurveillance.Models;
using AMRSurveillance.ViewModels;

namespace AMRSurveillance.Imports.Mappers;

public class WorksheetImportMapper
{
    private static readonly Regex MicColumnRegex = new(
        @"^(.+)_MIC$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InterpretationColumnRegex = new(
        @"^(.+)_INT$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly Dictionary<string, string> _nameToCodeMap;

    public WorksheetImportMapper()
    {
        _nameToCodeMap = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
    }

    public WorksheetImportMapper(IEnumerable<Antibiotic> antibiotics)
    {
        _nameToCodeMap = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var abx in antibiotics)
        {
            if (string.IsNullOrWhiteSpace(abx.AntibioticCode))
                continue;

            var code = abx.AntibioticCode.Trim().ToUpperInvariant();

            // Register SQL code itself (backward compatibility)
            _nameToCodeMap.TryAdd(code, code);

            // Register human-readable antibiotic name (e.g., "Cefazolin" -> "CZO")
            if (!string.IsNullOrWhiteSpace(abx.AntibioticName))
            {
                var name = abx.AntibioticName.Trim();
                _nameToCodeMap.TryAdd(name, code);

                var normalizedName = NormalizeKey(name);
                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    _nameToCodeMap.TryAdd(normalizedName, code);
                }
            }
        }
    }

    // =====================================================
    // Map Excel Row
    // =====================================================

    public WorksheetImportRow Map(
        IXLRow row,
        IReadOnlyDictionary<string, int> columnMap)
    {
        var rawGramReaction =
            GetNullableValue(
                row,
                columnMap,
                "GramReaction");

        var normalizedGramReaction =
            NormalizePositiveNegative(rawGramReaction);

        var isGramPositive =
            string.Equals(
                normalizedGramReaction,
                "Positive",
                StringComparison.OrdinalIgnoreCase);

        var isGramNegative =
            string.Equals(
                normalizedGramReaction,
                "Negative",
                StringComparison.OrdinalIgnoreCase);

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

            GramReaction = normalizedGramReaction,

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

            // Match Worksheet.js: If Gram-Negative (Negative), Catalase and Coagulase must be NULL
            // GN -> Catalase & Coagulase must be NULL
            Catalase = isGramNegative
            ? null
            : NormalizePositiveNegative(
                GetNullableValue(row, columnMap, "Catalase")),

            Coagulase = isGramNegative
            ? null
            : NormalizePositiveNegative(GetNullableValue(row, columnMap, "Coagulase")),

            // GP -> Oxidase & LactoseFermentation must be NULL
            Oxidase = isGramPositive
            ? null
            : NormalizePositiveNegative(
                GetNullableValue(row, columnMap, "Oxidase")),

            LactoseFermentation = isGramPositive
            ? null
            : NormalizePositiveNegative(
                GetNullableValue(row, columnMap, "LactoseFermentation")),

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
        // Dynamic AST Columns (Resolves Readable Name -> SQL Code)
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
                var headerAntibioticToken =
                    micMatch.Groups[1].Value.Trim();

                var antibioticCode =
                    ResolveAntibioticCode(headerAntibioticToken);

                result.MICResults[antibioticCode] =
                    GetNullableValue(
                        row,
                        columnMap,
                        columnName);

                continue;
            }

            // ---------------------------------------------
            // Interpretation
            // ---------------------------------------------

            var interpretationMatch =
                InterpretationColumnRegex.Match(columnName);

            if (interpretationMatch.Success)
            {
                var headerAntibioticToken =
                    interpretationMatch.Groups[1].Value.Trim();

                var antibioticCode =
                    ResolveAntibioticCode(headerAntibioticToken);

                result.InterpretationResults[antibioticCode] =
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

            // Also register space-stripped alias for fixed columns (e.g., "Accepted ID" -> "AcceptedID")
            if (!header.EndsWith("_MIC", StringComparison.OrdinalIgnoreCase) &&
                !header.EndsWith("_INT", StringComparison.OrdinalIgnoreCase))
            {
                var compactHeader = header.Replace(" ", string.Empty);
                if (!string.IsNullOrWhiteSpace(compactHeader) &&
                    !columnMap.ContainsKey(compactHeader))
                {
                    columnMap.Add(
                        compactHeader,
                        cell.Address.ColumnNumber);
                }
            }
        }

        return columnMap;
    }

    // =====================================================
    // Resolve Antibiotic Name or Code -> SQL Code
    // =====================================================

    private string ResolveAntibioticCode(string headerToken)
    {
        if (_nameToCodeMap.TryGetValue(headerToken, out var code))
            return code;

        var normalized = NormalizeKey(headerToken);
        if (_nameToCodeMap.TryGetValue(normalized, out code))
            return code;

        return headerToken.ToUpperInvariant();
    }

    private static string NormalizeKey(string value)
    {
        return Regex.Replace(value.Trim(), @"[\s\-_/]+", string.Empty);
    }

    // =====================================================
    // Normalize Lookup Values
    // =====================================================

    private static string? NormalizePositiveNegative(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        if (string.Equals(trimmed, "Positive", StringComparison.OrdinalIgnoreCase))
            return "Positive";

        if (string.Equals(trimmed, "Negative", StringComparison.OrdinalIgnoreCase))
            return "Negative";

        return trimmed;
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
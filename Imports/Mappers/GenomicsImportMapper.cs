
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.ViewModels;
using ClosedXML.Excel;
using System.Globalization;

namespace AMRSurveillance.Imports.Mappers;

public class GenomicsImportMapper
    : IImportMapper<GenomicsRecordViewModel>
{
    public GenomicsRecordViewModel Map(
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

        DateTime? ReadDate(string columnName)
        {
            if (!headerMap.TryGetValue(columnName, out var columnNumber))
                return null;

            var cell = row.Cell(columnNumber);

            if (cell.IsEmpty())
                return null;

            if (cell.TryGetValue<DateTime>(out var date))
                return date;

            var text = cell.GetString().Trim();

            if (DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out date))
                return date;

            // Invalid dates are left null here.
            // The validator must report the invalid source value.
            return null;
        }

        long? ReadLong(string columnName)
        {
            var value = ReadString(columnName);

            if (string.IsNullOrWhiteSpace(value))
                return null;

            return long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number)
                ? number
                : null;
        }

        int? ReadInt(string columnName)
        {
            var value = ReadString(columnName);

            if (string.IsNullOrWhiteSpace(value))
                return null;

            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number)
                ? number
                : null;
        }

        decimal? ReadDecimal(string columnName)
        {
            var value = ReadString(columnName);

            if (string.IsNullOrWhiteSpace(value))
                return null;

            return decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var number)
                ? number
                : null;
        }

        bool? ReadBoolean(string columnName)
        {
            var value = ReadString(columnName);

            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (bool.TryParse(value, out var boolean))
                return boolean;

            if (value.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("True", StringComparison.OrdinalIgnoreCase)
                || value.Equals("1", StringComparison.OrdinalIgnoreCase))
                return true;

            if (value.Equals("No", StringComparison.OrdinalIgnoreCase)
                || value.Equals("False", StringComparison.OrdinalIgnoreCase)
                || value.Equals("0", StringComparison.OrdinalIgnoreCase))
                return false;

            return null;
        }

        var model = new GenomicsRecordViewModel
        {
            IsolateID = ReadString("IsolateID") ?? string.Empty,

            KENID = ReadString("KENID"),
            MRSNID = ReadString("MRSNID"),
            DNASampleID = ReadString("DNASampleID"),
            GenomicONTID = ReadString("GenomicONTID"),
            GenomicILMID = ReadString("GenomicILMID"),
            ProjectCode = ReadString("ProjectCode"),
            SeqRunID = ReadString("SeqRunID"),
            SubmittingProtocol = ReadString("SubmittingProtocol"),
            WHONETID = ReadString("WHONETID"),
            WGSID = ReadString("WGSID"),

            SequencingDate = ReadDate("SequencingDate"),
            GenomeLength = ReadLong("GenomeLength"),
            GCPercent = ReadDecimal("GCPercent"),
            NoOfContigs = ReadInt("NoOfContigs"),
            AssemblyN50 = ReadLong("AssemblyN50"),
            CompleteGenome = ReadBoolean("CompleteGenome"),
            NoOfPlasmids = ReadInt("NoOfPlasmids"),

            StrainType = ReadString("StrainType"),
            Phylogroup = ReadString("Phylogroup"),
            Serotype = ReadString("Serotype"),
            Fimtype = ReadString("Fimtype"),
            SCCmecType = ReadString("SCCmecType"),
            SpaType = ReadString("SpaType"),
            OLocus = ReadString("OLocus"),
            KLocus = ReadString("KLocus"),

            VirulenceScore = ReadDecimal("VirulenceScore"),
            ResistanceScore = ReadDecimal("ResistanceScore")
        };

        foreach (var header in headerMap)
        {
            var columnName = header.Key;

            if (IsFixedField(columnName))
                continue;

            var value = ReadGeneValue(row.Cell(header.Value));

            model.GeneValues[columnName] = value;
        }

        return model;
    }

    private static bool IsFixedField(string name)
    {
        return FixedFields.Contains(
            name,
            StringComparer.OrdinalIgnoreCase);
    }

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

    private static bool? ReadGeneValue(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        var value = cell.GetString().Trim();

        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (value.Equals("Detected", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Positive", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Present", StringComparison.OrdinalIgnoreCase)
            || value.Equals("True", StringComparison.OrdinalIgnoreCase)
            || value == "1")
            return true;

        if (value.Equals("Not detected", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Negative", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Absent", StringComparison.OrdinalIgnoreCase)
            || value.Equals("False", StringComparison.OrdinalIgnoreCase)
            || value == "0")
            return false;

        if (value.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Not tested", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Not available", StringComparison.OrdinalIgnoreCase)
            || value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            return null;

        // The validator must reject unrecognised gene values.
        return null;
    }
}

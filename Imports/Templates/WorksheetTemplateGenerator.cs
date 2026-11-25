using AMRSurveillance.Data;
using AMRSurveillance.Services.Interfaces;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Imports.Templates;

public class WorksheetTemplateGenerator(
    IASTConfigurationService astConfigurationService,
    ApplicationDbContext context)
{
    // =====================================================
    // Generate Worksheet Import Template
    // =====================================================

    public async Task<byte[]> GenerateAsync()
    {
        var antibiotics =
            await astConfigurationService
                .GetActiveAntibioticsAsync();

        var activeCards = await context.ASTCards
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.ASTCardOrganisms)
            .Include(c => c.ASTCardAntibiotics)
            .OrderBy(c => c.ASTCard)
            .ToListAsync();

        using var workbook = new XLWorkbook();

        // =================================================
        // Worksheet
        // =================================================

        var worksheet = workbook.Worksheets.Add("Worksheet");

        // =================================================
        // Fixed Columns
        // =================================================

        List<string> columns =
        [
            "IsolateID",
            "PatientID",
            "CultureResult",
            "ManuscriptID",
            "AcceptedID",
            "WHONETID",

            "GramReaction",
            "Morphology",
            "Hemolysis",
            "Catalase",
            "Oxidase",
            "Coagulase",
            "LactoseFermentation",

            "Comments",

            "ASTCard",

            "ESBL",
            "CefoxitinScreen",
            "InducibleClindamycinResistance",
            "ASTComments"
        ];

        // =================================================
        // Dynamic Antibiotic Columns (Alphabetical Schema Order)
        // =================================================

        foreach (var antibiotic in antibiotics)
        {
            if (antibiotic.MICEnabled)
            {
                columns.Add($"{antibiotic.AntibioticCode}_MIC");
            }

            if (antibiotic.InterpretationEnabled)
            {
                columns.Add($"{antibiotic.AntibioticCode}_INT");
            }
        }

        // =================================================
        // Write Header
        // =================================================

        for (var index = 0; index < columns.Count; index++)
        {
            var cell = worksheet.Cell(1, index + 1);

            cell.Value = columns[index];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // =================================================
        // Formatting
        // =================================================

        var usedRange = worksheet.RangeUsed();

        if (usedRange != null)
        {
            usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        worksheet.Row(1).Height = 25;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();

        // =================================================
        // Configured Cards Reference Sheet
        // =================================================

        if (activeCards.Count > 0)
        {
            var cardRefSheet = workbook.Worksheets.Add("ASTCards");
            cardRefSheet.Cell(1, 1).Value = "ASTCard";
            cardRefSheet.Cell(1, 2).Value = "Description";
            cardRefSheet.Cell(1, 3).Value = "ConfiguredAntibiotics";
            cardRefSheet.Cell(1, 4).Value = "MappedOrganisms";

            var headerRange = cardRefSheet.Range(1, 1, 1, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            var rowIdx = 2;
            foreach (var card in activeCards)
            {
                var orderedAbx = card.ASTCardAntibiotics
                    .OrderBy(a => a.DisplayOrder)
                    .ThenBy(a => a.AntibioticCode, StringComparer.OrdinalIgnoreCase)
                    .Select(a => a.AntibioticCode)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                var mappedOrgs = card.ASTCardOrganisms
                    .Select(o => o.WHONETID)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(o => o);

                cardRefSheet.Cell(rowIdx, 1).Value = card.ASTCard;
                cardRefSheet.Cell(rowIdx, 2).Value = card.Description ?? string.Empty;
                cardRefSheet.Cell(rowIdx, 3).Value = string.Join(", ", orderedAbx);
                cardRefSheet.Cell(rowIdx, 4).Value = string.Join(", ", mappedOrgs);
                rowIdx++;
            }

            cardRefSheet.SheetView.FreezeRows(1);
            cardRefSheet.Columns().AdjustToContents();
        }

        // =================================================
        // Instructions Sheet
        // =================================================

        var instructions = workbook.Worksheets.Add("Instructions");

        instructions.Cell(1, 1).Value = "Worksheet Import Instructions";
        instructions.Cell(1, 1).Style.Font.Bold = true;

        instructions.Cell(3, 1).Value = "1.";
        instructions.Cell(3, 2).Value = "Do not change the column names in the Worksheet sheet.";

        instructions.Cell(4, 1).Value = "2.";
        instructions.Cell(4, 2).Value = "IsolateID, PatientID, and CultureResult are required.";

        instructions.Cell(5, 1).Value = "3.";
        instructions.Cell(5, 2).Value = "PatientID must already exist in the Patient reference table.";

        instructions.Cell(6, 1).Value = "4.";
        instructions.Cell(6, 2).Value = "WHONETID must correspond to an organism in the Organism reference table.";

        instructions.Cell(7, 1).Value = "5.";
        instructions.Cell(7, 2).Value = "Multiple AST cards should be separated using semicolons (e.g., GN83;XN05).";

        instructions.Cell(8, 1).Value = "6.";
        instructions.Cell(8, 2).Value = "MIC and interpretation columns are generated from active antibiotics in alphabetical schema order.";

        instructions.Cell(9, 1).Value = "7.";
        instructions.Cell(9, 2).Value = "Do not manually add, remove, or rename antibiotic columns.";

        instructions.Cell(10, 1).Value = "8.";
        instructions.Cell(10, 2).Value = "Use POS or NEG for applicable screening and AST finding fields.";

        instructions.Columns().AdjustToContents();

        // =================================================
        // Return File
        // =================================================

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }
}
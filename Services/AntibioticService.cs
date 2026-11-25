using System.Data;
using System.Text.RegularExpressions;
using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public partial class AntibioticService(ApplicationDbContext context) : IAntibioticService
{
    [GeneratedRegex(@"^[A-Z0-9_]+$")]
    private static partial Regex AntibioticCodeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_]+$")]
    private static partial Regex ColumnNameRegex();

    public async Task<List<Antibiotic>> GetAllAsync()
    {
        return await context.Antibiotics
            .AsNoTracking()
            .Include(a => a.AntibioticClass)
            .OrderBy(a => a.AntibioticName)
            .ToListAsync();
    }

    public async Task<Antibiotic?> GetByIdAsync(string antibioticCode)
    {
        return await context.Antibiotics
            .Include(a => a.AntibioticClass)
            .FirstOrDefaultAsync(a => a.AntibioticCode == antibioticCode);
    }

    public async Task<bool> ExistsAsync(string antibioticCode)
    {
        return await context.Antibiotics
            .AnyAsync(a => a.AntibioticCode == antibioticCode);
    }

    public async Task CreateAsync(Antibiotic antibiotic)
    {
        antibiotic.AntibioticCode = antibiotic.AntibioticCode.Trim().ToUpperInvariant();
        var normalizedCards = ParseASTCards(antibiotic.ASTCard);
        antibiotic.ASTCard = string.Join(";", normalizedCards);

        await using var transaction = await context.Database.BeginTransactionAsync();

        context.Antibiotics.Add(antibiotic);
        await context.SaveChangesAsync();

        await SyncASTCardMappingsAsync(antibiotic.AntibioticCode, normalizedCards);
        await context.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    public async Task UpdateAsync(
        string antibioticCode,
        Antibiotic antibiotic)
    {
        var existing = await context.Antibiotics
            .FirstOrDefaultAsync(a => a.AntibioticCode == antibioticCode);

        if (existing == null)
            return;

        var normalizedCards = ParseASTCards(antibiotic.ASTCard);

        existing.AntibioticName = antibiotic.AntibioticName;
        existing.AntibioticClassName = antibiotic.AntibioticClassName;
        existing.ASTCard = string.Join(";", normalizedCards);
        existing.MICEnabled = antibiotic.MICEnabled;
        existing.InterpretationEnabled = antibiotic.InterpretationEnabled;
        existing.Active = antibiotic.Active;
        existing.IncludeInMonthlyReport = antibiotic.IncludeInMonthlyReport;

        await using var transaction = await context.Database.BeginTransactionAsync();

        await SyncASTCardMappingsAsync(existing.AntibioticCode, normalizedCards);
        await context.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    public async Task DeleteAsync(string antibioticCode)
    {
        if (string.IsNullOrWhiteSpace(antibioticCode))
            return;

        var normalizedCode = antibioticCode.Trim().ToUpperInvariant();

        var antibiotic = await context.Antibiotics
            .FirstOrDefaultAsync(a => a.AntibioticCode == normalizedCode);

        if (antibiotic == null)
            return;

        if (await HasWorksheetResultsAsync(normalizedCode))
        {
            throw new InvalidOperationException(
                $"Antibiotic '{normalizedCode}' cannot be deleted because at least one Worksheet record contains MIC or interpretation data for it.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync();

        var cardMappings = await context.ASTCardAntibiotics
            .Where(ca => ca.AntibioticCode == normalizedCode)
            .ToListAsync();

        if (cardMappings.Count > 0)
        {
            context.ASTCardAntibiotics.RemoveRange(cardMappings);
        }

        context.Antibiotics.Remove(antibiotic);
        await context.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    // =====================================================
    // Sync ASTCards & ASTCardAntibiotics
    // =====================================================

    private async Task SyncASTCardMappingsAsync(
        string antibioticCode,
        List<string> targetCards)
    {
        var targetCardSet = new HashSet<string>(
            targetCards,
            StringComparer.OrdinalIgnoreCase);

        var existingMappings = await context.ASTCardAntibiotics
            .Where(ca => ca.AntibioticCode == antibioticCode)
            .ToListAsync();

        var mappingsToRemove = existingMappings
            .Where(m => !targetCardSet.Contains(m.ASTCard))
            .ToList();

        if (mappingsToRemove.Count > 0)
        {
            context.ASTCardAntibiotics.RemoveRange(mappingsToRemove);
        }

        var existingCardCodes = existingMappings
            .Select(m => m.ASTCard)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var cardCode in targetCards)
        {
            var cardExists = await context.ASTCards
                .AnyAsync(c => c.ASTCard == cardCode);

            if (!cardExists)
            {
                context.ASTCards.Add(new ASTCardDefinition
                {
                    ASTCard = cardCode,
                    IsActive = true
                });
            }

            if (!existingCardCodes.Contains(cardCode))
            {
                var maxOrder = await context.ASTCardAntibiotics
                    .Where(ca => ca.ASTCard == cardCode)
                    .Select(ca => (int?)ca.DisplayOrder)
                    .MaxAsync() ?? 0;

                context.ASTCardAntibiotics.Add(new ASTCardAntibiotic
                {
                    ASTCard = cardCode,
                    AntibioticCode = antibioticCode,
                    DisplayOrder = maxOrder + 1
                });
            }
        }
    }

    // =====================================================
    // Guard Against Deleting Antibiotics With Worksheet Data
    // =====================================================

    private async Task<bool> HasWorksheetResultsAsync(string antibioticCode)
    {
        if (!AntibioticCodeRegex().IsMatch(antibioticCode))
        {
            throw new InvalidOperationException(
                $"Invalid antibiotic code: '{antibioticCode}'.");
        }

        var micColumn = $"{antibioticCode}_MIC";
        var intColumn = $"{antibioticCode}_INT";

        if (!ColumnNameRegex().IsMatch(micColumn) ||
            !ColumnNameRegex().IsMatch(intColumn))
        {
            throw new InvalidOperationException(
                $"Invalid AST column identifier for antibiotic '{antibioticCode}'.");
        }

        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var checkColumnsCmd = connection.CreateCommand();
            checkColumnsCmd.CommandText = """
                SELECT COUNT(1)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME = 'Worksheet'
                  AND COLUMN_NAME IN (@MicColumn, @IntColumn);
                """;

            var micColParam = checkColumnsCmd.CreateParameter();
            micColParam.ParameterName = "@MicColumn";
            micColParam.Value = micColumn;
            checkColumnsCmd.Parameters.Add(micColParam);

            var intColParam = checkColumnsCmd.CreateParameter();
            intColParam.ParameterName = "@IntColumn";
            intColParam.Value = intColumn;
            checkColumnsCmd.Parameters.Add(intColParam);

            var existingColumnCount = Convert.ToInt32(
                await checkColumnsCmd.ExecuteScalarAsync());

            if (existingColumnCount == 0)
            {
                return false;
            }

            await using var checkDataCmd = connection.CreateCommand();
            checkDataCmd.CommandText = $"""
                SELECT TOP (1) 1
                FROM [dbo].[Worksheet]
                WHERE NULLIF(LTRIM(RTRIM(CAST([{micColumn}] AS NVARCHAR(100)))), N'') IS NOT NULL
                   OR NULLIF(LTRIM(RTRIM(CAST([{intColumn}] AS NVARCHAR(100)))), N'') IS NOT NULL;
                """;

            var result = await checkDataCmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    // =====================================================
    // Helpers
    // =====================================================

    private static List<string> ParseASTCards(string? rawAstCard)
    {
        if (string.IsNullOrWhiteSpace(rawAstCard))
            return [];

        return rawAstCard
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
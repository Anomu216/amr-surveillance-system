using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class ASTConfigurationService(ApplicationDbContext context)
    : IASTConfigurationService
{
    // =====================================================
    // Active Antibiotics
    // =====================================================

    public async Task<List<Antibiotic>> GetActiveAntibioticsAsync()
    {
        return await context.Antibiotics
            .AsNoTracking()
            .Where(a => a.Active)
            .OrderBy(a => a.AntibioticName)
            .ToListAsync();
    }

    // =====================================================
    // Antibiotics by AST Card (Database-Driven Order)
    // =====================================================

    public async Task<List<Antibiotic>> GetAntibioticsByCardAsync(
        string astCard)
    {
        if (string.IsNullOrWhiteSpace(astCard))
            return [];

        var normalizedCard = astCard.Trim().ToUpperInvariant();

        var orderedCardEntries = await context.ASTCardAntibiotics
            .AsNoTracking()
            .Where(ca =>
                ca.ASTCard == normalizedCard &&
                ca.Card.IsActive)
            .OrderBy(ca => ca.DisplayOrder)
            .ThenBy(ca => ca.AntibioticCode)
            .ToListAsync();

        if (orderedCardEntries.Count > 0)
        {
            var orderMap = orderedCardEntries
                .GroupBy(ca => ca.AntibioticCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().DisplayOrder,
                    StringComparer.OrdinalIgnoreCase);

            var codes = orderMap.Keys.ToList();

            var antibiotics = await context.Antibiotics
                .AsNoTracking()
                .Where(a => a.Active && codes.Contains(a.AntibioticCode))
                .ToListAsync();

            return
            [
                .. antibiotics
                    .OrderBy(a => orderMap.TryGetValue(a.AntibioticCode.Trim(), out var order)
                        ? order
                        : int.MaxValue)
                    .ThenBy(a => a.AntibioticName, StringComparer.OrdinalIgnoreCase)
            ];
        }

        // Fallback to legacy semicolon-delimited Antibiotic.ASTCard
        var legacyCandidates = await context.Antibiotics
            .AsNoTracking()
            .Where(a =>
                a.Active &&
                a.ASTCard.Contains(normalizedCard))
            .OrderBy(a => a.AntibioticName)
            .ToListAsync();

        return
        [
            .. legacyCandidates.Where(a =>
                a.ASTCard
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(c => string.Equals(c, normalizedCard, StringComparison.OrdinalIgnoreCase)))
        ];
    }

    // =====================================================
    // AST Cards (Active DB Cards + Legacy Fallback)
    // =====================================================

    public async Task<List<string>> GetASTCardsAsync()
    {
        var dbCards = await context.ASTCards
            .AsNoTracking()
            .Where(c => c.IsActive && !string.IsNullOrWhiteSpace(c.ASTCard))
            .Select(c => c.ASTCard.Trim().ToUpperInvariant())
            .ToListAsync();

        var legacyCardStrings = await context.Antibiotics
            .AsNoTracking()
            .Where(a =>
                a.Active &&
                !string.IsNullOrWhiteSpace(a.ASTCard))
            .Select(a => a.ASTCard)
            .ToListAsync();

        var legacyCards = legacyCardStrings
            .SelectMany(cards =>
                cards.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries))
            .Select(card => card.ToUpperInvariant());

        return
        [
            .. dbCards
                .Concat(legacyCards)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(card => card)
        ];
    }
}
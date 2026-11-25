
using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Imports.Services;

public class GeneReferenceImportService
    : IImportService<GeneReference>
{
    private readonly ApplicationDbContext _context;
    private readonly IGeneReferenceSchemaService _schemaService;

    public GeneReferenceImportService(
        ApplicationDbContext context,
        IGeneReferenceSchemaService schemaService)
    {
        _context = context;
        _schemaService = schemaService;
    }

    public async Task CreateRangeAsync(
        IEnumerable<GeneReference> entities)
    {
        var genes = entities
            .Where(g => g != null)
            .ToList();

        if (genes.Count == 0)
            return;

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            // Ensure the corresponding Genomics column exists
            // for each gene before saving the reference records.
            foreach (var gene in genes)
            {
                await _schemaService.EnsureGeneColumnAsync(
                    gene.GeneName);
            }

            await _context.GeneReferences.AddRangeAsync(genes);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

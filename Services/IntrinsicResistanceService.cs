using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class IntrinsicResistanceService(ApplicationDbContext context) : IIntrinsicResistanceService
{
    // =========================================================
    // Get All
    // =========================================================

    public async Task<List<IntrinsicResistance>> GetAllAsync()
    {
        return await context.IntrinsicResistances
            .AsNoTracking()
            .Include(i => i.Organism)
            .Include(i => i.Antibiotic)
            .OrderBy(i => i.WHONETID)
            .ThenBy(i => i.AntibioticCode)
            .ToListAsync();
    }

    // =========================================================
    // Search
    // =========================================================

    public async Task<List<IntrinsicResistance>> SearchAsync(
        string? whonetID,
        string? antibioticCode,
        string? search)
    {
        var query = context.IntrinsicResistances
            .AsNoTracking()
            .Include(i => i.Organism)
            .Include(i => i.Antibiotic)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(whonetID))
        {
            query = query.Where(i =>
                i.WHONETID == whonetID);
        }

        if (!string.IsNullOrWhiteSpace(antibioticCode))
        {
            query = query.Where(i =>
                i.AntibioticCode == antibioticCode);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();

            query = query.Where(i =>
                i.WHONETID.Contains(trimmedSearch) ||
                i.AntibioticCode.Contains(trimmedSearch) ||
                (i.Antibiotic != null &&
                 i.Antibiotic.AntibioticName.Contains(trimmedSearch)));
        }

        return await query
            .OrderBy(i => i.WHONETID)
            .ThenBy(i => i.AntibioticCode)
            .ToListAsync();
    }

    // =========================================================
    // Get By ID
    // =========================================================

    public async Task<IntrinsicResistance?> GetByIdAsync(
        string whonetID,
        string antibioticCode)
    {
        return await context.IntrinsicResistances
            .Include(i => i.Organism)
            .Include(i => i.Antibiotic)
            .FirstOrDefaultAsync(i =>
                i.WHONETID == whonetID &&
                i.AntibioticCode == antibioticCode);
    }

    // =========================================================
    // Exists
    // =========================================================

    public async Task<bool> ExistsAsync(
        string whonetID,
        string antibioticCode)
    {
        return await context.IntrinsicResistances
            .AnyAsync(i =>
                i.WHONETID == whonetID &&
                i.AntibioticCode == antibioticCode);
    }

    // =========================================================
    // Organism Exists
    // =========================================================

    public async Task<bool> OrganismExistsAsync(
        string whonetID)
    {
        return await context.Organisms
            .AnyAsync(o =>
                o.WHONETID == whonetID);
    }

    // =========================================================
    // Antibiotic Exists
    // =========================================================

    public async Task<bool> AntibioticExistsAsync(
        string antibioticCode)
    {
        return await context.Antibiotics
            .AnyAsync(a =>
                a.AntibioticCode == antibioticCode);
    }

    // =========================================================
    // Create
    // =========================================================

    public async Task CreateAsync(
        IntrinsicResistance intrinsicResistance)
    {
        context.IntrinsicResistances.Add(
            intrinsicResistance);

        await context.SaveChangesAsync();
    }

    // =========================================================
    // Delete
    // =========================================================

    public async Task DeleteAsync(
        string whonetID,
        string antibioticCode)
    {
        var intrinsicResistance =
            await context.IntrinsicResistances
                .FirstOrDefaultAsync(i =>
                    i.WHONETID == whonetID &&
                    i.AntibioticCode == antibioticCode);

        if (intrinsicResistance is null)
            return;

        context.IntrinsicResistances.Remove(
            intrinsicResistance);

        await context.SaveChangesAsync();
    }
}
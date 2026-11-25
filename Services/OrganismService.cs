using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class OrganismService(ApplicationDbContext context) : IOrganismService
{
    // =========================================================
    // Get All
    // =========================================================

    public async Task<List<Organism>> GetAllAsync()
    {
        return await context.Organisms
            .AsNoTracking()
            .Include(o => o.IntrinsicResistances)
            .OrderBy(o => o.WHONETID)
            .ToListAsync();
    }

    // =========================================================
    // Get By ID
    // =========================================================

    public async Task<Organism?> GetByIdAsync(
        string whonetID)
    {
        return await context.Organisms
            .Include(o => o.IntrinsicResistances)
            .FirstOrDefaultAsync(o =>
                o.WHONETID == whonetID);
    }

    // =========================================================
    // Exists
    // =========================================================

    public async Task<bool> ExistsAsync(
        string whonetID)
    {
        return await context.Organisms
            .AnyAsync(o =>
                o.WHONETID == whonetID);
    }

    // =========================================================
    // Create
    // =========================================================

    public async Task CreateAsync(
        Organism organism)
    {
        context.Organisms.Add(organism);

        await context.SaveChangesAsync();
    }

    // =========================================================
    // Delete
    // =========================================================

    public async Task DeleteAsync(
        string whonetID)
    {
        var organism =
            await context.Organisms
                .FirstOrDefaultAsync(o =>
                    o.WHONETID == whonetID);

        if (organism is null)
            return;

        context.Organisms.Remove(organism);

        await context.SaveChangesAsync();
    }
}
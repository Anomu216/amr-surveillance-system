using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Models;

namespace AMRSurveillance.Imports.Services;

public class FacilityImportService : IImportService<Facility>
{
    private readonly ApplicationDbContext _context;

    public FacilityImportService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CreateRangeAsync(
        IEnumerable<Facility> entities)
    {
        var facilities =
            entities.ToList();

        if (!facilities.Any())
        {
            return;
        }

        await _context.Facilities
            .AddRangeAsync(facilities);

        await _context.SaveChangesAsync();
    }
}
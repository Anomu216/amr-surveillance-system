using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class FacilityService :  IFacilityService,  IImportService<Facility>
{
    private readonly ApplicationDbContext _context;

    public FacilityService(ApplicationDbContext context)
    {
        _context = context;
    }

    // =============================================
    // Get All
    // =============================================

    public async Task<List<Facility>> GetAllAsync()
    {
        return await _context.Facilities
            .OrderBy(f => f.FacilityID)
            .ToListAsync();
    }

    // =============================================
    // Get By ID
    // =============================================

    public async Task<Facility?> GetByIdAsync(string facilityID)
    {
        return await _context.Facilities
            .FirstOrDefaultAsync(f =>
                f.FacilityID == facilityID);
    }

    // =============================================
    // Exists
    // =============================================

    public async Task<bool> ExistsAsync(string facilityID)
    {
        return await _context.Facilities
            .AnyAsync(f =>
                f.FacilityID == facilityID);
    }

    // =============================================
    // Create
    // =============================================

    public async Task CreateAsync(Facility facility)
    {
        _context.Facilities.Add(facility);

        await _context.SaveChangesAsync();
    }

    // =============================================
    // Update
    // =============================================

    public async Task UpdateAsync(Facility facility)
    {
        _context.Facilities.Update(facility);

        await _context.SaveChangesAsync();
    }

    // =============================================
    // Bulk Create
    // =============================================

    public async Task CreateRangeAsync(
        IEnumerable<Facility> facilities)
    {
        await _context.Facilities.AddRangeAsync(facilities);

        await _context.SaveChangesAsync();
    }
}
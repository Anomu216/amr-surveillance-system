using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface IFacilityService
{
    // =============================================
    // Retrieval
    // =============================================

    Task<List<Facility>> GetAllAsync();

    Task<Facility?> GetByIdAsync(string facilityID);

    Task<bool> ExistsAsync(string facilityID);

    // =============================================
    // CRUD
    // =============================================

    Task CreateAsync(Facility facility);

    Task UpdateAsync(Facility facility);

    // =============================================
    // Bulk Import
    // =============================================

    Task CreateRangeAsync(IEnumerable<Facility> facilities);
}
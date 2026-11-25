
using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface IShipmentService
{
    // Retrieve shipments
    Task<List<Shipment>> GetAllAsync();

    Task<Shipment?> GetByIdAsync(string kenID);

    // Identifier validation
    Task<bool> ExistsAsync(string kenID);

    Task<bool> MRSNIDExistsAsync(
        string mrsnID,
        string? excludeKENID = null);

    // Create and update shipments
    Task CreateAsync(Shipment shipment);

    Task UpdateAsync(Shipment shipment);
    //Delete
    Task DeleteAsync(string kenID);
    // Validate worksheet relationship
    Task<bool> WorksheetExistsAsync(string isolateID);
}

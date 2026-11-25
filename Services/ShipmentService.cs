using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class ShipmentService(ApplicationDbContext context) : IShipmentService
{
    // =========================================================
    // Get All
    // =========================================================

    public async Task<List<Shipment>> GetAllAsync()
    {
        return await context.Shipments
            .AsNoTracking()
            .Include(s => s.Worksheet)
            .OrderBy(s => s.KENID)
            .ToListAsync();
    }

    // =========================================================
    // Get By ID
    // =========================================================

    public async Task<Shipment?> GetByIdAsync(string kenID)
    {
        return await context.Shipments
            .Include(s => s.Worksheet)
            .FirstOrDefaultAsync(s => s.KENID == kenID);
    }

    // =========================================================
    // Exists
    // =========================================================

    public async Task<bool> ExistsAsync(string kenID)
    {
        return await context.Shipments
            .AnyAsync(s => s.KENID == kenID);
    }

    // =========================================================
    // MRSNID Exists
    // =========================================================

    public async Task<bool> MRSNIDExistsAsync(
        string mrsnID,
        string? excludeKENID = null)
    {
        if (string.IsNullOrWhiteSpace(mrsnID))
        {
            return false;
        }

        var normalizedMRSNID = mrsnID.Trim();

        var query = context.Shipments
            .Where(s => s.MRSNID != null
                && s.MRSNID == normalizedMRSNID);

        if (!string.IsNullOrWhiteSpace(excludeKENID))
        {
            query = query.Where(s => s.KENID != excludeKENID);
        }

        return await query.AnyAsync();
    }

    // =========================================================
    // Worksheet Exists
    // =========================================================

    public async Task<bool> WorksheetExistsAsync(string isolateID)
    {
        return await context.Worksheets
            .AnyAsync(w => w.IsolateID == isolateID);
    }

    // =========================================================
    // Create
    // =========================================================

    public async Task CreateAsync(Shipment shipment)
    {
        NormalizeShipment(shipment);

        context.Shipments.Add(shipment);

        await context.SaveChangesAsync();
    }

    // =========================================================
    // Update
    // =========================================================

    public async Task UpdateAsync(Shipment shipment)
    {
        NormalizeShipment(shipment);

        context.Shipments.Update(shipment);

        await context.SaveChangesAsync();
    }

    // =========================================================
    // Delete
    // =========================================================

    public async Task DeleteAsync(string kenID)
    {
        var shipment = await context.Shipments
            .FirstOrDefaultAsync(s => s.KENID == kenID);

        if (shipment is null)
            return;

        context.Shipments.Remove(shipment);

        await context.SaveChangesAsync();
    }

    // =========================================================
    // Helpers
    // =========================================================

    private static void NormalizeShipment(Shipment shipment)
    {
        shipment.KENID = shipment.KENID.Trim();
        shipment.IsolateID = shipment.IsolateID.Trim();

        shipment.MALDIID = NormalizeOptional(shipment.MALDIID);
        shipment.MRSNID = NormalizeOptional(shipment.MRSNID);
        shipment.ShipmentNumber = NormalizeOptional(shipment.ShipmentNumber);
        shipment.BoxNumber = NormalizeOptional(shipment.BoxNumber);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
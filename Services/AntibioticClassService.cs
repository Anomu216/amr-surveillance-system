using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class AntibioticClassService : IAntibioticClassService
{
    private readonly ApplicationDbContext _context;

    public AntibioticClassService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AntibioticClass>> GetAllAsync()
    {
        return await _context.AntibioticClasses
            .AsNoTracking()
            .OrderBy(a => a.AntibioticClassName)
            .ToListAsync();
    }

    public async Task<AntibioticClass?> GetByIdAsync(
        string antibioticClassName)
    {
        return await _context.AntibioticClasses
            .FirstOrDefaultAsync(a =>
                a.AntibioticClassName == antibioticClassName);
    }

    public async Task<bool> ExistsAsync(
        string antibioticClassName)
    {
        return await _context.AntibioticClasses
            .AnyAsync(a =>
                a.AntibioticClassName == antibioticClassName);
    }

    public async Task CreateAsync(
        AntibioticClass antibioticClass)
    {
        _context.AntibioticClasses.Add(antibioticClass);

        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(
        string originalName,
        AntibioticClass antibioticClass)
    {
        var existing =
            await GetByIdAsync(originalName);

        if (existing == null)
            return;

        existing.AntibioticClassName =
            antibioticClass.AntibioticClassName;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(
        string antibioticClassName)
    {
        var antibioticClass =
            await GetByIdAsync(antibioticClassName);

        if (antibioticClass == null)
            return;

        _context.AntibioticClasses.Remove(antibioticClass);

        await _context.SaveChangesAsync();
    }
}
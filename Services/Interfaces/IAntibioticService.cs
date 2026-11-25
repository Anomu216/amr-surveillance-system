using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface IAntibioticService
{
    Task<List<Antibiotic>> GetAllAsync();

    Task<Antibiotic?> GetByIdAsync(string antibioticCode);

    Task<bool> ExistsAsync(string antibioticCode);

    Task CreateAsync(Antibiotic antibiotic);

    Task UpdateAsync(string antibioticCode, Antibiotic antibiotic);

    Task DeleteAsync(string antibioticCode);
}
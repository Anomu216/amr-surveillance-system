using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface IAntibioticClassService
{
    Task<List<AntibioticClass>> GetAllAsync();

    Task<AntibioticClass?> GetByIdAsync(
        string antibioticClassName);

    Task<bool> ExistsAsync(
        string antibioticClassName);

    Task CreateAsync(
        AntibioticClass antibioticClass);

    Task UpdateAsync(
        string originalName,
        AntibioticClass antibioticClass);

    Task DeleteAsync(
        string antibioticClassName);
}
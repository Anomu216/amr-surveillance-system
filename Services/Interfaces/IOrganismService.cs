using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface IOrganismService
{
    Task<List<Organism>> GetAllAsync();

    Task<Organism?> GetByIdAsync(
        string whonetID);

    Task<bool> ExistsAsync(
        string whonetID);

    Task CreateAsync(
        Organism organism);

    Task DeleteAsync(
        string whonetID);
}
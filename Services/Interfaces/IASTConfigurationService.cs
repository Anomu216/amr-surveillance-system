using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface IASTConfigurationService
{
    Task<List<Antibiotic>> GetActiveAntibioticsAsync();

    Task<List<Antibiotic>> GetAntibioticsByCardAsync(
        string astCard);

    Task<List<string>> GetASTCardsAsync();
}
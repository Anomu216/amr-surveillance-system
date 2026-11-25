
using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface IPatientService
{
    Task<IEnumerable<Patient>> GetAllAsync();

    Task<Patient?> GetByIdAsync(string patientID);

    Task<Patient?> GetDetailsByIdAsync(string patientID);

    Task<IReadOnlyList<string>> SearchPatientIdsAsync(
        string query,
        int maxResults = 10);

    Task CreateAsync(Patient patient);

    Task UpdateAsync(Patient patient);

    Task<bool> ExistsAsync(string patientID);
}
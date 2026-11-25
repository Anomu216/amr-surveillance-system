using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public record DerivedWorksheetClassification(
    bool? MDR,
    bool? MRSA,
    bool? Carbapenemase);

public interface IWorksheetService
{
    // =====================================================
    // Worksheet Retrieval
    // =====================================================

    Task<List<Worksheet>> GetAllAsync();

    Task<Worksheet?> GetByIdAsync(
        string isolateID);

    Task<bool> ExistsAsync(
        string isolateID);


    // =====================================================
    // Reference Validation
    // =====================================================

    Task<bool> PatientExistsAsync(
        string patientID);

    Task<bool> OrganismExistsAsync(
        string whonetID);


    // =====================================================
    // Derived Classification
    // =====================================================

    Task<DerivedWorksheetClassification> CalculateDerivedResultsAsync(
        string? whonetId,
        string? cefoxitinScreen,
        IDictionary<string, string?>? interpretationsByCode);


    // =====================================================
    // Worksheet CRUD
    // =====================================================

    Task CreateAsync(
        Worksheet worksheet);

    Task UpdateAsync(
        string isolateID,
        Worksheet worksheet);

    Task DeleteAsync(
        string isolateID);
}

using AMRSurveillance.Models;
using AMRSurveillance.ViewModels;

namespace AMRSurveillance.Services.Interfaces;

public interface IGenomicsService
{
    // Retrieve all records for the unified Genomics table.
    Task<List<GenomicsRecordViewModel>> GetAllRecordsAsync();

    // Retrieve one record, including its dynamic gene values.
    Task<GenomicsRecordViewModel?> GetRecordByIsolateIdAsync(
        string isolateId);

    // Retrieve the registered gene names for dynamic columns.
    Task<List<string>> GetRegisteredGeneNamesAsync();

    // Check whether an isolate already has a Genomics record.
    Task<bool> ExistsAsync(string isolateId);

    // Create a Genomics record with fixed fields and gene values.
    Task CreateAsync(GenomicsRecordViewModel model);

    // Update fixed fields and gene values for an existing isolate.
    Task UpdateAsync( string isolateId,GenomicsRecordViewModel model);

    // Delete a Genomics record without deleting shared gene columns.
    Task DeleteAsync(string isolateId);
}

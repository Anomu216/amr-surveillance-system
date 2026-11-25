using AMRSurveillance.ViewModels;

namespace AMRSurveillance.Services.Interfaces;

public interface IASTSchemaService
{
    Task EnsureASTColumnsAsync();

    Task SaveASTResultsAsync(
        string isolateID,
        IEnumerable<WorksheetAntibioticViewModel> antibiotics);
    Task LoadASTResultsAsync(
    string isolateID,
    IList<WorksheetAntibioticViewModel> antibiotics);
}
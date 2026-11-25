using AMRSurveillance.Models;
using AMRSurveillance.ViewModels;

namespace AMRSurveillance.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(
        DashboardViewModel filters,
        CancellationToken cancellationToken = default);

    Task<DashboardExportViewModel> GetExportDataAsync(
        DashboardViewModel filters,
        CancellationToken cancellationToken = default);

    Task<List<ResistanceSpikeAlert>> DetectRollingQuarterlySpikesAsync(
        List<string>? effectiveFacilityIds,
        List<string> globalOrganismIds,
        List<string> astOrganismIds,
        List<Antibiotic> validAntibiotics,
        string? recruitmentType = null,
        CancellationToken cancellationToken = default);

    Task<List<ResistanceSpikeAlert>> DetectSystemWideRollingSpikesAsync(
        CancellationToken cancellationToken = default);
}
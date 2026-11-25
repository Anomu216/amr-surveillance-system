using AMRSurveillance.ViewModels;

namespace AMRSurveillance.Services.Interfaces;

public interface IReportService
{
    Task<ReportViewModel> GenerateMonthlyReportAsync(
        string? periodKey,
        DateTime? customStart,
        DateTime? customEnd,
        List<string>? facilityIds,
        List<string>? organismIds,
        string? haiCai,
        CancellationToken cancellationToken = default);

    Task<List<MonthlyRollupSpikeAlert>> DetectMonthlyRollupSpikesAsync(
        CancellationToken cancellationToken = default);
}
using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface IDataExportService
{
    Task<byte[]> GenerateExcelAsync(
        DataRequest dataRequest,
        CancellationToken cancellationToken = default);
}
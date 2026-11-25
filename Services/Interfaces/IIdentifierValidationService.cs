using AMRSurveillance.ViewModels.DataRequests;

namespace AMRSurveillance.Services.Interfaces;

public interface IIdentifierValidationService
{
    Task<List<DataRequestIdentifierValidationViewModel>>
        ValidateIdentifiersAsync(
            string identifierType,
            IEnumerable<string> identifiers,
            CancellationToken cancellationToken = default);
}
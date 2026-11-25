
namespace AMRSurveillance.Services.Interfaces;

public interface IGeneReferenceSchemaService
{
    Task EnsureGeneColumnAsync(string geneName);
}

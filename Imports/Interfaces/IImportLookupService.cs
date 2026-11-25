using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.Imports.Interfaces;

public interface IImportLookupService
{
    IEnumerable<SelectListItem> GetValues(string lookupName);

    Task<IEnumerable<SelectListItem>> GetValuesAsync(
        string lookupName);
}
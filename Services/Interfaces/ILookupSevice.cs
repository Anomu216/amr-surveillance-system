using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.Services.Interfaces;

public interface ILookupService
{
    // ============================================================
    // DATABASE-BACKED LOOKUPS
    // ============================================================

    Task<IEnumerable<SelectListItem>> GetFacilitiesAsync();

    Task<IEnumerable<SelectListItem>> GetAntibioticClassesAsync();

    Task<IEnumerable<SelectListItem>> GetOrganismsAsync();

    Task<IEnumerable<SelectListItem>> GetAntibioticsAsync();


    // ============================================================
    // GEOGRAPHICAL LOOKUPS
    // ============================================================

    Task<IEnumerable<SelectListItem>> GetCountiesAsync();

    Task<IEnumerable<SelectListItem>> GetSubCountiesAsync(string county);


    // ============================================================
    // STATIC PATIENT LOOKUPS
    // ============================================================

    IEnumerable<SelectListItem> GetYesNo();

    IEnumerable<SelectListItem> GetAcceptReject();

    IEnumerable<SelectListItem> GetSex();

    IEnumerable<SelectListItem> GetAgeUnits();

    IEnumerable<SelectListItem> GetMilitaryCivilian();

    IEnumerable<SelectListItem> GetPatientTypes();

    IEnumerable<SelectListItem> GetResidenceDescriptions();

    IEnumerable<SelectListItem> GetClinicalPresentations();

    IEnumerable<SelectListItem> GetHospitalLocations();

    IEnumerable<SelectListItem> GetInfectionSources();

    IEnumerable<SelectListItem> GetInfectionSites();

    IEnumerable<SelectListItem> GetSkinInfectionSymptoms();

    IEnumerable<SelectListItem> GetOccupationCategories();

    IEnumerable<SelectListItem> GetHAICAI();
}
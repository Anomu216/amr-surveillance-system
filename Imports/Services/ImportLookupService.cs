using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.Imports.Services;

public class ImportLookupService(ILookupService lookupService) : IImportLookupService
{
    public IEnumerable<SelectListItem> GetValues(
        string lookupName)
    {
        return lookupName switch
        {
            "AcceptReject" =>
                lookupService.GetAcceptReject(),

            "YesNo" =>
                lookupService.GetYesNo(),

            "Sex" =>
                lookupService.GetSex(),

            "AgeUnit" =>
                lookupService.GetAgeUnits(),

            "MilitaryCivilian" =>
                lookupService.GetMilitaryCivilian(),

            "PatientType" =>
                lookupService.GetPatientTypes(),

            "ResidenceDescription" =>
                lookupService.GetResidenceDescriptions(),

            "ClinicalPresentation" =>
                lookupService.GetClinicalPresentations(),

            "HospitalLocation" =>
                lookupService.GetHospitalLocations(),

            "InfectionSource" =>
                lookupService.GetInfectionSources(),

            "InfectionSite" =>
                lookupService.GetInfectionSites(),

            "SkinInfectionSymptoms" =>
                lookupService.GetSkinInfectionSymptoms(),

            "OccupationCategory" =>
                lookupService.GetOccupationCategories(),

            "HAICAI" =>
                lookupService.GetHAICAI(),

            _ => throw new ArgumentException(
                $"Unknown import lookup: {lookupName}",
                nameof(lookupName))
        };
    }

    public async Task<IEnumerable<SelectListItem>> GetValuesAsync(
        string lookupName)
    {
        return lookupName switch
        {
            "County" =>
                await lookupService.GetCountiesAsync(),

            _ => GetValues(lookupName)
        };
    }
}
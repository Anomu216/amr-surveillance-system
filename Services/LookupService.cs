using AMRSurveillance.Data;
using AMRSurveillance.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class LookupService(ApplicationDbContext context) : ILookupService
{
    private static SelectListItem Placeholder(string text = "-- Select --")
    {
        return new SelectListItem
        {
            Value = "",
            Text = text
        };
    }

    // ============================================================
    // GENERAL LOOKUPS
    // ============================================================

    public IEnumerable<SelectListItem> GetYesNo()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Yes", Text = "Yes" },
            new SelectListItem { Value = "No", Text = "No" },
            new SelectListItem { Value = "Unknown", Text = "Unknown" }
        ];
    }

    public IEnumerable<SelectListItem> GetAcceptReject()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Accepted", Text = "Accepted" },
            new SelectListItem { Value = "Rejected", Text = "Rejected" },
            new SelectListItem { Value = "Skipped", Text = "Skipped" }
        ];
    }

    public IEnumerable<SelectListItem> GetSex()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Male", Text = "Male" },
            new SelectListItem { Value = "Female", Text = "Female" },
            new SelectListItem { Value = "Unknown", Text = "Unknown" }
        ];
    }

    public IEnumerable<SelectListItem> GetAgeUnits()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Days", Text = "Days" },
            new SelectListItem { Value = "Months", Text = "Months" },
            new SelectListItem { Value = "Years", Text = "Years" }
        ];
    }

    public IEnumerable<SelectListItem> GetMilitaryCivilian()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Military", Text = "Military" },
            new SelectListItem { Value = "Civilian", Text = "Civilian" },
            new SelectListItem { Value = "not indicated", Text = "Not indicated" }
        ];
    }

    public IEnumerable<SelectListItem> GetPatientTypes()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Inpatient", Text = "Inpatient" },
            new SelectListItem { Value = "Outpatient", Text = "Outpatient" },
            new SelectListItem { Value = "not indicated", Text = "Not indicated" }
        ];
    }

    public IEnumerable<SelectListItem> GetResidenceDescriptions()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Urban", Text = "Urban" },
            new SelectListItem { Value = "Semi-Urban", Text = "Semi-Urban" },
            new SelectListItem { Value = "Rural", Text = "Rural" },
            new SelectListItem { Value = "Unknown", Text = "Unknown" }
        ];
    }

    public IEnumerable<SelectListItem> GetClinicalPresentations()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Blood infection", Text = "Blood infection" },
            new SelectListItem { Value = "Surgical Site Infection", Text = "Surgical Site Infection" },
            new SelectListItem { Value = "Skin and Soft Tissue Infection", Text = "Skin and Soft Tissue Infection" },
            new SelectListItem { Value = "Throat infection", Text = "Throat infection" },
            new SelectListItem { Value = "Respiratory infection", Text = "Respiratory infection" },
            new SelectListItem { Value = "Urinary Tract Infection", Text = "Urinary Tract Infection" },
            new SelectListItem { Value = "Other", Text = "Other" },
            new SelectListItem { Value = "Unknown", Text = "Unknown" }
        ];
    }

    public IEnumerable<SelectListItem> GetHospitalLocations()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "OPD", Text = "OPD" },
            new SelectListItem { Value = "Ward", Text = "Ward" },
            new SelectListItem { Value = "ICU", Text = "ICU" },
            new SelectListItem { Value = "HDU", Text = "HDU" },
            new SelectListItem { Value = "not indicated", Text = "Not indicated" }
        ];
    }

    // ============================================================
    // CLINICAL LOOKUPS
    // ============================================================

    public IEnumerable<SelectListItem> GetInfectionSources()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Pus", Text = "Pus" },
            new SelectListItem { Value = "Wound/skin swab", Text = "Wound/skin swab" },
            new SelectListItem { Value = "Throat swab", Text = "Throat swab" },
            new SelectListItem { Value = "Urine", Text = "Urine" },
            new SelectListItem { Value = "Blood", Text = "Blood" },
            new SelectListItem { Value = "Other", Text = "Other" },
            new SelectListItem { Value = "Unknown", Text = "Unknown" }
        ];
    }

    public IEnumerable<SelectListItem> GetInfectionSites()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Abdomen", Text = "Abdomen" },
            new SelectListItem { Value = "Ankle", Text = "Ankle" },
            new SelectListItem { Value = "Anus", Text = "Anus" },
            new SelectListItem { Value = "Arm", Text = "Arm" },
            new SelectListItem { Value = "Armpit", Text = "Armpit" },
            new SelectListItem { Value = "Axillary", Text = "Axillary" },
            new SelectListItem { Value = "Back", Text = "Back" },
            new SelectListItem { Value = "Bag", Text = "Bag" },
            new SelectListItem { Value = "Biceps", Text = "Biceps" },
            new SelectListItem { Value = "Blood", Text = "Blood" },
            new SelectListItem { Value = "Breast", Text = "Breast" },
            new SelectListItem { Value = "Buttocks", Text = "Buttocks" },
            new SelectListItem { Value = "Calf", Text = "Calf" },
            new SelectListItem { Value = "Catheter", Text = "Catheter" },
            new SelectListItem { Value = "Cervix", Text = "Cervix" },
            new SelectListItem { Value = "Cheek", Text = "Cheek" },
            new SelectListItem { Value = "Chest", Text = "Chest" },
            new SelectListItem { Value = "Chin", Text = "Chin" },
            new SelectListItem { Value = "Clean-catch", Text = "Clean-catch" },
            new SelectListItem { Value = "Ear", Text = "Ear" },
            new SelectListItem { Value = "Elbow", Text = "Elbow" },
            new SelectListItem { Value = "Eyebrow", Text = "Eyebrow" },
            new SelectListItem { Value = "Eyelid", Text = "Eyelid" },
            new SelectListItem { Value = "Face", Text = "Face" },
            new SelectListItem { Value = "Finger", Text = "Finger" },
            new SelectListItem { Value = "Foot", Text = "Foot" },
            new SelectListItem { Value = "Forehead", Text = "Forehead" },
            new SelectListItem { Value = "Groin", Text = "Groin" },
            new SelectListItem { Value = "Genitalia", Text = "Genitalia" },
            new SelectListItem { Value = "Gluteal", Text = "Gluteal" },
            new SelectListItem { Value = "Gums", Text = "Gums" },
            new SelectListItem { Value = "Hand", Text = "Hand" },
            new SelectListItem { Value = "Head", Text = "Head" },
            new SelectListItem { Value = "Heel", Text = "Heel" },
            new SelectListItem { Value = "Hip", Text = "Hip" },
            new SelectListItem { Value = "Knee", Text = "Knee" },
            new SelectListItem { Value = "Labia", Text = "Labia" },
            new SelectListItem { Value = "Leg", Text = "Leg" },
            new SelectListItem { Value = "Lip", Text = "Lip" },
            new SelectListItem { Value = "Limb", Text = "Limb" },
            new SelectListItem { Value = "Mandible", Text = "Mandible" },
            new SelectListItem { Value = "Mouth", Text = "Mouth" },
            new SelectListItem { Value = "Navel", Text = "Navel" },
            new SelectListItem { Value = "Neck", Text = "Neck" },
            new SelectListItem { Value = "Nipple", Text = "Nipple" },
            new SelectListItem { Value = "Pelvis", Text = "Pelvis" },
            new SelectListItem { Value = "Penis", Text = "Penis" },
            new SelectListItem { Value = "Rectum", Text = "Rectum" },
            new SelectListItem { Value = "Scalp", Text = "Scalp" },
            new SelectListItem { Value = "Scrotum", Text = "Scrotum" },
            new SelectListItem { Value = "Shin", Text = "Shin" },
            new SelectListItem { Value = "Shoulder", Text = "Shoulder" },
            new SelectListItem { Value = "Stomach", Text = "Stomach" },
            new SelectListItem { Value = "Thigh", Text = "Thigh" },
            new SelectListItem { Value = "Thorax", Text = "Thorax" },
            new SelectListItem { Value = "Tongue", Text = "Tongue" },
            new SelectListItem { Value = "Throat", Text = "Throat" },
            new SelectListItem { Value = "Thumb", Text = "Thumb" },
            new SelectListItem { Value = "Toe", Text = "Toe" },
            new SelectListItem { Value = "Unspecified", Text = "Unspecified" },
            new SelectListItem { Value = "Urethra", Text = "Urethra" },
            new SelectListItem { Value = "Vaginal", Text = "Vaginal" },
            new SelectListItem { Value = "Vulva", Text = "Vulva" },
            new SelectListItem { Value = "Wrist", Text = "Wrist" },
            new SelectListItem { Value = "ni", Text = "ni" }
        ];
    }

    public IEnumerable<SelectListItem> GetHAICAI()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "HAI", Text = "HAI" },
            new SelectListItem { Value = "CAI", Text = "CAI" },
            new SelectListItem { Value = "Unknown", Text = "Unknown" }
        ];
    }

    public IEnumerable<SelectListItem> GetSkinInfectionSymptoms()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Fever", Text = "Fever" },
            new SelectListItem { Value = "Local Pain", Text = "Local Pain" },
            new SelectListItem { Value = "Local temperature increase", Text = "Local temperature increase" },
            new SelectListItem { Value = "Localised swelling", Text = "Localized swelling" },
            new SelectListItem { Value = "Tenderness", Text = "Tenderness" },
            new SelectListItem { Value = "Increased redness", Text = "Increased redness" },
            new SelectListItem { Value = "Edema", Text = "Edema" },
            new SelectListItem { Value = "Foul smelling Odor or discharge", Text = "Foul smelling Odor or discharge" },
            new SelectListItem { Value = "Tissue necrosis", Text = "Tissue necrosis" },
            new SelectListItem { Value = "Cellulitis", Text = "Cellulitis" },
            new SelectListItem { Value = "Swollen lymph nodes", Text = "Swollen lymph nodes" },
            new SelectListItem { Value = "Discharging pus", Text = "Discharging pus" },
            new SelectListItem { Value = "Abscess formation", Text = "Abscess formation" },
            new SelectListItem { Value = "Open wound", Text = "Open wound" },
            new SelectListItem { Value = "Incision", Text = "Incision" },
            new SelectListItem { Value = "New or increased pain", Text = "New or increased pain" },
            new SelectListItem { Value = "Fluid or pus-filled pustules/vesicles", Text = "Fluid or pus-filled pustules/vesicles" },
            new SelectListItem { Value = "Bed sores", Text = "Bed sore" },
            new SelectListItem { Value = "Ulcerated lesion", Text = "Ulcerated lesion" },
            new SelectListItem { Value = "Bloody exudate", Text = "Bloody exudate" },
            new SelectListItem { Value = "Gas in tissues", Text = "Gas in tissues" },
            new SelectListItem { Value = "Black discoloration", Text = "Black discoloration" },
            new SelectListItem { Value = "Sulfure granules in discharges", Text = "Sulfure granules in discharges" },
            new SelectListItem { Value = "Other", Text = "Other" },
            new SelectListItem { Value = "N/A", Text = "N/A" }
        ];
    }

    public IEnumerable<SelectListItem> GetOccupationCategories()
    {
        return
        [
            Placeholder(),
            new SelectListItem { Value = "Soldier", Text = "Soldier" },
            new SelectListItem { Value = "ni", Text = "ni" },
            new SelectListItem { Value = "Other", Text = "Other" },
            new SelectListItem { Value = "Health professional", Text = "Health professional" },
            new SelectListItem { Value = "Administration", Text = "Administration" },
            new SelectListItem { Value = "Hospitality/food", Text = "Hospitality/food" },
            new SelectListItem { Value = "Construction", Text = "Construction" },
            new SelectListItem { Value = "Transportation", Text = "Transportation" },
            new SelectListItem { Value = "Agriculture", Text = "Agriculture" },
            new SelectListItem { Value = "Business person", Text = "Business person" },
            new SelectListItem { Value = "Student", Text = "Student" },
            new SelectListItem { Value = "Child", Text = "Child" },
            new SelectListItem { Value = "Homemaker", Text = "Homemaker" },
            new SelectListItem { Value = "Casual labourer", Text = "Casual labourer" },
            new SelectListItem { Value = "Unemployed", Text = "Unemployed" },
            new SelectListItem { Value = "Education", Text = "Education" },
            new SelectListItem { Value = "Artisan", Text = "Artisan" },
            new SelectListItem { Value = "Marketing/sales", Text = "Marketing/sales" },
            new SelectListItem { Value = "Security", Text = "Security" },
            new SelectListItem { Value = "Professional", Text = "Professional" }
        ];
    }

    // ============================================================
    // GEOGRAPHICAL LOOKUPS
    // ============================================================

    public Task<IEnumerable<SelectListItem>> GetCountiesAsync()
    {
        var counties = KenyaAdministrativeUnits.CountySubCounties
            .Keys
            .OrderBy(x => x)
            .Select(x => new SelectListItem
            {
                Value = x,
                Text = x
            });

        return Task.FromResult(
            new[] { Placeholder() }.Concat(counties));
    }

    public Task<IEnumerable<SelectListItem>> GetSubCountiesAsync(string county)
    {
        if (string.IsNullOrWhiteSpace(county))
        {
            return Task.FromResult<IEnumerable<SelectListItem>>([]);
        }

        if (!KenyaAdministrativeUnits.CountySubCounties
            .TryGetValue(county.Trim(), out var subCounties))
        {
            return Task.FromResult<IEnumerable<SelectListItem>>([]);
        }

        var result = subCounties
            .OrderBy(x => x)
            .Select(x => new SelectListItem
            {
                Value = x,
                Text = x
            });

        return Task.FromResult(result);
    }

    // ============================================================
    // FACILITY LOOKUP
    // ============================================================

    public async Task<IEnumerable<SelectListItem>> GetFacilitiesAsync()
    {
        return await context.Facilities
            .AsNoTracking()
            .OrderBy(f => f.FacilityName)
            .Select(f => new SelectListItem
            {
                Value = f.FacilityID,
                Text = f.FacilityName
            })
            .ToListAsync();
    }

    // ============================================================
    // REFERENCE DATA LOOKUPS
    // ============================================================

    public async Task<IEnumerable<SelectListItem>> GetAntibioticClassesAsync()
    {
        return await context.AntibioticClasses
            .AsNoTracking()
            .OrderBy(c => c.AntibioticClassName)
            .Select(c => new SelectListItem
            {
                Value = c.AntibioticClassName,
                Text = c.AntibioticClassName
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem>> GetOrganismsAsync()
    {
        return await context.Organisms
            .AsNoTracking()
            .OrderBy(o => o.WHONETID)
            .Select(o => new SelectListItem
            {
                Value = o.WHONETID,
                Text = o.WHONETID
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem>> GetAntibioticsAsync()
    {
        return await context.Antibiotics
            .AsNoTracking()
            .OrderBy(a => a.AntibioticName)
            .Select(a => new SelectListItem
            {
                Value = a.AntibioticCode,
                Text = a.AntibioticName
            })
            .ToListAsync();
    }
}
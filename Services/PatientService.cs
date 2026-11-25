using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class PatientService(ApplicationDbContext context) : IPatientService
{
    private const string NotApplicableValue = "N/A";

    public async Task<IEnumerable<Patient>> GetAllAsync()
    {
        return await context.Patients
            .Include(p => p.Facility)
            .AsNoTracking()
            .OrderByDescending(p => p.EnrollmentDate)
            .ToListAsync();
    }

    public async Task<Patient?> GetByIdAsync(string patientID)
    {
        return await context.Patients
            .Include(p => p.Facility)
            .FirstOrDefaultAsync(p => p.PatientID == patientID);
    }

    public async Task<Patient?> GetDetailsByIdAsync(string patientID)
    {
        return await context.Patients
            .Include(p => p.Facility)
            .Include(p => p.Worksheets)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatientID == patientID);
    }

    public async Task<IReadOnlyList<string>> SearchPatientIdsAsync(
        string query,
        int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query
            .Trim()
            .ToUpperInvariant();

        return await context.Patients
            .AsNoTracking()
            .Where(p =>
                p.PatientID != null &&
                p.PatientID.Contains(normalizedQuery))
            .OrderBy(p => p.PatientID)
            .Take(maxResults)
            .Select(p => p.PatientID)
            .ToListAsync();
    }

    public async Task CreateAsync(Patient patient)
    {
        ApplyApplicabilityRules(patient);

        context.Patients.Add(patient);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Patient patient)
    {
        ApplyApplicabilityRules(patient);

        if (context.Entry(patient).State == EntityState.Detached)
        {
            context.Patients.Update(patient);
        }

        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(string patientID)
    {
        return await context.Patients
            .AnyAsync(p => p.PatientID == patientID);
    }

    // ============================================================
    // ENFORCE N/A VS NULL (UNENTERED) APPLICABILITY RULES
    // ============================================================

    public static void ApplyApplicabilityRules(Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);

        patient.PatientID = patient.PatientID?.Trim().ToUpperInvariant() ?? string.Empty;
        patient.AcceptReject = patient.AcceptReject?.Trim() ?? string.Empty;
        patient.FacilityID = patient.FacilityID?.Trim().ToUpperInvariant() ?? string.Empty;

        bool isRejected = string.Equals(
            patient.AcceptReject,
            "Rejected",
            StringComparison.OrdinalIgnoreCase);

        bool isIso = patient.IsISOCase;

        if (isRejected)
        {
            patient.Age = null;
            patient.DateOfBirth = null;
            patient.HouseholdSize = null;
            patient.PresentationDate = null;
            patient.InfectionOnset = null;
            patient.WeightKg = null;
            patient.TemperatureC = null;
            patient.BPSystolic = null;
            patient.BPDiastolic = null;
            patient.Pulse = null;
            patient.OxygenSaturation = null;
            patient.AntibioticDays = null;

            patient.AgeUnit = NotApplicableValue;
            patient.Sex = NotApplicableValue;
            patient.PresumptiveID = NotApplicableValue;
            patient.ResidenceVillage = NotApplicableValue;
            patient.ResidenceCounty = NotApplicableValue;
            patient.ResidenceSubCounty = NotApplicableValue;
            patient.ResidenceDescription = NotApplicableValue;
            patient.MILCIV = NotApplicableValue;
            patient.OccupationCategory = NotApplicableValue;

            patient.ChronicIllness = NotApplicableValue;
            patient.ChronicSkinCondition = NotApplicableValue;
            patient.BodyPiercings = NotApplicableValue;
            patient.AntacidUse = NotApplicableValue;
            patient.Immunosuppressed = NotApplicableValue;
            patient.HouseholdMemberInfection = NotApplicableValue;
            patient.HouseholdHospitalWorker = NotApplicableValue;
            patient.OutdoorInjury = NotApplicableValue;
            patient.Pregnant = NotApplicableValue;
            patient.PreviousInfection = NotApplicableValue;
            patient.PriorHospitalAdmission = NotApplicableValue;
            patient.LongTermFacility = NotApplicableValue;
            patient.InjectionDrugUse = NotApplicableValue;
            patient.NonInjectionDrugUse = NotApplicableValue;
            patient.Homeless = NotApplicableValue;
            patient.Imprisoned = NotApplicableValue;
            patient.NurseryChildcareBoarding = NotApplicableValue;
            patient.ContactSports = NotApplicableValue;
            patient.TravelOutsideCounty = NotApplicableValue;

            patient.ClinicalPresentation = NotApplicableValue;
            patient.SkinInfectionSymptoms = NotApplicableValue;
            patient.PatientType = NotApplicableValue;
            patient.HospitalLocation = NotApplicableValue;
            patient.HospitalWard = NotApplicableValue;
            patient.ICUAdmission = NotApplicableValue;
            patient.WardAdmission = NotApplicableValue;
            patient.Sedated = NotApplicableValue;
            patient.InvasiveDevice = NotApplicableValue;
            patient.InfectionPresentAtAdmission = NotApplicableValue;
            patient.CurrentAntibioticUse = NotApplicableValue;
            patient.AntibioticsUsed = NotApplicableValue;
            patient.SurgeryForInfection = NotApplicableValue;
            patient.HAICAI = NotApplicableValue;
            patient.InfectionSource = NotApplicableValue;
            patient.InfectionSite = NotApplicableValue;
            patient.ConsentSample = NotApplicableValue;
            return;
        }

        // Core demographics & identifiers
        patient.PresumptiveID = NormalizeField(patient.PresumptiveID, !isIso);
        patient.AgeUnit = NormalizeField(patient.AgeUnit, false);
        patient.Sex = NormalizeField(patient.Sex, false);

        // Community / residence (N/A for ISO cases)
        patient.ResidenceVillage = NormalizeField(patient.ResidenceVillage, isIso);
        patient.ResidenceCounty = NormalizeField(patient.ResidenceCounty, isIso);
        patient.ResidenceSubCounty = NormalizeField(patient.ResidenceSubCounty, isIso);
        patient.ResidenceDescription = NormalizeField(patient.ResidenceDescription, isIso);
        patient.MILCIV = NormalizeField(patient.MILCIV, isIso);
        patient.OccupationCategory = NormalizeField(patient.OccupationCategory, isIso);
        if (isIso)
        {
            patient.HouseholdSize = null;
        }

        // Pregnancy applicability: N/A if ISO, non-Female, Age < 12 years, or Age > 50 years
        double? ageYears = GetAgeInYears(patient.Age, patient.AgeUnit);
        bool isPregnancyNotApplicable =
            isIso ||
            (!string.IsNullOrWhiteSpace(patient.Sex) &&
             !string.Equals(patient.Sex.Trim(), "Female", StringComparison.OrdinalIgnoreCase)) ||
            (ageYears.HasValue && (ageYears.Value < 12.0 || ageYears.Value > 50.0));

        // Risk factors (N/A for ISO cases; NULL when unentered for active patients)
        patient.ChronicIllness = NormalizeField(patient.ChronicIllness, isIso);
        patient.ChronicSkinCondition = NormalizeField(patient.ChronicSkinCondition, isIso);
        patient.BodyPiercings = NormalizeField(patient.BodyPiercings, isIso);
        patient.AntacidUse = NormalizeField(patient.AntacidUse, isIso);
        patient.Immunosuppressed = NormalizeField(patient.Immunosuppressed, isIso);
        patient.HouseholdMemberInfection = NormalizeField(patient.HouseholdMemberInfection, isIso);
        patient.HouseholdHospitalWorker = NormalizeField(patient.HouseholdHospitalWorker, isIso);
        patient.OutdoorInjury = NormalizeField(patient.OutdoorInjury, isIso);
        patient.Pregnant = NormalizeField(patient.Pregnant, isPregnancyNotApplicable);
        patient.PreviousInfection = NormalizeField(patient.PreviousInfection, isIso);
        patient.PriorHospitalAdmission = NormalizeField(patient.PriorHospitalAdmission, isIso);
        patient.LongTermFacility = NormalizeField(patient.LongTermFacility, isIso);
        patient.InjectionDrugUse = NormalizeField(patient.InjectionDrugUse, isIso);
        patient.NonInjectionDrugUse = NormalizeField(patient.NonInjectionDrugUse, isIso);
        patient.Homeless = NormalizeField(patient.Homeless, isIso);
        patient.Imprisoned = NormalizeField(patient.Imprisoned, isIso);
        patient.NurseryChildcareBoarding = NormalizeField(patient.NurseryChildcareBoarding, isIso);
        patient.ContactSports = NormalizeField(patient.ContactSports, isIso);
        patient.TravelOutsideCounty = NormalizeField(patient.TravelOutsideCounty, isIso);

        // Clinical presentation & outpatient rules
        patient.ClinicalPresentation = NormalizeField(patient.ClinicalPresentation, false);

        var presentation = patient.ClinicalPresentation?.Trim();
        bool skinSymptomsNotApplicable =
            isIso ||
            (!string.IsNullOrWhiteSpace(presentation) &&
             !string.Equals(presentation, "Surgical Site Infection", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(presentation, "Skin and Soft Tissue Infection", StringComparison.OrdinalIgnoreCase));

        patient.SkinInfectionSymptoms = NormalizeField(patient.SkinInfectionSymptoms, skinSymptomsNotApplicable);
        patient.PatientType = NormalizeField(patient.PatientType, false);
        patient.HospitalLocation = NormalizeField(patient.HospitalLocation, false);

        bool isOutpatient = string.Equals(
            patient.PatientType?.Trim(),
            "Outpatient",
            StringComparison.OrdinalIgnoreCase);

        patient.HospitalWard = NormalizeField(patient.HospitalWard, isOutpatient);
        patient.ICUAdmission = NormalizeField(patient.ICUAdmission, isOutpatient);
        patient.WardAdmission = NormalizeField(patient.WardAdmission, isOutpatient);
        patient.InfectionPresentAtAdmission = NormalizeField(patient.InfectionPresentAtAdmission, isOutpatient);

        // Clinical assessment & vitals (N/A for ISO cases)
        patient.Sedated = NormalizeField(patient.Sedated, isIso);
        patient.InvasiveDevice = NormalizeField(patient.InvasiveDevice, isIso);

        if (isIso)
        {
            patient.WeightKg = null;
            patient.TemperatureC = null;
            patient.BPSystolic = null;
            patient.BPDiastolic = null;
            patient.Pulse = null;
            patient.OxygenSaturation = null;
        }

        // Antibiotic history
        patient.CurrentAntibioticUse = NormalizeField(patient.CurrentAntibioticUse, isIso);

        bool takingAntibiotics =
            !isIso &&
            string.Equals(patient.CurrentAntibioticUse?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase);

        bool antibioticsUsedNotApplicable =
            isIso ||
            (!string.IsNullOrWhiteSpace(patient.CurrentAntibioticUse) &&
             !string.Equals(patient.CurrentAntibioticUse.Trim(), "Yes", StringComparison.OrdinalIgnoreCase));

        if (!takingAntibiotics)
        {
            patient.AntibioticDays = null;
        }

        patient.AntibioticsUsed = NormalizeField(patient.AntibioticsUsed, antibioticsUsedNotApplicable);
        patient.SurgeryForInfection = NormalizeField(patient.SurgeryForInfection, isIso);
        patient.HAICAI = NormalizeField(patient.HAICAI, false);

        // Infection & consent
        patient.InfectionSource = NormalizeField(patient.InfectionSource, false);
        patient.InfectionSite = NormalizeField(patient.InfectionSite, isIso);
        patient.ConsentSample = isIso
            ? (string.IsNullOrWhiteSpace(patient.ConsentSample) ? "Unknown" : patient.ConsentSample.Trim())
            : NormalizeField(patient.ConsentSample, false);
    }

    private static string? NormalizeField(string? rawValue, bool isNotApplicable)
    {
        if (isNotApplicable)
        {
            return NotApplicableValue;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim();
        return string.Equals(trimmed, NotApplicableValue, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "Not Applicable", StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }

    private static double? GetAgeInYears(int? age, string? ageUnit)
    {
        if (!age.HasValue || age.Value < 0 || string.IsNullOrWhiteSpace(ageUnit))
        {
            return null;
        }

        return ageUnit.Trim().ToLowerInvariant() switch
        {
            "days" => age.Value / 365.25,
            "months" => age.Value / 12.0,
            _ => age.Value
        };
    }
}
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Models;
using AMRSurveillance.Models;
using AMRSurveillance.Services;
using AMRSurveillance.Services.Interfaces;
using ClosedXML.Excel;
using System.Text.RegularExpressions;

namespace AMRSurveillance.Imports.Validators;

public partial class PatientImportValidator(
    IImportMapper<Patient> mapper,
    IImportLookupService importLookupService,
    ILookupService lookupService,
    IFacilityService facilityService,
    IPatientService patientService) : IEntityImportValidator<Patient>
{
    private const string NotApplicableValue = "N/A";

    public async Task<ImportResult<Patient>> ValidateAsync(
        IXLWorksheet worksheet)
    {
        var result = new ImportResult<Patient>();

        var headerMap = BuildHeaderMap(worksheet);

        var seenPatientIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var lookupValues = LoadLookupValues();

        var counties = (await lookupService.GetCountiesAsync())
            .Select(x => x.Value?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var lastRow =
            worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (int rowNumber = 2;
             rowNumber <= lastRow;
             rowNumber++)
        {
            var row = worksheet.Row(rowNumber);

            if (IsEmptyRow(row))
            {
                continue;
            }

            var errors =
                new List<ImportRowError>();

            Patient patient;

            try
            {
                patient = mapper.Map(
                    worksheet,
                    row,
                    headerMap);
            }
            catch (Exception ex)
            {
                AddError(
                    errors,
                    rowNumber,
                    string.Empty,
                    $"Unable to read row: {ex.Message}");

                result.Errors.AddRange(errors);

                continue;
            }

            // =========================================================
            // PATIENT ID
            // =========================================================

            if (string.IsNullOrWhiteSpace(patient.PatientID))
            {
                AddError(
                    errors,
                    rowNumber,
                    "PatientID",
                    "Patient ID is required.");
            }
            else
            {
                patient.PatientID =
                    WhitespaceRegex().Replace(
                        patient.PatientID
                            .Trim()
                            .ToUpperInvariant(),
                        string.Empty);

                if (!IsValidPatientId(patient.PatientID))
                {
                    AddError(
                        errors,
                        rowNumber,
                        "PatientID",
                        "Patient ID must start with AMR or ISO and follow the format AMR/FFF/DDD[L] or ISO/FFF/DDD[L], for example AMR/BSA/001 or ISO/DFM/1234A.");
                }

                if (!seenPatientIds.Add(patient.PatientID))
                {
                    AddError(
                        errors,
                        rowNumber,
                        "PatientID",
                        "Duplicate Patient ID in the import file.");
                }

                if (await PatientExistsAsync(patient.PatientID))
                {
                    AddError(
                        errors,
                        rowNumber,
                        "PatientID",
                        "Patient ID already exists in the database.");
                }
            }

            // =========================================================
            // ACCEPT / REJECT
            // =========================================================

            ValidateRequiredLookup(
                errors,
                rowNumber,
                "AcceptReject",
                patient.AcceptReject,
                lookupValues,
                "AcceptReject",
                allowNotApplicable: false);

            // =========================================================
            // FACILITY
            // =========================================================

            if (string.IsNullOrWhiteSpace(patient.FacilityID))
            {
                AddError(
                    errors,
                    rowNumber,
                    "FacilityID",
                    "Facility is required.");
            }
            else
            {
                patient.FacilityID =
                    patient.FacilityID
                        .Trim()
                        .ToUpperInvariant();

                if (!await facilityService.ExistsAsync(
                    patient.FacilityID))
                {
                    AddError(
                        errors,
                        rowNumber,
                        "FacilityID",
                        "Facility does not exist.");
                }

                ValidatePatientFacilityMatch(
                    errors,
                    rowNumber,
                    patient.PatientID,
                    patient.FacilityID);
            }

            // =========================================================
            // REQUIRED DATES FOR ALL RECORDS
            // =========================================================

            ValidateDate(
                errors,
                rowNumber,
                "EnrollmentDate",
                patient.EnrollmentDate);

            ValidateDate(
                errors,
                rowNumber,
                "ReceivingDate",
                patient.ReceivingDate);

            if (!patient.EnrollmentDate.HasValue)
            {
                AddError(
                    errors,
                    rowNumber,
                    "EnrollmentDate",
                    "Enrollment Date is required.");
            }

            if (!patient.ReceivingDate.HasValue)
            {
                AddError(
                    errors,
                    rowNumber,
                    "ReceivingDate",
                    "Receiving Date is required.");
            }

            if (patient.EnrollmentDate.HasValue &&
                patient.ReceivingDate.HasValue &&
                patient.ReceivingDate.Value.Date <
                patient.EnrollmentDate.Value.Date)
            {
                AddError(
                    errors,
                    rowNumber,
                    "ReceivingDate",
                    "Receiving Date cannot be earlier than Enrollment Date.");
            }

            // =========================================================
            // REJECTED SAMPLE WORKFLOW
            // Mirrors PatientController & patient.js
            // =========================================================

            if (IsRejectedSample(patient))
            {
                PatientService.ApplyApplicabilityRules(patient);

                if (errors.Count > 0)
                {
                    result.Errors.AddRange(errors);
                }
                else
                {
                    result.Records.Add(patient);
                }

                continue;
            }

            // =========================================================
            // ISO RULES & PRESUMPTIVE ID
            // =========================================================

            ValidateIsoRules(
                errors,
                rowNumber,
                patient);

            // =========================================================
            // REMAINING DATE VALIDATION
            // =========================================================

            ValidateDate(
                errors,
                rowNumber,
                "DateOfBirth",
                patient.DateOfBirth);

            ValidateDate(
                errors,
                rowNumber,
                "PresentationDate",
                patient.PresentationDate);

            ValidateDate(
                errors,
                rowNumber,
                "InfectionOnset",
                patient.InfectionOnset);

            if (patient.EnrollmentDate.HasValue &&
                patient.DateOfBirth.HasValue &&
                patient.DateOfBirth.Value.Date >
                patient.EnrollmentDate.Value.Date)
            {
                AddError(
                    errors,
                    rowNumber,
                    "DateOfBirth",
                    "Date of Birth cannot be later than Enrollment Date.");
            }

            // =========================================================
            // AGE AND DATE OF BIRTH
            // =========================================================

            ValidateAgeRules(
                errors,
                rowNumber,
                patient,
                lookupValues);

            // =========================================================
            // SEX
            // =========================================================

            ValidateRequiredLookup(
                errors,
                rowNumber,
                "Sex",
                patient.Sex,
                lookupValues,
                "Sex",
                allowNotApplicable: false);

            // =========================================================
            // RESIDENCE
            // =========================================================

            await ValidateResidence(
                errors,
                rowNumber,
                patient,
                counties);

            ValidateLookup(
                errors,
                rowNumber,
                "ResidenceDescription",
                patient.ResidenceDescription,
                lookupValues,
                "ResidenceDescription",
                allowNotApplicable: patient.IsISOCase);

            // =========================================================
            // MILITARY / CIVILIAN AND OCCUPATION
            // =========================================================

            ValidateLookup(
                errors,
                rowNumber,
                "MILCIV",
                patient.MILCIV,
                lookupValues,
                "MilitaryCivilian",
                allowNotApplicable: patient.IsISOCase);

            ValidateLookup(
                errors,
                rowNumber,
                "OccupationCategory",
                patient.OccupationCategory,
                lookupValues,
                "OccupationCategory",
                allowNotApplicable: patient.IsISOCase);

            ValidateMilitaryOccupationRules(
                errors,
                rowNumber,
                patient);

            // =========================================================
            // HOUSEHOLD
            // =========================================================

            if (patient.HouseholdSize.HasValue &&
                patient.HouseholdSize < 1)
            {
                AddError(
                    errors,
                    rowNumber,
                    "HouseholdSize",
                    "Household Size must be at least 1.");
            }

            // =========================================================
            // RISK FACTORS
            // =========================================================

            bool isPregnancyNotApplicable = IsPregnancyNotApplicable(patient);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "ChronicIllness",
                patient.ChronicIllness,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "ChronicSkinCondition",
                patient.ChronicSkinCondition,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "BodyPiercings",
                patient.BodyPiercings,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "AntacidUse",
                patient.AntacidUse,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "Immunosuppressed",
                patient.Immunosuppressed,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "HouseholdMemberInfection",
                patient.HouseholdMemberInfection,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "HouseholdHospitalWorker",
                patient.HouseholdHospitalWorker,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "OutdoorInjury",
                patient.OutdoorInjury,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "Pregnant",
                patient.Pregnant,
                lookupValues,
                allowNotApplicable: isPregnancyNotApplicable);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "PreviousInfection",
                patient.PreviousInfection,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "PriorHospitalAdmission",
                patient.PriorHospitalAdmission,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "LongTermFacility",
                patient.LongTermFacility,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "InjectionDrugUse",
                patient.InjectionDrugUse,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "NonInjectionDrugUse",
                patient.NonInjectionDrugUse,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "Homeless",
                patient.Homeless,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "Imprisoned",
                patient.Imprisoned,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "NurseryChildcareBoarding",
                patient.NurseryChildcareBoarding,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "ContactSports",
                patient.ContactSports,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "TravelOutsideCounty",
                patient.TravelOutsideCounty,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidatePregnancyRules(
                errors,
                rowNumber,
                patient);

            // =========================================================
            // CLINICAL PRESENTATION & SKIN INFECTION SYMPTOMS
            // =========================================================

            ValidateRequiredLookup(
                errors,
                rowNumber,
                "ClinicalPresentation",
                patient.ClinicalPresentation,
                lookupValues,
                "ClinicalPresentation",
                allowNotApplicable: false);

            bool skinSymptomsApplicable = IsSkinSymptomsApplicable(patient);

            ValidateMultipleLookup(
                errors,
                rowNumber,
                "SkinInfectionSymptoms",
                patient.SkinInfectionSymptoms,
                lookupValues,
                "SkinInfectionSymptoms",
                allowNotApplicable: !skinSymptomsApplicable);

            ValidateSkinInfectionSymptomsRules(
                errors,
                rowNumber,
                patient);

            ValidateRequiredLookup(
                errors,
                rowNumber,
                "PatientType",
                patient.PatientType,
                lookupValues,
                "PatientType",
                allowNotApplicable: false);

            if (!patient.PresentationDate.HasValue)
            {
                AddError(
                    errors,
                    rowNumber,
                    "PresentationDate",
                    "Presentation Date is required.");
            }

            // =========================================================
            // HOSPITAL INFORMATION
            // =========================================================

            bool isOutpatient = IsOutpatient(patient);

            ValidateLookup(
                errors,
                rowNumber,
                "HospitalLocation",
                patient.HospitalLocation,
                lookupValues,
                "HospitalLocation",
                allowNotApplicable: false);

            ValidateLookup(
                errors,
                rowNumber,
                "ICUAdmission",
                patient.ICUAdmission,
                lookupValues,
                "YesNo",
                allowNotApplicable: isOutpatient);

            ValidateLookup(
                errors,
                rowNumber,
                "WardAdmission",
                patient.WardAdmission,
                lookupValues,
                "YesNo",
                allowNotApplicable: isOutpatient);

            ValidatePatientTypeRules(
                errors,
                rowNumber,
                patient);

            // =========================================================
            // CLINICAL ASSESSMENT
            // =========================================================

            ValidateRiskFactor(
                errors,
                rowNumber,
                "Sedated",
                patient.Sedated,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "InvasiveDevice",
                patient.InvasiveDevice,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateRiskFactor(
                errors,
                rowNumber,
                "InfectionPresentAtAdmission",
                patient.InfectionPresentAtAdmission,
                lookupValues,
                allowNotApplicable: isOutpatient);

            ValidateInfectionOnsetRules(
                errors,
                rowNumber,
                patient);

            // =========================================================
            // CLINICAL MEASUREMENTS
            // =========================================================

            ValidateClinicalMeasurements(
                errors,
                rowNumber,
                patient);

            // =========================================================
            // ANTIBIOTICS
            // =========================================================

            if (!patient.IsISOCase)
            {
                ValidateRiskFactor(
                    errors,
                    rowNumber,
                    "CurrentAntibioticUse",
                    patient.CurrentAntibioticUse,
                    lookupValues,
                    allowNotApplicable: false);

                ValidateAntibioticRules(
                    errors,
                    rowNumber,
                    patient);
            }

            ValidateRiskFactor(
                errors,
                rowNumber,
                "SurgeryForInfection",
                patient.SurgeryForInfection,
                lookupValues,
                allowNotApplicable: patient.IsISOCase);

            ValidateLookup(
                errors,
                rowNumber,
                "HAICAI",
                patient.HAICAI,
                lookupValues,
                "HAICAI",
                allowNotApplicable: false);

            ValidateLookup(
                errors,
                rowNumber,
                "InfectionSource",
                patient.InfectionSource,
                lookupValues,
                "InfectionSource",
                allowNotApplicable: false);

            if (!patient.IsISOCase)
            {
                ValidateLookup(
                    errors,
                    rowNumber,
                    "InfectionSite",
                    patient.InfectionSite,
                    lookupValues,
                    "InfectionSite",
                    allowNotApplicable: false);
            }

            // =========================================================
            // CONSENT
            // =========================================================

            if (!patient.IsISOCase)
            {
                ValidateRequiredLookup(
                    errors,
                    rowNumber,
                    "ConsentSample",
                    patient.ConsentSample,
                    lookupValues,
                    "YesNo",
                    allowNotApplicable: false);
            }

            // =========================================================
            // STORE VALID RECORD OR ERRORS
            // =========================================================

            if (errors.Count > 0)
            {
                result.Errors.AddRange(errors);
            }
            else
            {
                PatientService.ApplyApplicabilityRules(patient);
                result.Records.Add(patient);
            }
        }

        return result;
    }

    // ================================================================
    // HEADER MAP
    // ================================================================

    private static Dictionary<string, int> BuildHeaderMap(
        IXLWorksheet worksheet)
    {
        var map =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        var lastColumn =
            worksheet.Row(1)
                .LastCellUsed()?
                .Address.ColumnNumber ?? 0;

        for (int column = 1;
             column <= lastColumn;
             column++)
        {
            var header =
                worksheet.Cell(1, column)
                    .GetString()
                    .Trim();

            if (!string.IsNullOrWhiteSpace(header))
            {
                map[header] = column;
            }
        }

        return map;
    }

    // ================================================================
    // LOOKUP LOADING
    // ================================================================

    private Dictionary<string, HashSet<string>> LoadLookupValues()
    {
        var lookupNames = new[]
        {
            "AcceptReject",
            "AgeUnit",
            "YesNo",
            "Sex",
            "MilitaryCivilian",
            "PatientType",
            "ResidenceDescription",
            "ClinicalPresentation",
            "HospitalLocation",
            "InfectionSource",
            "InfectionSite",
            "SkinInfectionSymptoms",
            "OccupationCategory",
            "HAICAI"
        };

        var values =
            new Dictionary<string, HashSet<string>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var lookupName in lookupNames)
        {
            values[lookupName] =
                importLookupService
                    .GetValues(lookupName)
                    .Select(x => x.Value?.Trim())
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);
        }

        return values;
    }

    // ================================================================
    // PATIENT EXISTENCE
    // ================================================================

    private async Task<bool> PatientExistsAsync(
        string patientID)
    {
        return await patientService.ExistsAsync(
            patientID);
    }

    // ================================================================
    // PATIENT ID & HELPERS
    // ================================================================

    private static bool IsValidPatientId(
        string patientID)
    {
        return PatientIdRegex().IsMatch(patientID);
    }

    private static bool IsMissingOrNotApplicable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return string.Equals(trimmed, NotApplicableValue, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "Not Applicable", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNotApplicableToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return string.Equals(trimmed, NotApplicableValue, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "Not Applicable", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOutpatient(Patient patient)
    {
        return string.Equals(
            patient.PatientType?.Trim(),
            "Outpatient",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSkinSymptomsApplicable(Patient patient)
    {
        if (patient.IsISOCase)
        {
            return false;
        }

        var presentation = patient.ClinicalPresentation?.Trim();
        return string.Equals(
                   presentation,
                   "Surgical Site Infection",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   presentation,
                   "Skin and Soft Tissue Infection",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPregnancyNotApplicable(Patient patient)
    {
        if (patient.IsISOCase)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(patient.Sex) &&
            !string.Equals(patient.Sex.Trim(), "Female", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (patient.Age.HasValue && patient.Age.Value >= 0 && !string.IsNullOrWhiteSpace(patient.AgeUnit))
        {
            double ageYears = patient.AgeUnit.Trim().ToLowerInvariant() switch
            {
                "days" => patient.Age.Value / 365.25,
                "months" => patient.Age.Value / 12.0,
                _ => patient.Age.Value
            };

            if (ageYears < 12.0 || ageYears > 50.0)
            {
                return true;
            }
        }

        return false;
    }

    // ================================================================
    // FACILITY MATCHING
    // ================================================================

    private static void ValidatePatientFacilityMatch(
        List<ImportRowError> errors,
        int rowNumber,
        string patientID,
        string facilityID)
    {
        if (string.IsNullOrWhiteSpace(patientID) ||
            string.IsNullOrWhiteSpace(facilityID))
        {
            return;
        }

        var parts =
            patientID.Split('/');

        if (parts.Length != 3)
        {
            return;
        }

        var patientFacility =
            parts[1].Trim();

        if (!patientFacility.Equals(
            facilityID.Trim(),
            StringComparison.OrdinalIgnoreCase))
        {
            AddError(
                errors,
                rowNumber,
                "FacilityID",
                $"The facility code in the Patient ID ({patientFacility}) does not match the selected Facility ID ({facilityID}).");
        }
    }

    // ================================================================
    // REJECTED SAMPLE
    // ================================================================

    private static bool IsRejectedSample(
        Patient patient)
    {
        return string.Equals(
            patient.AcceptReject?.Trim(),
            "Rejected",
            StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // ISO VALIDATION
    // ================================================================

    private static void ValidateIsoRules(
        List<ImportRowError> errors,
        int rowNumber,
        Patient patient)
    {
        if (!patient.IsISOCase)
        {
            if (!IsMissingOrNotApplicable(patient.PresumptiveID))
            {
                AddError(
                    errors,
                    rowNumber,
                    "PresumptiveID",
                    "Presumptive ID should only be provided for ISO patients.");
            }

            patient.PresumptiveID = NotApplicableValue;
            return;
        }

        if (IsMissingOrNotApplicable(
            patient.PresumptiveID))
        {
            AddError(
                errors,
                rowNumber,
                "PresumptiveID",
                "Presumptive ID is required for ISO cases.");
        }
        else
        {
            patient.PresumptiveID =
                patient.PresumptiveID!.Trim();
        }

        // Community / Residence
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "ResidenceVillage", patient.ResidenceVillage);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "ResidenceCounty", patient.ResidenceCounty);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "ResidenceSubCounty", patient.ResidenceSubCounty);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "ResidenceDescription", patient.ResidenceDescription);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "MILCIV", patient.MILCIV);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "OccupationCategory", patient.OccupationCategory);
        ValidateIsoFieldBlank(errors, rowNumber, "HouseholdSize", patient.HouseholdSize);

        // Risk Factors (all 19 are N/A for ISO cases)
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "ChronicIllness", patient.ChronicIllness);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "ChronicSkinCondition", patient.ChronicSkinCondition);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "BodyPiercings", patient.BodyPiercings);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "AntacidUse", patient.AntacidUse);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "Immunosuppressed", patient.Immunosuppressed);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "HouseholdMemberInfection", patient.HouseholdMemberInfection);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "HouseholdHospitalWorker", patient.HouseholdHospitalWorker);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "OutdoorInjury", patient.OutdoorInjury);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "Pregnant", patient.Pregnant);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "PreviousInfection", patient.PreviousInfection);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "PriorHospitalAdmission", patient.PriorHospitalAdmission);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "LongTermFacility", patient.LongTermFacility);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "InjectionDrugUse", patient.InjectionDrugUse);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "NonInjectionDrugUse", patient.NonInjectionDrugUse);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "Homeless", patient.Homeless);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "Imprisoned", patient.Imprisoned);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "NurseryChildcareBoarding", patient.NurseryChildcareBoarding);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "ContactSports", patient.ContactSports);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "TravelOutsideCounty", patient.TravelOutsideCounty);

        // Clinical Assessment
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "Sedated", patient.Sedated);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "InvasiveDevice", patient.InvasiveDevice);

        // Vital Signs
        ValidateIsoFieldBlank(errors, rowNumber, "WeightKg", patient.WeightKg);
        ValidateIsoFieldBlank(errors, rowNumber, "TemperatureC", patient.TemperatureC);
        ValidateIsoFieldBlank(errors, rowNumber, "OxygenSaturation", patient.OxygenSaturation);
        ValidateIsoFieldBlank(errors, rowNumber, "BPSystolic", patient.BPSystolic);
        ValidateIsoFieldBlank(errors, rowNumber, "BPDiastolic", patient.BPDiastolic);
        ValidateIsoFieldBlank(errors, rowNumber, "Pulse", patient.Pulse);

        // Antibiotic History
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "CurrentAntibioticUse", patient.CurrentAntibioticUse);
        ValidateIsoFieldBlank(errors, rowNumber, "AntibioticDays", patient.AntibioticDays);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "AntibioticsUsed", patient.AntibioticsUsed);
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "SurgeryForInfection", patient.SurgeryForInfection);

        // Infection
        ValidateIsoFieldBlankOrNA(errors, rowNumber, "InfectionSite", patient.InfectionSite);

        // Consent mirrors updateISOFieldRules() in patient.js
        patient.ConsentSample = "Unknown";
    }

    private static void ValidateIsoFieldBlankOrNA(
        List<ImportRowError> errors,
        int rowNumber,
        string columnName,
        string? value)
    {
        if (!IsMissingOrNotApplicable(value))
        {
            AddError(
                errors,
                rowNumber,
                columnName,
                $"{columnName} must be blank or N/A for ISO cases.");
        }
    }

    private static void ValidateIsoFieldBlank(
        List<ImportRowError> errors,
        int rowNumber,
        string columnName,
        object? value)
    {
        if (value != null)
        {
            AddError(
                errors,
                rowNumber,
                columnName,
                $"{columnName} must be blank for ISO cases.");
        }
    }

    // ================================================================
    // AGE VALIDATION
    // ================================================================

    private static void ValidateAgeRules(
        List<ImportRowError> errors,
        int rowNumber,
        Patient patient,
        Dictionary<string, HashSet<string>> lookupValues)
    {
        if (!patient.Age.HasValue)
        {
            AddError(
                errors,
                rowNumber,
                "Age",
                "Age is required.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
            patient.AgeUnit))
        {
            AddError(
                errors,
                rowNumber,
                "AgeUnit",
                "Please specify whether the age is in days, months, or years.");

            return;
        }

        ValidateLookup(
            errors,
            rowNumber,
            "AgeUnit",
            patient.AgeUnit,
            lookupValues,
            "AgeUnit",
            allowNotApplicable: false);

        var ageUnit =
            patient.AgeUnit
                .Trim()
                .ToLowerInvariant();

        if (ageUnit == "days")
        {
            if (patient.Age.Value < 0 ||
                patient.Age.Value > 43830)
            {
                AddError(
                    errors,
                    rowNumber,
                    "Age",
                    "Age in days must be non-negative.");
            }
        }
        else if (ageUnit == "years")
        {
            if (patient.Age.Value < 0 ||
                patient.Age.Value > 120)
            {
                AddError(
                    errors,
                    rowNumber,
                    "Age",
                    "Age must be between 0 and 120 years.");
            }
        }
        else if (ageUnit == "months")
        {
            if (patient.Age.Value < 0 ||
                patient.Age.Value > 1440)
            {
                AddError(
                    errors,
                    rowNumber,
                    "Age",
                    "Age must be between 0 and 1,440 months.");
            }
        }
        else
        {
            return;
        }

        if (!patient.DateOfBirth.HasValue ||
            !patient.EnrollmentDate.HasValue)
        {
            return;
        }

        var calculatedAge =
            CalculateAge(
                patient.DateOfBirth.Value,
                patient.EnrollmentDate.Value,
                ageUnit);

        if (!calculatedAge.HasValue)
        {
            return;
        }

        if (patient.Age.Value !=
            calculatedAge.Value)
        {
            var unitText = ageUnit switch
            {
                "days" => "day(s)",
                "months" => "month(s)",
                _ => "year(s)"
            };

            AddError(
                errors,
                rowNumber,
                "Age",
                $"Age does not match the Date of Birth and Enrollment Date. " +
                $"The calculated age is {calculatedAge.Value} {unitText}.");
        }
    }

    private static int? CalculateAge(
        DateTime dateOfBirth,
        DateTime enrollmentDate,
        string ageUnit)
    {
        if (dateOfBirth.Date >
            enrollmentDate.Date)
        {
            return null;
        }

        if (ageUnit == "days")
        {
            return (enrollmentDate.Date - dateOfBirth.Date).Days;
        }

        var totalMonths =
            ((enrollmentDate.Year - dateOfBirth.Year) * 12) +
            enrollmentDate.Month -
            dateOfBirth.Month;

        if (enrollmentDate.Day <
            dateOfBirth.Day)
        {
            totalMonths--;
        }

        totalMonths = Math.Max(0, totalMonths);

        if (ageUnit == "months")
        {
            return totalMonths;
        }

        if (ageUnit == "years")
        {
            return totalMonths / 12;
        }

        return null;
    }

    // ================================================================
    // RESIDENCE
    // ================================================================

    private async Task ValidateResidence(
        List<ImportRowError> errors,
        int rowNumber,
        Patient patient,
        HashSet<string> counties)
    {
        if (patient.IsISOCase)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
            patient.ResidenceCounty))
        {
            AddError(
                errors,
                rowNumber,
                "ResidenceCounty",
                "Residence County is required.");
        }
        else if (!counties.Contains(
            patient.ResidenceCounty.Trim()))
        {
            AddError(
                errors,
                rowNumber,
                "ResidenceCounty",
                "Residence County is not valid.");
        }

        if (!string.IsNullOrWhiteSpace(
            patient.ResidenceSubCounty))
        {
            if (string.IsNullOrWhiteSpace(
                patient.ResidenceCounty))
            {
                AddError(
                    errors,
                    rowNumber,
                    "ResidenceSubCounty",
                    "Residence County must be provided when Residence Sub-County is provided.");
            }
            else
            {
                var subCounties =
                    await lookupService
                        .GetSubCountiesAsync(
                            patient.ResidenceCounty.Trim());

                var validSubCounties =
                    subCounties
                        .Select(x => x.Value?.Trim())
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!)
                        .ToHashSet(
                            StringComparer.OrdinalIgnoreCase);

                if (!validSubCounties.Contains(
                    patient.ResidenceSubCounty.Trim()))
                {
                    AddError(
                        errors,
                        rowNumber,
                        "ResidenceSubCounty",
                        "Residence Sub-County is not valid for the selected Residence County.");
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(
            patient.ResidenceCounty))
        {
            AddError(
                errors,
                rowNumber,
                "ResidenceSubCounty",
                "Select a Sub-County.");
        }
    }

    // ================================================================
    // MILITARY / OCCUPATION
    // ================================================================

    private static void ValidateMilitaryOccupationRules(
        List<ImportRowError> errors,
        int rowNumber,
        Patient patient)
    {
        if (patient.IsISOCase)
        {
            return;
        }

        var militaryStatus =
            patient.MILCIV?
                .Trim()
                .ToLowerInvariant();

        var occupation =
            patient.OccupationCategory?
                .Trim()
                .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(
            militaryStatus) ||
            string.IsNullOrWhiteSpace(
                occupation))
        {
            return;
        }

        if (militaryStatus == "military" &&
            occupation != "soldier")
        {
            AddError(
                errors,
                rowNumber,
                "OccupationCategory",
                "Occupation Category must be Soldier when Military / Civilian is Military.");
        }

        if (occupation == "soldier" &&
            militaryStatus != "military")
        {
            AddError(
                errors,
                rowNumber,
                "MILCIV",
                "Military / Civilian must be Military when Occupation Category is Soldier.");
        }
    }

    // ================================================================
    // PREGNANCY
    // ================================================================

    private static void ValidatePregnancyRules(
        List<ImportRowError> errors,
        int rowNumber,
        Patient patient)
    {
        if (IsMissingOrNotApplicable(patient.Pregnant))
        {
            return;
        }

        var pregnant =
            patient.Pregnant!
                .Trim()
                .ToLowerInvariant();

        if (pregnant != "yes")
        {
            return;
        }

        var sex =
            patient.Sex?
                .Trim()
                .ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(sex) &&
            sex != "female")
        {
            AddError(
                errors,
                rowNumber,
                "Pregnant",
                "Pregnancy status cannot be Yes for non-female participants.");
            return;
        }

        if (patient.Age.HasValue &&
            patient.Age.Value >= 0 &&
            !string.IsNullOrWhiteSpace(patient.AgeUnit))
        {
            double ageYears = patient.AgeUnit.Trim().ToLowerInvariant() switch
            {
                "days" => patient.Age.Value / 365.25,
                "months" => patient.Age.Value / 12.0,
                _ => patient.Age.Value
            };

            if (ageYears < 12.0 || ageYears > 50.0)
            {
                AddError(
                    errors,
                    rowNumber,
                    "Pregnant",
                    "Pregnancy status is only applicable to female participants aged 12 to 50 years.");
            }
        }
    }

    // ================================================================
    // SKIN INFECTION SYMPTOMS APPLICABILITY
    // Mirrors PatientController & patient.js
    // ================================================================

    private static void ValidateSkinInfectionSymptomsRules(
        List<ImportRowError> errors,
        int rowNumber,
        Patient patient)
    {
        if (!IsSkinSymptomsApplicable(patient) &&
            !IsMissingOrNotApplicable(
                patient.SkinInfectionSymptoms))
        {
            AddError(
                errors,
                rowNumber,
                "SkinInfectionSymptoms",
                "Skin infection symptoms can only be recorded for Surgical Site Infection or Skin and Soft Tissue Infection.");
        }
    }

    // ================================================================
    // PATIENT TYPE / HOSPITAL RULES
    // Mirrors PatientController & patient.js
    // ================================================================

    private static void ValidatePatientTypeRules(
        List<ImportRowError> errors,
        int rowNumber,
        Patient patient)
    {
        if (string.IsNullOrWhiteSpace(
            patient.PatientType))
        {
            return;
        }

        var patientType =
            patient.PatientType
                .Trim()
                .ToLowerInvariant();

        var hospitalLocation =
            patient.HospitalLocation?
                .Trim()
                .ToLowerInvariant();

        var hasWard =
            !IsMissingOrNotApplicable(
                patient.HospitalWard);

        var hasIcuAdmission =
            !IsMissingOrNotApplicable(
                patient.ICUAdmission);

        var hasWardAdmission =
            !IsMissingOrNotApplicable(
                patient.WardAdmission);

        var hasInfectionAtAdmission =
            !IsMissingOrNotApplicable(
                patient.InfectionPresentAtAdmission);

        if (patientType == "inpatient")
        {
            if (string.IsNullOrWhiteSpace(
                patient.HospitalLocation))
            {
                AddError(
                    errors,
                    rowNumber,
                    "HospitalLocation",
                    "Hospital Location is required for an inpatient.");
            }
            else if (hospitalLocation == "opd")
            {
                AddError(
                    errors,
                    rowNumber,
                    "HospitalLocation",
                    "Hospital location cannot be OPD for an inpatient.");
            }

            if (!patient.IsISOCase && !hasWard)
            {
                AddError(
                    errors,
                    rowNumber,
                    "HospitalWard",
                    "Hospital Ward is required for an inpatient.");
            }
        }
        else if (patientType == "outpatient")
        {
            if (hospitalLocation != "opd")
            {
                AddError(
                    errors,
                    rowNumber,
                    "HospitalLocation",
                    "Hospital location must be OPD for an outpatient.");
            }

            if (hasWard)
            {
                AddError(
                    errors,
                    rowNumber,
                    "HospitalWard",
                    "Hospital ward should not be recorded for an outpatient.");
            }

            if (hasIcuAdmission)
            {
                AddError(
                    errors,
                    rowNumber,
                    "ICUAdmission",
                    "ICU admission should not be recorded for an outpatient.");
            }

            if (hasWardAdmission)
            {
                AddError(
                    errors,
                    rowNumber,
                    "WardAdmission",
                    "Ward admission should not be recorded for an outpatient.");
            }

            if (hasInfectionAtAdmission)
            {
                AddError(
                    errors,
                    rowNumber,
                    "InfectionPresentAtAdmission",
                    "Infection present at admission should not be recorded for an outpatient.");
            }
        }
    }

    // ================================================================
    // INFECTION ONSET
    // ================================================================

    private static void ValidateInfectionOnsetRules(
        List<ImportRowError> errors,
        int rowNumber,
        Patient patient)
    {
        if (!patient.InfectionOnset.HasValue)
        {
            return;
        }

        if (!patient.PresentationDate.HasValue)
        {
            return;
        }

        if (IsMissingOrNotApplicable(
            patient.InfectionPresentAtAdmission))
        {
            return;
        }

        var admissionStatus =
            patient.InfectionPresentAtAdmission!
                .Trim()
                .ToLowerInvariant();

        var onset =
            patient.InfectionOnset.Value.Date;

        var presentation =
            patient.PresentationDate.Value.Date;

        if (admissionStatus == "yes" &&
            onset > presentation)
        {
            AddError(
                errors,
                rowNumber,
                "InfectionOnset",
                "Infection onset must be on or before the Presentation Date when infection was present at admission.");
        }

        if (admissionStatus == "no" &&
            onset < presentation)
        {
            AddError(
                errors,
                rowNumber,
                "InfectionOnset",
                "Infection onset must be on or after the Presentation Date when infection was not present at admission.");
        }
    }

    // ================================================================
    // CLINICAL MEASUREMENTS
    // ================================================================

    private static void ValidateClinicalMeasurements(
        List<ImportRowError> errors,
        int rowNumber,
        Patient patient)
    {
        if (patient.WeightKg.HasValue &&
            (patient.WeightKg < 0.1m ||
             patient.WeightKg > 300m))
        {
            AddError(
                errors,
                rowNumber,
                "WeightKg",
                "Weight must be between 0.1 and 300 kg.");
        }

        if (patient.TemperatureC.HasValue &&
            (patient.TemperatureC < 30m ||
             patient.TemperatureC > 45m))
        {
            AddError(
                errors,
                rowNumber,
                "TemperatureC",
                "Temperature must be between 30 and 45 °C.");
        }

        if (patient.BPSystolic.HasValue &&
            patient.BPSystolic <= 0)
        {
            AddError(
                errors,
                rowNumber,
                "BPSystolic",
                "BP Systolic must be greater than 0.");
        }

        if (patient.BPDiastolic.HasValue &&
            patient.BPDiastolic <= 0)
        {
            AddError(
                errors,
                rowNumber,
                "BPDiastolic",
                "BP Diastolic must be greater than 0.");
        }

        if (patient.Pulse.HasValue &&
            (patient.Pulse < 0 ||
             patient.Pulse > 300))
        {
            AddError(
                errors,
                rowNumber,
                "Pulse",
                "Pulse must be between 0 and 300 beats per minute.");
        }

        if (patient.OxygenSaturation.HasValue &&
            (patient.OxygenSaturation < 0m ||
             patient.OxygenSaturation > 100m))
        {
            AddError(
                errors,
                rowNumber,
                "OxygenSaturation",
                "Oxygen Saturation must be between 0 and 100.");
        }
    }

    // ================================================================
    // ANTIBIOTICS
    // ================================================================

    private static void ValidateAntibioticRules(
        List<ImportRowError> errors,
        int rowNumber,
        Patient patient)
    {
        var currentlyUsing =
            patient.CurrentAntibioticUse?
                .Trim()
                .ToLowerInvariant();

        if (currentlyUsing == "yes")
        {
            if (!patient.AntibioticDays.HasValue)
            {
                AddError(
                    errors,
                    rowNumber,
                    "AntibioticDays",
                    "Enter the number of days on antibiotics.");
            }
            else if (patient.AntibioticDays < 0)
            {
                AddError(
                    errors,
                    rowNumber,
                    "AntibioticDays",
                    "Antibiotic Days cannot be negative.");
            }

            patient.AntibioticsUsed =
                patient.AntibioticsUsed?.Trim();
        }
        else
        {
            if (patient.AntibioticDays.HasValue)
            {
                AddError(
                    errors,
                    rowNumber,
                    "AntibioticDays",
                    "Antibiotic Days should be blank when the patient is not currently receiving antibiotics.");
            }

            if (!IsMissingOrNotApplicable(
                patient.AntibioticsUsed))
            {
                AddError(
                    errors,
                    rowNumber,
                    "AntibioticsUsed",
                    "Antibiotics Used should be blank or N/A when the patient is not currently receiving antibiotics.");
            }
        }
    }

    // ================================================================
    // LOOKUP VALIDATION
    // ================================================================

    private static void ValidateRequiredLookup(
        List<ImportRowError> errors,
        int rowNumber,
        string columnName,
        string? value,
        Dictionary<string, HashSet<string>> lookupValues,
        string lookupName,
        bool allowNotApplicable = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(
                errors,
                rowNumber,
                columnName,
                $"{columnName} is required.");

            return;
        }

        ValidateLookup(
            errors,
            rowNumber,
            columnName,
            value,
            lookupValues,
            lookupName,
            allowNotApplicable);
    }

    private static void ValidateLookup(
        List<ImportRowError> errors,
        int rowNumber,
        string columnName,
        string? value,
        Dictionary<string, HashSet<string>> lookupValues,
        string lookupName,
        bool allowNotApplicable = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (allowNotApplicable && IsNotApplicableToken(value))
        {
            return;
        }

        if (!lookupValues.TryGetValue(
            lookupName,
            out var validValues))
        {
            return;
        }

        if (!validValues.Contains(value.Trim()))
        {
            AddError(
                errors,
                rowNumber,
                columnName,
                $"Invalid value '{value}'.");
        }
    }

    private static void ValidateRiskFactor(
        List<ImportRowError> errors,
        int rowNumber,
        string columnName,
        string? value,
        Dictionary<string, HashSet<string>> lookupValues,
        bool allowNotApplicable = false)
    {
        ValidateLookup(
            errors,
            rowNumber,
            columnName,
            value,
            lookupValues,
            "YesNo",
            allowNotApplicable);
    }

    private static void ValidateMultipleLookup(
        List<ImportRowError> errors,
        int rowNumber,
        string columnName,
        string? value,
        Dictionary<string, HashSet<string>> lookupValues,
        string lookupName,
        bool allowNotApplicable = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (allowNotApplicable && IsNotApplicableToken(value))
        {
            return;
        }

        var values =
            value.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (!lookupValues.TryGetValue(
            lookupName,
            out var validValues))
        {
            return;
        }

        foreach (var item in values)
        {
            if (!validValues.Contains(item))
            {
                AddError(
                    errors,
                    rowNumber,
                    columnName,
                    $"Invalid value '{item}'.");
            }
        }
    }

    // ================================================================
    // DATE VALIDATION
    // ================================================================

    private static void ValidateDate(
        List<ImportRowError> errors,
        int rowNumber,
        string columnName,
        DateTime? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        if (value.Value.Date >
            DateTime.Today)
        {
            AddError(
                errors,
                rowNumber,
                columnName,
                $"{columnName} cannot be a future date.");
        }
    }

    // ================================================================
    // EMPTY ROW
    // ================================================================

    private static bool IsEmptyRow(
        IXLRow row)
    {
        var lastCell =
            row.LastCellUsed();

        if (lastCell == null)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(
            row.CellsUsed()
                .Select(c => c.GetString())
                .FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x)));
    }

    // ================================================================
    // ERROR HELPER
    // ================================================================

    private static void AddError(
        List<ImportRowError> errors,
        int rowNumber,
        string columnName,
        string message)
    {
        errors.Add(
            new ImportRowError
            {
                RowNumber = rowNumber,
                ColumnName = columnName,
                Message = message
            });
    }

    [GeneratedRegex(@"^(?:AMR|ISO)/[A-Z]{3}/\d{3,4}[A-Z]?$")]
    private static partial Regex PatientIdRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
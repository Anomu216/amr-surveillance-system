using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Models;
using AMRSurveillance.Imports.Templates;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace AMRSurveillance.Controllers;

[Authorize(Roles = AppRoles.SystemAdministrator + "," + AppRoles.DataEntryStaff)]
public partial class PatientController(
    IPatientService patientService,
    ILookupService lookupService,
    IImportWorkflow<Patient> importWorkflow) : Controller
{
    private const string PatientImportSessionKey =
        "PatientImportRecords";

    private const string NotApplicableValue = "N/A";

    // ============================================================
    // INDEX
    // ============================================================

    public async Task<IActionResult> Index()
    {
        var patients = await patientService.GetAllAsync();

        return View(patients);
    }

    // ============================================================
    // DETAILS
    // ============================================================

    [HttpGet("Patient/Details/{**id}")]
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        id = Uri.UnescapeDataString(id).Trim();

        var patient = await patientService
            .GetDetailsByIdAsync(id);

        if (patient == null)
        {
            return NotFound();
        }

        return View(patient);
    }

    // ============================================================
    // CREATE - GET
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new PatientViewModel
        {
            EnrollmentDate = DateTime.Today,
            ReceivingDate = DateTime.Today,
            PresentationDate = DateTime.Today
        };

        await PopulateLookups(model);

        return View(model);
    }

    // ============================================================
    // POPULATE LOOKUPS
    // ============================================================

    private async Task PopulateLookups(PatientViewModel model)
    {
        model.FacilityList = await lookupService
            .GetFacilitiesAsync();

        model.CountyList = await lookupService
            .GetCountiesAsync();

        model.SubCountyList = string.IsNullOrWhiteSpace(model.ResidenceCounty)
            ? []
            : await lookupService.GetSubCountiesAsync(model.ResidenceCounty);

        model.AcceptRejectList = lookupService
            .GetAcceptReject();

        model.YesNoList = lookupService
            .GetYesNo();

        model.SexList = lookupService
            .GetSex();

        model.AgeUnitList = lookupService
            .GetAgeUnits();

        model.MilitaryCivilianList = lookupService
            .GetMilitaryCivilian();

        model.ResidenceDescriptionList = lookupService
            .GetResidenceDescriptions();

        model.OccupationCategoryList = lookupService
            .GetOccupationCategories();

        model.ClinicalPresentationList = lookupService
            .GetClinicalPresentations();

        model.PatientTypeList = lookupService
            .GetPatientTypes();

        model.HospitalLocationList = lookupService
            .GetHospitalLocations();

        model.InfectionSourceList = lookupService
            .GetInfectionSources();

        model.InfectionSiteList = lookupService
            .GetInfectionSites();

        model.SkinInfectionSymptomsList = lookupService
            .GetSkinInfectionSymptoms();

        model.HAICAIList = lookupService
            .GetHAICAI();
    }

    // ============================================================
    // CREATE - POST
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        PatientViewModel model)
    {
        NormalizePatientIdentifiers(model);

        await PopulateLookups(model);

        if (IsRejectedSample(model))
        {
            ValidateDates(model);

            ValidatePatientIdAndFacility(model);

            PruneRejectedSampleModelState(model);
        }
        else
        {
            ValidateDates(model);

            ValidatePatientIdAndFacility(model);

            ValidateDemographicsAndVitals(model);

            ValidateAgeAndPresumptiveId(model);

            ValidateClinicalConditions(model);

            ValidateAntibioticHistory(model);
        }

        // Duplicate Patient ID check

        if (!string.IsNullOrWhiteSpace(model.PatientID) &&
            await patientService.ExistsAsync(model.PatientID))
        {
            ModelState.AddModelError(
                nameof(model.PatientID),
                "A patient with this Patient ID already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var patient = MapToEntity(model);

        await patientService.CreateAsync(patient);

        TempData["SuccessMessage"] =
            $"Patient {patient.PatientID} registered successfully.";

        return RedirectToAction(nameof(Create));
    }

    // ============================================================
    // SEARCH PATIENT
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Search(
        string patientId)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            return Json(new
            {
                exists = false
            });
        }

        patientId = patientId
            .Trim()
            .ToUpperInvariant();

        var exists = await patientService.ExistsAsync(patientId);

        if (!exists)
        {
            return Json(new
            {
                exists = false
            });
        }

        return Json(new
        {
            exists = true,
            patientId
        });
    }

    // ============================================================
    // PATIENT ID SUGGESTIONS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Suggestions(
        string query)
    {
        var patientIds = await patientService
            .SearchPatientIdsAsync(query);

        return Json(patientIds);
    }

    // ============================================================
    // EDIT - GET
    // ============================================================

    [HttpGet("Patient/Edit")]
    [HttpGet("Patient/Edit/{**patientId}")]
    public async Task<IActionResult> Edit(
        string? patientId,
        [FromQuery(Name = "patientId")] string? queryPatientId = null,
        [FromQuery] string? id = null)
    {
        var targetId = Uri.UnescapeDataString(
            patientId ?? queryPatientId ?? id ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(targetId))
        {
            return NotFound();
        }

        var patient = await patientService.GetByIdAsync(targetId);

        if (patient == null)
        {
            return NotFound();
        }

        var model = MapToViewModel(patient);

        await PopulateLookups(model);

        return View(model);
    }

    // ============================================================
    // EDIT - POST
    // ============================================================

    [HttpPost("Patient/Edit/{**patientId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string patientId,
        PatientViewModel model)
    {
        NormalizePatientIdentifiers(model);

        if (string.IsNullOrWhiteSpace(patientId))
        {
            return NotFound();
        }

        patientId = Uri.UnescapeDataString(patientId).Trim();

        if (!string.Equals(
                patientId,
                model.PatientID,
                StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        await PopulateLookups(model);

        if (IsRejectedSample(model))
        {
            ValidateDates(model);

            ValidatePatientIdAndFacility(model);

            PruneRejectedSampleModelState(model);
        }
        else
        {
            ValidateDates(model);

            ValidatePatientIdAndFacility(model);

            ValidateDemographicsAndVitals(model);

            ValidateAgeAndPresumptiveId(model);

            ValidateClinicalConditions(model);

            ValidateAntibioticHistory(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var patient = await patientService.GetByIdAsync(patientId);

        if (patient == null)
        {
            return NotFound();
        }

        UpdateEntity(patient, model);

        try
        {
            await patientService.UpdateAsync(patient);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await patientService.ExistsAsync(patientId))
            {
                return NotFound();
            }

            throw;
        }

        TempData["SuccessMessage"] =
            $"Patient {patient.PatientID} updated successfully.";

        return RedirectToAction(
            nameof(Edit),
            new { patientId = patient.PatientID });
    }

    // ============================================================
    // PATIENT IMPORT - GET
    // ============================================================

    [HttpGet]
    public IActionResult Import()
    {
        var model =
            new ImportViewModel<Patient>();

        return View(model);
    }

    // ============================================================
    // PATIENT IMPORT - VALIDATE
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        ImportViewModel<Patient> model)
    {
        var fields =
            PatientImportDefinition.Fields;

        var requiredHeaders = fields
            .Select(f => f.Name)
            .ToList();

        if (model.File == null ||
            model.File.Length == 0)
        {
            model.IsValidated = true;

            model.Errors.Add(new ImportRowError
            {
                RowNumber = 0,
                ColumnName = string.Empty,
                Message = "Please select an Excel file."
            });

            return View(model);
        }

        var validatedModel =
            await importWorkflow.ValidateAsync(
                model.File,
                requiredHeaders,
                PatientImportSessionKey);

        return View(validatedModel);
    }

    // ============================================================
    // PATIENT IMPORT - CONFIRM
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport()
    {
        var summary =
            await importWorkflow.ConfirmImportAsync(
                PatientImportSessionKey);

        TempData["ImportSuccess"] =
            $"{summary.SuccessfulRows} patient record(s) imported successfully.";

        return RedirectToAction(nameof(Create));
    }

    // ============================================================
    // PATIENT IMPORT - DOWNLOAD TEMPLATE
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> DownloadImportTemplate()
    {
        return await importWorkflow.DownloadTemplateAsync(
            PatientImportDefinition.Fields,
            "PatientImportTemplate.xlsx");
    }

    // ============================================================
    // PATIENT IMPORT - CLEAR SESSION
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearImport()
    {
        importWorkflow.ClearSession(
            PatientImportSessionKey);

        return RedirectToAction(nameof(Import));
    }

    // ============================================================
    // NORMALIZE PATIENT IDENTIFIERS
    // ============================================================

    private static void NormalizePatientIdentifiers(
        PatientViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.PatientID))
        {
            model.PatientID = model.PatientID
                .Trim()
                .ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(model.FacilityID))
        {
            model.FacilityID = model.FacilityID
                .Trim()
                .ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(model.PresumptiveID))
        {
            model.PresumptiveID = model.PresumptiveID.Trim();
        }
    }

    // ============================================================
    // REJECTED SAMPLE MODELSTATE PRUNING
    // ============================================================

    private void PruneRejectedSampleModelState(
        PatientViewModel model)
    {
        var retainedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(model.PatientID),
            nameof(model.AcceptReject),
            nameof(model.FacilityID),
            nameof(model.EnrollmentDate),
            nameof(model.ReceivingDate)
        };

        foreach (var key in ModelState.Keys.ToList())
        {
            var rootKey = key.Split('.', '[')[0];
            if (!retainedFields.Contains(rootKey))
            {
                ModelState.Remove(key);
            }
        }
    }

    // ============================================================
    // DATE VALIDATION
    // ============================================================

    private void ValidateDates(
        PatientViewModel model)
    {
        bool isRejected = IsRejectedSample(model);

        if (model.EnrollmentDate.HasValue &&
            model.EnrollmentDate.Value.Date > DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(model.EnrollmentDate),
                "Enrollment date cannot be in the future.");
        }

        if (model.ReceivingDate.HasValue &&
            model.ReceivingDate.Value.Date > DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(model.ReceivingDate),
                "Receiving date cannot be in the future.");
        }

        if (model.EnrollmentDate.HasValue &&
            model.ReceivingDate.HasValue &&
            model.ReceivingDate.Value.Date <
            model.EnrollmentDate.Value.Date)
        {
            ModelState.AddModelError(
                nameof(model.ReceivingDate),
                "Receiving date cannot be earlier than the enrollment date.");
        }

        if (isRejected)
        {
            return;
        }

        if (model.PresentationDate.HasValue &&
            model.PresentationDate.Value.Date > DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(model.PresentationDate),
                "Presentation date cannot be in the future.");
        }

        if (model.InfectionOnset.HasValue &&
            model.InfectionOnset.Value.Date > DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(model.InfectionOnset),
                "Infection onset date cannot be in the future.");
        }
    }

    // ============================================================
    // PATIENT ID AND FACILITY VALIDATION
    // ============================================================

    private void ValidatePatientIdAndFacility(
        PatientViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.PatientID))
        {
            ModelState.AddModelError(
                nameof(model.PatientID),
                "Patient ID is required.");

            return;
        }

        if (!PatientIdRegex().IsMatch(model.PatientID))
        {
            ModelState.AddModelError(
                nameof(model.PatientID),
                "Patient ID must start with AMR or ISO and follow the format " +
                "AMR/FFF/DDD[L] or ISO/FFF/DDD[L], for example AMR/BSA/001 " +
                "or ISO/DFM/1234A.");

            return;
        }

        if (string.IsNullOrWhiteSpace(model.FacilityID))
        {
            ModelState.AddModelError(
                nameof(model.FacilityID),
                "Please select a facility.");

            return;
        }

        var patientIdParts =
            model.PatientID.Split('/');

        var patientFacilityCode =
            patientIdParts[1];

        if (!string.Equals(
                patientFacilityCode,
                model.FacilityID,
                StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                nameof(model.PatientID),
                $"The facility code in the Patient ID " +
                $"({patientFacilityCode}) does not match " +
                $"the selected Facility ID ({model.FacilityID}).");
        }
    }

    // ============================================================
    // REJECTED SAMPLE CHECK
    // ============================================================

    private static bool IsRejectedSample(
        PatientViewModel model)
    {
        return string.Equals(
            model.AcceptReject?.Trim(),
            "Rejected",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMissingOrNotApplicable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var trimmed = value.Trim();
        return string.Equals(trimmed, NotApplicableValue, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "Not Applicable", StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================
    // DEMOGRAPHICS, MILITARY/OCCUPATION & VITALS VALIDATION
    // ============================================================

    private void ValidateDemographicsAndVitals(
        PatientViewModel model)
    {
        var isIsoPatient =
            model.PatientID?.StartsWith(
                "ISO",
                StringComparison.OrdinalIgnoreCase) == true;

        if (string.IsNullOrWhiteSpace(model.Sex))
        {
            ModelState.AddModelError(
                nameof(model.Sex),
                "Sex is required.");
        }

        if (!isIsoPatient)
        {
            if (string.IsNullOrWhiteSpace(model.ResidenceCounty))
            {
                ModelState.AddModelError(
                    nameof(model.ResidenceCounty),
                    "County of residence is required.");
            }
            else if (string.IsNullOrWhiteSpace(model.ResidenceSubCounty))
            {
                ModelState.AddModelError(
                    nameof(model.ResidenceSubCounty),
                    "Sub-county of residence is required.");
            }

            var milciv = model.MILCIV?.Trim();
            var occupation = model.OccupationCategory?.Trim();

            if (!string.IsNullOrWhiteSpace(milciv) &&
                !string.IsNullOrWhiteSpace(occupation))
            {
                if (string.Equals(milciv, "Military", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(occupation, "Soldier", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        nameof(model.OccupationCategory),
                        "Military participants must have \"Soldier\" selected as the occupation category.");
                }
                else if (string.Equals(occupation, "Soldier", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(milciv, "Military", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        nameof(model.MILCIV),
                        "Participants with \"Soldier\" selected as their occupation must be classified as Military.");
                }
            }

            if (string.Equals(model.Pregnant?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase))
            {
                var isFemale = string.Equals(model.Sex?.Trim(), "Female", StringComparison.OrdinalIgnoreCase);
                var ageYears = GetAgeInYears(model.Age, model.AgeUnit);

                if (!isFemale)
                {
                    ModelState.AddModelError(
                        nameof(model.Pregnant),
                        "Pregnancy status cannot be Yes for non-female participants.");
                }
                else if (ageYears.HasValue && (ageYears.Value < 12.0 || ageYears.Value > 50.0))
                {
                    ModelState.AddModelError(
                        nameof(model.Pregnant),
                        "Pregnancy status is only applicable to female participants aged 12 to 50 years.");
                }
            }

            if (model.BPSystolic.HasValue &&
                model.BPDiastolic.HasValue &&
                model.BPSystolic.Value <= model.BPDiastolic.Value)
            {
                ModelState.AddModelError(
                    nameof(model.BPSystolic),
                    "Systolic blood pressure must be greater than diastolic blood pressure.");
            }
        }

        if (model.InfectionOnset.HasValue &&
            model.PresentationDate.HasValue &&
            !string.IsNullOrWhiteSpace(model.InfectionPresentAtAdmission))
        {
            var admissionStatus = model.InfectionPresentAtAdmission.Trim();
            var onset = model.InfectionOnset.Value.Date;
            var presentation = model.PresentationDate.Value.Date;

            if (string.Equals(admissionStatus, "Yes", StringComparison.OrdinalIgnoreCase) &&
                onset > presentation)
            {
                ModelState.AddModelError(
                    nameof(model.InfectionOnset),
                    "Infection onset must be on or before the Presentation Date when infection was present at admission.");
            }
            else if (string.Equals(admissionStatus, "No", StringComparison.OrdinalIgnoreCase) &&
                     onset < presentation)
            {
                ModelState.AddModelError(
                    nameof(model.InfectionOnset),
                    "Infection onset must be on or after the Presentation Date when infection was not present at admission.");
            }
        }
    }

    // ============================================================
    // CLINICAL CONDITIONAL VALIDATION
    // ============================================================

    private void ValidateClinicalConditions(
        PatientViewModel model)
    {
        var patientType =
            model.PatientType?.Trim();

        var hospitalLocation =
            model.HospitalLocation?.Trim();

        var isIsoPatient =
            model.PatientID?.StartsWith(
                "ISO",
                StringComparison.OrdinalIgnoreCase) == true;

        var isOutpatient =
            string.Equals(
                patientType,
                "Outpatient",
                StringComparison.OrdinalIgnoreCase);

        var isInpatient =
            string.Equals(
                patientType,
                "Inpatient",
                StringComparison.OrdinalIgnoreCase);

        if (isOutpatient)
        {
            if (!string.Equals(
                    hospitalLocation,
                    "OPD",
                    StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(model.HospitalLocation),
                    "Hospital location must be OPD for an outpatient.");
            }

            if (!IsMissingOrNotApplicable(model.HospitalWard))
            {
                ModelState.AddModelError(
                    nameof(model.HospitalWard),
                    "Hospital ward should not be recorded for an outpatient.");
            }

            if (!IsMissingOrNotApplicable(model.WardAdmission))
            {
                ModelState.AddModelError(
                    nameof(model.WardAdmission),
                    "Ward admission should not be recorded for an outpatient.");
            }

            if (!IsMissingOrNotApplicable(model.ICUAdmission))
            {
                ModelState.AddModelError(
                    nameof(model.ICUAdmission),
                    "ICU admission should not be recorded for an outpatient.");
            }

            if (!IsMissingOrNotApplicable(
                    model.InfectionPresentAtAdmission))
            {
                ModelState.AddModelError(
                    nameof(model.InfectionPresentAtAdmission),
                    "Infection present at admission should not be recorded for an outpatient.");
            }
        }
        else if (isInpatient)
        {
            if (string.Equals(
                    hospitalLocation,
                    "OPD",
                    StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(model.HospitalLocation),
                    "Hospital location cannot be OPD for an inpatient.");
            }

            if (!isIsoPatient && IsMissingOrNotApplicable(model.HospitalWard))
            {
                ModelState.AddModelError(
                    nameof(model.HospitalWard),
                    "Hospital ward is required for an inpatient.");
            }
        }

        var presentation =
            model.ClinicalPresentation?.Trim();

        var skinSymptomsApplicable =
            string.Equals(
                presentation,
                "Surgical Site Infection",
                StringComparison.OrdinalIgnoreCase)
            ||
            string.Equals(
                presentation,
                "Skin and Soft Tissue Infection",
                StringComparison.OrdinalIgnoreCase);

        if (!skinSymptomsApplicable &&
            !IsMissingOrNotApplicable(
                model.SkinInfectionSymptoms))
        {
            ModelState.AddModelError(
                nameof(model.SkinInfectionSymptoms),
                "Skin infection symptoms can only be recorded for " +
                "Surgical Site Infection or Skin and Soft Tissue Infection.");
        }
    }

    // ============================================================
    // MAP VIEWMODEL TO ENTITY
    // ============================================================

    private static Patient MapToEntity(
        PatientViewModel model)
    {
        var patient = new Patient
        {
            PatientID = model.PatientID?.Trim() ?? string.Empty,
            AcceptReject = model.AcceptReject?.Trim() ?? string.Empty,
            FacilityID = model.FacilityID?.Trim() ?? string.Empty,
            EnrollmentDate = model.EnrollmentDate,
            ReceivingDate = model.ReceivingDate
        };

        UpdateEntity(patient, model);
        return patient;
    }

    // ============================================================
    // MAP ENTITY TO VIEWMODEL
    // ============================================================

    private static PatientViewModel MapToViewModel(
        Patient patient)
    {
        return new PatientViewModel
        {
            PatientID = patient.PatientID ?? string.Empty,
            AcceptReject = patient.AcceptReject ?? string.Empty,
            FacilityID = patient.FacilityID ?? string.Empty,

            EnrollmentDate = patient.EnrollmentDate,
            ReceivingDate = patient.ReceivingDate,
            PresumptiveID = ToViewModelField(patient.PresumptiveID),

            Age = patient.Age,
            AgeUnit = ToViewModelField(patient.AgeUnit),
            DateOfBirth = patient.DateOfBirth,
            Sex = ToViewModelField(patient.Sex),

            ResidenceVillage =
                ToViewModelField(patient.ResidenceVillage),
            ResidenceCounty =
                ToViewModelField(patient.ResidenceCounty),
            ResidenceSubCounty =
                ToViewModelField(patient.ResidenceSubCounty),
            ResidenceDescription =
                ToViewModelField(patient.ResidenceDescription),

            MILCIV =
                ToViewModelField(patient.MILCIV),
            OccupationCategory =
                ToViewModelField(patient.OccupationCategory),
            HouseholdSize =
                patient.HouseholdSize,

            ChronicIllness =
                ToViewModelField(patient.ChronicIllness),
            ChronicSkinCondition =
                ToViewModelField(patient.ChronicSkinCondition),
            BodyPiercings =
                ToViewModelField(patient.BodyPiercings),
            AntacidUse =
                ToViewModelField(patient.AntacidUse),
            Immunosuppressed =
                ToViewModelField(patient.Immunosuppressed),
            HouseholdMemberInfection =
                ToViewModelField(patient.HouseholdMemberInfection),
            HouseholdHospitalWorker =
                ToViewModelField(patient.HouseholdHospitalWorker),
            OutdoorInjury =
                ToViewModelField(patient.OutdoorInjury),
            Pregnant =
                ToViewModelField(patient.Pregnant),
            PreviousInfection =
                ToViewModelField(patient.PreviousInfection),
            PriorHospitalAdmission =
                ToViewModelField(patient.PriorHospitalAdmission),
            LongTermFacility =
                ToViewModelField(patient.LongTermFacility),
            InjectionDrugUse =
                ToViewModelField(patient.InjectionDrugUse),
            NonInjectionDrugUse =
                ToViewModelField(patient.NonInjectionDrugUse),
            Homeless =
                ToViewModelField(patient.Homeless),
            Imprisoned =
                ToViewModelField(patient.Imprisoned),
            NurseryChildcareBoarding =
                ToViewModelField(patient.NurseryChildcareBoarding),
            ContactSports =
                ToViewModelField(patient.ContactSports),
            TravelOutsideCounty =
                ToViewModelField(patient.TravelOutsideCounty),

            ClinicalPresentation =
                ToViewModelField(patient.ClinicalPresentation),
            SkinInfectionSymptoms =
                ToViewModelField(patient.SkinInfectionSymptoms),
            PatientType =
                ToViewModelField(patient.PatientType),
            PresentationDate =
                patient.PresentationDate,
            HospitalLocation =
                ToViewModelField(patient.HospitalLocation),
            HospitalWard =
                ToViewModelField(patient.HospitalWard),
            ICUAdmission =
                ToViewModelField(patient.ICUAdmission),
            WardAdmission =
                ToViewModelField(patient.WardAdmission),

            Sedated =
                ToViewModelField(patient.Sedated),
            InvasiveDevice =
                ToViewModelField(patient.InvasiveDevice),
            InfectionPresentAtAdmission =
                ToViewModelField(patient.InfectionPresentAtAdmission),
            InfectionOnset =
                patient.InfectionOnset,

            WeightKg =
                patient.WeightKg,
            TemperatureC =
                patient.TemperatureC,
            BPSystolic =
                patient.BPSystolic,
            BPDiastolic =
                patient.BPDiastolic,
            Pulse =
                patient.Pulse,
            OxygenSaturation =
                patient.OxygenSaturation,

            CurrentAntibioticUse =
                ToViewModelField(patient.CurrentAntibioticUse),
            AntibioticDays =
                patient.AntibioticDays,
            AntibioticsUsed =
                ToViewModelField(patient.AntibioticsUsed),
            SurgeryForInfection =
                ToViewModelField(patient.SurgeryForInfection),
            HAICAI =
                ToViewModelField(patient.HAICAI),

            InfectionSource =
                ToViewModelField(patient.InfectionSource),
            InfectionSite =
                ToViewModelField(patient.InfectionSite),

            ConsentSample =
                ToViewModelField(patient.ConsentSample)
        };
    }

    private static string ToViewModelField(string? value)
    {
        if (IsMissingOrNotApplicable(value))
            return string.Empty;

        return value!.Trim();
    }

    // ============================================================
    // UPDATE EXISTING ENTITY & ENFORCE N/A VS NULL RULES
    // ============================================================

    private static void UpdateEntity(
        Patient patient,
        PatientViewModel model)
    {
        patient.AcceptReject =
            model.AcceptReject?.Trim() ?? string.Empty;
        patient.FacilityID =
            model.FacilityID?.Trim() ?? string.Empty;
        patient.EnrollmentDate =
            model.EnrollmentDate;
        patient.ReceivingDate =
            model.ReceivingDate;

        bool isRejected = IsRejectedSample(model);
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
        patient.PresumptiveID = NormalizeField(model.PresumptiveID, !isIso);
        patient.Age = model.Age;
        patient.AgeUnit = NormalizeField(model.AgeUnit, false);
        patient.DateOfBirth = model.DateOfBirth;
        patient.Sex = NormalizeField(model.Sex, false);

        // Community / residence (N/A for ISO cases)
        patient.ResidenceVillage = NormalizeField(model.ResidenceVillage, isIso);
        patient.ResidenceCounty = NormalizeField(model.ResidenceCounty, isIso);
        patient.ResidenceSubCounty = NormalizeField(model.ResidenceSubCounty, isIso);
        patient.ResidenceDescription = NormalizeField(model.ResidenceDescription, isIso);
        patient.MILCIV = NormalizeField(model.MILCIV, isIso);
        patient.OccupationCategory = NormalizeField(model.OccupationCategory, isIso);
        patient.HouseholdSize = isIso ? null : model.HouseholdSize;

        // Pregnancy applicability: N/A if ISO, non-Female, Age < 12 years, or Age > 50 years
        double? ageYears = GetAgeInYears(model.Age, model.AgeUnit);
        bool isPregnancyNotApplicable =
            isIso ||
            (!string.IsNullOrWhiteSpace(model.Sex) &&
             !string.Equals(model.Sex.Trim(), "Female", StringComparison.OrdinalIgnoreCase)) ||
            (ageYears.HasValue && (ageYears.Value < 12.0 || ageYears.Value > 50.0));

        // Risk factors (all N/A for ISO cases; NULL when unentered for active patients)
        patient.ChronicIllness = NormalizeField(model.ChronicIllness, isIso);
        patient.ChronicSkinCondition = NormalizeField(model.ChronicSkinCondition, isIso);
        patient.BodyPiercings = NormalizeField(model.BodyPiercings, isIso);
        patient.AntacidUse = NormalizeField(model.AntacidUse, isIso);
        patient.Immunosuppressed = NormalizeField(model.Immunosuppressed, isIso);
        patient.HouseholdMemberInfection = NormalizeField(model.HouseholdMemberInfection, isIso);
        patient.HouseholdHospitalWorker = NormalizeField(model.HouseholdHospitalWorker, isIso);
        patient.OutdoorInjury = NormalizeField(model.OutdoorInjury, isIso);
        patient.Pregnant = NormalizeField(model.Pregnant, isPregnancyNotApplicable);
        patient.PreviousInfection = NormalizeField(model.PreviousInfection, isIso);
        patient.PriorHospitalAdmission = NormalizeField(model.PriorHospitalAdmission, isIso);
        patient.LongTermFacility = NormalizeField(model.LongTermFacility, isIso);
        patient.InjectionDrugUse = NormalizeField(model.InjectionDrugUse, isIso);
        patient.NonInjectionDrugUse = NormalizeField(model.NonInjectionDrugUse, isIso);
        patient.Homeless = NormalizeField(model.Homeless, isIso);
        patient.Imprisoned = NormalizeField(model.Imprisoned, isIso);
        patient.NurseryChildcareBoarding = NormalizeField(model.NurseryChildcareBoarding, isIso);
        patient.ContactSports = NormalizeField(model.ContactSports, isIso);
        patient.TravelOutsideCounty = NormalizeField(model.TravelOutsideCounty, isIso);

        // Clinical presentation & outpatient rules
        patient.ClinicalPresentation = NormalizeField(model.ClinicalPresentation, false);

        var presentation = model.ClinicalPresentation?.Trim();
        bool skinSymptomsNotApplicable =
            isIso ||
            (!string.IsNullOrWhiteSpace(presentation) &&
             !string.Equals(presentation, "Surgical Site Infection", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(presentation, "Skin and Soft Tissue Infection", StringComparison.OrdinalIgnoreCase));

        patient.SkinInfectionSymptoms = NormalizeField(model.SkinInfectionSymptoms, skinSymptomsNotApplicable);
        patient.PatientType = NormalizeField(model.PatientType, false);
        patient.PresentationDate = model.PresentationDate;
        patient.HospitalLocation = NormalizeField(model.HospitalLocation, false);

        bool isOutpatient = string.Equals(
            model.PatientType?.Trim(),
            "Outpatient",
            StringComparison.OrdinalIgnoreCase);

        patient.HospitalWard = NormalizeField(model.HospitalWard, isOutpatient);
        patient.ICUAdmission = NormalizeField(model.ICUAdmission, isOutpatient);
        patient.WardAdmission = NormalizeField(model.WardAdmission, isOutpatient);
        patient.InfectionPresentAtAdmission = NormalizeField(model.InfectionPresentAtAdmission, isOutpatient);
        patient.InfectionOnset = model.InfectionOnset;

        // Clinical assessment & vitals (N/A for ISO cases)
        patient.Sedated = NormalizeField(model.Sedated, isIso);
        patient.InvasiveDevice = NormalizeField(model.InvasiveDevice, isIso);

        patient.WeightKg = isIso ? null : model.WeightKg;
        patient.TemperatureC = isIso ? null : model.TemperatureC;
        patient.BPSystolic = isIso ? null : model.BPSystolic;
        patient.BPDiastolic = isIso ? null : model.BPDiastolic;
        patient.Pulse = isIso ? null : model.Pulse;
        patient.OxygenSaturation = isIso ? null : model.OxygenSaturation;

        // Antibiotic history
        patient.CurrentAntibioticUse = NormalizeField(model.CurrentAntibioticUse, isIso);

        bool takingAntibiotics =
            !isIso &&
            string.Equals(model.CurrentAntibioticUse?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase);

        bool antibioticsUsedNotApplicable =
            isIso ||
            (!string.IsNullOrWhiteSpace(model.CurrentAntibioticUse) &&
             !string.Equals(model.CurrentAntibioticUse.Trim(), "Yes", StringComparison.OrdinalIgnoreCase));

        patient.AntibioticDays = takingAntibiotics ? model.AntibioticDays : null;
        patient.AntibioticsUsed = NormalizeField(model.AntibioticsUsed, antibioticsUsedNotApplicable);
        patient.SurgeryForInfection = NormalizeField(model.SurgeryForInfection, isIso);
        patient.HAICAI = NormalizeField(model.HAICAI, false);

        // Infection & consent
        patient.InfectionSource = NormalizeField(model.InfectionSource, false);
        patient.InfectionSite = NormalizeField(model.InfectionSite, isIso);
        patient.ConsentSample = isIso
            ? (string.IsNullOrWhiteSpace(model.ConsentSample) ? "Unknown" : model.ConsentSample.Trim())
            : NormalizeField(model.ConsentSample, false);
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

    // ============================================================
    // ANTIBIOTIC HISTORY VALIDATION
    // ============================================================

    private void ValidateAntibioticHistory(
        PatientViewModel model)
    {
        bool currentlyUsingAntibiotics =
            string.Equals(
                model.CurrentAntibioticUse?.Trim(),
                "Yes",
                StringComparison.OrdinalIgnoreCase);

        if (currentlyUsingAntibiotics)
        {
            if (!model.AntibioticDays.HasValue)
            {
                ModelState.AddModelError(
                    nameof(model.AntibioticDays),
                    "Enter the number of days on antibiotics.");
            }
            else if (model.AntibioticDays.Value < 0)
            {
                ModelState.AddModelError(
                    nameof(model.AntibioticDays),
                    "Number of days cannot be negative.");
            }

            model.AntibioticsUsed =
                model.AntibioticsUsed?.Trim() ?? string.Empty;

            return;
        }

        model.AntibioticDays = null;
        model.AntibioticsUsed = string.Empty;

        ModelState.Remove(
            nameof(model.AntibioticDays));

        ModelState.Remove(
            nameof(model.AntibioticsUsed));
    }

    // ============================================================
    // AGE AND PRESUMPTIVE ID VALIDATION
    // ============================================================

    private void ValidateAgeAndPresumptiveId(
        PatientViewModel model)
    {
        if (!model.Age.HasValue)
        {
            ModelState.AddModelError(
                nameof(model.Age),
                "Age is required.");

            return;
        }

        if (string.IsNullOrWhiteSpace(model.AgeUnit))
        {
            ModelState.AddModelError(
                nameof(model.AgeUnit),
                "Please specify whether the age is in days, months, or years.");

            return;
        }

        var ageUnit =
            model.AgeUnit.Trim();

        if (string.Equals(
                ageUnit,
                "Days",
                StringComparison.OrdinalIgnoreCase))
        {
            if (model.Age.Value < 0 ||
                model.Age.Value > 43830)
            {
                ModelState.AddModelError(
                    nameof(model.Age),
                    "Age in days must be non-negative.");
            }
        }
        else if (string.Equals(
                     ageUnit,
                     "Years",
                     StringComparison.OrdinalIgnoreCase))
        {
            if (model.Age.Value < 0 ||
                model.Age.Value > 120)
            {
                ModelState.AddModelError(
                    nameof(model.Age),
                    "Age must be between 0 and 120 years.");
            }
        }
        else if (string.Equals(
                     ageUnit,
                     "Months",
                     StringComparison.OrdinalIgnoreCase))
        {
            if (model.Age.Value < 0 ||
                model.Age.Value > 1440)
            {
                ModelState.AddModelError(
                    nameof(model.Age),
                    "Age must be between 0 and 1,440 months.");
            }
        }
        else
        {
            ModelState.AddModelError(
                nameof(model.AgeUnit),
                "Age unit must be Days, Months, or Years.");

            return;
        }

        if (model.DateOfBirth.HasValue &&
            model.EnrollmentDate.HasValue)
        {
            var dob =
                model.DateOfBirth.Value.Date;

            var enrollment =
                model.EnrollmentDate.Value.Date;

            if (dob > enrollment)
            {
                ModelState.AddModelError(
                    nameof(model.DateOfBirth),
                    "Date of birth cannot be later than the enrollment date.");

                return;
            }

            if (string.Equals(
                    ageUnit,
                    "Days",
                    StringComparison.OrdinalIgnoreCase))
            {
                var totalDays =
                    (enrollment - dob).Days;

                if (model.Age.Value != totalDays)
                {
                    ModelState.AddModelError(
                        nameof(model.Age),
                        $"Age does not match the date of birth and enrollment date. " +
                        $"The calculated age is {totalDays} day(s).");
                }
            }
            else
            {
                var totalMonths =
                    ((enrollment.Year - dob.Year) * 12)
                    + enrollment.Month
                    - dob.Month;

                if (enrollment.Day < dob.Day)
                {
                    totalMonths--;
                }

                totalMonths =
                    Math.Max(0, totalMonths);

                if (string.Equals(
                        ageUnit,
                        "Months",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (model.Age.Value != totalMonths)
                    {
                        ModelState.AddModelError(
                            nameof(model.Age),
                            $"Age does not match the date of birth and enrollment date. " +
                            $"The calculated age is {totalMonths} month(s).");
                    }
                }
                else if (string.Equals(
                             ageUnit,
                             "Years",
                             StringComparison.OrdinalIgnoreCase))
                {
                    var completedYears =
                        totalMonths / 12;

                    if (model.Age.Value != completedYears)
                    {
                        ModelState.AddModelError(
                            nameof(model.Age),
                            $"Age does not match the date of birth and enrollment date. " +
                            $"The calculated age is {completedYears} year(s).");
                    }
                }
            }
        }

        var isIsoPatient =
            model.PatientID?.StartsWith(
                "ISO",
                StringComparison.OrdinalIgnoreCase) == true;

        if (isIsoPatient)
        {
            if (string.IsNullOrWhiteSpace(
                    model.PresumptiveID))
            {
                ModelState.AddModelError(
                    nameof(model.PresumptiveID),
                    "Presumptive ID is required for ISO patients.");
            }
        }
        else
        {
            model.PresumptiveID = string.Empty;
        }
    }

    // ============================================================
    // GET SUB-COUNTIES
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetSubCounties(
        string county)
    {
        if (string.IsNullOrWhiteSpace(county))
        {
            return Json(Array.Empty<object>());
        }

        var subCounties =
            await lookupService
                .GetSubCountiesAsync(county);

        return Json(subCounties);
    }

    [GeneratedRegex(@"^(?:AMR|ISO)/[A-Z]{3}/\d{3,4}[A-Z]?$")]
    private static partial Regex PatientIdRegex();
}
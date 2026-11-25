
namespace AMRSurveillance.ViewModels;

using Microsoft.AspNetCore.Mvc.Rendering;

public class PatientViewModel
{
    // =====================================================
    // Sample Information
    // =====================================================

    public string PatientID { get; set; } = string.Empty;
    public string AcceptReject { get; set; } = string.Empty;
    public string FacilityID { get; set; } = string.Empty;
    public string? PresumptiveID { get; set; }
    public DateTime? EnrollmentDate { get; set; }
    public DateTime? ReceivingDate { get; set; }


    // =====================================================
    // Demographics
    // =====================================================

    public int? Age { get; set; }
    public string? AgeUnit { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Sex { get; set; }

    public string? ResidenceVillage { get; set; }
    public string? ResidenceCounty { get; set; }
    public string? ResidenceSubCounty { get; set; }
    public string? ResidenceDescription { get; set; }

    public string? MILCIV { get; set; }
    public string? OccupationCategory { get; set; }
    public int? HouseholdSize { get; set; }


    // =====================================================
    // Risk Factors
    // =====================================================

    public string? ChronicIllness { get; set; }
    public string? ChronicSkinCondition { get; set; }
    public string? BodyPiercings { get; set; }
    public string? AntacidUse { get; set; }
    public string? Immunosuppressed { get; set; }
    public string? HouseholdMemberInfection { get; set; }
    public string? HouseholdHospitalWorker { get; set; }
    public string? OutdoorInjury { get; set; }
    public string? Pregnant { get; set; }
    public string? PreviousInfection { get; set; }
    public string? PriorHospitalAdmission { get; set; }
    public string? LongTermFacility { get; set; }
    public string? InjectionDrugUse { get; set; }
    public string? NonInjectionDrugUse { get; set; }
    public string? Homeless { get; set; }
    public string? Imprisoned { get; set; }
    public string? NurseryChildcareBoarding { get; set; }
    public string? ContactSports { get; set; }
    public string? TravelOutsideCounty { get; set; }


    // =====================================================
    // Clinical Presentation
    // =====================================================

    public string? ClinicalPresentation { get; set; }
    public string? SkinInfectionSymptoms { get; set; }
    public string? PatientType { get; set; }

    public DateTime? PresentationDate { get; set; }

    public string? HospitalLocation { get; set; }
    public string? HospitalWard { get; set; }
    public string? ICUAdmission { get; set; }
    public string? WardAdmission { get; set; }


    // =====================================================
    // Clinical Assessment
    // =====================================================

    public string? Sedated { get; set; }
    public string? InvasiveDevice { get; set; }
    public string? InfectionPresentAtAdmission { get; set; }
    public DateTime? InfectionOnset { get; set; }


    // =====================================================
    // Vital Signs
    // =====================================================

    public decimal? WeightKg { get; set; }
    public decimal? TemperatureC { get; set; }

    public int? BPSystolic { get; set; }
    public int? BPDiastolic { get; set; }
    public int? Pulse { get; set; }

    public decimal? OxygenSaturation { get; set; }


    // =====================================================
    // Antibiotic History
    // =====================================================

    public string? CurrentAntibioticUse { get; set; }
    public int? AntibioticDays { get; set; }
    public string? AntibioticsUsed { get; set; }
    public string? SurgeryForInfection { get; set; }
    public string? HAICAI { get; set; }


    // =====================================================
    // Infection
    // =====================================================

    public string? InfectionSource { get; set; }
    public string? InfectionSite { get; set; }


    // =====================================================
    // Consent
    // =====================================================

    public string? ConsentSample { get; set; }


    // =====================================================
    // Lookup Collections
    // =====================================================

    public IEnumerable<SelectListItem> AcceptRejectList { get; set; } = [];
    public IEnumerable<SelectListItem> AgeUnitList { get; set; } = [];
    public IEnumerable<SelectListItem> FacilityList { get; set; } = [];
    public IEnumerable<SelectListItem> SexList { get; set; } = [];
    public IEnumerable<SelectListItem> CountyList { get; set; } = [];
    public IEnumerable<SelectListItem> SubCountyList { get; set; } = [];
    public IEnumerable<SelectListItem> ResidenceDescriptionList { get; set; } = [];
    public IEnumerable<SelectListItem> MilitaryCivilianList { get; set; } = [];
    public IEnumerable<SelectListItem> OccupationCategoryList { get; set; } = [];
    public IEnumerable<SelectListItem> YesNoList { get; set; } = [];
    public IEnumerable<SelectListItem> ClinicalPresentationList { get; set; } = [];
    public IEnumerable<SelectListItem> PatientTypeList { get; set; } = [];
    public IEnumerable<SelectListItem> HospitalLocationList { get; set; } = [];
    public IEnumerable<SelectListItem> InfectionSourceList { get; set; } = [];
    public IEnumerable<SelectListItem> InfectionSiteList { get; set; } = [];
    public IEnumerable<SelectListItem> SkinInfectionSymptomsList { get; set; } = [];
    public IEnumerable<SelectListItem> HAICAIList { get; set; } = [];
}

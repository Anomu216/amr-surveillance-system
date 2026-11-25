using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AMRSurveillance.Models;

public class Patient
{
    // ============================================================
    // REQUIRED SAMPLE INFORMATION
    // ============================================================

    [Key]
    [Required]
    [StringLength(20)]
    public string PatientID { get; set; } = string.Empty;

    [Required]
    public string AcceptReject { get; set; } = string.Empty;

    [Required]
    public string FacilityID { get; set; } = string.Empty;

    public DateTime? EnrollmentDate { get; set; }

    public DateTime? ReceivingDate { get; set; }


    // ============================================================
    // AGE
    // ============================================================

    public int? Age { get; set; }

    public string? AgeUnit { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Sex { get; set; }


    // ============================================================
    // ISO / PRESUMPTIVE ID
    // ============================================================

    public string? PresumptiveID { get; set; }


    // ============================================================
    // RESIDENCE / DEMOGRAPHICS
    // ============================================================

    public string? ResidenceVillage { get; set; }

    public string? ResidenceCounty { get; set; }

    public string? ResidenceSubCounty { get; set; }

    public string? ResidenceDescription { get; set; }

    public string? MILCIV { get; set; }

    public string? OccupationCategory { get; set; }

    public int? HouseholdSize { get; set; }


    // ============================================================
    // RISK FACTORS
    // ============================================================

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


    // ============================================================
    // CLINICAL PRESENTATION
    // ============================================================

    public string? ClinicalPresentation { get; set; }

    [MaxLength(500)]
    public string? SkinInfectionSymptoms { get; set; }

    public string? PatientType { get; set; }

    public DateTime? PresentationDate { get; set; }

    public string? HospitalLocation { get; set; }

    public string? HospitalWard { get; set; }

    public string? ICUAdmission { get; set; }

    public string? WardAdmission { get; set; }


    // ============================================================
    // CLINICAL ASSESSMENT
    // ============================================================

    public string? Sedated { get; set; }

    public string? InvasiveDevice { get; set; }

    public string? InfectionPresentAtAdmission { get; set; }

    public DateTime? InfectionOnset { get; set; }


    // ============================================================
    // VITAL SIGNS
    // ============================================================

    public decimal? WeightKg { get; set; }

    public decimal? TemperatureC { get; set; }

    public int? BPSystolic { get; set; }

    public int? BPDiastolic { get; set; }

    public int? Pulse { get; set; }

    public decimal? OxygenSaturation { get; set; }


    // ============================================================
    // ANTIBIOTIC HISTORY
    // ============================================================

    public string? CurrentAntibioticUse { get; set; }

    public int? AntibioticDays { get; set; }

    public string? AntibioticsUsed { get; set; }

    public string? SurgeryForInfection { get; set; }

    public string? HAICAI { get; set; }


    // ============================================================
    // INFECTION
    // ============================================================

    public string? InfectionSource { get; set; }

    public string? InfectionSite { get; set; }


    // ============================================================
    // CONSENT
    // ============================================================

    public string? ConsentSample { get; set; }


    // ============================================================
    // ISO CASE
    // ============================================================

    [NotMapped]
    public bool IsISOCase =>
        PatientID.StartsWith(
            "ISO",
            StringComparison.OrdinalIgnoreCase);


    // ============================================================
    // NAVIGATION PROPERTIES
    // ============================================================

    public Facility? Facility { get; set; }

public ICollection<Worksheet> Worksheets { get; set; } = [];
}
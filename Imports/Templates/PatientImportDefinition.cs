using AMRSurveillance.Imports.Models;

namespace AMRSurveillance.Imports.Templates;

public static class PatientImportDefinition
{
    public static readonly IReadOnlyList<ImportFieldDefinition> Fields =
    [
        // =====================================================
        // IDENTITY & REGISTRATION
        // =====================================================

        new()
        {
            Name = "PatientID",
            Type = ImportFieldType.String,
            Required = true
        },

        new()
        {
            Name = "AcceptReject",
            Type = ImportFieldType.String,
            LookupName = "AcceptReject"
        },

        new()
        {
            Name = "FacilityID",
            Type = ImportFieldType.String,
            Required = true
        },

        new()
        {
            Name = "EnrollmentDate",
            Type = ImportFieldType.Date
        },

        new()
        {
            Name = "ReceivingDate",
            Type = ImportFieldType.Date
        },

        new()
        {
            Name = "PresumptiveID",
            Type = ImportFieldType.String
        },


        // =====================================================
        // DEMOGRAPHICS
        // =====================================================

        new()
        {
            Name = "Age",
            Type = ImportFieldType.Integer
        },

        new()
        {
            Name = "AgeUnit",
            Type = ImportFieldType.String,
            LookupName = "AgeUnit"
        },

        new()
        {
            Name = "DateOfBirth",
            Type = ImportFieldType.Date
        },

        new()
        {
            Name = "Sex",
            Type = ImportFieldType.String,
            LookupName = "Sex"
        },

        new()
        {
            Name = "ResidenceVillage",
            Type = ImportFieldType.String
        },

        new()
        {
            Name = "ResidenceCounty",
            Type = ImportFieldType.String,
            LookupName = "County"
        },

        new()
        {
            Name = "ResidenceSubCounty",
            Type = ImportFieldType.String
        },

        new()
        {
            Name = "ResidenceDescription",
            Type = ImportFieldType.String,
            LookupName = "ResidenceDescription"
        },

        new()
        {
            Name = "MILCIV",
            Type = ImportFieldType.String,
            LookupName = "MilitaryCivilian"
        },

        new()
        {
            Name = "OccupationCategory",
            Type = ImportFieldType.String,
            LookupName = "OccupationCategory"
        },

        new()
        {
            Name = "HouseholdSize",
            Type = ImportFieldType.Integer
        },


        // =====================================================
        // PERSONAL HISTORY & POTENTIAL RISK FACTORS
        // =====================================================

        new()
        {
            Name = "ChronicIllness",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "ChronicSkinCondition",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "BodyPiercings",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "AntacidUse",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "Immunosuppressed",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "HouseholdMemberInfection",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "HouseholdHospitalWorker",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "OutdoorInjury",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "Pregnant",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "PreviousInfection",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "PriorHospitalAdmission",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "LongTermFacility",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "InjectionDrugUse",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "NonInjectionDrugUse",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "Homeless",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "Imprisoned",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "NurseryChildcareBoarding",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "ContactSports",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "TravelOutsideCounty",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },


        // =====================================================
        // CLINICAL PRESENTATION
        // =====================================================

        new()
        {
            Name = "ClinicalPresentation",
            Type = ImportFieldType.String,
            LookupName = "ClinicalPresentation"
        },

        new()
        {
            Name = "SkinInfectionSymptoms",
            Type = ImportFieldType.String
        },

        new()
        {
            Name = "PatientType",
            Type = ImportFieldType.String,
            LookupName = "PatientType"
        },

        new()
        {
            Name = "PresentationDate",
            Type = ImportFieldType.Date
        },

        new()
        {
            Name = "HospitalLocation",
            Type = ImportFieldType.String,
            LookupName = "HospitalLocation"
        },

        new()
        {
            Name = "HospitalWard",
            Type = ImportFieldType.String
        },

        new()
        {
            Name = "ICUAdmission",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "WardAdmission",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },


        // =====================================================
        // CLINICAL ASSESSMENT
        // =====================================================

        new()
        {
            Name = "Sedated",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "InvasiveDevice",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "InfectionPresentAtAdmission",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "InfectionOnset",
            Type = ImportFieldType.Date
        },

        new()
        {
            Name = "WeightKg",
            Type = ImportFieldType.Decimal
        },

        new()
        {
            Name = "TemperatureC",
            Type = ImportFieldType.Decimal
        },

        new()
        {
            Name = "BPSystolic",
            Type = ImportFieldType.Integer
        },

        new()
        {
            Name = "BPDiastolic",
            Type = ImportFieldType.Integer
        },

        new()
        {
            Name = "Pulse",
            Type = ImportFieldType.Integer
        },

        new()
        {
            Name = "OxygenSaturation",
            Type = ImportFieldType.Decimal
        },


        // =====================================================
        // ANTIBIOTICS & INFECTION-RELATED INFORMATION
        // =====================================================

        new()
        {
            Name = "CurrentAntibioticUse",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "AntibioticDays",
            Type = ImportFieldType.Integer
        },

        new()
        {
            Name = "AntibioticsUsed",
            Type = ImportFieldType.String
        },

        new()
        {
            Name = "SurgeryForInfection",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        },

        new()
        {
            Name = "HAICAI",
            Type = ImportFieldType.String,
            LookupName = "HAICAI"
        },

        new()
        {
            Name = "InfectionSource",
            Type = ImportFieldType.String,
            LookupName = "InfectionSource"
        },

        new()
        {
            Name = "InfectionSite",
            Type = ImportFieldType.String,
            LookupName = "InfectionSite"
        },


        // =====================================================
        // CONSENT
        // =====================================================

        new()
        {
            Name = "ConsentSample",
            Type = ImportFieldType.String,
            LookupName = "YesNo"
        }
    ];
}
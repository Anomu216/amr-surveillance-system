using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Models;
using ClosedXML.Excel;
using System.Globalization;

namespace AMRSurveillance.Imports.Mappers;

public class PatientImportMapper : IImportMapper<Patient>
{
    private const string NotApplicableValue = "N/A";

    public Patient Map(
        IXLWorksheet worksheet,
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap)
    {
        return new Patient
        {
            // =====================================================
            // IDENTITY & REGISTRATION
            // =====================================================

            PatientID = GetRequiredString(
                row,
                headerMap,
                "PatientID"),

            AcceptReject = GetRequiredString(
                row,
                headerMap,
                "AcceptReject"),

            FacilityID = GetRequiredString(
                row,
                headerMap,
                "FacilityID"),

            EnrollmentDate = GetDate(
                row,
                headerMap,
                "EnrollmentDate"),

            ReceivingDate = GetDate(
                row,
                headerMap,
                "ReceivingDate"),

            PresumptiveID = GetNullableString(
                row,
                headerMap,
                "PresumptiveID"),


            // =====================================================
            // DEMOGRAPHICS
            // =====================================================

            Age = GetInt(
                row,
                headerMap,
                "Age"),

            AgeUnit = GetNullableString(
                row,
                headerMap,
                "AgeUnit"),

            DateOfBirth = GetDate(
                row,
                headerMap,
                "DateOfBirth"),

            Sex = GetNullableString(
                row,
                headerMap,
                "Sex"),

            ResidenceVillage = GetNullableString(
                row,
                headerMap,
                "ResidenceVillage"),

            ResidenceCounty = GetNullableString(
                row,
                headerMap,
                "ResidenceCounty"),

            ResidenceSubCounty = GetNullableString(
                row,
                headerMap,
                "ResidenceSubCounty"),

            ResidenceDescription = GetNullableString(
                row,
                headerMap,
                "ResidenceDescription"),

            MILCIV = GetNullableString(
                row,
                headerMap,
                "MILCIV"),

            OccupationCategory = GetNullableString(
                row,
                headerMap,
                "OccupationCategory"),

            HouseholdSize = GetInt(
                row,
                headerMap,
                "HouseholdSize"),


            // =====================================================
            // PERSONAL HISTORY & POTENTIAL RISK FACTORS
            // =====================================================

            ChronicIllness = GetNullableString(
                row,
                headerMap,
                "ChronicIllness"),

            ChronicSkinCondition = GetNullableString(
                row,
                headerMap,
                "ChronicSkinCondition"),

            BodyPiercings = GetNullableString(
                row,
                headerMap,
                "BodyPiercings"),

            AntacidUse = GetNullableString(
                row,
                headerMap,
                "AntacidUse"),

            Immunosuppressed = GetNullableString(
                row,
                headerMap,
                "Immunosuppressed"),

            HouseholdMemberInfection = GetNullableString(
                row,
                headerMap,
                "HouseholdMemberInfection"),

            HouseholdHospitalWorker = GetNullableString(
                row,
                headerMap,
                "HouseholdHospitalWorker"),

            OutdoorInjury = GetNullableString(
                row,
                headerMap,
                "OutdoorInjury"),

            Pregnant = GetNullableString(
                row,
                headerMap,
                "Pregnant"),

            PreviousInfection = GetNullableString(
                row,
                headerMap,
                "PreviousInfection"),

            PriorHospitalAdmission = GetNullableString(
                row,
                headerMap,
                "PriorHospitalAdmission"),

            LongTermFacility = GetNullableString(
                row,
                headerMap,
                "LongTermFacility"),

            InjectionDrugUse = GetNullableString(
                row,
                headerMap,
                "InjectionDrugUse"),

            NonInjectionDrugUse = GetNullableString(
                row,
                headerMap,
                "NonInjectionDrugUse"),

            Homeless = GetNullableString(
                row,
                headerMap,
                "Homeless"),

            Imprisoned = GetNullableString(
                row,
                headerMap,
                "Imprisoned"),

            NurseryChildcareBoarding = GetNullableString(
                row,
                headerMap,
                "NurseryChildcareBoarding"),

            ContactSports = GetNullableString(
                row,
                headerMap,
                "ContactSports"),

            TravelOutsideCounty = GetNullableString(
                row,
                headerMap,
                "TravelOutsideCounty"),


            // =====================================================
            // CLINICAL PRESENTATION
            // =====================================================

            ClinicalPresentation = GetNullableString(
                row,
                headerMap,
                "ClinicalPresentation"),

            SkinInfectionSymptoms = GetNullableString(
                row,
                headerMap,
                "SkinInfectionSymptoms"),

            PatientType = GetNullableString(
                row,
                headerMap,
                "PatientType"),

            PresentationDate = GetDate(
                row,
                headerMap,
                "PresentationDate"),

            HospitalLocation = GetNullableString(
                row,
                headerMap,
                "HospitalLocation"),

            HospitalWard = GetNullableString(
                row,
                headerMap,
                "HospitalWard"),

            ICUAdmission = GetNullableString(
                row,
                headerMap,
                "ICUAdmission"),

            WardAdmission = GetNullableString(
                row,
                headerMap,
                "WardAdmission"),


            // =====================================================
            // CLINICAL ASSESSMENT
            // =====================================================

            Sedated = GetNullableString(
                row,
                headerMap,
                "Sedated"),

            InvasiveDevice = GetNullableString(
                row,
                headerMap,
                "InvasiveDevice"),

            InfectionPresentAtAdmission = GetNullableString(
                row,
                headerMap,
                "InfectionPresentAtAdmission"),

            InfectionOnset = GetDate(
                row,
                headerMap,
                "InfectionOnset"),

            WeightKg = GetDecimal(
                row,
                headerMap,
                "WeightKg"),

            TemperatureC = GetDecimal(
                row,
                headerMap,
                "TemperatureC"),

            BPSystolic = GetInt(
                row,
                headerMap,
                "BPSystolic"),

            BPDiastolic = GetInt(
                row,
                headerMap,
                "BPDiastolic"),

            Pulse = GetInt(
                row,
                headerMap,
                "Pulse"),

            OxygenSaturation = GetDecimal(
                row,
                headerMap,
                "OxygenSaturation"),


            // =====================================================
            // ANTIBIOTICS & INFECTION-RELATED INFORMATION
            // =====================================================

            CurrentAntibioticUse = GetNullableString(
                row,
                headerMap,
                "CurrentAntibioticUse"),

            AntibioticDays = GetInt(
                row,
                headerMap,
                "AntibioticDays"),

            AntibioticsUsed = GetNullableString(
                row,
                headerMap,
                "AntibioticsUsed"),

            SurgeryForInfection = GetNullableString(
                row,
                headerMap,
                "SurgeryForInfection"),

            HAICAI = GetNullableString(
                row,
                headerMap,
                "HAICAI"),

            InfectionSource = GetNullableString(
                row,
                headerMap,
                "InfectionSource"),

            InfectionSite = GetNullableString(
                row,
                headerMap,
                "InfectionSite"),


            // =====================================================
            // CONSENT
            // =====================================================

            ConsentSample = GetNullableString(
                row,
                headerMap,
                "ConsentSample")
        };
    }


    // =========================================================
    // STRING
    // =========================================================

    private static string GetRequiredString(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap,
        string columnName)
    {
        return GetNullableString(row, headerMap, columnName) ?? string.Empty;
    }

    private static string? GetNullableString(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap,
        string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out var column))
        {
            return null;
        }

        var value = row
            .Cell(column)
            .GetString()
            .Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (IsNotApplicableToken(value))
        {
            return NotApplicableValue;
        }

        return value;
    }

    private static bool IsNotApplicableToken(string value)
    {
        return string.Equals(value, NotApplicableValue, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Not Applicable", StringComparison.OrdinalIgnoreCase);
    }


    // =========================================================
    // INTEGER
    // =========================================================

    private static int? GetInt(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap,
        string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out var column))
        {
            return null;
        }

        var cell =
            row.Cell(column);

        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<int>(
            out var integerValue))
        {
            return integerValue;
        }

        var text =
            cell.GetString().Trim();

        if (string.IsNullOrWhiteSpace(text) ||
            IsNotApplicableToken(text))
        {
            return null;
        }

        if (int.TryParse(
            text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedValue))
        {
            return parsedValue;
        }

        return null;
    }


    // =========================================================
    // DECIMAL
    // =========================================================

    private static decimal? GetDecimal(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap,
        string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out var column))
        {
            return null;
        }

        var cell =
            row.Cell(column);

        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<decimal>(
            out var decimalValue))
        {
            return decimalValue;
        }

        var text =
            cell.GetString().Trim();

        if (string.IsNullOrWhiteSpace(text) ||
            IsNotApplicableToken(text))
        {
            return null;
        }

        if (decimal.TryParse(
            text,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsedValue))
        {
            return parsedValue;
        }

        return null;
    }


    // =========================================================
    // DATE
    // =========================================================

    private static DateTime? GetDate(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap,
        string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out var column))
        {
            return null;
        }

        var cell =
            row.Cell(column);

        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<DateTime>(
            out var dateValue))
        {
            return dateValue.Date;
        }

        var text =
            cell.GetString().Trim();

        if (string.IsNullOrWhiteSpace(text) ||
            IsNotApplicableToken(text))
        {
            return null;
        }

        string[] formats =
        [
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd-MM-yyyy",
            "d-M-yyyy",
            "yyyy-MM-dd",
            "MM/dd/yyyy",
            "M/d/yyyy"
        ];

        if (DateTime.TryParseExact(
            text,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate))
        {
            return parsedDate.Date;
        }

        if (DateTime.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsedDate))
        {
            return parsedDate.Date;
        }

        return null;
    }
}
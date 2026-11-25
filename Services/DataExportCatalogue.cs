using AMRSurveillance.ViewModels.DataRequests;

namespace AMRSurveillance.Services;

public static class DataExportCatalogue
{
    // ============================================================
    // IDENTIFIER TYPES
    // ============================================================

    public static List<DataRequestOptionViewModel> GetIdentifierTypes()
    {
        return new List<DataRequestOptionViewModel>
        {
            new()
            {
                Value = "PatientID",
                Text = "Patient ID"
            },
            new()
            {
                Value = "IsolateID",
                Text = "Isolate ID"
            },
            new()
            {
                Value = "KENID",
                Text = "KEN ID"
            },
            new()
            {
                Value = "MRSNID",
                Text = "MRSN ID"
            }
        };
    }


    // ============================================================
    // EXPORTABLE LOGICAL DATASETS
    // ============================================================

    public static List<DataRequestDatasetViewModel> GetDatasets()
    {
        return new List<DataRequestDatasetViewModel>
        {
            GetPatientDataset(),
            GetFacilityDataset(),
            GetWorksheetDataset(),
            GetOrganismDataset(),
            GetShipmentDataset(),
            GetGenomicsDataset()
        };
    }


    // ============================================================
    // PATIENT DATASET
    // ============================================================

    private static DataRequestDatasetViewModel GetPatientDataset()
    {
        return new DataRequestDatasetViewModel
        {
            Name = "Patient",
            Value = "Patient",
            Columns = new List<DataRequestColumnViewModel>
            {
                Column("PatientID", "Patient ID"),
                Column("EnrollmentDate", "Enrollment Date"),
                Column("ReceivingDate", "Receiving Date"),
                Column("FacilityID", "Facility ID"),
                Column("Sex", "Sex"),

                Column("AcceptReject", "Accept / Reject"),
                Column("ResidenceCounty", "Residence County"),
                Column("ResidenceSubCounty", "Residence Sub-County"),
                Column("MILCIV", "Military / Civilian"),

                Column("ClinicalPresentation", "Clinical Presentation"),
                
                Column("PatientType", "Patient Type"),
                Column("HospitalLocation", "Hospital Location"),
                Column("HospitalWard", "Hospital Ward"),

                Column("InfectionSource", "Infection Source"),
                Column("HAICAI","HAI/CAI"),
                Column("CurrentAntibioticUse", "Current Antibiotic Use"),
            }
        };
    }


    // ============================================================
    // FACILITY DATASET
    // ============================================================

    private static DataRequestDatasetViewModel GetFacilityDataset()
    {
        return new DataRequestDatasetViewModel
        {
            Name = "Facility",
            Value = "Facility",
            Columns = new List<DataRequestColumnViewModel>
            {
                Column("FacilityID", "Facility ID"),
                Column("FacilityName", "Facility Name"),
                Column("County", "County"),
                Column("SubCounty", "Sub-County"),
                Column("FacilityLevel", "Facility Level"),
                Column("Ownership", "Ownership")
            }
        };
    }


    // ============================================================
    // WORKSHEET / ISOLATE DATASET
    // ============================================================

    private static DataRequestDatasetViewModel GetWorksheetDataset()
    {
        return new DataRequestDatasetViewModel
        {
            Name = "Worksheet / Isolate",
            Value = "Worksheet",
            Columns = new List<DataRequestColumnViewModel>
            {
                Column("IsolateID", "Isolate ID"),
                Column("PatientID", "Patient ID"),
                Column("CultureResult", "Culture Result"),
                Column("AcceptedID", "Accepted Identification"),
                Column("WHONETID", "WHONET ID"),

                Column("GramReaction", "Gram Reaction"),
                Column("Morphology", "Morphology"),
                Column("Hemolysis", "Hemolysis"),
                Column("Catalase", "Catalase"),
                Column("Oxidase", "Oxidase"),
                Column("Coagulase", "Coagulase"),
                Column("LactoseFermentation", "Lactose Fermentation"),

                Column("ASTCard", "AST Card"),
                Column("ASTComments", "AST Comments"),

                Column("ESBL", "ESBL"),
                Column("CefoxitinScreen", "Cefoxitin Screen"),
                Column(
                    "InducibleClindamycinResistance",
                    "Inducible Clindamycin Resistance"),

                Column("MDR", "Multidrug Resistance (MDR)"),
                Column("Carbapenemase", "Carbapenemase"),
                Column("MRSA", "MRSA"),

                Column("Comments", "Comments")
            }
        };
    }


    // ============================================================
    // ORGANISM DATASET
    // ============================================================

    private static DataRequestDatasetViewModel GetOrganismDataset()
    {
        return new DataRequestDatasetViewModel
        {
            Name = "Organism",
            Value = "Organism",
            Columns = new List<DataRequestColumnViewModel>
            {
                Column("WHONETID", "WHONET ID")
            }
        };
    }


    // ============================================================
    // SHIPMENT DATASET
    //
    // Shipment provides an important identifier bridge. Where an
    // uploaded MRSNID exists in Shipment, the associated KENID can
    // be used by the export service to resolve related records.
    // ============================================================

    private static DataRequestDatasetViewModel GetShipmentDataset()
    {
        return new DataRequestDatasetViewModel
        {
            Name = "Shipment",
            Value = "Shipment",
            Columns = new List<DataRequestColumnViewModel>
            {
                Column("KENID", "KEN ID"),
                Column("MRSNID", "MRSN ID"),
                Column("IsolateID", "Isolate ID"),
                Column("MALDIID", "MALDI ID"),
                Column("ShipmentNumber", "Shipment Number"),
                Column("BoxNumber", "Box Number")
            }
        };
    }


    // ============================================================
    // GENOMICS DATASET
    // ============================================================

    private static DataRequestDatasetViewModel GetGenomicsDataset()
    {
        return new DataRequestDatasetViewModel
        {
            Name = "Genomics",
            Value = "Genomics",
            Columns = new List<DataRequestColumnViewModel>
            {
                Column("IsolateID", "Isolate ID"),
                Column("KENID", "KEN ID"),
                Column("MRSNID", "MRSN ID"),
                Column("DNASampleID", "DNA Sample ID"),

                Column("GenomicONTID", "ONT ID"),
                Column("GenomicILMID", "Illumina ID"),
                Column("ProjectCode", "Project Code"),
                Column("SeqRunID", "Sequencing Run ID"),
                Column("SequencingDate", "Sequencing Date"),

                Column("WHONETID", "WHONET ID"),
                Column("WGSID", "WGS ID"),
                Column("GCPercent", "GC Percentage"),

                Column("StrainType", "Strain Type"),
                Column("Phylogroup", "Phylogroup"),
                Column("Serotype", "Serotype"),
                Column("Fimtype", "Fim Type"),
                Column("SCCmecType", "SCCmec Type"),
                Column("SpaType", "Spa Type"),
                Column("OLocus", "O Locus"),
                Column("KLocus", "K Locus"),

                Column("VirulenceScore", "Virulence Score"),
                Column("ResistanceScore", "Resistance Score")
            }
        };
    }


    // ============================================================
    // COLUMN HELPER
    // ============================================================

    private static DataRequestColumnViewModel Column(
        string value,
        string text)
    {
        return new DataRequestColumnViewModel
        {
            Value = value,
            Text = text
        };
    }
}
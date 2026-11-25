using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AMRSurveillance.Migrations
{
    /// <inheritdoc />
    public partial class AddGenomicsAndGeneReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AntibioticClass",
                columns: table => new
                {
                    AntibioticClassName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntibioticClass", x => x.AntibioticClassName);
                });

            migrationBuilder.CreateTable(
                name: "Facility",
                columns: table => new
                {
                    FacilityID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FacilityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    County = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubCounty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FacilityLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ownership = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facility", x => x.FacilityID);
                });

            migrationBuilder.CreateTable(
                name: "GeneReference",
                columns: table => new
                {
                    GeneName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GeneDescription = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    GeneCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApplicableOrganism = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneReference", x => x.GeneName);
                });

            migrationBuilder.CreateTable(
                name: "Organism",
                columns: table => new
                {
                    WHONETID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organism", x => x.WHONETID);
                });

            migrationBuilder.CreateTable(
                name: "Antibiotic",
                columns: table => new
                {
                    AntibioticCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AntibioticName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AntibioticClassName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ASTCard = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MICEnabled = table.Column<bool>(type: "bit", nullable: false),
                    InterpretationEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Antibiotic", x => x.AntibioticCode);
                    table.ForeignKey(
                        name: "FK_Antibiotic_AntibioticClass_AntibioticClassName",
                        column: x => x.AntibioticClassName,
                        principalTable: "AntibioticClass",
                        principalColumn: "AntibioticClassName",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Patient",
                columns: table => new
                {
                    PatientID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcceptReject = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FacilityID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EnrollmentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    AgeUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Sex = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PresumptiveID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResidenceVillage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResidenceCounty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResidenceSubCounty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResidenceDescription = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MILCIV = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccupationCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HouseholdSize = table.Column<int>(type: "int", nullable: true),
                    ChronicIllness = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChronicSkinCondition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BodyPiercings = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AntacidUse = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Immunosuppressed = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HouseholdMemberInfection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HouseholdHospitalWorker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OutdoorInjury = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Pregnant = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PreviousInfection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PriorHospitalAdmission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LongTermFacility = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InjectionDrugUse = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NonInjectionDrugUse = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Homeless = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Imprisoned = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NurseryChildcareBoarding = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContactSports = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TravelOutsideCounty = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClinicalPresentation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SkinInfectionSymptoms = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PatientType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PresentationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HospitalLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HospitalWard = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ICUAdmission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WardAdmission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Sedated = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvasiveDevice = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InfectionPresentAtAdmission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InfectionOnset = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    TemperatureC = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    BPSystolic = table.Column<int>(type: "int", nullable: true),
                    BPDiastolic = table.Column<int>(type: "int", nullable: true),
                    Pulse = table.Column<int>(type: "int", nullable: true),
                    OxygenSaturation = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CurrentAntibioticUse = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AntibioticDays = table.Column<int>(type: "int", nullable: true),
                    AntibioticsUsed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SurgeryForInfection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HAICAI = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InfectionSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InfectionSite = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConsentSample = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patient", x => x.PatientID);
                    table.ForeignKey(
                        name: "FK_Patient_Facility_FacilityID",
                        column: x => x.FacilityID,
                        principalTable: "Facility",
                        principalColumn: "FacilityID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntrinsicResistance",
                columns: table => new
                {
                    WHONETID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AntibioticCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntrinsicResistance", x => new { x.WHONETID, x.AntibioticCode });
                    table.ForeignKey(
                        name: "FK_IntrinsicResistance_Antibiotic_AntibioticCode",
                        column: x => x.AntibioticCode,
                        principalTable: "Antibiotic",
                        principalColumn: "AntibioticCode",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IntrinsicResistance_Organism_WHONETID",
                        column: x => x.WHONETID,
                        principalTable: "Organism",
                        principalColumn: "WHONETID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Worksheet",
                columns: table => new
                {
                    IsolateID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CultureResult = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AcceptedID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WHONETID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GramReaction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Morphology = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Hemolysis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Catalase = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Oxidase = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Coagulase = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LactoseFermentation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ASTCard = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MDR = table.Column<bool>(type: "bit", nullable: true),
                    ESBL = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Carbapenemase = table.Column<bool>(type: "bit", nullable: true),
                    MRSA = table.Column<bool>(type: "bit", nullable: true),
                    CefoxitinScreen = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InducibleClindamycinResistance = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ASTComments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Worksheet", x => x.IsolateID);
                    table.ForeignKey(
                        name: "FK_Worksheet_Organism_WHONETID",
                        column: x => x.WHONETID,
                        principalTable: "Organism",
                        principalColumn: "WHONETID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Worksheet_Patient_PatientID",
                        column: x => x.PatientID,
                        principalTable: "Patient",
                        principalColumn: "PatientID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shipment",
                columns: table => new
                {
                    KENID = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsolateID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MALDIID = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MRSNID = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ShipmentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShipmentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BoxNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipment", x => x.KENID);
                    table.ForeignKey(
                        name: "FK_Shipment_Worksheet_IsolateID",
                        column: x => x.IsolateID,
                        principalTable: "Worksheet",
                        principalColumn: "IsolateID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Genomics",
                columns: table => new
                {
                    IsolateID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    KENID = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MRSNID = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DNASampleID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GenomicONTID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GenomicILMID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProjectCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SeqRunID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SequencingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittingProtocol = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    WHONETID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WGSID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GenomeLength = table.Column<long>(type: "bigint", nullable: true),
                    GCPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    NoOfContigs = table.Column<int>(type: "int", nullable: true),
                    AssemblyN50 = table.Column<long>(type: "bigint", nullable: true),
                    CompleteGenome = table.Column<bool>(type: "bit", nullable: true),
                    NoOfPlasmids = table.Column<int>(type: "int", nullable: true),
                    StrainType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Phylogroup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Serotype = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Fimtype = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SCCmecType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SpaType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OLocus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    KLocus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VirulenceScore = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    ResistanceScore = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genomics", x => x.IsolateID);
                    table.ForeignKey(
                        name: "FK_Genomics_Organism_WHONETID",
                        column: x => x.WHONETID,
                        principalTable: "Organism",
                        principalColumn: "WHONETID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Genomics_Shipment_KENID",
                        column: x => x.KENID,
                        principalTable: "Shipment",
                        principalColumn: "KENID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Genomics_Worksheet_IsolateID",
                        column: x => x.IsolateID,
                        principalTable: "Worksheet",
                        principalColumn: "IsolateID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Antibiotic_Active",
                table: "Antibiotic",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_Antibiotic_AntibioticClassName",
                table: "Antibiotic",
                column: "AntibioticClassName");

            migrationBuilder.CreateIndex(
                name: "IX_Antibiotic_AntibioticName",
                table: "Antibiotic",
                column: "AntibioticName");

            migrationBuilder.CreateIndex(
                name: "IX_Facility_FacilityName",
                table: "Facility",
                column: "FacilityName");

            migrationBuilder.CreateIndex(
                name: "IX_GeneReference_ApplicableOrganism",
                table: "GeneReference",
                column: "ApplicableOrganism");

            migrationBuilder.CreateIndex(
                name: "IX_GeneReference_GeneCategory",
                table: "GeneReference",
                column: "GeneCategory");

            migrationBuilder.CreateIndex(
                name: "IX_GeneReference_IsActive",
                table: "GeneReference",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Genomics_KENID",
                table: "Genomics",
                column: "KENID");

            migrationBuilder.CreateIndex(
                name: "IX_Genomics_MRSNID",
                table: "Genomics",
                column: "MRSNID");

            migrationBuilder.CreateIndex(
                name: "IX_Genomics_SequencingDate",
                table: "Genomics",
                column: "SequencingDate");

            migrationBuilder.CreateIndex(
                name: "IX_Genomics_StrainType",
                table: "Genomics",
                column: "StrainType");

            migrationBuilder.CreateIndex(
                name: "IX_Genomics_WHONETID",
                table: "Genomics",
                column: "WHONETID");

            migrationBuilder.CreateIndex(
                name: "IX_IntrinsicResistance_AntibioticCode",
                table: "IntrinsicResistance",
                column: "AntibioticCode");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_EnrollmentDate",
                table: "Patient",
                column: "EnrollmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_FacilityID",
                table: "Patient",
                column: "FacilityID");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_PatientType",
                table: "Patient",
                column: "PatientType");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_Sex",
                table: "Patient",
                column: "Sex");

            migrationBuilder.CreateIndex(
                name: "IX_Shipment_IsolateID",
                table: "Shipment",
                column: "IsolateID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Worksheet_ASTCard",
                table: "Worksheet",
                column: "ASTCard");

            migrationBuilder.CreateIndex(
                name: "IX_Worksheet_Carbapenemase",
                table: "Worksheet",
                column: "Carbapenemase");

            migrationBuilder.CreateIndex(
                name: "IX_Worksheet_CultureResult",
                table: "Worksheet",
                column: "CultureResult");

            migrationBuilder.CreateIndex(
                name: "IX_Worksheet_MDR",
                table: "Worksheet",
                column: "MDR");

            migrationBuilder.CreateIndex(
                name: "IX_Worksheet_MRSA",
                table: "Worksheet",
                column: "MRSA");

            migrationBuilder.CreateIndex(
                name: "IX_Worksheet_PatientID",
                table: "Worksheet",
                column: "PatientID");

            migrationBuilder.CreateIndex(
                name: "IX_Worksheet_WHONETID",
                table: "Worksheet",
                column: "WHONETID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneReference");

            migrationBuilder.DropTable(
                name: "Genomics");

            migrationBuilder.DropTable(
                name: "IntrinsicResistance");

            migrationBuilder.DropTable(
                name: "Shipment");

            migrationBuilder.DropTable(
                name: "Antibiotic");

            migrationBuilder.DropTable(
                name: "Worksheet");

            migrationBuilder.DropTable(
                name: "AntibioticClass");

            migrationBuilder.DropTable(
                name: "Organism");

            migrationBuilder.DropTable(
                name: "Patient");

            migrationBuilder.DropTable(
                name: "Facility");
        }
    }
}

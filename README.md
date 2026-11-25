# AMR Surveillance System

An ASP.NET Core web application for Antimicrobial Resistance (AMR) surveillance, patient questionnaire capture, laboratory worksheet and shipment tracking, genomics and reference data management, data request workflows, auditing, and longitudinal resistance reporting.

## Key Modules & Capabilities

### 1. Patient Surveillance & Questionnaire
- **Multi-Section Partial Views (`Views/Patient/Sections/`):**
  - `_SampleInformation.cshtml` (`PatientID`, `PresumptiveID`, `AcceptReject`, `FacilityID`, `EnrollmentDate`, `ReceivingDate`)
  - `_Demographics.cshtml` (`Sex`, `DateOfBirth`, `Age`, `AgeUnit`, `MILCIV`, `OccupationCategory`, `HouseholdSize`, `ResidenceCounty`, `ResidenceSubCounty`, `ResidenceVillage`, `ResidenceDescription`)
  - `_RiskFactors.cshtml` (risk factor radio toggles, ISO case handling, and female age-eligibility rules for pregnancy)
  - `_Consent.cshtml` (`ConsentSample`)
  - `_ClinicalPresentation.cshtml` (`ClinicalPresentation`, conditional `SkinInfectionSymptoms`, `PatientType`, `HospitalLocation`, and inpatient ward/ICU fields)
  - `_ClinicalAssessment.cshtml` (`Sedated`, `InvasiveDevice`, `InfectionPresentAtAdmission`, `InfectionOnset`, and vital signs)
  - `_AntibioticsHistory.cshtml` (`CurrentAntibioticUse`, `AntibioticDays`, `AntibioticsUsed`, `SurgeryForInfection`, `HAICAI`, `InfectionSource`, `InfectionSite`)
- **Client & Server Validation (`wwwroot/js/patient.js` & `PatientController.cs`):**
  - Enforces `PatientID` format (`AMR/FFF/DDD[L]` or `ISO/FFF/DDD[L]`) and matching facility codes.
  - Handles **Rejected** samples with minimal required fields while pruning non-retained `ModelState` entries and normalizing unentered/non-applicable values.
  - Applies **ISO** case field visibility and `N/A` vs. `null` persistence rules.
  - Cross-validates `Age` against `DateOfBirth` and `EnrollmentDate`, `MILCIV` vs. `OccupationCategory` (`Military` / `Soldier`), `BPSystolic` vs. `BPDiastolic`, `PatientType` vs. `HospitalLocation`, `ClinicalPresentation` vs. `InfectionSource`, and date ordering/future dates.

### 2. Laboratory Worksheets, Shipments & Genomics
- **Worksheets & AST:** `WorksheetController`, `WorksheetService`, `Worksheet.js`, and AST card configurations (`ASTCardController`, `ASTConfigurationService`, `ASTSchemaService`).
- **Shipments:** `ShipmentController` and `ShipmentService` (including support for multiple shipments per isolate).
- **Genomics & Gene Reference:** `GenomicsController`, `GeneReferenceController`, `GenomicService`, `GeneReferenceSchemaService`, and `genomics.js`.

### 3. Reference Data, Governance & Data Requests
- **Reference Data (`Controllers/ReferenceData/`):** Organisms, Antibiotics (including monthly report flags), Antibiotic Classes, Intrinsic Resistance rules, and AST Cards.
- **Data Requests & Approvals:** `DataRequestController`, `DataRequestService`, expiration tracking (`AddDataRequestExpiration`), and `ApprovalQueueController`.
- **Identity, Security & Auditing:** ASP.NET Core Identity with role-based access (`AppRoles`), forced password change (`AddMustChangePassword`), user provisioning/management (`UserManagementController`, `AdminProvisioner`, `IdentitySeeder`), `AuditSaveChangesInterceptor`, `AuditLogController`, and `NotificationController` / `NotificationBellViewComponent`.

### 4. Surveillance Reports
- **21st–20th Reporting Cycles:** Anchored on laboratory receiving dates with preset and custom date ranges plus multi-select filtering by facilities, organisms, and infection origin (`HAI` / `CAI`).
- **6-Period Longitudinal Rollup:** Tracks patients, recovered isolates, ESKAPE+ counts, and resistance rates with interactive phenotype checkboxes (`GN-ESKAPEE MDR`, `MRSA`, `CRE`, `ESBL`), small-sample caveats (`n < 15`), spike alerts, and narrative copy/print support.

### 5. Bulk Excel Imports
- Template generation, validation, preview, and staged import confirmation across Patients, Worksheets, Shipments, Facilities, Genomics, and Gene References via `Imports/`.

## Project Structure


├── Controllers/              # Feature controllers, ReferenceData/, and Genomics/
├── Data/                     # ApplicationDbContext and Interceptors/
├── Extensions/               # Session and helper extensions
├── Imports/                  # Interfaces, Mappers, Models, Services, Templates, Validators
├── Migrations/               # EF Core migrations and ApplicationDbContextModelSnapshot.cs
├── Models/                   # Domain entities and ApplicationUser
├── Security/                 # Roles, identity seeding, EastAfricaTime, and request filters
├── Services/                 # Domain services, Interfaces/, and KenyaAdminstrativeUnits.cs
├── ViewComponents/           # NotificationBellViewComponent.cs
├── ViewModels/               # Feature view models and DataRequests/
├── Views/                    # Razor views, partials, and shared layouts
└── wwwroot/                  # css/, js/ (patient.js, Worksheet.js, genomics.js, site.js), lib/

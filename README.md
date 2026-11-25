# AMR Surveillance System

ASP.NET Core AMR surveillance platform featuring analytics dashboards, validated multi-section patient and clinical data entry, bulk Excel imports, monthly (21st–20th cycle) and custom resistance reports, data request and approval workflows with expiration tracking, configurable reference data and dynamic AST/gene schemas, laboratory worksheets, shipments, genomics tracking, notifications, and audit logging.

## Core Modules & Capabilities

### 1. Surveillance & Analytics Dashboards
- Aggregates key surveillance and laboratory activity metrics across facilities, organisms, resistance phenotypes, and operational workflows via `DashboardService` and `DashboardExportViewModel`.
- Provides operational oversight of case capture, isolate and shipment processing, pending approvals/requests, and exportable summary reporting.

### 2. Multi-Section Data Entry, Cross-Field Validations & Bulk Imports
- **Patient & Clinical Questionnaire:** Built around `PatientViewModel`, `wwwroot/js/patient.js`, and modular Razor partials under `Views/Patient/Sections/`:
  - `_SampleInformation.cshtml` (`PatientID`, `PresumptiveID`, `AcceptReject`, `FacilityID`, `EnrollmentDate`, `ReceivingDate`)
  - `_Demographics.cshtml` (`Sex`, `DateOfBirth`, `Age`, `AgeUnit`, `MILCIV`, `OccupationCategory`, `HouseholdSize`, `ResidenceCounty`, `ResidenceSubCounty`, `ResidenceVillage`, `ResidenceDescription`)
  - `_RiskFactors.cshtml` (risk factor radio toggles, ISO case handling, and female age-eligibility rules for pregnancy)
  - `_Consent.cshtml` (`ConsentSample`)
  - `_ClinicalPresentation.cshtml` (`ClinicalPresentation`, conditional `SkinInfectionSymptoms`, `PatientType`, `HospitalLocation`, and inpatient ward/ICU/admission fields)
  - `_ClinicalAssessment.cshtml` (`Sedated`, `InvasiveDevice`, `InfectionPresentAtAdmission`, `InfectionOnset`, and vital signs)
  - `_AntibioticsHistory.cshtml` (`CurrentAntibioticUse`, `AntibioticDays`, `AntibioticsUsed`, `SurgeryForInfection`, `HAICAI`, `InfectionSource`, `InfectionSite`)
- **Dynamic Field Behavior & Workflow Rules:**
  - County-to-sub-county cascading dropdowns powered by `KenyaAdminstrativeUnits` / `LookupService` and `GetSubCounties`.
  - **Rejected** sample workflow retaining minimal core fields while clearing non-applicable inputs, pruning non-retained `ModelState` entries, and normalizing unentered/non-applicable values.
  - **ISO** case field visibility and `N/A` vs. `null` persistence rules across community, risk factor, vital sign, and antibiotic fields.
  - Conditional visibility and rules for pregnancy eligibility (female, ages 12–50), outpatient (`OPD`) vs. inpatient ward/ICU rules, skin infection symptoms, and antibiotic exposure details.
- **Client & Server Validation Parity:**
  - Enforces `PatientID` format (`AMR/FFF/DDD[L]` or `ISO/FFF/DDD[L]`) and facility code alignment.
  - Validates future-date limits, chronological date ordering (`EnrollmentDate` vs. `ReceivingDate`, `InfectionOnset` vs. `PresentationDate`), and `Age` consistency against `DateOfBirth` and `EnrollmentDate`.
  - Enforces `Military` ↔ `Soldier` occupation alignment, `BPSystolic` > `BPDiastolic`, and `ClinicalPresentation` vs. `InfectionSource` rules.
- **Laboratory Worksheets, Shipments & Genomics:**
  - AST and worksheet capture via `WorksheetController`, `WorksheetService`, and `wwwroot/js/Worksheet.js`.
  - Isolate shipment management including multiple shipments per isolate via `ShipmentController` and `ShipmentService`.
  - Genomics and gene reference tracking via `GenomicsController`, `GeneReferenceController`, `GenomicService`, `GeneReferenceSchemaService`, and `wwwroot/js/genomics.js`.
- **Bulk Excel Imports:**
  - Provides template generation, file/row validation, error reporting, preview, and staged confirmation via `ImportWorkflowService`, `ExcelImportService`, and domain-specific mappers, validators, and templates under `Imports/` (covering Patients, Worksheets, Shipments, Facilities, Genomics, and Gene References).

### 3. Custom, Monthly & Longitudinal Surveillance Reports
- **21st–20th Monthly Cycles & Custom Date Ranges:** Anchored on laboratory receiving dates with support for preset/monthly cycles and custom ranges, plus multi-select filtering by facilities, organisms, and infection origin (`HAI` / `CAI`).
- **6-Period Longitudinal Rollup:** Tracks patients, recovered isolates, ESKAPE+ counts, and resistance rates across reporting periods with dynamic phenotype checkboxes (`GN-ESKAPEE MDR`, `MRSA`, `CRE`, `ESBL`), small-sample caveats (`n < 15`), spike alerts, and narrative copy/print support.

### 4. Data Requests, Approval Chains, Security & Auditability
- **Data Requests & Expiration:** Handles data request submission, review, and expiration tracking via `DataRequestController`, `DataRequestService`, `DataRequestViewModel`, `DataRequestReviewViewModel`, and the `AddDataRequestExpiration` migration.
- **Approval Queues & Notifications:** Supports review/approval workflows (`ApprovalQueueController`), real-time notifications (`NotificationController`, `NotificationService`, `NotificationBellViewComponent`), and database audit trails (`AuditSaveChangesInterceptor`, `AuditLogController`, `AuditService`).
- **Identity & Access Control:** Uses ASP.NET Core Identity with role-based authorization (`AppRoles`, including `SystemAdministrator` and `DataEntryStaff`), forced password change support (`AddMustChangePassword`), user management, East Africa Time (`EastAfricaTime`) helpers, and provisioning support (`AMRSurveillanceProvisioning`).

### 5. Reference Data, Dynamic Schemas & Anti-Hardcoding Architecture
- **Reference-Driven Master Data:** Organisms, Antibiotics (including monthly report flags), Antibiotic Classes, Intrinsic Resistance rules, Facilities, Administrative Units, and AST Card definitions are managed through database-backed services and `ReferenceData` controllers/views rather than hardcoded in UI components or action logic.
- **Dynamic Schema & Rule Services:** `ASTSchemaService`, `ASTConfigurationService`, `GeneReferenceSchemaService`, `IntrinsicResistanceService`, `IdentifierValidationService`, and `LookupService` drive dynamic card schemas, gene reference schemas, intrinsic resistance evaluation, and lookup/validation behavior from configured data.

## Project Structure

```text
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

// ============================================================
// AMR SURVEILLANCE SYSTEM
// PATIENT QUESTIONNAIRE
// ============================================================

document.addEventListener("DOMContentLoaded", function () {

    "use strict";

    // ========================================================
    // MODULE 1: DOM ELEMENTS
    // ========================================================

    const patientForm = document.getElementById("patientForm");
    const sections = Array.from(document.querySelectorAll(".question-section"));
    const navLinks = Array.from(document.querySelectorAll("[data-section]"));
    const btnPrevious = document.getElementById("btnPrevious");
    const btnNext = document.getElementById("btnNext");
    const btnSave = document.getElementById("btnSave");

    // --------------------------------------------------------
    // Patient and sample information
    // --------------------------------------------------------

    const patientID = document.getElementById("PatientID");
    const presumptiveIdContainer = document.getElementById("presumptiveIdContainer");
    const presumptiveIdInput = document.getElementById("PresumptiveID");
    const facilityID = document.getElementById("FacilityID");
    const acceptReject = document.getElementById("AcceptReject");

    // --------------------------------------------------------
    // Demographics
    // --------------------------------------------------------

    const sex = document.getElementById("Sex");
    const age = document.getElementById("Age");
    const ageUnit = document.getElementById("AgeUnit");
    const dateOfBirth = document.getElementById("DateOfBirth");
    const pregnant = document.getElementById("Pregnant");
    const pregnancyContainer = document.getElementById("pregnancyContainer");
    const milciv = document.getElementById("MILCIV");
    const occupationCategory = document.getElementById("OccupationCategory");

    // --------------------------------------------------------
    // Clinical presentation
    // --------------------------------------------------------

    const clinicalPresentation = document.getElementById("ClinicalPresentation");
    const patientType = document.getElementById("PatientType");
    const hospitalLocation = document.getElementById("HospitalLocation");
    const hospitalWard = document.getElementById("HospitalWard");
    const icuAdmission = document.getElementById("ICUAdmission");
    const wardAdmission = document.getElementById("WardAdmission");
    const infectionPresentAtAdmission = document.getElementById("InfectionPresentAtAdmission");
    const hospitalWardContainer = document.getElementById("hospitalWardContainer");
    const icuContainer = document.getElementById("icuContainer");
    const wardAdmissionContainer = document.getElementById("wardAdmissionContainer");
    const infectionPresentAtAdmissionContainer = document.getElementById("infectionPresentAtAdmissionContainer");
    const sedated = document.getElementById("Sedated");
    const invasiveDevice = document.getElementById("InvasiveDevice");

    // --------------------------------------------------------
    // Vital Signs
    // --------------------------------------------------------

    const weightKg = document.getElementById("WeightKg");
    const temperatureC = document.getElementById("TemperatureC");
    const oxygenSaturation = document.getElementById("OxygenSaturation");
    const bpSystolic = document.getElementById("BPSystolic");
    const bpDiastolic = document.getElementById("BPDiastolic");
    const pulse = document.getElementById("Pulse");

    // --------------------------------------------------------
    // Antibiotics and infections
    // --------------------------------------------------------

    const currentAntibioticUse = document.getElementById("CurrentAntibioticUse");
    const surgeryForInfection = document.getElementById("SurgeryForInfection");
    const infectionSource = document.getElementById("InfectionSource");
    const infectionSite = document.getElementById("InfectionSite");
    const consentSample = document.getElementById("ConsentSample");
    const antibioticDetailsContainer = document.getElementById("antibioticDetailsContainer");
    const antibioticDaysContainer = document.getElementById("antibioticDaysContainer");
    const antibioticDays = document.getElementById("AntibioticDays");
    const antibioticsUsedContainer = document.getElementById("antibioticsUsedContainer");
    const antibioticsUsed = document.getElementById("AntibioticsUsed");
    const haiCai = document.getElementById("HAICAI");

    // --------------------------------------------------------
    // ISO / conditional sections
    // --------------------------------------------------------

    const communityFields = document.getElementById("communityFields");
    const riskFactorFields = document.getElementById("riskFactorFields");

    // --------------------------------------------------------
    // Residence
    // --------------------------------------------------------

    const county = document.getElementById("ResidenceCounty");
    const subCounty = document.getElementById("ResidenceSubCounty");

    // --------------------------------------------------------
    // Dates
    // --------------------------------------------------------

    const enrollmentDate = document.getElementById("EnrollmentDate");
    const receivingDate = document.getElementById("ReceivingDate");
    const presentationDate = document.getElementById("PresentationDate");
    const infectionOnset = document.getElementById("InfectionOnset");

    let currentIndex = 0;
    let progressReady = false;


    // ============================================================
    // MODULE 2: COMMON UTILITY FUNCTIONS
    // ============================================================

    function isEmpty(value) {
        if (value === null || value === undefined) return true;
        const trimmed = String(value).trim();
        return trimmed === "" ||
            trimmed.toLowerCase() === "n/a" ||
            trimmed.toLowerCase() === "not applicable";
    }


    function isNotApplicableValue(value) {
        if (value === null || value === undefined) return false;
        const trimmed = String(value).trim().toLowerCase();
        return trimmed === "n/a" || trimmed === "not applicable";
    }


    function scrubStoredNotApplicablePlaceholders() {
        if (!patientForm) return;

        patientForm
            .querySelectorAll("input[type='text'], textarea")
            .forEach(field => {
                if (isNotApplicableValue(field.value)) {
                    field.value = "";
                }
            });
    }


    function show(element) {
        if (!element) return;
        element.classList.remove("d-none");
        element.style.display = "";
    }


    function hide(element) {
        if (!element) return;
        element.classList.add("d-none");
        element.style.display = "none";
    }


    function clearControl(element) {
        if (!element) return;

        if (
            element.type === "checkbox" ||
            element.type === "radio"
        ) {
            element.checked = false;

        } else if (element.tagName === "SELECT") {
            element.selectedIndex = 0;

        } else {
            element.value = "";
        }

        resetValidation(element);
    }


    function clearContainer(container) {
        if (!container) return;
        container.querySelectorAll(
            "input, select, textarea"
        ).forEach(clearControl);
    }


    function markValid(element) {
        if (!element) return;
        element.classList.remove("is-invalid");
        element.classList.remove("input-validation-error");
        element.classList.add("is-valid");
        element.removeAttribute("aria-invalid");
    }


    function markInvalid(element, message) {
        if (!element) return;
        element.classList.remove("is-valid");
        element.classList.add("is-invalid");
        element.setAttribute("aria-invalid", "true");

        if (message) {
            const fieldContainer = getFieldContainer(element);
            const validationMessage = fieldContainer
                ? fieldContainer.querySelector(
                    ".invalid-feedback, .field-validation-error"
                )
                : null;

            if (validationMessage) {
                validationMessage.textContent = message;
            }
        }
    }


    function resetValidation(element) {
        if (!element) return;
        element.classList.remove("is-valid");
        element.classList.remove("is-invalid");
        element.removeAttribute("aria-invalid");
    }


    function today() {
        const date = new Date();
        date.setHours(0, 0, 0, 0);
        return date;
    }


    function parseDate(value) {
        if (isEmpty(value)) return null;
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return null;
        }
        date.setHours(0, 0, 0, 0);
        return date;
    }


    function getFieldContainer(element) {
        if (!element) return null;
        return element.closest(
            ".mb-3, .form-group, .col, .col-md-6, .col-12"
        );
    }


    // ============================================================
    // MODULE 2A: REJECTED SAMPLE WORKFLOW
    // ============================================================

    let previousAcceptRejectValue = acceptReject ? acceptReject.value : "";


    function isRejectedSample() {
        return !!acceptReject &&
            acceptReject.value.trim().toLowerCase() === "rejected";
    }


    function clearRejectedSampleFields() {
        if (!patientForm) return;

        const retainedFieldIds = [
            "PatientID",
            "AcceptReject",
            "FacilityID",
            "EnrollmentDate",
            "ReceivingDate"
        ];

        patientForm
            .querySelectorAll("input, select, textarea")
            .forEach(field => {
                if (field.type === "hidden") return;
                if (retainedFieldIds.includes(field.id)) return;
                clearControl(field);
            });
    }


    function updateRejectedSampleWorkflow() {
        const rejected = isRejectedSample();

        sections.forEach(section => {
            if (section.id === "sample" || section.id === "consent") {
                return;
            }
            section.classList.toggle("d-none", rejected);
        });

        navLinks.forEach(link => {
            const keepVisible =
                link.dataset.section === "sample" ||
                link.dataset.section === "consent";
            link.classList.toggle("d-none", rejected && !keepVisible);
        });

        if (rejected) {
            const sampleIndex = sections.findIndex(section => section.id === "sample");
            if (sampleIndex >= 0) {
                currentIndex = sampleIndex;
            }
        }

        updateNavigationButtons();
        updateProgress();
    }


    if (acceptReject) {
        acceptReject.addEventListener("change", function () {
            const newValue = acceptReject.value.trim();
            const previousValue = previousAcceptRejectValue.trim();
            const isNowRejected = newValue.toLowerCase() === "rejected";
            const wasPreviouslyRejected = previousValue.toLowerCase() === "rejected";

            if (isNowRejected && !wasPreviouslyRejected && previousValue) {
                const confirmed = window.confirm(
                    "You are changing this record to Rejected. " +
                    "All questionnaire information except Patient ID, " +
                    "Accept/Reject, Facility, Enrollment Date, and " +
                    "Receiving Date will be cleared. " +
                    "Do you want to continue?"
                );

                if (!confirmed) {
                    acceptReject.value = previousAcceptRejectValue;
                    resetValidation(acceptReject);
                    return;
                }

                clearRejectedSampleFields();
            }

            previousAcceptRejectValue = acceptReject.value;
            updateRejectedSampleWorkflow();
        });
    }


    // ============================================================
    // MODULE 3: QUESTIONNAIRE NAVIGATION
    // ============================================================

    function updateNavigationButtons() {
        if (isRejectedSample()) {
            if (btnSave) btnSave.classList.remove("d-none");
            if (btnNext) btnNext.classList.add("d-none");
            if (btnPrevious) btnPrevious.disabled = currentIndex === 0;
            return;
        }

        if (btnPrevious) {
            btnPrevious.disabled = currentIndex === 0;
        }

        const isLastSection = currentIndex === sections.length - 1;

        if (btnNext) {
            btnNext.classList.toggle("d-none", isLastSection);
        }

        if (btnSave) {
            btnSave.classList.toggle("d-none", !isLastSection);
        }
    }


    function showSection(index) {
        if (!sections.length) return;

        if (isRejectedSample()) {
            const allowedSections = ["sample", "consent"];
            const requestedSection = sections[index];
            if (!requestedSection || !allowedSections.includes(requestedSection.id)) {
                return;
            }
        }

        if (index < 0 || index >= sections.length) {
            return;
        }

        sections.forEach(section => section.classList.add("d-none"));

        navLinks.forEach(link => {
            link.classList.remove("active");
            link.setAttribute("aria-current", "false");
        });

        const selectedSection = sections[index];
        selectedSection.classList.remove("d-none");

        const selectedLink = navLinks.find(
            link => link.dataset.section === selectedSection.id
        );

        if (selectedLink) {
            selectedLink.classList.add("active");
            selectedLink.setAttribute("aria-current", "step");
        }

        currentIndex = index;
        updateNavigationButtons();

        window.scrollTo({
            top: 0,
            behavior: "smooth"
        });

        if (typeof updateProgress === "function") {
            updateProgress();
        }
    }


    if (btnNext) {
        btnNext.addEventListener("click", function (event) {
            event.preventDefault();
            showSection(currentIndex + 1);
        });
    }


    if (btnPrevious) {
        btnPrevious.addEventListener("click", function (event) {
            event.preventDefault();
            showSection(currentIndex - 1);
        });
    }


    navLinks.forEach(link => {
        link.addEventListener("click", function (event) {
            event.preventDefault();
            const targetId = link.dataset.section;
            const targetIndex = sections.findIndex(section => section.id === targetId);
            if (targetIndex >= 0) {
                showSection(targetIndex);
            }
        });
    });


    // ============================================================
    // MODULE 4: PATIENT ID AND FACILITY VALIDATION
    // ============================================================

    const patientIdRegex = /^(?:AMR|ISO)\/[A-Z]{3}\/\d{3,4}[A-Z]?$/;


    function isISOCase() {
        return !!patientID &&
            patientID.value.trim().toUpperCase().startsWith("ISO");
    }


    function getPatientIdFacilitySegment() {
        if (!patientID) return "";
        const parts = patientID.value.trim().toUpperCase().split("/");
        return parts.length === 3 ? parts[1] : "";
    }


    function validatePatientID() {
        if (!patientID) return true;

        const value = patientID.value.trim().toUpperCase().replace(/\s+/g, "");

        if (value === "") {
            resetValidation(patientID);
            return true;
        }

        if (!patientIdRegex.test(value)) {
            markInvalid(
                patientID,
                "Patient ID must start with AMR or ISO and follow the format AMR/FFF/DDD[L] or ISO/FFF/DDD[L], for example AMR/BSA/001 or ISO/DFM/1234A."
            );
            return false;
        }

        patientID.value = value;
        markValid(patientID);
        return true;
    }


    function validatePatientIDFacilityMatch() {
        if (
            !patientID ||
            !facilityID ||
            isEmpty(patientID.value) ||
            isEmpty(facilityID.value)
        ) {
            return true;
        }

        if (!patientIdRegex.test(patientID.value.trim().toUpperCase())) {
            return false;
        }

        const patientFacilityCode = getPatientIdFacilitySegment();
        const selectedFacilityCode = facilityID.value.trim().toUpperCase();

        if (patientFacilityCode !== selectedFacilityCode) {
            markInvalid(
                patientID,
                `Patient ID facility code "${patientFacilityCode}" does not match the selected FacilityID "${selectedFacilityCode}".`
            );
            return false;
        }

        markValid(patientID);
        return true;
    }


    // ============================================================
    // ISO-SPECIFIC FIELD RULES
    // ============================================================

    function updateISOFieldRules() {
        const iso = isISOCase();

        const isoFields = [
            sedated,
            invasiveDevice,
            weightKg,
            temperatureC,
            oxygenSaturation,
            bpSystolic,
            bpDiastolic,
            pulse,
            currentAntibioticUse,
            antibioticDays,
            antibioticsUsed,
            surgeryForInfection,
            infectionSite
        ];

        isoFields.forEach(field => {
            if (!field) return;
            const container = getFieldContainer(field);

            if (container) {
                container.classList.toggle("d-none", iso);
                container.style.display = iso ? "none" : "";
            }

            if (iso) {
                clearControl(field);
                resetValidation(field);
            }
        });

        if (consentSample) {
            const consentContainer = getFieldContainer(consentSample);

            if (iso) {
                consentSample.value = "Unknown";
                resetValidation(consentSample);

                if (consentContainer) {
                    consentContainer.classList.add("d-none");
                    consentContainer.style.display = "none";
                }
            } else if (consentContainer) {
                consentContainer.classList.remove("d-none");
                consentContainer.style.display = "";
            }
        }
    }


    function updatePatientIDRules() {
        if (!patientID) return;

        patientID.value = patientID.value.toUpperCase().replace(/\s+/g, "");

        if (isRejectedSample()) {
            if (presumptiveIdContainer) {
                presumptiveIdContainer.classList.add("d-none");
                presumptiveIdContainer.style.display = "none";
            }
            if (presumptiveIdInput) {
                clearControl(presumptiveIdInput);
                resetValidation(presumptiveIdInput);
            }
            updateProgress();
            return;
        }

        const iso = isISOCase();

        if (presumptiveIdContainer) {
            presumptiveIdContainer.classList.toggle("d-none", !iso);
            presumptiveIdContainer.style.display = iso ? "" : "none";
        }

        if (!iso && presumptiveIdInput) {
            clearControl(presumptiveIdInput);
            resetValidation(presumptiveIdInput);
        }

        if (communityFields) {
            communityFields.classList.toggle("d-none", iso);
            if (iso) clearContainer(communityFields);
        }

        if (riskFactorFields) {
            riskFactorFields.classList.toggle("d-none", iso);
            if (iso) clearContainer(riskFactorFields);
        }

        validatePatientIDFacilityMatch();
        updateISOFieldRules();
        updatePregnancy();
        updatePatientType();
        updateAntibiotics();
        updateProgress();
    }


    if (patientID) {
        patientID.addEventListener("input", function () {
            patientID.value = patientID.value.toUpperCase().replace(/\s+/g, "");
            validatePatientID();
            validatePatientIDFacilityMatch();
            updatePatientIDRules();
        });

        patientID.addEventListener("blur", function () {
            validatePatientID();
            validatePatientIDFacilityMatch();
        });
    }


    // ============================================================
    // MODULE 5: CONDITIONAL QUESTIONNAIRE FIELDS
    // ============================================================

    function getAgeInYearsForEligibility() {
        if (!age || isEmpty(age.value)) return null;
        const num = Number(age.value);
        if (!Number.isFinite(num) || num < 0) return null;

        const unit = ageUnit?.value?.trim().toLowerCase();
        if (unit === "days") return num / 365.25;
        if (unit === "months") return num / 12;
        return num;
    }


    function updatePregnancy() {
        if (!pregnancyContainer) return;

        const isFemale = !!sex && sex.value.trim().toLowerCase() === "female";
        const ageYears = getAgeInYearsForEligibility();

        const eligible =
            !isRejectedSample() &&
            !isISOCase() &&
            isFemale &&
            ageYears !== null &&
            ageYears >= 12 &&
            ageYears <= 50;

        pregnancyContainer.classList.toggle("d-none", !eligible);
        pregnancyContainer.style.display = eligible ? "" : "none";

        if (!eligible) {
            clearContainer(pregnancyContainer);
        }
    }


    function validatePregnancyConsistency() {
        if (!pregnant || isRejectedSample() || isISOCase()) {
            resetValidation(pregnant);
            return true;
        }

        const pregVal = pregnant.value.trim().toLowerCase();
        if (isEmpty(pregVal)) {
            resetValidation(pregnant);
            return true;
        }

        const isFemale = !!sex && sex.value.trim().toLowerCase() === "female";
        const ageYears = getAgeInYearsForEligibility();

        if (pregVal === "yes" && !isFemale) {
            markInvalid(pregnant, "Pregnancy status cannot be Yes for non-female participants.");
            return false;
        }

        if (pregVal === "yes" && ageYears !== null && (ageYears < 12 || ageYears > 50)) {
            markInvalid(pregnant, "Pregnancy status is only applicable to female participants aged 12 to 50 years.");
            return false;
        }

        markValid(pregnant);
        return true;
    }


    function updatePatientType() {
        if (!patientType) return;

        const typeValue = patientType.value.trim().toLowerCase();
        const inpatient = typeValue === "inpatient";
        const outpatient = typeValue === "outpatient";

        const conditionalContainers = [
            hospitalWardContainer,
            icuContainer,
            wardAdmissionContainer,
            infectionPresentAtAdmissionContainer
        ];

        conditionalContainers.forEach(container => {
            if (!container) return;
            container.classList.toggle("d-none", !inpatient);
            container.style.display = inpatient ? "" : "none";
        });

        if (!infectionPresentAtAdmissionContainer && infectionPresentAtAdmission) {
            const container = getFieldContainer(infectionPresentAtAdmission);
            if (container) {
                container.classList.toggle("d-none", !inpatient);
                container.style.display = inpatient ? "" : "none";
            }
        }

        if (hospitalLocation) {
            const options = Array.from(hospitalLocation.options);

            if (outpatient) {
                options.forEach(opt => {
                    const optVal = opt.value.trim().toUpperCase();
                    opt.disabled = optVal !== "" && optVal !== "OPD";
                });

                const opdOption = options.find(opt => opt.value.trim().toUpperCase() === "OPD");
                if (opdOption) {
                    hospitalLocation.value = opdOption.value;
                }
                resetValidation(hospitalLocation);

            } else if (inpatient) {
                options.forEach(opt => {
                    const optVal = opt.value.trim().toUpperCase();
                    opt.disabled = optVal === "OPD";
                });

                if (hospitalLocation.value.trim().toUpperCase() === "OPD") {
                    hospitalLocation.selectedIndex = 0;
                }
                resetValidation(hospitalLocation);

            } else {
                options.forEach(opt => {
                    opt.disabled = false;
                });
                resetValidation(hospitalLocation);
            }
        }

        if (!inpatient) {
            clearControl(hospitalWard);
            clearControl(icuAdmission);
            clearControl(wardAdmission);
            clearControl(infectionPresentAtAdmission);
        }

        updateProgress();
    }


    function validatePatientTypeHospitalLocation() {
        if (isRejectedSample() || !patientType || !hospitalLocation) {
            return true;
        }

        const typeValue = patientType.value.trim().toLowerCase();
        const locationValue = hospitalLocation.value.trim().toUpperCase();

        if (!typeValue || !locationValue) {
            resetValidation(hospitalLocation);
            return true;
        }

        if (typeValue === "outpatient" && locationValue !== "OPD") {
            markInvalid(
                hospitalLocation,
                'Outpatient cases must have Hospital Location set to "OPD".'
            );
            return false;
        }

        if (typeValue === "inpatient" && locationValue === "OPD") {
            markInvalid(
                hospitalLocation,
                'Inpatient cases cannot have Hospital Location set to "OPD".'
            );
            return false;
        }

        markValid(hospitalLocation);
        return true;
    }


    function updateAntibiotics() {
        if (isISOCase()) {
            return;
        }

        const takingAntibiotics =
            !!currentAntibioticUse &&
            currentAntibioticUse.value.trim().toLowerCase() === "yes";

        if (currentAntibioticUse) {
            const useContainer = getFieldContainer(currentAntibioticUse);
            if (useContainer) {
                useContainer.classList.remove("d-none");
                useContainer.style.display = "";
            }
        }

        if (antibioticDetailsContainer) {
            antibioticDetailsContainer.classList.toggle("d-none", !takingAntibiotics);
            antibioticDetailsContainer.style.display = takingAntibiotics ? "" : "none";
        }

        [antibioticDaysContainer, antibioticsUsedContainer].forEach(container => {
            if (!container) return;
            container.classList.toggle("d-none", !takingAntibiotics);
            container.style.display = takingAntibiotics ? "" : "none";
        });

        if (!takingAntibiotics) {
            clearControl(antibioticDays);
            clearControl(antibioticsUsed);
        }
    }


    if (sex) {
        sex.addEventListener("change", function () {
            updatePregnancy();
            validatePregnancyConsistency();
        });
    }

    if (pregnant) {
        pregnant.addEventListener("change", validatePregnancyConsistency);
    }

    if (patientType) {
        patientType.addEventListener("change", updatePatientType);
    }

    if (hospitalLocation) {
        hospitalLocation.addEventListener("change", validatePatientTypeHospitalLocation);
    }

    if (hospitalWard) {
        const handleHospitalWardChange = function () {
            if (!isEmpty(hospitalWard.value)) {
                markValid(hospitalWard);
            } else {
                resetValidation(hospitalWard);
            }
            updateProgress();
        };

        hospitalWard.addEventListener("change", handleHospitalWardChange);
        hospitalWard.addEventListener("input", handleHospitalWardChange);
    }

    if (currentAntibioticUse) {
        currentAntibioticUse.addEventListener("change", updateAntibiotics);
    }


    // ============================================================
    // MODULE 6: SKIN INFECTION SYMPTOMS
    // ============================================================

    const skinSymptomCheckboxes = Array.from(
        document.querySelectorAll(".skin-symptom")
    );

    const skinInfectionSymptoms = document.getElementById("SkinInfectionSymptoms");


    function isSkinRelatedPresentation() {
        if (!clinicalPresentation) return false;
        const value = clinicalPresentation.value.trim().replace(/\s+/g, " ").toLowerCase();
        return value === "surgical site infection" ||
            value === "skin and soft tissue infection";
    }


    function getSkinSymptomsContainer() {
        const namedContainer = document.getElementById("skinInfectionSymptomsContainer");
        if (namedContainer) return namedContainer;
        if (skinInfectionSymptoms) return getFieldContainer(skinInfectionSymptoms);
        if (skinSymptomCheckboxes.length > 0) return getFieldContainer(skinSymptomCheckboxes[0]);
        return null;
    }


    function updateSkinInfectionSymptomsVisibility() {
        const container = getSkinSymptomsContainer();
        if (!container) return;

        const applicable = !isRejectedSample() && !isISOCase() && isSkinRelatedPresentation();

        container.classList.toggle("d-none", !applicable);
        container.style.display = applicable ? "" : "none";

        if (!applicable) {
            skinSymptomCheckboxes.forEach(checkbox => {
                checkbox.checked = false;
            });

            if (skinInfectionSymptoms) {
                skinInfectionSymptoms.value = "";
                resetValidation(skinInfectionSymptoms);
            }
        }
    }


    function updateSkinInfectionSymptoms() {
        if (!skinInfectionSymptoms) return;

        if (!isSkinRelatedPresentation() || isRejectedSample() || isISOCase()) {
            skinInfectionSymptoms.value = "";
            return;
        }

        const selected = skinSymptomCheckboxes
            .filter(checkbox => checkbox.checked)
            .map(checkbox => checkbox.value);

        skinInfectionSymptoms.value = selected.join(";");
    }


    function restoreSkinInfectionSymptoms() {
        if (!skinInfectionSymptoms) return;

        if (!isSkinRelatedPresentation() || isNotApplicableValue(skinInfectionSymptoms.value)) {
            skinSymptomCheckboxes.forEach(checkbox => {
                checkbox.checked = false;
            });
            if (isNotApplicableValue(skinInfectionSymptoms.value)) {
                skinInfectionSymptoms.value = "";
            }
            return;
        }

        const savedSymptoms = skinInfectionSymptoms.value
            .split(";")
            .map(symptom => symptom.trim())
            .filter(symptom => symptom !== "" && !isNotApplicableValue(symptom));

        const selectedSymptoms = new Set(savedSymptoms);

        skinSymptomCheckboxes.forEach(checkbox => {
            checkbox.checked = selectedSymptoms.has(checkbox.value.trim());
        });
    }


    skinSymptomCheckboxes.forEach(checkbox => {
        checkbox.addEventListener("change", updateSkinInfectionSymptoms);
    });


    if (clinicalPresentation) {
        clinicalPresentation.addEventListener("change", function () {
            updateSkinInfectionSymptomsVisibility();
            updateSkinInfectionSymptoms();
            validateClinicalPresentationInfectionSource();
        });
    }


    if (infectionSource) {
        infectionSource.addEventListener("change", validateClinicalPresentationInfectionSource);
    }


    // ============================================================
    // CLINICAL PRESENTATION <-> INFECTION SOURCE
    // ============================================================

    function validateClinicalPresentationInfectionSource() {
        if (isRejectedSample()) {
            if (infectionSource) resetValidation(infectionSource);
            return true;
        }

        if (!clinicalPresentation || !infectionSource) {
            return true;
        }

        const presentation = clinicalPresentation.value.trim().toLowerCase();
        const source = infectionSource.value.trim().toLowerCase();

        if (!presentation || !source) {
            resetValidation(infectionSource);
            return true;
        }

        const sourceRules = {
            "blood infection": ["blood"],
            "surgical site infection": ["pus", "wound/skin swab"],
            "skin and soft tissue infection": ["pus", "wound/skin swab"],
            "throat infection": ["throat swab"],
            "other": ["other"],
            "unknown": ["unknown"]
        };

        if (presentation === "respiratory infection" || !sourceRules[presentation]) {
            resetValidation(infectionSource);
            return true;
        }

        const allowedSources = sourceRules[presentation];

        if (!allowedSources.includes(source)) {
            markInvalid(
                infectionSource,
                `Infection Source must be ${allowedSources
                    .map(value =>
                        value
                            .split(" ")
                            .map(word => word.charAt(0).toUpperCase() + word.slice(1))
                            .join(" ")
                    )
                    .join(" or ")
                } when Clinical Presentation is ${clinicalPresentation.value}.`
            );
            return false;
        }

        markValid(infectionSource);
        return true;
    }

    updateSkinInfectionSymptomsVisibility();
    updateSkinInfectionSymptoms();
    validateClinicalPresentationInfectionSource();


    // ============================================================
    // MODULE 7: COUNTY–SUBCOUNTY CASCADE
    // ============================================================

    let subCountyRequestNumber = 0;


    async function loadSubCounties(preserveSelection = false) {
        if (!county || !subCounty) return;

        const selectedCounty = county.value.trim();
        const previousSubCounty = preserveSelection ? subCounty.value : "";
        const requestNumber = ++subCountyRequestNumber;

        subCounty.innerHTML = '<option value="">-- Select Sub-County --</option>';
        subCounty.disabled = true;

        if (isEmpty(selectedCounty)) {
            subCounty.disabled = false;
            return;
        }

        try {
            const url = county.dataset.subcountyUrl;
            if (!url) {
                throw new Error("Sub-county URL is missing from the County dropdown.");
            }

            const response = await fetch(
                `${url}?county=${encodeURIComponent(selectedCounty)}`,
                {
                    method: "GET",
                    headers: { "Accept": "application/json" }
                }
            );

            if (!response.ok) {
                throw new Error(`Sub-county request failed: HTTP ${response.status}`);
            }

            const data = await response.json();
            if (requestNumber !== subCountyRequestNumber) return;

            if (!Array.isArray(data)) {
                throw new Error("The sub-county endpoint did not return a JSON array.");
            }

            data.forEach(item => {
                const option = document.createElement("option");
                const value = item.value ?? item.Value ?? "";
                const text = item.text ?? item.Text ?? "";
                option.value = String(value);
                option.textContent = String(text);
                subCounty.appendChild(option);
            });

            if (
                previousSubCounty &&
                Array.from(subCounty.options).some(option => option.value === previousSubCounty)
            ) {
                subCounty.value = previousSubCounty;
            }
        } catch (error) {
            if (requestNumber === subCountyRequestNumber) {
                console.error("County-to-subcounty cascade failed:", error);
                const option = document.createElement("option");
                option.value = "";
                option.textContent = "Unable to load sub-counties: " + error.message;
                subCounty.appendChild(option);
            }
        } finally {
            if (requestNumber === subCountyRequestNumber) {
                subCounty.disabled = false;
            }
        }
    }


    if (county && subCounty) {
        county.addEventListener("change", function () {
            loadSubCounties(false);
        });
        loadSubCounties(true);
    }


    // ============================================================
    // MODULE 8: DATE VALIDATION
    // ============================================================

    function validateNotFuture(element) {
        if (!element || isEmpty(element.value)) {
            if (element) resetValidation(element);
            return true;
        }

        const selectedDate = parseDate(element.value);
        if (!selectedDate || selectedDate > today()) {
            markInvalid(element, "Date cannot be in the future.");
            return false;
        }

        markValid(element);
        return true;
    }


    function validateReceivingDate() {
        if (!receivingDate || isEmpty(receivingDate.value)) {
            if (receivingDate) resetValidation(receivingDate);
            return true;
        }

        const receiving = parseDate(receivingDate.value);
        if (!receiving || receiving > today()) {
            markInvalid(receivingDate, "Receiving date cannot be in the future.");
            return false;
        }

        if (enrollmentDate && !isEmpty(enrollmentDate.value)) {
            const enrollment = parseDate(enrollmentDate.value);
            if (enrollment && receiving < enrollment) {
                markInvalid(
                    receivingDate,
                    "Receiving date cannot be earlier than enrollment date."
                );
                return false;
            }
        }

        markValid(receivingDate);
        return true;
    }


    function validateDOBAgainstEnrollment() {
        if (
            !dateOfBirth ||
            !enrollmentDate ||
            isEmpty(dateOfBirth.value) ||
            isEmpty(enrollmentDate.value)
        ) {
            return true;
        }

        const dob = parseDate(dateOfBirth.value);
        const enrollment = parseDate(enrollmentDate.value);
        if (!dob || !enrollment) return true;

        if (dob > enrollment) {
            markInvalid(
                dateOfBirth,
                "Date of birth cannot be later than enrollment date."
            );
            return false;
        }

        markValid(dateOfBirth);
        return true;
    }


    function validateInfectionOnsetAgainstPresentation() {
        if (!infectionPresentAtAdmission || !infectionOnset || !presentationDate) {
            return true;
        }

        if (isEmpty(infectionOnset.value)) {
            resetValidation(infectionOnset);
            return true;
        }

        const onset = parseDate(infectionOnset.value);
        if (!onset || onset > today()) {
            markInvalid(infectionOnset, "Date cannot be in the future.");
            return false;
        }

        const admissionStatus = infectionPresentAtAdmission.value.trim().toLowerCase();
        if (isEmpty(admissionStatus) || isEmpty(presentationDate.value)) {
            resetValidation(infectionOnset);
            return true;
        }

        const presentation = parseDate(presentationDate.value);
        if (!presentation) return true;

        if (admissionStatus === "yes" && onset > presentation) {
            markInvalid(
                infectionOnset,
                "Infection onset must be on or before the Presentation Date when infection was present at admission."
            );
            return false;
        }

        if (admissionStatus === "no" && onset < presentation) {
            markInvalid(
                infectionOnset,
                "Infection onset must be on or after the Presentation Date when infection was not present at admission."
            );
            return false;
        }

        resetValidation(infectionOnset);
        return true;
    }


    function checkHAIClassificationWarning() {
        const warning = document.getElementById("haiClassificationWarning");
        if (!warning) return;

        warning.classList.add("d-none");
        warning.textContent = "";

        if (!haiCai || !infectionOnset || !presentationDate) return;
        if (isEmpty(infectionOnset.value) || isEmpty(presentationDate.value)) return;
        if (haiCai.value.trim().toUpperCase() !== "HAI") return;

        const onset = parseDate(infectionOnset.value);
        const presentation = parseDate(presentationDate.value);
        if (!onset || !presentation) return;

        if (onset < presentation) {
            warning.textContent =
                "Warning: Infection onset was before the Presentation Date. " +
                "Please verify whether this infection should be classified as HAI.";
            warning.classList.remove("d-none");
        }
    }


    function validateAllDates() {
        let valid = true;

        const dateFields = isRejectedSample()
            ? [enrollmentDate, receivingDate]
            : [enrollmentDate, receivingDate, presentationDate, infectionOnset];

        dateFields.forEach(element => {
            if (!validateNotFuture(element)) {
                valid = false;
            }
        });

        if (!validateReceivingDate()) {
            valid = false;
        }

        if (!isRejectedSample()) {
            if (!validateDOBAgainstEnrollment()) {
                valid = false;
            }
            if (!validateInfectionOnsetAgainstPresentation()) {
                valid = false;
            }
        }

        return valid;
    }


    [enrollmentDate, presentationDate, infectionOnset].forEach(element => {
        if (!element) return;

        element.addEventListener("change", function () {
            validateAllDates();
            checkHAIClassificationWarning();

            if (element === enrollmentDate) {
                validateAgeConsistency();
            }
        });
    });


    if (infectionPresentAtAdmission) {
        infectionPresentAtAdmission.addEventListener("change", function () {
            validateInfectionOnsetAgainstPresentation();
            checkHAIClassificationWarning();
        });
    }


    if (haiCai) {
        haiCai.addEventListener("change", checkHAIClassificationWarning);
    }


    if (receivingDate) {
        receivingDate.addEventListener("change", validateAllDates);
    }


    if (dateOfBirth) {
        dateOfBirth.addEventListener("change", function () {
            validateDOBAgainstEnrollment();
            validateAgeConsistency();
        });
    }


    // ============================================================
    // MODULE 9: AGE VALIDATION
    // ============================================================

    function calculateAgeInMonths(dobValue, referenceValue) {
        const dob = parseDate(dobValue);
        const referenceDate = parseDate(referenceValue);
        if (!dob || !referenceDate || dob > referenceDate) return null;

        let months =
            (referenceDate.getFullYear() - dob.getFullYear()) * 12 +
            referenceDate.getMonth() -
            dob.getMonth();

        if (referenceDate.getDate() < dob.getDate()) {
            months--;
        }

        return Math.max(0, months);
    }


    function calculateAgeInDays(dobValue, referenceValue) {
        const dob = parseDate(dobValue);
        const referenceDate = parseDate(referenceValue);
        if (!dob || !referenceDate || dob > referenceDate) return null;

        const millisecondsPerDay = 1000 * 60 * 60 * 24;
        return Math.floor((referenceDate.getTime() - dob.getTime()) / millisecondsPerDay);
    }


    function validateAge() {
        if (isRejectedSample()) {
            resetValidation(age);
            return true;
        }

        if (!age || isEmpty(age.value)) {
            if (age) resetValidation(age);
            return true;
        }

        const value = Number(age.value);
        if (!Number.isFinite(value) || !Number.isInteger(value) || value < 0) {
            markInvalid(age, "Enter a valid non-negative whole-number age.");
            return false;
        }

        markValid(age);
        return true;
    }


    function validateAgeConsistency() {
        if (isRejectedSample()) {
            resetValidation(age);
            resetValidation(ageUnit);
            return true;
        }

        if (!dateOfBirth || !enrollmentDate || !age || !ageUnit) {
            return true;
        }

        if (!isEmpty(age.value) && isEmpty(ageUnit.value)) {
            markInvalid(ageUnit, "Age unit must be selected.");
            return false;
        }

        if (isEmpty(age.value) || isEmpty(ageUnit.value)) {
            return true;
        }

        if (isEmpty(dateOfBirth.value) || isEmpty(enrollmentDate.value)) {
            return true;
        }

        const dob = parseDate(dateOfBirth.value);
        const enrollment = parseDate(enrollmentDate.value);
        if (!dob || !enrollment) return true;

        if (dob > enrollment) {
            markInvalid(dateOfBirth, "Date of birth cannot be later than enrollment date.");
            resetValidation(age);
            return false;
        }

        const enteredAge = Number(age.value);
        if (!Number.isFinite(enteredAge) || !Number.isInteger(enteredAge) || enteredAge < 0) {
            return false;
        }

        const unit = ageUnit.value.trim().toLowerCase();
        let expectedAge;
        let unitLabel;

        switch (unit) {
            case "days":
                expectedAge = calculateAgeInDays(dateOfBirth.value, enrollmentDate.value);
                unitLabel = "days";
                break;

            case "months":
                expectedAge = calculateAgeInMonths(dateOfBirth.value, enrollmentDate.value);
                unitLabel = "months";
                break;

            case "years": {
                const completedMonths = calculateAgeInMonths(dateOfBirth.value, enrollmentDate.value);
                expectedAge = completedMonths === null ? null : Math.floor(completedMonths / 12);
                unitLabel = "years";
                break;
            }

            default:
                return true;
        }

        if (expectedAge === null || expectedAge === undefined) {
            return true;
        }

        if (enteredAge !== expectedAge) {
            markInvalid(
                age,
                "Age does not match the Date of Birth and Enrollment Date. " +
                `Calculated age: ${expectedAge} ${unitLabel}.`
            );
            return false;
        }

        resetValidation(age);
        resetValidation(ageUnit);

        const ageFeedback = document.getElementById("ageValidationFeedback");
        if (ageFeedback) {
            ageFeedback.textContent = "";
        }

        return true;
    }


    if (age) {
        age.addEventListener("input", function () {
            validateAge();
            validateAgeConsistency();
            updatePregnancy();
            validatePregnancyConsistency();
        });

        age.addEventListener("change", function () {
            validateAgeConsistency();
            validatePregnancyConsistency();
        });
    }


    if (ageUnit) {
        ageUnit.addEventListener("change", function () {
            validateAgeConsistency();
            updatePregnancy();
            validatePregnancyConsistency();
        });
    }


    // ============================================================
    // MILITARY / OCCUPATION VALIDATION
    // ============================================================

    function validateMilitaryOccupation() {
        if (isRejectedSample() || isISOCase()) {
            resetValidation(milciv);
            resetValidation(occupationCategory);
            return true;
        }

        if (!milciv || !occupationCategory) {
            return true;
        }

        const militaryStatus = milciv.value.trim().toLowerCase();
        const occupation = occupationCategory.value.trim().toLowerCase();

        if (militaryStatus === "" || occupation === "") {
            resetValidation(milciv);
            resetValidation(occupationCategory);
            return true;
        }

        if (militaryStatus === "military" && occupation !== "soldier") {
            markInvalid(
                occupationCategory,
                'Military participants must have "Soldier" selected as the occupation category.'
            );
            resetValidation(milciv);
            return false;
        }

        if (occupation === "soldier" && militaryStatus !== "military") {
            markInvalid(
                milciv,
                'Participants with "Soldier" selected as their occupation must be classified as Military.'
            );
            resetValidation(occupationCategory);
            return false;
        }

        resetValidation(milciv);
        resetValidation(occupationCategory);
        return true;
    }


    if (milciv) {
        milciv.addEventListener("change", validateMilitaryOccupation);
    }

    if (occupationCategory) {
        occupationCategory.addEventListener("change", validateMilitaryOccupation);
    }


    // ============================================================
    // MODULE 10: NUMERIC FIELD VALIDATION
    // ============================================================

    const numericRules = [
        {
            id: "HouseholdSize",
            min: 1,
            max: 100,
            message: "Household size must be between 1 and 100."
        },
        {
            id: "WeightKg",
            min: 0.1,
            max: 300,
            message: "Weight must be between 0.1 and 300 kg."
        },
        {
            id: "TemperatureC",
            min: 30,
            max: 45,
            message: "Temperature must be between 30 and 45 °C."
        },
        {
            id: "BPSystolic",
            min: 30,
            max: 300,
            message: "Enter a valid systolic blood pressure."
        },
        {
            id: "BPDiastolic",
            min: 10,
            max: 200,
            message: "Enter a valid diastolic blood pressure."
        },
        {
            id: "Pulse",
            min: 0,
            max: 300,
            message: "Pulse must be between 0 and 300 beats per minute."
        },
        {
            id: "OxygenSaturation",
            min: 0,
            max: 100,
            message: "Oxygen saturation must be between 0 and 100."
        }
    ];


    function validateNumeric(rule) {
        if (isRejectedSample()) {
            return true;
        }

        const element = document.getElementById(rule.id);

        const isoExcludedNumericFields = [
            "WeightKg",
            "TemperatureC",
            "BPSystolic",
            "BPDiastolic",
            "Pulse",
            "OxygenSaturation"
        ];

        if (isISOCase() && isoExcludedNumericFields.includes(rule.id)) {
            resetValidation(element);
            return true;
        }

        if (!element || isEmpty(element.value)) {
            if (element) resetValidation(element);
            return true;
        }

        const value = Number(element.value);
        if (!Number.isFinite(value) || value < rule.min || value > rule.max) {
            markInvalid(element, rule.message);
            return false;
        }

        markValid(element);
        return true;
    }


    function validateBloodPressureOrder() {
        if (isRejectedSample() || isISOCase() || !bpSystolic || !bpDiastolic) {
            return true;
        }

        if (isEmpty(bpSystolic.value) || isEmpty(bpDiastolic.value)) {
            return true;
        }

        const sys = Number(bpSystolic.value);
        const dia = Number(bpDiastolic.value);

        if (!Number.isFinite(sys) || !Number.isFinite(dia)) {
            return true;
        }

        if (sys <= dia) {
            markInvalid(
                bpSystolic,
                "Systolic blood pressure must be greater than diastolic blood pressure."
            );
            return false;
        }

        markValid(bpSystolic);
        markValid(bpDiastolic);
        return true;
    }


    function validateAllNumbers() {
        let valid = true;

        numericRules.forEach(rule => {
            if (!validateNumeric(rule)) {
                valid = false;
            }
        });

        if (!validateBloodPressureOrder()) {
            valid = false;
        }

        return valid;
    }


    numericRules.forEach(rule => {
        const element = document.getElementById(rule.id);
        if (!element) return;

        element.addEventListener("input", function () {
            validateNumeric(rule);
            if (rule.id === "BPSystolic" || rule.id === "BPDiastolic") {
                validateBloodPressureOrder();
            }
        });
    });


    function validateAntibioticDays() {
        if (isRejectedSample() || isISOCase()) {
            resetValidation(antibioticDays);
            return true;
        }

        if (
            !currentAntibioticUse ||
            currentAntibioticUse.value.trim().toLowerCase() !== "yes"
        ) {
            return true;
        }

        if (!antibioticDays) {
            return true;
        }

        const value = antibioticDays.value.trim();
        const days = Number(value);

        if (value === "" || !Number.isFinite(days) || !Number.isInteger(days) || days < 0) {
            markInvalid(antibioticDays, "Enter a non-negative whole number of days.");
            return false;
        }

        markValid(antibioticDays);
        return true;
    }


    if (antibioticDays) {
        antibioticDays.addEventListener("input", validateAntibioticDays);
    }


    // ============================================================
    // MODULE 11: FACILITY VALIDATION
    // ============================================================

    function validateFacility() {
        if (!facilityID || isEmpty(facilityID.value)) {
            if (facilityID) resetValidation(facilityID);
            return true;
        }

        const validOption = Array.from(facilityID.options).some(
            option => option.value === facilityID.value
        );

        if (!validOption) {
            markInvalid(facilityID, "Select a valid facility.");
            return false;
        }

        markValid(facilityID);
        return true;
    }


    if (facilityID) {
        facilityID.addEventListener("change", function () {
            validateFacility();
            validatePatientIDFacilityMatch();
        });
    }


    // ============================================================
    // MODULE 12: FORM PROGRESS AND SECTION COMPLETION
    // ============================================================

    const requiredFields = {
        sample: [
            "PatientID",
            "AcceptReject",
            "FacilityID",
            "EnrollmentDate",
            "ReceivingDate"
        ],
        demographics: [
            "Age",
            "AgeUnit",
            "Sex",
            "ResidenceCounty"
        ],
        risk: [],
        clinical: [
            "ClinicalPresentation",
            "PatientType",
            "PresentationDate"
        ],
        assessment: [],
        antibiotics: [],
        consent: [
            "ConsentSample"
        ]
    };


    progressReady = true;


    function isElementVisible(element) {
        if (!element) return false;

        let current = element;
        while (current && current !== document.documentElement) {
            if (current.classList?.contains("d-none")) return false;
            if (current.style && current.style.display === "none") return false;
            current = current.parentElement;
        }

        return true;
    }


    function fieldHasValue(fieldId) {
        const field = document.getElementById(fieldId);
        if (!field || field.disabled || !isElementVisible(field)) {
            return true;
        }

        if (field.type === "checkbox" || field.type === "radio") {
            const group = Array.from(
                patientForm?.querySelectorAll(`[name="${field.name}"]`) || []
            );
            return group.some(item => item.checked);
        }

        return !isEmpty(field.value);
    }


    function getApplicableRequiredFieldIds(sectionId) {
        if (isRejectedSample()) {
            return sectionId === "sample"
                ? ["PatientID", "AcceptReject", "FacilityID", "EnrollmentDate", "ReceivingDate"]
                : [];
        }

        const iso = isISOCase();
        const ids = [...(requiredFields[sectionId] || [])];

        if (sectionId === "sample" && iso) {
            ids.push("PresumptiveID");
        }

        if (sectionId === "demographics") {
            if (iso) {
                return ids.filter(id => id !== "ResidenceCounty");
            }
            if (county && !isEmpty(county.value)) {
                ids.push("ResidenceSubCounty");
            }
        }

        if (sectionId === "clinical") {
            const isInpatient =
                !!patientType &&
                patientType.value.trim().toLowerCase() === "inpatient";

            if (isInpatient && !iso) {
                ids.push("HospitalWard");
            }
        }

        if (sectionId === "antibiotics" && !iso) {
            const takingAntibiotics =
                !!currentAntibioticUse &&
                currentAntibioticUse.value.trim().toLowerCase() === "yes";

            if (takingAntibiotics) {
                ids.push("AntibioticDays", "AntibioticsUsed");
            }
        }

        if (sectionId === "consent" && iso) {
            return [];
        }

        return ids.filter(id => {
            const field = document.getElementById(id);
            return field && !field.disabled && isElementVisible(field);
        });
    }


    function isSectionComplete(sectionId) {
        if (isRejectedSample() && sectionId !== "sample") {
            return true;
        }

        if (isISOCase() && (sectionId === "consent" || sectionId === "risk")) {
            return true;
        }

        const fieldIds = getApplicableRequiredFieldIds(sectionId);

        if (!fieldIds.length) {
            const sectionEl = document.getElementById(sectionId);
            if (!sectionEl) return true;

            const inputs = Array.from(
                sectionEl.querySelectorAll("input, select, textarea")
            ).filter(el =>
                el.type !== "hidden" &&
                !el.disabled &&
                isElementVisible(el)
            );

            return inputs.some(el =>
                (el.type === "checkbox" || el.type === "radio")
                    ? el.checked
                    : !isEmpty(el.value)
            );
        }

        return fieldIds.every(fieldHasValue);
    }


    function updateProgress() {
        if (!progressReady) return;

        sections.forEach(section => {
            const link = navLinks.find(item => item.dataset.section === section.id);
            if (!link) return;

            const complete = isSectionComplete(section.id);
            link.classList.toggle("completed", complete);

            const icon = link.querySelector(".section-status-icon, .completion-icon");
            if (icon) {
                icon.textContent = complete ? "✓" : "○";
            }
        });

        const progressBar = document.getElementById("formProgressBar");
        const progressText = document.getElementById("formProgressText");

        const allRequiredIds = sections.flatMap(section =>
            getApplicableRequiredFieldIds(section.id)
        );

        const completedCount = allRequiredIds.filter(fieldHasValue).length;

        const percentage = allRequiredIds.length
            ? Math.round((completedCount / allRequiredIds.length) * 100)
            : 0;

        if (progressBar) {
            progressBar.style.width = `${percentage}%`;
            progressBar.setAttribute("aria-valuenow", String(percentage));
            progressBar.setAttribute("aria-valuetext", `${percentage}% complete`);
        }

        if (progressText) {
            progressText.textContent = `${percentage}% complete`;
        }
    }


    // ============================================================
    // MODULE 13: COMPLETE FORM VALIDATION
    // ============================================================

    function validateRequiredFields() {
        let valid = true;

        if (isRejectedSample()) {
            const rejectedRequiredIds = [
                "PatientID",
                "AcceptReject",
                "FacilityID",
                "EnrollmentDate",
                "ReceivingDate"
            ];

            rejectedRequiredIds.forEach(id => {
                const field = document.getElementById(id);
                if (!field || field.disabled || !isElementVisible(field)) return;

                if (isEmpty(field.value)) {
                    markInvalid(field, "This field is required.");
                    valid = false;
                } else {
                    markValid(field);
                }
            });

            return valid;
        }

        const requiredIds = [
            "PatientID",
            "AcceptReject",
            "FacilityID",
            "EnrollmentDate",
            "ReceivingDate",
            "Age",
            "AgeUnit",
            "Sex",
            "ResidenceCounty",
            "ClinicalPresentation",
            "PatientType",
            "PresentationDate",
            "ConsentSample"
        ];

        requiredIds.forEach(id => {
            const field = document.getElementById(id);
            if (!field || field.disabled || !isElementVisible(field)) return;

            if (isISOCase() && (id === "ConsentSample" || id === "ResidenceCounty")) {
                return;
            }

            if (isEmpty(field.value)) {
                markInvalid(field, "This field is required.");
                valid = false;
            } else {
                markValid(field);
            }
        });

        const isInpatient =
            !!patientType &&
            patientType.value.trim().toLowerCase() === "inpatient";

        if (
            isInpatient &&
            !isISOCase() &&
            hospitalWard &&
            !hospitalWard.disabled &&
            isElementVisible(hospitalWard) &&
            isEmpty(hospitalWard.value)
        ) {
            markInvalid(hospitalWard, "Hospital ward is required for an inpatient.");
            valid = false;
        }

        if (
            county &&
            subCounty &&
            !isEmpty(county.value) &&
            isElementVisible(subCounty) &&
            isEmpty(subCounty.value)
        ) {
            markInvalid(subCounty, "Select a sub-county.");
            valid = false;
        }

        if (isISOCase() && presumptiveIdInput && isEmpty(presumptiveIdInput.value)) {
            markInvalid(presumptiveIdInput, "Presumptive ID is required for ISO cases.");
            valid = false;
        }

        if (
            !isISOCase() &&
            currentAntibioticUse &&
            currentAntibioticUse.value.trim().toLowerCase() === "yes"
        ) {
            if (antibioticDays && isEmpty(antibioticDays.value)) {
                markInvalid(antibioticDays, "Enter the number of days on antibiotics.");
                valid = false;
            }

            if (antibioticsUsed && isEmpty(antibioticsUsed.value)) {
                markInvalid(antibioticsUsed, "Enter the antibiotics reported by the patient.");
                valid = false;
            }
        }

        return valid;
    }


    function validateCompleteForm() {
        let valid = true;

        if (isRejectedSample()) {
            if (!validateRequiredFields()) valid = false;
            if (!validatePatientID()) valid = false;
            if (!validatePatientIDFacilityMatch()) valid = false;
            if (!validateFacility()) valid = false;
            if (!validateAllDates()) valid = false;
            return valid;
        }

        if (!validateRequiredFields()) valid = false;
        if (!validatePatientID()) valid = false;
        if (!validatePatientIDFacilityMatch()) valid = false;
        if (!validateFacility()) valid = false;
        if (!validateAllDates()) valid = false;
        if (!validateAge()) valid = false;
        if (!validateAgeConsistency()) valid = false;
        if (!validatePregnancyConsistency()) valid = false;
        if (!validateAllNumbers()) valid = false;
        if (!validateMilitaryOccupation()) valid = false;
        if (!validatePatientTypeHospitalLocation()) valid = false;
        if (!validateAntibioticDays()) valid = false;
        if (!validateClinicalPresentationInfectionSource()) valid = false;
        if (!validateInfectionOnsetAgainstPresentation()) valid = false;

        return valid;
    }


    // ============================================================
    // MODULE 14: NAVIGATE TO THE FIRST INVALID FIELD
    // ============================================================

    function getInvalidFields() {
        if (!patientForm) return [];

        return Array.from(
            patientForm.querySelectorAll("input, select, textarea")
        ).filter(field => {
            if (field.disabled || !isElementVisible(field)) {
                return false;
            }

            return (
                field.classList.contains("is-invalid") ||
                field.classList.contains("input-validation-error") ||
                field.getAttribute("aria-invalid") === "true"
            );
        });
    }


    function showFirstInvalidField() {
        const invalidFields = getInvalidFields();
        if (!invalidFields.length) return false;

        const firstField = invalidFields[0];
        const section = firstField.closest(".question-section");
        const sectionIndex = sections.indexOf(section);

        if (sectionIndex >= 0) {
            showSection(sectionIndex);
        }

        requestAnimationFrame(() => {
            firstField.scrollIntoView({
                behavior: "smooth",
                block: "center"
            });
            firstField.focus({ preventScroll: true });
        });

        return true;
    }


    // ============================================================
    // MODULE 15: FORM SUBMISSION
    // ============================================================

    if (patientForm) {
        patientForm.noValidate = true;

        patientForm.addEventListener("submit", function (event) {
            updatePatientIDRules();
            updateRejectedSampleWorkflow();

            if (!isRejectedSample()) {
                updateISOFieldRules();
                updatePregnancy();
                updatePatientType();
                updateAntibiotics();
                updateSkinInfectionSymptomsVisibility();
                updateSkinInfectionSymptoms();
            }

            const valid = validateCompleteForm();

            if (!valid) {
                event.preventDefault();
                showFirstInvalidField();
                updateProgress();
            }
        });
    }


    // ============================================================
    // MODULE 16: INITIALIZATION AND SERVER-RETURNED ERRORS
    // ============================================================

    if (btnSave) {
        btnSave.classList.add("d-none");
    }

    scrubStoredNotApplicablePlaceholders();

    updatePatientIDRules();
    updateRejectedSampleWorkflow();
    updateISOFieldRules();
    updatePregnancy();
    updatePatientType();
    updateAntibiotics();

    restoreSkinInfectionSymptoms();
    updateSkinInfectionSymptomsVisibility();
    updateSkinInfectionSymptoms();

    validatePatientIDFacilityMatch();
    validatePregnancyConsistency();
    validateInfectionOnsetAgainstPresentation();
    checkHAIClassificationWarning();
    updateProgress();


    const hasServerErrors = patientForm?.querySelector(
        ".input-validation-error, .field-validation-error"
    );

    if (hasServerErrors) {
        showFirstInvalidField();
    } else if (sections.length > 0) {
        showSection(0);
    }

});


// ============================================================
// END OF PATIENT QUESTIONNAIRE SCRIPT
// ============================================================
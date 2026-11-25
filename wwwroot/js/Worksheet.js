// ============================================================
// PATIENT ID AND ISOLATE ID VALIDATION (PATIENT ID FIRST)
// ============================================================

document.addEventListener("DOMContentLoaded", function () {
    const patientIDInput = document.getElementById("PatientID");
    const isolateIDInput = document.getElementById("IsolateID");
    const worksheetForm = document.getElementById("worksheetForm");

    const routinePatientIdRegex =
        /^[A-Z]{3}\/[A-Z]{3}\/\d{3,4}[A-Z]?$/;

    const isoPatientIdRegex =
        /^ISO\/[A-Z0-9/-]+$/;

    const isolateSuffixRegex = /^[A-Z0-9]+$/;

    function normalizeId(input) {
        if (!input) return "";

        const cleaned = input.value
            .toUpperCase()
            .replace(/\s+/g, "");

        if (input.value !== cleaned) {
            input.value = cleaned;
        }

        return cleaned.trim();
    }

    function validatePatientIdFormat(showPopup = false) {
        if (!patientIDInput) return true;

        const value = normalizeId(patientIDInput);
        patientIDInput.setCustomValidity("");

        if (value === "") return true;

        if (!routinePatientIdRegex.test(value) &&
            !isoPatientIdRegex.test(value)) {
            patientIDInput.setCustomValidity(
                "Enter a valid Patient ID using the slash-delimited format (e.g. ABC/DEF/001 or ISO/...)."
            );
            if (showPopup) patientIDInput.reportValidity();
            return false;
        }

        return true;
    }

    function validateIsolateId(showPopup = false) {
        if (!isolateIDInput) return true;

        const isolateValue = normalizeId(isolateIDInput);
        const patientValue = normalizeId(patientIDInput);

        isolateIDInput.setCustomValidity("");

        if (isolateValue === "") return true;

        if (!patientValue) {
            isolateIDInput.setCustomValidity(
                "Enter the Patient ID first before entering the Isolate ID."
            );
            if (showPopup) isolateIDInput.reportValidity();
            return false;
        }

        const expectedPrefix = patientValue + "-";

        if (!isolateValue.startsWith(expectedPrefix)) {
            isolateIDInput.setCustomValidity(
                `The Isolate ID must start with '${expectedPrefix}' followed by an alphanumeric suffix.`
            );
            if (showPopup) isolateIDInput.reportValidity();
            return false;
        }

        const suffix = isolateValue.slice(expectedPrefix.length);

        if (!isolateSuffixRegex.test(suffix)) {
            isolateIDInput.setCustomValidity(
                "The Isolate ID suffix must contain only letters and/or numbers after the hyphen."
            );
            if (showPopup) isolateIDInput.reportValidity();
            return false;
        }

        return true;
    }

    if (patientIDInput) {
        patientIDInput.addEventListener("input", function () {
            validatePatientIdFormat(false);
            validateIsolateId(false);
        });

        patientIDInput.addEventListener("blur", function () {
            validatePatientIdFormat(false);
            validateIsolateId(false);
        });
    }

    if (isolateIDInput) {
        isolateIDInput.addEventListener("focus", function () {
            if (isolateIDInput.readOnly || isolateIDInput.disabled) return;
            const patientValue = normalizeId(patientIDInput);
            if (patientValue === "") {
                isolateIDInput.setCustomValidity(
                    "Enter the Patient ID first before entering the Isolate ID."
                );
                isolateIDInput.reportValidity();
            }
        });

        isolateIDInput.addEventListener("input", function () {
            validateIsolateId(false);
        });

        isolateIDInput.addEventListener("blur", function () {
            validateIsolateId(false);
        });
    }

    if (worksheetForm) {
        worksheetForm.addEventListener("submit", function (event) {
            const patientFormatValid = validatePatientIdFormat(false);
            const isolateValid = validateIsolateId(false);

            if (!patientFormatValid || !isolateValid) {
                event.preventDefault();

                if (!patientFormatValid && patientIDInput) {
                    patientIDInput.reportValidity();
                    patientIDInput.focus();
                } else if (!isolateValid && isolateIDInput) {
                    isolateIDInput.reportValidity();
                    isolateIDInput.focus();
                }
            } else {
                patientIDInput?.setCustomValidity("");
                isolateIDInput?.setCustomValidity("");
            }
        }, true);
    }
});

// ============================================================
// WORKSHEET DYNAMIC SECTIONS, AST CARDS & DERIVED PREVIEW
// ============================================================

document.addEventListener("DOMContentLoaded", function () {
    const worksheetForm = document.getElementById("worksheetForm");
    const cultureResult = document.getElementById("CultureResult");
    const acceptedID = document.getElementById("AcceptedID");
    const whonetID = document.getElementById("WHONETID");
    const cefoxitinScreen = document.getElementById("CefoxitinScreen");

    const organismSection = document.getElementById("organismIdentificationSection");
    const laboratorySection = document.getElementById("laboratoryCharacteristicsSection");
    const astCardsSection = document.getElementById("astCardsSection");
    const astResultsSection = document.getElementById("astResultsSection");
    const astFindingsSection = document.getElementById("astFindingsSection");
    const derivedResultsSection = document.getElementById("derivedResultsSection");

    const astCardCheckboxes = document.querySelectorAll(".ast-card-checkbox");
    const antibioticRows = Array.from(document.querySelectorAll(".ast-antibiotic-row"));
    const astResultsBody = document.querySelector("#astResultsTable tbody") ||
        document.getElementById("astResultsBody");
    const astResultsContainer = document.getElementById("astResultsContainer");
    const astNoSelectionMessage = document.getElementById("astNoSelectionMessage");

    const esblField = document.getElementById("esblField");
    const cefoxitinField = document.getElementById("cefoxitinField");
    const inducibleClindamycinField =
        document.getElementById("inducibleClindamycinField");

    const mrsaResult = document.getElementById("mrsaResult");
    const mdrDisplay = document.getElementById("MDRDisplay");
    const mrsaDisplay = document.getElementById("MRSA");
    const carbapenemaseDisplay =
        document.getElementById("CarbapenemaseDisplay");

    const mdrReason = document.getElementById("MDRReason");
    const mrsaReason = document.getElementById("MRSAReason");
    const carbapenemaseReason =
        document.getElementById("CarbapenemaseReason");

    const derivedQCClasses = document.getElementById("derivedQCClasses");

    const carbapenemCodes = new Set(["MEM", "IPM", "ETP", "DOR"]);

    // =====================================================
    // INTRINSIC RESISTANCE LOOKUP MAP (WHONETID -> SET OF KEYS)
    // =====================================================

    const intrinsicMapByOrganism = new Map();

    function normalizeToken(value) {
        return (value || "")
            .trim()
            .toUpperCase()
            .replace(/\s+/g, " ");
    }

    (function loadIntrinsicResistanceMap() {
        const rawJson = worksheetForm?.dataset.intrinsicMap || "";
        if (!rawJson) return;

        try {
            const parsed = JSON.parse(rawJson);
            for (const [orgKey, entries] of Object.entries(parsed)) {
                const normalizedOrg = normalizeToken(orgKey);
                if (!normalizedOrg || !Array.isArray(entries)) continue;

                const entrySet = new Set(
                    entries
                        .map(normalizeToken)
                        .filter(Boolean)
                );

                intrinsicMapByOrganism.set(normalizedOrg, entrySet);
            }
        } catch {
            // Keep server-rendered hidden input flags if JSON is unavailable
        }
    })();

    // =====================================================
    // HELPERS
    // =====================================================

    function normalize(value) {
        return (value || "").trim().toUpperCase();
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

    function clearValue(id) {
        const element = document.getElementById(id);
        if (element) element.value = "";
    }

    function setText(element, value) {
        if (!element) return;

        if (element.matches("input, select, textarea")) {
            element.value = value;
        } else {
            element.textContent = value;
        }
    }

    function getOrganism() {
        return normalizeToken(whonetID?.value);
    }

    function isEscherichiaColi() {
        return getOrganism() === "ESCHERICHIA COLI";
    }

    function isKlebsiellaPneumoniae() {
        return getOrganism() === "KLEBSIELLA PNEUMONIAE";
    }

    function isStaphylococcusAureus() {
        return getOrganism() === "STAPHYLOCOCCUS AUREUS";
    }

    function getCultureResult() {
        return normalize(cultureResult?.value);
    }

    function isNoGrowthResult() {
        const val = getCultureResult();
        return val === "NO GROWTH" || val === "NSG" || val === "NG48";
    }

    // =====================================================
    // MIC WITHOUT SIR INTERPRETATION ERROR DISPLAY
    // =====================================================

    function validateRowMicSir(row, highlight = false) {
        const micInput = row.querySelector('input[name$=".MIC"]');
        const intInput = row.querySelector('input[name$=".Interpretation"]');
        const intCell = intInput?.closest("td") || row.cells[3];

        if (!intCell) return true;

        const micVal = (micInput?.value || "").trim();
        const intVal = normalize(intInput?.value);
        const missingSir = micVal !== "" && !["S", "I", "R"].includes(intVal);

        let errorEl = intCell.querySelector(".mic-missing-sir-error");
        if (missingSir) {
            if (!errorEl) {
                errorEl = document.createElement("div");
                errorEl.className = "text-danger small mt-1 mic-missing-sir-error";
                errorEl.textContent = "Select S, I, or R when MIC is entered.";
                intCell.appendChild(errorEl);
            }
            if (highlight && micInput) {
                micInput.classList.add("is-invalid");
            }
            return false;
        } else {
            if (errorEl) errorEl.remove();
            if (micInput) micInput.classList.remove("is-invalid");
            return true;
        }
    }

    function validateAllVisibleMicSir(highlight = false) {
        const selectedCards = getSelectedASTCards();
        let allValid = true;
        const invalidAntibiotics = [];

        antibioticRows.forEach(row => {
            if (!antibioticMatchesSelectedCard(row, selectedCards)) {
                const err = row.querySelector(".mic-missing-sir-error");
                if (err) err.remove();
                return;
            }

            const isValid = validateRowMicSir(row, highlight);
            if (!isValid) {
                allValid = false;
                const abxCode = normalizeToken(
                    row.querySelector('input[name$=".AntibioticCode"]')?.value ||
                    row.dataset.antibioticCode
                );
                if (abxCode) invalidAntibiotics.push(abxCode);
            }
        });

        let summaryAlert = document.getElementById("micSirValidationAlert");
        if (!allValid) {
            if (!summaryAlert && astResultsSection) {
                summaryAlert = document.createElement("div");
                summaryAlert.id = "micSirValidationAlert";
                summaryAlert.className = "alert alert-danger mt-3 mb-0";
                summaryAlert.setAttribute("role", "alert");
                const cardBody = astResultsSection.querySelector(".card-body");
                cardBody?.appendChild(summaryAlert);
            }
            if (summaryAlert) {
                summaryAlert.textContent =
                    `MIC entered without SIR interpretation for: ${invalidAntibiotics.join(", ")}. Please select S, I, or R.`;
                show(summaryAlert);
            }
        } else if (summaryAlert) {
            hide(summaryAlert);
        }

        return allValid;
    }

    antibioticRows.forEach(row => {
        const micInput = row.querySelector('input[name$=".MIC"]');
        if (micInput) {
            micInput.addEventListener("input", function () {
                validateRowMicSir(row, false);
                validateAllVisibleMicSir(false);
            });
            micInput.addEventListener("blur", function () {
                validateRowMicSir(row, true);
                validateAllVisibleMicSir(true);
            });
        }
    });

    // =====================================================
    // SYNC INTRINSIC RESISTANCE FLAGS ON ORGANISM CHANGE
    // =====================================================

    function syncIntrinsicResistanceForOrganism() {
        const selectedOrganism = getOrganism();
        const hasMapData = intrinsicMapByOrganism.size > 0;
        const intrinsicSet = selectedOrganism
            ? (intrinsicMapByOrganism.get(selectedOrganism) || new Set())
            : new Set();

        antibioticRows.forEach(row => {
            const code = normalizeToken(
                row.querySelector('input[name$=".AntibioticCode"]')?.value ||
                row.dataset.antibioticCode
            );
            const name = normalizeToken(
                row.querySelector('input[name$=".AntibioticName"]')?.value ||
                row.dataset.antibioticName
            );
            const intrinsicInput = row.querySelector(
                'input[name$=".IsIntrinsicallyResistant"]'
            );

            let isIntrinsic = false;

            if (hasMapData) {
                isIntrinsic =
                    selectedOrganism !== "" &&
                    ((code && intrinsicSet.has(code)) ||
                        (name && intrinsicSet.has(name)));

                if (intrinsicInput) {
                    intrinsicInput.value = isIntrinsic ? "True" : "False";
                }
            } else {
                const inputVal = normalizeToken(intrinsicInput?.value);
                const attrVal = normalizeToken(row.dataset.intrinsic);
                isIntrinsic = inputVal === "TRUE" || attrVal === "TRUE" || attrVal === "1";
            }

            row.dataset.isIntrinsic = isIntrinsic ? "true" : "false";

            const firstCell = row.querySelector("td");
            if (!firstCell) return;

            let badge = firstCell.querySelector(".intrinsic-resistance-badge");
            if (isIntrinsic) {
                if (!badge) {
                    badge = document.createElement("span");
                    badge.className = "badge bg-secondary-subtle text-secondary border ms-1 intrinsic-resistance-badge";
                    badge.title = "Intrinsically resistant for selected organism (excluded from MDR)";
                    badge.textContent = "Intrinsic";
                    const titleEl = firstCell.querySelector("strong");
                    if (titleEl) {
                        titleEl.insertAdjacentElement("afterend", badge);
                    } else {
                        firstCell.appendChild(badge);
                    }
                }
            } else if (badge) {
                badge.remove();
            }
        });
    }

    // =====================================================
    // ORGANISM-TO-CARD FILTERING
    // =====================================================

    function filterASTCardsByOrganism() {
        const selectedOrganism = getOrganism();

        astCardCheckboxes.forEach(checkbox => {
            const wrapper = checkbox.closest(".ast-card-option") || checkbox.closest(".form-check");
            const rawOrganisms = (checkbox.dataset.organisms || "").trim();

            if (!rawOrganisms || !selectedOrganism) {
                if (wrapper) show(wrapper);
                checkbox.disabled = false;
                return;
            }

            const allowedOrganisms = rawOrganisms
                .split(";")
                .map(normalizeToken)
                .filter(Boolean);

            const isAllowed = allowedOrganisms.includes(selectedOrganism);

            if (isAllowed) {
                if (wrapper) show(wrapper);
                checkbox.disabled = false;
            } else {
                checkbox.checked = false;
                checkbox.disabled = true;
                if (wrapper) hide(wrapper);
            }
        });
    }

    // =====================================================
    // ORGANISM-SPECIFIC AST FINDINGS
    // =====================================================

    function updateASTFindingVisibility() {
        const eColi = isEscherichiaColi();
        const klebsiella = isKlebsiellaPneumoniae();
        const staphAureus = isStaphylococcusAureus();

        if (eColi || klebsiella) {
            show(esblField);
        } else {
            hide(esblField);
            clearValue("ESBL");
        }

        if (staphAureus) {
            show(cefoxitinField);
            show(inducibleClindamycinField);
            show(mrsaResult);
        } else {
            hide(cefoxitinField);
            hide(inducibleClindamycinField);
            hide(mrsaResult);

            clearValue("CefoxitinScreen");
            clearValue("InducibleClindamycinResistance");
        }

        updateDerivedResults();
    }

    // =====================================================
    // NO-GROWTH HANDLING
    // =====================================================

    function syncNoGrowthState() {
        if (acceptedID) acceptedID.value = "";
        if (!whonetID) return;

        if (normalize(whonetID.value) === "NO GROWTH") {
            whonetID.value = "";
            if (window.jQuery &&
                window.jQuery.fn.select2 &&
                window.jQuery(whonetID).hasClass("select2-hidden-accessible")) {
                window.jQuery(whonetID).val("").trigger("change");
            }
        }
    }

    // =====================================================
    // AST CARDS & ORDERING (CUSTOM FOR 1 CARD, ALPHABETICAL FOR >1)
    // =====================================================

    function getSelectedASTCards() {
        return Array.from(astCardCheckboxes)
            .filter(checkbox => checkbox.checked && !checkbox.disabled)
            .map(checkbox => normalize(checkbox.value))
            .filter(Boolean);
    }

    function antibioticMatchesSelectedCard(row, selectedCards) {
        const configuredCards = (row.dataset.astCards || "")
            .split(";")
            .map(normalize)
            .filter(Boolean);

        return configuredCards.some(card => selectedCards.includes(card));
    }

    function getCustomOrderForSingleCard(row, cardCode) {
        const rawMap = (row.dataset.cardOrders || "").trim();

        if (rawMap) {
            try {
                if (rawMap.startsWith("{")) {
                    const parsed = JSON.parse(rawMap);
                    for (const [key, value] of Object.entries(parsed)) {
                        if (normalize(key) === cardCode) {
                            const ord = Number.parseInt(value, 10);
                            if (!Number.isNaN(ord) && ord > 0) {
                                return ord;
                            }
                        }
                    }
                } else {
                    const pairs = rawMap
                        .split(";")
                        .map(p => p.trim())
                        .filter(Boolean);

                    for (const pair of pairs) {
                        const [key, ordStr] = pair.split(":");
                        if (key && normalize(key) === cardCode) {
                            const ord = Number.parseInt(ordStr, 10);
                            if (!Number.isNaN(ord) && ord > 0) {
                                return ord;
                            }
                        }
                    }
                }
            } catch {
                // Fall back to single data-card-order attribute
            }
        }

        const singleOrder = Number.parseInt(row.dataset.cardOrder || "999999", 10);
        return (!Number.isNaN(singleOrder) && singleOrder > 0) ? singleOrder : 999999;
    }

    function compareAlphabetically(a, b) {
        const nameA = (a.dataset.antibioticName || "").trim();
        const nameB = (b.dataset.antibioticName || "").trim();
        const nameCompare = nameA.localeCompare(nameB, undefined, { sensitivity: "base" });
        if (nameCompare !== 0) return nameCompare;

        const codeA = (a.dataset.antibioticCode || "").trim();
        const codeB = (b.dataset.antibioticCode || "").trim();
        return codeA.localeCompare(codeB, undefined, { sensitivity: "base" });
    }

    function sortAntibioticRows(selectedCards) {
        if (!astResultsBody || antibioticRows.length === 0) return;

        const useCustomSingleCardOrder = selectedCards.length === 1;
        const singleCardCode = useCustomSingleCardOrder ? selectedCards[0] : "";

        const sortedRows = [...antibioticRows].sort((a, b) => {
            if (useCustomSingleCardOrder) {
                const orderA = getCustomOrderForSingleCard(a, singleCardCode);
                const orderB = getCustomOrderForSingleCard(b, singleCardCode);

                if (orderA !== orderB) {
                    return orderA - orderB;
                }
            }

            return compareAlphabetically(a, b);
        });

        sortedRows.forEach(row => astResultsBody.appendChild(row));
    }

    function updateASTResults() {
        const selectedCards = getSelectedASTCards();

        if (selectedCards.length === 0) {
            hide(astResultsSection);
            hide(astResultsContainer);
            show(astNoSelectionMessage);

            antibioticRows.forEach(hide);
            updateDerivedResults();
            return;
        }

        sortAntibioticRows(selectedCards);

        let visibleRows = 0;

        antibioticRows.forEach(row => {
            if (antibioticMatchesSelectedCard(row, selectedCards)) {
                row.classList.remove("d-none");
                row.style.display = "table-row";
                visibleRows++;
            } else {
                row.classList.add("d-none");
                row.style.display = "none";
            }
        });

        if (visibleRows > 0) {
            show(astResultsSection);
            show(astResultsContainer);
            hide(astNoSelectionMessage);
        } else {
            show(astResultsSection);
            hide(astResultsContainer);
            show(astNoSelectionMessage);

            setText(
                astNoSelectionMessage,
                "No active antibiotics are configured for the selected AST card(s)."
            );
        }

        validateAllVisibleMicSir(false);
        updateDerivedResults();
    }

    astCardCheckboxes.forEach(checkbox => {
        checkbox.addEventListener("change", updateASTResults);
    });

    // =====================================================
    // S / I / R BUTTONS
    // =====================================================

    function initializeInterpretationButtons() {
        document.querySelectorAll(".ast-interpretation-selector")
            .forEach(selector => {
                const buttons =
                    selector.querySelectorAll(".ast-interpretation-btn");

                const target = document.getElementById(
                    selector.dataset.target
                );

                if (!target) return;

                function updateButtonState() {
                    buttons.forEach(button => {
                        const isActive =
                            button.dataset.value === target.value &&
                            target.value !== "";
                        button.classList.toggle("active", isActive);
                        button.setAttribute("aria-pressed", isActive ? "true" : "false");
                    });
                }

                buttons.forEach(button => {
                    button.addEventListener("click", function (event) {
                        event.preventDefault();

                        const selectedValue = button.dataset.value;

                        if (target.value === selectedValue) {
                            target.value = "";
                        } else {
                            target.value = selectedValue;
                        }

                        updateButtonState();
                        const row = selector.closest("tr");
                        if (row) validateRowMicSir(row, false);
                        validateAllVisibleMicSir(false);
                        updateDerivedResults();
                    });
                });

                updateButtonState();
            });
    }

    // =====================================================
    // DERIVED MDR / MRSA / CARBAPENEMASE PREVIEW
    // =====================================================

    function getAntibioticResult(row) {
        const code = normalizeToken(
            row.querySelector('input[name$=".AntibioticCode"]')?.value ||
            row.dataset.antibioticCode
        );

        const category = (
            row.querySelector('input[name$=".AntibioticClassName"]')?.value ||
            row.dataset.antibioticClass ||
            ""
        ).trim();

        const interpretation = normalize(
            row.querySelector('input[name$=".Interpretation"]')?.value
        );

        const intrinsicInputVal = normalizeToken(
            row.querySelector('input[name$=".IsIntrinsicallyResistant"]')?.value
        );
        const intrinsic =
            row.dataset.isIntrinsic === "true" ||
            intrinsicInputVal === "TRUE" ||
            intrinsicInputVal === "1";

        const interpretationEnabledInput = row.querySelector(
            'input[name$=".InterpretationEnabled"]'
        );
        const interpretationEnabled = interpretationEnabledInput
            ? normalizeToken(interpretationEnabledInput.value) === "TRUE"
            : true;

        return {
            code,
            category,
            interpretation,
            intrinsic,
            interpretationEnabled
        };
    }

    function getDerivedCategoryResults() {
        const testedCategories = new Set();
        const nonSusceptibleCategories = new Map();
        const selectedCards = getSelectedASTCards();

        let anyCarbapenemTested = false;
        let anyCarbapenemResistant = false;

        antibioticRows.forEach(row => {
            if (!antibioticMatchesSelectedCard(row, selectedCards)) {
                return;
            }

            const result = getAntibioticResult(row);

            if (!result.interpretationEnabled ||
                result.intrinsic ||
                !["S", "I", "R"].includes(result.interpretation)) {
                return;
            }

            const upperCat = result.category.toUpperCase();
            const isCarbapenem =
                upperCat.includes("CARBAPENEM") ||
                carbapenemCodes.has(result.code);

            if (isCarbapenem) {
                anyCarbapenemTested = true;
                if (result.interpretation === "R") {
                    anyCarbapenemResistant = true;
                }
            }

            const effectiveCategory = result.category || (isCarbapenem ? "Carbapenems" : "");
            if (!effectiveCategory) {
                return;
            }

            const categoryKey = effectiveCategory.toUpperCase();
            testedCategories.add(categoryKey);

            if (result.interpretation === "I" ||
                result.interpretation === "R") {
                nonSusceptibleCategories.set(categoryKey, effectiveCategory);
            }
        });

        return {
            testedCategories,
            nonSusceptibleCategories,
            anyCarbapenemTested,
            anyCarbapenemResistant
        };
    }

    function updateDerivedResults() {
        if (getCultureResult() !== "GROWTH") return;

        const staphAureus = isStaphylococcusAureus();
        const screen = normalize(cefoxitinScreen?.value);

        const cefoxitinPositive = ["POS", "POSITIVE"].includes(screen);
        const cefoxitinNegative = ["NEG", "NEGATIVE"].includes(screen);

        const mrsa = staphAureus
            ? cefoxitinPositive ? true
                : cefoxitinNegative ? false
                    : null
            : null;

        if (staphAureus) {
            show(mrsaResult);

            if (mrsa === true) {
                setText(mrsaDisplay, "Yes");
                setText(
                    mrsaReason,
                    "Cefoxitin Screen is positive; MRSA is confirmed."
                );
            } else if (mrsa === false) {
                setText(mrsaDisplay, "No");
                setText(
                    mrsaReason,
                    "Cefoxitin Screen is negative; MRSA is not indicated by this screen."
                );
            } else {
                setText(mrsaDisplay, "Unknown");
                setText(
                    mrsaReason,
                    "Select a Cefoxitin Screen result to assess MRSA."
                );
            }
        } else {
            hide(mrsaResult);
        }

        const {
            testedCategories,
            nonSusceptibleCategories,
            anyCarbapenemTested,
            anyCarbapenemResistant
        } = getDerivedCategoryResults();

        let mdrStatus = null;
        let mdrReasonText = "";
        let classesText = "";

        if (staphAureus && mrsa === true) {
            mdrStatus = true;
            mdrReasonText = "Isolate is MRSA, thus MDR.";
            classesText = "MDR basis: MRSA.";
        } else if (testedCategories.size < 3) {
            mdrStatus = null;
            mdrReasonText =
                `Only ${testedCategories.size} antimicrobial categories have valid results. At least 3 tested categories are required.`;
            classesText = "Insufficient tested categories to determine MDR.";
        } else {
            mdrStatus = nonSusceptibleCategories.size >= 3;

            if (mdrStatus) {
                mdrReasonText =
                    "Non-susceptibility is present in at least 3 antimicrobial categories.";
            } else {
                mdrReasonText =
                    "Fewer than 3 antimicrobial categories show non-susceptibility.";
            }

            const classes = Array.from(nonSusceptibleCategories.values());

            classesText = classes.length
                ? "Categories with non-susceptibility: " + classes.join(", ")
                : "No tested categories currently show non-susceptibility.";
        }

        if (mdrDisplay) {
            mdrDisplay.classList.remove(
                "bg-danger",
                "bg-success",
                "text-white",
                "border-danger",
                "border-success"
            );

            if (mdrStatus === true) {
                setText(mdrDisplay, "MDR");
                mdrDisplay.classList.add("bg-danger", "text-white", "border-danger");
            } else if (mdrStatus === false) {
                setText(mdrDisplay, "Non-MDR");
                mdrDisplay.classList.add("bg-success", "text-white", "border-success");
            } else {
                setText(mdrDisplay, "Unknown");
            }
        }

        setText(mdrReason, mdrReasonText);
        setText(derivedQCClasses, classesText);

        if (carbapenemaseDisplay) {
            carbapenemaseDisplay.classList.remove(
                "bg-danger",
                "bg-success",
                "text-white",
                "border-danger",
                "border-success"
            );

            if (anyCarbapenemTested) {
                if (anyCarbapenemResistant) {
                    setText(carbapenemaseDisplay, "Positive (R)");
                    carbapenemaseDisplay.classList.add("bg-danger", "text-white", "border-danger");
                    setText(
                        carbapenemaseReason,
                        "At least one tested carbapenem agent is Resistant (R)."
                    );
                } else {
                    setText(carbapenemaseDisplay, "Negative");
                    carbapenemaseDisplay.classList.add("bg-success", "text-white", "border-success");
                    setText(
                        carbapenemaseReason,
                        "Tested carbapenem agent(s) are not Resistant (R)."
                    );
                }
            } else {
                setText(carbapenemaseDisplay, "Unknown");
                setText(
                    carbapenemaseReason,
                    "No carbapenem results recorded for the selected card(s)."
                );
            }
        }

        updateCefoxitinOxacillinWarning();
    }

    // =====================================================
    // CEFOXITIN / OXACILLIN MISMATCH WARNING
    // =====================================================

    function updateCefoxitinOxacillinWarning() {
        const findingsBody =
            astFindingsSection?.querySelector(".card-body");

        if (!findingsBody) return;

        let warning = document.getElementById("cefoxitinOxacillinWarning");

        if (!warning) {
            warning = document.createElement("div");
            warning.id = "cefoxitinOxacillinWarning";
            warning.className = "alert alert-warning mt-3 mb-0";
            warning.setAttribute("role", "alert");
            findingsBody.appendChild(warning);
        }

        const screen = normalize(cefoxitinScreen?.value);

        const oxacillinRow = antibioticRows.find(row => {
            const code = row.querySelector(
                'input[name$=".AntibioticCode"]'
            )?.value || row.dataset.antibioticCode;

            return normalize(code) === "OXA";
        });

        const oxacillin = normalize(
            oxacillinRow?.querySelector(
                'input[name$=".Interpretation"]'
            )?.value
        );

        const mismatch =
            isStaphylococcusAureus() &&
            ((screen === "POS" && oxacillin === "S") ||
                (screen === "NEG" && oxacillin === "R"));

        if (mismatch) {
            warning.textContent =
                "Warning: Cefoxitin Screen and Oxacillin interpretation disagree. Review these results before final validation.";
            show(warning);
        } else {
            hide(warning);
        }
    }

    // =====================================================
    // CULTURE RESULT RULES
    // =====================================================

    function updateCultureResult() {
        const result = getCultureResult();

        if (result === "") {
            hide(organismSection);
            hide(laboratorySection);
            hide(astCardsSection);
            hide(astResultsSection);
            hide(astFindingsSection);
            hide(derivedResultsSection);
            syncNoGrowthState();
            return;
        }

        if (result === "REJECTED") {
            hide(organismSection);
            hide(laboratorySection);
            hide(astCardsSection);
            hide(astResultsSection);
            hide(astFindingsSection);
            hide(derivedResultsSection);
            syncNoGrowthState();
            return;
        }

        if (isNoGrowthResult()) {
            hide(organismSection);
            hide(laboratorySection);
            hide(astCardsSection);
            hide(astResultsSection);
            hide(astFindingsSection);
            hide(derivedResultsSection);
            syncNoGrowthState();
            return;
        }

        if (result === "GROWTH") {
            show(organismSection);
            show(laboratorySection);
            show(astCardsSection);
            show(astFindingsSection);
            show(derivedResultsSection);

            syncNoGrowthState();
            syncIntrinsicResistanceForOrganism();
            filterASTCardsByOrganism();
            updateASTResults();
            updateASTFindingVisibility();
            updateDerivedResults();
            return;
        }

        hide(organismSection);
        hide(laboratorySection);
        hide(astCardsSection);
        hide(astResultsSection);
        hide(astFindingsSection);
        hide(derivedResultsSection);
    }

    // =====================================================
    // EVENTS
    // =====================================================

    function handleOrganismChanged() {
        syncIntrinsicResistanceForOrganism();

        if (getCultureResult() === "GROWTH") {
            filterASTCardsByOrganism();
            updateASTResults();
            updateASTFindingVisibility();
            updateDerivedResults();
        }
    }

    if (whonetID) {
        whonetID.addEventListener("change", handleOrganismChanged);

        if (window.jQuery) {
            window.jQuery(whonetID).on("change select2:select select2:clear", handleOrganismChanged);
        }
    }

    if (cefoxitinScreen) {
        cefoxitinScreen.addEventListener(
            "change",
            updateDerivedResults
        );
    }

    if (cultureResult) {
        cultureResult.addEventListener(
            "change",
            updateCultureResult
        );
    }

    if (worksheetForm) {
        worksheetForm.addEventListener("submit", function (event) {
            const result = getCultureResult();

            if (result === "") {
                event.preventDefault();
                alert("Please select a Culture Result.");
                cultureResult?.focus();
                return;
            }

            if (isNoGrowthResult() || result === "REJECTED") {
                syncNoGrowthState();
                return;
            }

            if (result === "GROWTH") {
                syncIntrinsicResistanceForOrganism();
                updateASTFindingVisibility();
                updateASTResults();
                updateDerivedResults();

                const micSirValid = validateAllVisibleMicSir(true);
                if (!micSirValid) {
                    event.preventDefault();
                    const firstInvalid = astResultsSection?.querySelector(".mic-missing-sir-error");
                    firstInvalid?.scrollIntoView({ behavior: "smooth", block: "center" });
                }
            }
        });
    }

    // =====================================================
    // INITIAL SETUP
    // =====================================================

    initializeInterpretationButtons();
    syncIntrinsicResistanceForOrganism();
    updateCultureResult();
});
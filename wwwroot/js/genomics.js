
"use strict";

document.addEventListener("DOMContentLoaded", function () {
    const forms = document.querySelectorAll("[data-genomics-form]");

    forms.forEach(function (form) {
        const isolateInput = form.querySelector('[name="IsolateID"]');
        const submitButtons = form.querySelectorAll(
            'button[type="submit"], input[type="submit"]'
        );

        let submitting = false;

        // Normalise whitespace without changing the ID's case.
        // Case sensitivity should follow the application's ID convention.
        if (isolateInput) {
            isolateInput.addEventListener("blur", function () {
                isolateInput.value = isolateInput.value.trim();
            });
        }

        // Validate the dynamic gene result fields.
        function validateGeneValues() {
            let valid = true;

            const geneInputs = form.querySelectorAll(
                '[data-gene-result="true"]'
            );

            geneInputs.forEach(function (input) {
                input.setCustomValidity("");

                const value = input.value.trim();

                // Blank means unknown/not tested.
                if (value !== "" && value !== "0" && value !== "1") {
                    input.setCustomValidity(
                        "Enter 1 (present), 0 (absent), or leave blank (unknown/not tested)."
                    );
                    valid = false;
                }
            });

            return valid;
        }

        // Validate optional numeric values.
        function validateNumericFields() {
            let valid = true;

            const numericInputs = form.querySelectorAll(
                '[data-genomics-number="true"]'
            );

            numericInputs.forEach(function (input) {
                input.setCustomValidity("");

                const value = input.value.trim();

                // Optional numeric fields may remain blank.
                if (value === "") {
                    return;
                }

                const number = Number(value);

                if (!Number.isFinite(number)) {
                    input.setCustomValidity("Enter a valid number.");
                    valid = false;
                    return;
                }

                const min = input.dataset.min;
                const max = input.dataset.max;

                if (min !== undefined && number < Number(min)) {
                    input.setCustomValidity(
                        "The value must be at least " + min + "."
                    );
                    valid = false;
                }

                if (max !== undefined && number > Number(max)) {
                    input.setCustomValidity(
                        "The value must not exceed " + max + "."
                    );
                    valid = false;
                }
            });

            return valid;
        }

        // Revalidate a field as the user changes it.
        form.addEventListener("input", function (event) {
            if (
                event.target.matches('[data-gene-result="true"]') ||
                event.target.matches('[data-genomics-number="true"]')
            ) {
                event.target.setCustomValidity("");
            }
        });

        form.addEventListener("submit", function (event) {
            if (submitting) {
                event.preventDefault();
                return;
            }

            if (isolateInput) {
                isolateInput.value = isolateInput.value.trim();
            }

            const genesValid = validateGeneValues();
            const numbersValid = validateNumericFields();

            if (!form.checkValidity() || !genesValid || !numbersValid) {
                event.preventDefault();
                form.reportValidity();
                return;
            }

            submitting = true;

            submitButtons.forEach(function (button) {
                button.disabled = true;
            });
        });

        // If the browser restores the form after navigation, allow submission.
        window.addEventListener("pageshow", function () {
            submitting = false;

            submitButtons.forEach(function (button) {
                button.disabled = false;
            });
        });
    });
});

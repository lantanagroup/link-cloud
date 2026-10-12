/* Category charts share one colour list. Status donuts keep green, red, and yellow. */
(function () {
    var success = getComputedStyle(document.documentElement).getPropertyValue("--au-success").trim();
    // Automation.UI's chart colours (greys removed); its green is the shared success token.
    var category = [
        "#4da3ff", success, "#ffc107", "#dc3545", "#6f42c1", "#20c997",
        "#fd7e14", "#17a2b8", "#e83e8c", "#6610f2", "#007bff"
    ];
    var resourceMix = category.slice(0, 6);
    var fallback = category.slice();
    var manifest = category.slice();
    var dashboardStatus = [success, "#dc3545", "#ffc107", "#4da3ff", "#6f42c1"];

    window.luChartPalette = {
        resourceMix: resourceMix,
        fallback: fallback,
        manifest: manifest,
        dashboardStatus: dashboardStatus,
        normalizationLine: "#6f42c1",
        dataAcquisitionLine: "#4da3ff",
        measureLine: "#20c997",
        validationLine: success,
        succeeded: success,
        failed: "#dc3545",
        cancelled: "#ffc107",
        other: "#17a2b8",
        accent: "#4da3ff",
        orange: "#fd7e14",
        muted: "#17a2b8",
        gray: "#9aa8b5",
        entryStatus: {
            submitted: success,
            submitting: "#4da3ff",
            noteligable: "#fd7e14",
            failedsubmission: "#dc3545",
            pendingvalidation: "#ffc107",
            unknown: "#17a2b8"
        },
        dataAcqStatus: {
            completed: success,
            processing: "#4da3ff",
            failed: "#dc3545",
            pending: "#ffc107",
            unknown: "#17a2b8"
        },
        measureFunnel: {
            "no measure report": "#17a2b8",
            notreportable: "#ffc107",
            readyforvalidation: success
        },
        validationFunnel: {
            notvalidated: "#17a2b8",
            failedvalidation: "#dc3545",
            passedvalidation: success
        }
    };
})();

/* Category charts share one colour list. Status donuts keep green, red, and yellow. */
(function () {
    var success = getComputedStyle(document.documentElement).getPropertyValue("--au-success").trim();
    var category = [
        "#3b82c4", "#2aa89a", "#e0a045", "#d16a8a", "#7b6ad6",
        "#e07a3d", "#4aa3c7", "#6a9a4a", "#c46bb5", "#5c7cfa"
    ];
    var resourceMix = category.slice(0, 6);
    var fallback = category.slice();
    var manifest = category.slice();
    var dashboardStatus = [success, "#dc3545", "#ffc107", "#3b82c4", "#7b6ad6"];

    window.luChartPalette = {
        resourceMix: resourceMix,
        fallback: fallback,
        manifest: manifest,
        dashboardStatus: dashboardStatus,
        normalizationLine: "#7b6ad6",
        dataAcquisitionLine: "#3b82c4",
        measureLine: "#2aa89a",
        validationLine: success,
        succeeded: success,
        failed: "#dc3545",
        cancelled: "#ffc107",
        other: "#4aa3c7",
        accent: "#3b82c4",
        orange: "#e07a3d",
        muted: "#4aa3c7",
        gray: "#9aa8b5",
        entryStatus: {
            submitted: success,
            submitting: "#3b82c4",
            noteligable: "#e0a045",
            failedsubmission: "#dc3545",
            pendingvalidation: "#ffc107",
            unknown: "#4aa3c7"
        },
        dataAcqStatus: {
            completed: success,
            processing: "#3b82c4",
            failed: "#dc3545",
            pending: "#ffc107",
            unknown: "#4aa3c7"
        },
        measureFunnel: {
            "no measure report": "#4aa3c7",
            notreportable: "#ffc107",
            readyforvalidation: success
        },
        validationFunnel: {
            notvalidated: "#4aa3c7",
            failedvalidation: "#dc3545",
            passedvalidation: success
        }
    };
})();

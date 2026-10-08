/* One chart palette. Values match the Automation charts that used to inline these arrays. */
(function () {
    var resourceMix = ["#343a40", "#28a745", "#6f42c1", "#545c64", "#fd7e14", "#6c757d"];
    var fallback = ["#343a40", "#6f42c1", "#ffc107", "#dc3545", "#28a745", "#6c757d"];
    var manifest = [
        "#343a40", "#28a745", "#ffc107", "#dc3545", "#6f42c1", "#111", "#fd7e14",
        "#545c64", "#e83e8c", "#6c757d", "#1a1a1a", "#adb5bd", "#343a40", "#868e96"
    ];
    var dashboardStatus = ["#28a745", "#dc3545", "#ffc107", "#343a40", "#6c757d"];

    window.luChartPalette = {
        resourceMix: resourceMix,
        fallback: fallback,
        manifest: manifest,
        dashboardStatus: dashboardStatus,
        normalizationLine: "#6f42c1",
        dataAcquisitionLine: "#343a40",
        measureLine: "#343a40",
        validationLine: "#28a745",
        succeeded: "#28a745",
        failed: "#dc3545",
        cancelled: "#ffc107",
        other: "#6c757d",
        accent: "#343a40",
        muted: "#545c64",
        gray: "#6c757d",
        entryStatus: {
            submitted: "#28a745",
            submitting: "#343a40",
            noteligable: "#ffc107",
            failedsubmission: "#dc3545",
            pendingvalidation: "#6c757d",
            unknown: "#545c64"
        },
        dataAcqStatus: {
            completed: "#28a745",
            processing: "#343a40",
            failed: "#dc3545",
            pending: "#ffc107",
            unknown: "#6c757d"
        },
        measureFunnel: {
            "no measure report": "#6c757d",
            notreportable: "#ffc107",
            readyforvalidation: "#28a745"
        },
        validationFunnel: {
            notvalidated: "#6c757d",
            failedvalidation: "#dc3545",
            passedvalidation: "#28a745"
        }
    };
})();

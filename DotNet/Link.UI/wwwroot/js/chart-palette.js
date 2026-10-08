/* One chart palette. The success green is the shared token; other series keep their chart colors. */
(function () {
    var success = getComputedStyle(document.documentElement).getPropertyValue("--au-success").trim();
    var resourceMix = ["#343a40", success, "#6f42c1", "#545c64", "#fd7e14", "#6c757d"];
    var fallback = ["#343a40", "#6f42c1", "#ffc107", "#dc3545", success, "#6c757d"];
    var manifest = [
        "#343a40", success, "#ffc107", "#dc3545", "#6f42c1", "#111", "#fd7e14",
        "#545c64", "#e83e8c", "#6c757d", "#1a1a1a", "#adb5bd", "#343a40", "#868e96"
    ];
    var dashboardStatus = [success, "#dc3545", "#ffc107", "#343a40", "#6c757d"];

    window.luChartPalette = {
        resourceMix: resourceMix,
        fallback: fallback,
        manifest: manifest,
        dashboardStatus: dashboardStatus,
        normalizationLine: "#6f42c1",
        dataAcquisitionLine: "#343a40",
        measureLine: "#343a40",
        validationLine: success,
        succeeded: success,
        failed: "#dc3545",
        cancelled: "#ffc107",
        other: "#6c757d",
        accent: "#343a40",
        orange: "#fd7e14",
        muted: "#545c64",
        gray: "#6c757d",
        entryStatus: {
            submitted: success,
            submitting: "#343a40",
            noteligable: "#ffc107",
            failedsubmission: "#dc3545",
            pendingvalidation: "#6c757d",
            unknown: "#545c64"
        },
        dataAcqStatus: {
            completed: success,
            processing: "#343a40",
            failed: "#dc3545",
            pending: "#ffc107",
            unknown: "#6c757d"
        },
        measureFunnel: {
            "no measure report": "#6c757d",
            notreportable: "#ffc107",
            readyforvalidation: success
        },
        validationFunnel: {
            notvalidated: "#6c757d",
            failedvalidation: "#dc3545",
            passedvalidation: success
        }
    };
})();

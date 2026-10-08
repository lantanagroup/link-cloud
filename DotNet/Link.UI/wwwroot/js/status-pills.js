/* One status-to-pill map for script-rendered badges. Classes are the shared au-badge set. */
(function () {
    function norm(value) {
        return String(value || "").trim().toLowerCase().replace(/[\s_-]+/g, "");
    }

    function forRun(status) {
        var s = norm(status);
        if (s === "succeeded") return "au-badge-success";
        if (s === "failed") return "au-badge-danger";
        if (s === "cancelled" || s === "canceled" || s === "2") return "au-badge-warning";
        if (s === "3") return "au-badge-success";
        if (s === "4") return "au-badge-danger";
        if (s === "running" || s === "queued" || s === "livewindow" || s === "livewindowopen"
            || s === "finalizing" || s === "reportfinalization" || s === "collecting" || s === "collectingmetrics"
            || s === "0" || s === "1" || s === "5" || s === "6" || s === "7")
            return "au-badge-active";
        return "au-badge-muted";
    }

    function forLog(status) {
        var raw = String(status || "");
        var s = norm(status);
        if (!s) return "au-badge-muted";
        if (s === "failed" || s === "maxretriesreached" || raw.toLowerCase().indexOf("error") >= 0 || raw.toLowerCase().indexOf("fail") >= 0)
            return "au-badge-danger";
        if (s === "completed" || s === "succeeded" || s === "submitted") return "au-badge-success";
        if (s === "skipped") return "au-badge-skip";
        if (s === "cancelled" || s === "canceled") return "au-badge-warning";
        if (s === "processing" || s === "queued" || s === "pending" || s === "ready" || s === "running")
            return "au-badge-active";
        return forRun(status);
    }

    function forSchedule(status) {
        var s = norm(status);
        if (s === "submitted") return "au-badge-success";
        if (s === "endofperiod") return "au-badge-warning";
        if (s === "failed" || s === "error") return "au-badge-danger";
        if (s === "running" || s === "processing" || s === "queued") return "au-badge-active";
        return "au-badge-muted";
    }

    function forLevel(level) {
        var s = norm(level);
        if (!s) return "au-badge-muted";
        if (s === "fatal" || s === "error" || s.indexOf("fatal") >= 0 || s.indexOf("error") >= 0)
            return "au-badge-danger";
        if (s.indexOf("warn") >= 0) return "au-badge-warning";
        return "au-badge-muted";
    }

    function forCleanup(status) {
        var s = norm(status);
        if (s === "running") return "au-badge-active";
        if (s === "failed") return "au-badge-danger";
        if (s === "completed" || s === "succeeded") return "au-badge-success";
        return "au-badge-muted";
    }

    function forOutcome(outcome) {
        var s = norm(outcome);
        if (s === "succeeded" || s === "passed" || s === "pass") return "au-badge-success";
        if (s === "failed" || s === "fail") return "au-badge-danger";
        if (s === "cancelled" || s === "canceled") return "au-badge-warning";
        return "au-badge-muted";
    }

    function forSeverity(severity) {
        var raw = String(severity || "").toLowerCase();
        if (!raw.trim()) return "au-badge-muted";
        if (raw.indexOf("error") >= 0 || raw.indexOf("fatal") >= 0 || raw.indexOf("unacceptable") >= 0)
            return "au-badge-danger";
        if (raw.indexOf("warn") >= 0) return "au-badge-warning";
        return "au-badge-muted";
    }

    function hollow(tone) {
        if (tone === "au-badge-danger") return "border border-danger text-danger bg-transparent";
        if (tone === "au-badge-success") return "border border-success text-success bg-transparent";
        return "border border-secondary text-muted bg-transparent";
    }

    function forConnection(text) {
        var s = norm(text);
        if (s === "live") return "au-badge-active";
        if (s.indexOf("reconnect") >= 0) return "au-badge-warning";
        return "au-badge-muted";
    }

    function forCensus(census) {
        var s = norm(census);
        if (s === "admitted") return "au-badge-success";
        if (s === "dischargedduringwindow" || s === "discharged") return "au-badge-warning";
        return "au-badge-muted";
    }

    function forEvent(name) {
        var s = norm(name);
        if (s === "admit") return "au-badge-success";
        if (s === "discharge") return "au-badge-warning";
        return "au-badge-muted";
    }

    function forHealth(status) {
        var s = norm(status);
        if (s === "healthy" || s === "ok") return "au-badge-success";
        if (s === "degraded" || s === "warning") return "au-badge-warning";
        if (s === "unhealthy" || s === "error" || s === "failed") return "au-badge-danger";
        return "au-badge-muted";
    }

    function forMilestone(failed, completed) {
        if (failed) return "au-badge-danger";
        if (completed) return "au-badge-success";
        return "au-badge-muted";
    }

    function forCheck(passed, advisory) {
        if (passed) return "au-badge-success";
        if (advisory) return "au-badge-warning";
        return "au-badge-danger";
    }

    function forFlag(on) {
        return on ? "au-badge-success" : "au-badge-muted";
    }

    function forPeriod(status) {
        var s = norm(status);
        if (s === "overlap") return "au-badge-success";
        if (s === "outsidebefore" || s === "outsideafter") return "au-badge-warning";
        return "";
    }

    function forAccepting(on) {
        return on ? "au-badge-active" : "au-badge-muted";
    }

    function forReadiness(ready) {
        return ready ? "au-badge-success" : "au-badge-warning";
    }

    function hollowFromClass(className) {
        var name = String(className || "");
        if (name.indexOf("au-badge-success") >= 0 || name.indexOf("border-success") >= 0)
            return hollow("au-badge-success");
        if (name.indexOf("au-badge-danger") >= 0 || name.indexOf("border-danger") >= 0)
            return hollow("au-badge-danger");
        return hollow("au-badge-muted");
    }

    function solidFromClass(className) {
        var name = String(className || "");
        if (name.indexOf("border-success") >= 0 || name.indexOf("au-badge-success") >= 0) return "au-badge-success";
        if (name.indexOf("border-danger") >= 0 || name.indexOf("au-badge-danger") >= 0) return "au-badge-danger";
        if (name.indexOf("border-secondary") >= 0 || name.indexOf("au-badge-skip") >= 0) return "au-badge-skip";
        return "au-badge-muted";
    }

    function forApiStep(skipped, passed, stale) {
        var tone = skipped ? "au-badge-skip" : (passed === true ? "au-badge-success" : (passed === false ? "au-badge-danger" : "au-badge-muted"));
        if (!stale) return tone;
        if (skipped || passed == null) return hollow("au-badge-muted");
        return hollow(tone);
    }

    function forApiSummary(fail, allSkipped, pass, runnable, stale) {
        var tone = fail > 0 ? "au-badge-danger"
            : (allSkipped ? "au-badge-skip"
                : (pass === runnable && runnable > 0 ? "au-badge-success" : "au-badge-muted"));
        return stale ? hollow(tone === "au-badge-skip" ? "au-badge-muted" : tone) : tone;
    }

    window.luStatusPills = {
        forRun: forRun,
        forLog: forLog,
        forSchedule: forSchedule,
        forLevel: forLevel,
        forCleanup: forCleanup,
        forOutcome: forOutcome,
        forSeverity: forSeverity,
        forApiStep: forApiStep,
        forApiSummary: forApiSummary,
        forConnection: forConnection,
        forCensus: forCensus,
        forEvent: forEvent,
        forHealth: forHealth,
        forMilestone: forMilestone,
        forCheck: forCheck,
        forFlag: forFlag,
        forPeriod: forPeriod,
        forAccepting: forAccepting,
        forReadiness: forReadiness,
        hollowFromClass: hollowFromClass,
        solidFromClass: solidFromClass
    };
})();

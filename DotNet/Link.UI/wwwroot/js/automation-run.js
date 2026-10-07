(function () {
    var live = document.getElementById("liveStatus");
    if (!live) return;
    var runId = live.getAttribute("data-run-id");
    var logLines = [];

    function setLive(text, badge) {
        live.textContent = text;
        live.className = "badge " + badge;
    }

    function text(id, value) {
        var node = document.getElementById(id);
        if (node) node.textContent = value || "—";
    }

    function apply(page) {
        if (!page) return;
        var message = document.getElementById("runMessage");
        var summary = document.getElementById("runSummary");
        if (!page.found || !page.run) {
            if (summary) summary.classList.add("d-none");
            if (message) {
                message.classList.remove("d-none");
                message.textContent = page.message || "This run is not in Automation storage.";
            }
            return;
        }

        var run = page.run;
        if (summary) summary.classList.remove("d-none");
        if (message) message.classList.add("d-none");
        var title = document.getElementById("runTitle");
        if (title) title.textContent = run.runName || "Run";
        text("runStatus", run.statusLabel);
        text("runScenario", run.scenario);
        text("runPatients", String(run.patientCount || 0));
        text("runSeed", String(run.seed || 0));
        text("runStarted", run.startedAt ? new Date(run.startedAt).toISOString().replace(".000Z", "Z") : "");
        text("runFinished", run.finishedAt ? new Date(run.finishedAt).toISOString().replace(".000Z", "Z") : "");
        text("runDuration", run.duration);
        text("runTemplates", run.templateVersion ? "v" + run.templateVersion : "");

        var facility = document.getElementById("runFacility");
        if (facility) {
            facility.replaceChildren();
            if (run.facilityId) {
                var link = document.createElement("a");
                link.href = "/Tenants/Facility/" + encodeURIComponent(run.facilityId);
                link.textContent = run.facilityId;
                facility.append(link);
                if (run.throwawayFacility) {
                    var badge = document.createElement("span");
                    badge.className = "badge bg-secondary ms-1";
                    badge.textContent = "Automation facility";
                    facility.append(badge);
                }
            } else {
                facility.textContent = "—";
            }
        }

        var report = document.getElementById("runReport");
        if (report) {
            report.replaceChildren();
            if (run.reportId) {
                var reportLink = document.createElement("a");
                var facilityQuery = run.facilityId ? "?facilityId=" + encodeURIComponent(run.facilityId) : "";
                reportLink.href = "/Reports" + facilityQuery;
                reportLink.textContent = run.reportId;
                report.append(reportLink);
            } else {
                report.textContent = "—";
            }
        }

        var error = document.getElementById("runError");
        if (error) {
            error.textContent = run.error || "";
            error.classList.toggle("d-none", !run.error);
        }
    }

    function refresh() {
        if (!runId) return;
        fetch("/Automation/Runs/" + runId + "/status", { headers: { "Accept": "application/json" } })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(apply)
            .catch(function () { setLive("Live updates unavailable", "bg-secondary"); });
    }

    function appendLog(line) {
        if (!line) return;
        logLines.push(String(line));
        if (logLines.length > 200) logLines.shift();
        var node = document.getElementById("liveLog");
        if (!node) return;
        node.textContent = logLines.join("\n");
        node.classList.remove("text-muted");
        node.scrollTop = node.scrollHeight;
    }

    if (live.getAttribute("data-live") !== "yes" || typeof signalR === "undefined" || !runId) {
        if (live.getAttribute("data-live") === "yes") setLive("Live updates unavailable", "bg-secondary");
        return;
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/runs")
        .withAutomaticReconnect()
        .build();

    connection.on("status", refresh);
    connection.on("dashboardUpdate", refresh);
    connection.on("log", appendLog);
    connection.onreconnecting(function () { setLive("Reconnecting", "bg-warning text-dark"); });
    connection.onreconnected(function () {
        setLive("Live", "bg-success");
        connection.invoke("SubscribeRun", runId).catch(function () { setLive("Live updates unavailable", "bg-secondary"); });
    });
    connection.onclose(function () { setLive("Live updates unavailable", "bg-secondary"); });
    connection.start()
        .then(function () { return connection.invoke("SubscribeRun", runId); })
        .then(function () { setLive("Live", "bg-success"); })
        .catch(function () { setLive("Live updates unavailable", "bg-secondary"); });
})();

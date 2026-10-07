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
        var statusNode = document.getElementById("status");
        if (statusNode && run.statusLabel) statusNode.textContent = run.statusLabel;
        text("runName", run.runName);
        text("facilityId", run.facilityId);
        text("scheduleFacilityId", run.facilityId);
        text("headerReportId", run.reportId);
        text("started", run.startedAt ? new Date(run.startedAt).toISOString().replace(".000Z", "Z") : "");
        text("finished", run.finishedAt ? new Date(run.finishedAt).toISOString().replace(".000Z", "Z") : "");
        text("pipelineDuration", run.duration);
        var detailError = document.getElementById("error");
        if (detailError) detailError.textContent = run.error || "-";
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

        var retention = document.getElementById("runRetention");
        if (retention) {
            retention.textContent = run.retentionNotice || "";
            retention.classList.toggle("d-none", !run.retentionNotice);
        }

        var actions = document.getElementById("runActions");
        if (actions) {
            actions.replaceChildren();
            if (run.facilityId) {
                var acquisition = document.createElement("button");
                acquisition.type = "button";
                acquisition.className = "btn btn-sm btn-outline-secondary";
                acquisition.setAttribute("data-bs-toggle", "modal");
                acquisition.setAttribute("data-bs-target", "#dataAcqLogModal");
                acquisition.textContent = "Data acquisition logs";
                actions.append(acquisition);
            }
        }
    }

    var refreshTicket = 0;

    function refresh() {
        if (!runId) return;
        var ticket = ++refreshTicket;
        fetch("/Automation/Runs/" + runId + "/status", { headers: { "Accept": "application/json" } })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (page) {
                if (ticket !== refreshTicket) return;
                apply(page);
                if (typeof window.refreshStoredLogs === "function") window.refreshStoredLogs();
                if (typeof window.refreshRunPanels === "function") window.refreshRunPanels();
            })
            .catch(function () { });
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

    function catchUp() {
        return connection.invoke("SubscribeRun", runId).then(function () {
            setLive("Live", "bg-success");
            refresh();
        });
    }

    connection.on("status", refresh);
    connection.on("dashboardUpdate", refresh);
    connection.on("log", appendLog);
    connection.onreconnecting(function () { setLive("Reconnecting", "bg-warning text-dark"); });
    connection.onreconnected(function () {
        catchUp().catch(function () { setLive("Live updates unavailable", "bg-secondary"); });
    });
    connection.onclose(function () { setLive("Live updates unavailable", "bg-secondary"); });

    // A status broadcast can land before this page subscribes, and later summary
    // writes do not always broadcast. Keep reading the stored run until it ends.
    var poll = setInterval(function () {
        var node = document.getElementById("runStatus") || document.getElementById("status");
        var label = node ? node.textContent : "";
        if (label === "Succeeded" || label === "Failed" || label === "Cancelled") {
            clearInterval(poll);
            refresh();
            return;
        }
        refresh();
    }, 2000);

    refresh();
    connection.start()
        .then(catchUp)
        .catch(function () { setLive("Live updates unavailable", "bg-secondary"); });
})();

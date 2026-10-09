(function () {
    var live = document.getElementById("liveStatus");
    if (!live) return;
    var runId = live.getAttribute("data-run-id");
    var logLines = [];

    function setLive(text) {
        live.textContent = text;
        live.className = "badge " + window.luStatusPills.forConnection(text);
    }

    function text(id, value) {
        var node = document.getElementById(id);
        if (node) node.textContent = value || "—";
    }

    function showInstant(id, value) {
        var node = document.getElementById(id);
        if (!node) return;
        if (!value) {
            node.textContent = "—";
            return;
        }
        node.textContent = String(value);
        if (window.luPaintTimes) window.luPaintTimes(node);
    }

    function paintStatus(id, label) {
        var node = document.getElementById(id);
        if (!node) return;
        node.textContent = label || "Unknown";
        node.className = "badge " + window.luStatusPills.forRun(label);
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
        paintStatus("runStatus", run.statusLabel);
        paintStatus("status", run.statusLabel);
        text("runName", run.runName);
        text("facilityId", run.facilityId);
        text("scheduleFacilityId", run.facilityId);
        text("headerReportId", run.reportId);
        showInstant("started", run.startedAt);
        showInstant("finished", run.finishedAt);
        text("pipelineDuration", run.duration);
        var detailError = document.getElementById("error");
        if (detailError) detailError.textContent = run.error || "-";
        text("runScenario", run.scenario);
        text("runPatients", String(run.patientCount || 0));
        text("runSeed", String(run.seed || 0));
        showInstant("runStarted", run.startedAt);
        showInstant("runFinished", run.finishedAt);
        text("runDuration", run.duration);
        text("runTemplates", run.templateVersion ? "v" + run.templateVersion : "");

        var facilityValue = document.getElementById("facilityValue");
        if (facilityValue) {
            facilityValue.textContent = run.facilityId || "—";
            if (run.facilityId) facilityValue.setAttribute("title", run.facilityId);
            if (facilityValue.tagName === "A" && run.facilityId)
                facilityValue.setAttribute("href", "/Tenants/Facility/" + encodeURIComponent(run.facilityId));
            var facilityBadge = document.getElementById("runFacilityBadge");
            if (facilityBadge) facilityBadge.classList.toggle("d-none", !run.throwawayFacility);
        }

        var reportValue = document.getElementById("reportValue");
        if (reportValue) {
            reportValue.textContent = run.reportId || "—";
            if (run.reportId) reportValue.setAttribute("title", run.reportId);
            if (reportValue.tagName === "A" && run.reportId) {
                var facilityQuery = run.facilityId ? "?facilityId=" + encodeURIComponent(run.facilityId) : "";
                reportValue.setAttribute("href", "/Reports" + facilityQuery);
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
                acquisition.className = "btn btn-sm btn-au-link";
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
        if (live.getAttribute("data-live") === "yes") setLive("Live updates unavailable");
        return;
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/runs")
        .withAutomaticReconnect()
        .build();

    function catchUp() {
        return connection.invoke("SubscribeRun", runId).then(function () {
            setLive("Live");
            refresh();
        });
    }

    connection.on("status", refresh);
    connection.on("dashboardUpdate", refresh);
    connection.on("log", appendLog);
    connection.onreconnecting(function () { setLive("Reconnecting"); });
    connection.onreconnected(function () {
        catchUp().catch(function () { setLive("Live updates unavailable"); });
    });
    connection.onclose(function () { setLive("Live updates unavailable"); });

    // A status broadcast can land before this page subscribes, and later summary
    // writes do not always broadcast. Keep reading the stored run until it ends.
    var poll = setInterval(function () {
        var node = document.getElementById("runStatus") || document.getElementById("status");
        var label = node ? node.textContent : "";
        if (label === "Succeeded" || label === "Failed" || label === "Cancelled") {
            clearInterval(poll);
            refresh();
            connection.stop();
            live.remove();
            return;
        }
        refresh();
    }, 2000);

    refresh();
    connection.start()
        .then(catchUp)
        .catch(function () { setLive("Live updates unavailable"); });
})();

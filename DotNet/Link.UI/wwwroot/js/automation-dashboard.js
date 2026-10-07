(function () {
    var statusChart;
    var dayChart;
    var refreshing = false;

    function readStats() {
        var node = document.getElementById("automation-stats");
        if (!node) return null;
        try {
            return JSON.parse(node.textContent || "");
        } catch (err) {
            return null;
        }
    }

    function chartReady() {
        return typeof Chart !== "undefined";
    }

    function draw(stats) {
        if (!stats || !chartReady()) return;
        var statusEmpty = document.getElementById("statusEmpty");
        var statusCanvas = document.getElementById("statusChart");
        var dayCanvas = document.getElementById("dayChart");
        var total = stats.totalRuns || 0;
        if (statusEmpty) statusEmpty.classList.toggle("d-none", total > 0);
        if (statusCanvas) statusCanvas.classList.toggle("d-none", total === 0);

        var statusData = {
            labels: ["Succeeded", "Failed", "Cancelled", "Running", "Queued"],
            datasets: [{
                data: [stats.succeeded || 0, stats.failed || 0, stats.cancelled || 0, stats.running || 0, stats.queued || 0],
                backgroundColor: ["#28a745", "#dc3545", "#ffc107", "#4da3ff", "#6c757d"]
            }]
        };
        if (statusChart) {
            statusChart.data = statusData;
            statusChart.update();
        } else if (statusCanvas && total > 0) {
            statusChart = new Chart(statusCanvas, { type: "doughnut", data: statusData, options: { plugins: { legend: { position: "bottom" } } } });
        }

        var days = stats.runsPerDay || [];
        var dayData = {
            labels: days.map(function (day) {
                var text = String(day.date || "");
                var match = text.match(/^(\d{4})-(\d{2})-(\d{2})/);
                return match ? match[2] + "/" + match[3] : text;
            }),
            datasets: [
                { label: "Succeeded", data: days.map(function (day) { return day.succeeded || 0; }), backgroundColor: "#28a745" },
                { label: "Failed", data: days.map(function (day) { return day.failed || 0; }), backgroundColor: "#dc3545" },
                { label: "Cancelled", data: days.map(function (day) { return day.cancelled || 0; }), backgroundColor: "#ffc107" },
                { label: "Other", data: days.map(function (day) { return day.other || 0; }), backgroundColor: "#6c757d" }
            ]
        };
        if (dayChart) {
            dayChart.data = dayData;
            dayChart.update();
        } else if (dayCanvas) {
            dayChart = new Chart(dayCanvas, {
                type: "bar",
                data: dayData,
                options: { responsive: true, scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true, ticks: { precision: 0 } } }, plugins: { legend: { position: "bottom" } } }
            });
        }
    }

    function elapsed(iso) {
        var created = new Date(iso);
        if (Number.isNaN(created.getTime())) return "";
        var total = Math.max(0, Math.round((Date.now() - created.getTime()) / 1000));
        var minutes = Math.floor(total / 60);
        var seconds = total % 60;
        return String(minutes).padStart(2, "0") + ":" + String(seconds).padStart(2, "0");
    }

    function tick() {
        document.querySelectorAll(".js-elapsed").forEach(function (node) {
            node.textContent = elapsed(node.getAttribute("data-created-at"));
        });
    }

    function cell(text) {
        var item = document.createElement("td");
        item.textContent = text;
        return item;
    }

    function fillActive(runs) {
        var host = document.getElementById("activeRuns");
        var empty = document.getElementById("activeEmpty");
        if (!host) return;
        host.replaceChildren();
        (runs || []).forEach(function (run) {
            var col = document.createElement("div");
            col.className = "col-12 col-md-4";
            var card = document.createElement("div");
            card.className = "border rounded p-2 h-100";
            var link = document.createElement("a");
            link.className = "fw-semibold";
            link.href = "/Automation/Runs/" + run.runId;
            link.textContent = run.runName || "Run";
            var meta = document.createElement("div");
            meta.className = "small text-muted";
            meta.textContent = (run.patientCount || 0) + " patients · seed " + (run.seed || 0);
            var row = document.createElement("div");
            row.className = "d-flex justify-content-between gap-2 mt-1";
            var badge = document.createElement("span");
            badge.className = "badge au-badge-active";
            badge.textContent = run.statusLabel || run.status || "";
            var time = document.createElement("span");
            time.className = "small text-muted js-elapsed";
            time.setAttribute("data-created-at", run.createdAt || "");
            row.append(badge, time);
            card.append(link, meta, row);
            col.append(card);
            host.append(col);
        });
        if (empty) empty.classList.toggle("d-none", (runs || []).length > 0);
        var card = document.getElementById("activeCard");
        if (card) card.style.display = (runs || []).length > 0 ? "" : "none";
    }

    function fillHistory(page) {
        var body = document.getElementById("historyBody");
        var empty = document.getElementById("historyEmpty");
        var count = document.getElementById("historyCount");
        if (!body) return;
        body.replaceChildren();
        (page.recentRuns || []).forEach(function (run) {
            var row = document.createElement("tr");
            var name = document.createElement("td");
            var link = document.createElement("a");
            link.href = "/Automation/Runs/" + run.runId;
            link.textContent = run.runName || "Run";
            name.append(link);
            row.append(name);
            row.append(cell(run.statusLabel || ""));
            row.append(cell(String(run.patientCount || 0)));
            row.append(cell(run.createdAt ? new Date(run.createdAt).toISOString().replace("T", " ").replace(".000Z", "Z") : ""));
            var facility = document.createElement("td");
            if (run.facilityId) {
                var facilityLink = document.createElement("a");
                facilityLink.href = "/Tenants/Facility/" + encodeURIComponent(run.facilityId);
                facilityLink.textContent = run.facilityId;
                facility.append(facilityLink);
            } else {
                facility.textContent = "—";
            }
            row.append(facility);
            body.append(row);
        });
        if (empty) empty.classList.toggle("d-none", (page.recentRuns || []).length > 0);
        if (count) count.textContent = String(page.totalCount || 0);
    }

    function apply(page) {
        if (!page || !page.stats) return;
        var stats = page.stats;
        var active = document.getElementById("kpiActive");
        var total = document.getElementById("kpiTotal");
        var success = document.getElementById("kpiSuccess");
        var duration = document.getElementById("kpiDuration");
        if (active) active.textContent = String((stats.running || 0) + (stats.queued || 0));
        if (total) total.textContent = String(stats.totalRuns || 0);
        if (success) {
            var decided = (stats.succeeded || 0) + (stats.failed || 0);
            success.textContent = decided === 0 ? "—" : String(stats.successRate) + "%";
        }
        if (duration) {
            var seconds = stats.avgDurationSeconds || 0;
            if (seconds <= 0) duration.textContent = "—";
            else {
                var whole = Math.round(seconds);
                var minutes = Math.floor(whole / 60);
                var remain = whole % 60;
                duration.textContent = minutes + ":" + String(remain).padStart(2, "0");
            }
        }
        var node = document.getElementById("automation-stats");
        if (node) node.textContent = JSON.stringify(stats);
        draw(stats);
        fillActive(page.activeRuns);
        fillHistory(page);
        tick();
    }

    function refresh() {
        if (refreshing) return;
        refreshing = true;
        var dataUrl = new URL("/Automation/data", window.location.origin);
        dataUrl.search = window.location.search;
        fetch(dataUrl, { headers: { "Accept": "application/json" } })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (page) { if (page) apply(page); })
            .catch(function () { setLive("Live updates unavailable", "bg-secondary"); })
            .finally(function () { refreshing = false; });
    }

    function setLive(text, badge) {
        var node = document.getElementById("liveStatus");
        if (!node) return;
        node.textContent = text;
        node.className = "badge " + badge;
    }

    draw(readStats());
    tick();
    setInterval(tick, 1000);

    var live = document.getElementById("liveStatus");
    if (!live || live.getAttribute("data-live") !== "yes" || typeof signalR === "undefined") {
        if (live && live.getAttribute("data-live") === "yes") setLive("Live updates unavailable", "bg-secondary");
        return;
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/runs")
        .withAutomaticReconnect()
        .build();

    connection.on("dashboardUpdate", refresh);
    connection.onreconnecting(function () { setLive("Reconnecting", "bg-warning text-dark"); });
    connection.onreconnected(function () {
        setLive("Live", "bg-success");
        connection.invoke("SubscribeDashboard").catch(function () { setLive("Live updates unavailable", "bg-secondary"); });
    });
    connection.onclose(function () { setLive("Live updates unavailable", "bg-secondary"); });
    connection.start()
        .then(function () { return connection.invoke("SubscribeDashboard"); })
        .then(function () { setLive("Live", "bg-success"); })
        .catch(function () { setLive("Live updates unavailable", "bg-secondary"); });
})();

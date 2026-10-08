(function () {
    var statusChart;
    var dayChart;
    var refreshTicket = 0;

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
                backgroundColor: ["#28a745", "#dc3545", "#ffc107", "#343a40", "#6c757d"]
            }]
        };
        if (statusChart) {
            statusChart.data = statusData;
            statusChart.update();
        } else if (statusCanvas && total > 0) {
            statusChart = new Chart(statusCanvas, {
                type: "doughnut",
                data: statusData,
                options: { responsive: true, maintainAspectRatio: false, animation: false, plugins: { legend: { position: "bottom" } } }
            });
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
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true, ticks: { precision: 0 } } },
                    animation: false,
                    plugins: { legend: { position: "bottom" } }
                }
            });
        }
    }

    function elapsed(iso) {
        var created = new Date(iso);
        if (Number.isNaN(created.getTime())) return "";
        var total = Math.max(0, Math.round((Date.now() - created.getTime()) / 1000));
        var minutes = Math.floor(total / 60);
        var seconds = total % 60;
        return String(minutes).padStart(2, "0") + ":" + String(seconds).padStart(2, "0") + " elapsed";
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
            card.className = "border rounded p-3 h-100";
            var title = document.createElement("div");
            title.className = "fw-semibold";
            title.textContent = run.runName || "Run";
            var meta = document.createElement("div");
            meta.className = "small text-muted";
            meta.textContent = (run.patientCount || 0) + " patients · seed " + (run.seed || 0);
            var row = document.createElement("div");
            row.className = "d-flex justify-content-between align-items-center mt-2";
            var badge = document.createElement("span");
            badge.className = "badge au-badge-active";
            badge.textContent = run.statusLabel || run.status || "";
            var time = document.createElement("span");
            time.className = "small text-muted js-elapsed";
            time.setAttribute("data-created-at", run.createdAt || "");
            row.append(badge, time);
            var details = document.createElement("a");
            details.className = "btn btn-sm btn-au-neutral mt-2 w-100";
            var back = window.location.pathname + window.location.search;
            details.href = "/Automation/Runs/" + encodeURIComponent(run.runId) + "?returnUrl=" + encodeURIComponent(back.charAt(0) === "/" ? back : "/Automation");
            details.textContent = "View Details";
            card.append(title, meta, row, details);
            col.append(card);
            host.append(col);
        });
        if (empty) empty.classList.toggle("d-none", (runs || []).length > 0);
        var cardHost = document.getElementById("activeCard");
        if (cardHost) cardHost.style.display = (runs || []).length > 0 ? "" : "none";
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
        tick();
    }

    var recentTicket = 0;

    function recentCard() {
        return document.getElementById("recentRunsCard");
    }

    function recentQuery() {
        var card = recentCard();
        var params = new URLSearchParams();
        params.set("pageNumber", card ? (card.getAttribute("data-page-number") || "1") : "1");
        params.set("pageSize", card ? (card.getAttribute("data-page-size") || "20") : "20");
        params.set("sortBy", card ? (card.getAttribute("data-sort-by") || "createdAt") : "createdAt");
        params.set("sortDir", card ? (card.getAttribute("data-sort-dir") || "desc") : "desc");
        return params;
    }

    function recentAttr(key) {
        if (key === "pageNumber") return "data-page-number";
        if (key === "pageSize") return "data-page-size";
        if (key === "sortBy") return "data-sort-by";
        return "data-sort-dir";
    }

    function applyRecentFromLocation() {
        var card = recentCard();
        if (!card) return;
        var current = new URL(window.location.href);
        ["pageNumber", "pageSize", "sortBy", "sortDir"].forEach(function (key) {
            var value = current.searchParams.get(key);
            if (value) card.setAttribute(recentAttr(key), value);
        });
    }

    function writeRecentQuery() {
        if (!recentCard()) return;
        var params = recentQuery();
        var url = new URL(window.location.href);
        ["pageNumber", "pageSize", "sortBy", "sortDir"].forEach(function (key) {
            url.searchParams.set(key, params.get(key));
        });
        var next = url.pathname + "?" + url.searchParams.toString() + url.hash;
        var now = window.location.pathname + window.location.search + window.location.hash;
        if (next !== now) history.replaceState(null, "", next);
    }

    function refreshRecent() {
        var host = document.getElementById("recentRunsHost");
        if (!host) return;
        var ticket = ++recentTicket;
        fetch("/Automation/recent?" + recentQuery().toString(), { headers: { "Accept": "text/html" } })
            .then(function (response) { return response.ok ? response.text() : null; })
            .then(function (html) {
                if (ticket !== recentTicket || html == null) return;
                host.innerHTML = html;
                if (window.luPaintTimes) window.luPaintTimes(host);
                tick();
            })
            .catch(function () { });
    }

    function refresh() {
        writeRecentQuery();
        var ticket = ++refreshTicket;
        var dataUrl = new URL("/Automation/data", window.location.origin);
        dataUrl.search = recentQuery().toString();
        fetch(dataUrl, { headers: { "Accept": "application/json" } })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (page) {
                if (ticket !== refreshTicket) return;
                if (page) apply(page);
            })
            .catch(function () { });
        refreshRecent();
    }

    function antiforgeryToken() {
        var token = document.querySelector("#quickLaunchForm input[name='__RequestVerificationToken']");
        if (!token) token = document.querySelector("input[name='__RequestVerificationToken']");
        return token ? token.value : "";
    }

    function postRunAction(url, runId) {
        return fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": antiforgeryToken()
            },
            body: JSON.stringify({ id: runId })
        }).then(function (response) {
            return response.json().then(function (body) { return { ok: response.ok, body: body }; });
        });
    }

    function bindDashboardActions() {
        document.addEventListener("click", function (event) {
            if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || event.button !== 0) return;

            var sort = event.target.closest(".au-sort-header");
            if (sort && sort.closest("#recentRunsHost")) {
                event.preventDefault();
                var sortUrl = new URL(sort.href, window.location.origin);
                var card = recentCard();
                if (card) {
                    card.setAttribute("data-page-number", sortUrl.searchParams.get("pageNumber") || "1");
                    card.setAttribute("data-page-size", sortUrl.searchParams.get("pageSize") || card.getAttribute("data-page-size") || "20");
                    card.setAttribute("data-sort-by", sortUrl.searchParams.get("sortBy") || "createdAt");
                    card.setAttribute("data-sort-dir", sortUrl.searchParams.get("sortDir") || "desc");
                }
                refresh();
                return;
            }

            var pageLink = event.target.closest("#recentRunsHost .page-link");
            if (pageLink && pageLink.hasAttribute("data-page")) {
                event.preventDefault();
                var cardForPage = recentCard();
                if (cardForPage) cardForPage.setAttribute("data-page-number", pageLink.getAttribute("data-page") || "1");
                refresh();
                return;
            }

            var cancel = event.target.closest(".btn-cancel-run");
            if (cancel) {
                event.preventDefault();
                if (!confirm("Cancel this running test? This aborts pipeline work for the facility and removes generated FHIR data.")) return;
                var cancelId = cancel.getAttribute("data-run-id");
                cancel.disabled = true;
                postRunAction("/Automation/cancel", cancelId)
                    .then(function (result) {
                        if (result.ok && result.body && result.body.success) {
                            refresh();
                            return;
                        }
                        cancel.disabled = false;
                        var reason = result.body && (result.body.error || result.body.detail)
                            ? (result.body.error || result.body.detail)
                            : "This run is not running.";
                        alert(reason);
                    })
                    .catch(function () {
                        cancel.disabled = false;
                        alert("Cancel could not be completed. Refresh the page and check the run status.");
                    });
                return;
            }

            var remove = event.target.closest(".btn-delete-run");
            if (remove) {
                event.preventDefault();
                if (!confirm("Delete this run?")) return;
                var deleteId = remove.getAttribute("data-run-id");
                remove.disabled = true;
                postRunAction("/Automation/delete", deleteId)
                    .then(function (result) {
                        if (result.ok && result.body && result.body.success) {
                            refresh();
                            return;
                        }
                        remove.disabled = false;
                        alert("Unable to delete the run. It may still be active.");
                    })
                    .catch(function () {
                        remove.disabled = false;
                        alert("Unable to delete the run. Please try again.");
                    });
            }
        });

        document.addEventListener("change", function (event) {
            if (!event.target || event.target.id !== "pageSizeSelect") return;
            var card = recentCard();
            if (card) {
                card.setAttribute("data-page-size", event.target.value || "20");
                card.setAttribute("data-page-number", "1");
            }
            refresh();
        });

        var form = document.getElementById("quickLaunchForm");
        var choice = document.getElementById("qlChoice");
        var runButton = document.getElementById("qlRunButton");
        var dropdownButton = document.getElementById("quickLaunchDropdownButton");
        var search = document.getElementById("quickLaunchSearch");
        var typeFilter = document.getElementById("quickLaunchTypeFilter");
        var sortSelect = document.getElementById("quickLaunchSort");

        function optionMatches(option) {
            var query = search ? search.value.trim().toLowerCase() : "";
            var type = typeFilter ? typeFilter.value : "";
            var hay = ((option.dataset.name || "") + " " + (option.dataset.description || "") + " " + (option.dataset.measures || "") + " " + (option.dataset.method || "")).toLowerCase();
            if (type && (option.dataset.type || "") !== type) return false;
            return !query || hay.indexOf(query) >= 0;
        }

        function applyQuickLaunchSearch() {
            var visible = 0;
            document.querySelectorAll(".quick-launch-option").forEach(function (option) {
                var show = optionMatches(option);
                option.hidden = !show;
                if (show) visible += 1;
            });
            document.querySelectorAll(".quick-launch-group").forEach(function (group) {
                var any = group.querySelector(".quick-launch-option:not([hidden])");
                group.hidden = !any;
            });
            var empty = document.getElementById("quickLaunchNoResults");
            if (empty) empty.hidden = visible > 0;
        }

        function sortQuickLaunch() {
            var mode = sortSelect ? sortSelect.value : "name";
            document.querySelectorAll(".quick-launch-group").forEach(function (group) {
                var options = Array.prototype.slice.call(group.querySelectorAll(".quick-launch-option"));
                options.sort(function (a, b) {
                    if (mode === "updated") return Number(b.dataset.updated || 0) - Number(a.dataset.updated || 0);
                    return String(a.dataset.name || "").localeCompare(String(b.dataset.name || ""), undefined, { sensitivity: "base" });
                });
                options.forEach(function (option) { group.appendChild(option); });
            });
        }

        function selectScenario(id, label) {
            if (choice) choice.value = id ? "scenario:" + id : "";
            if (runButton) runButton.disabled = !id;
            if (dropdownButton) dropdownButton.textContent = label || "-- Select a scenario --";
            var hidden = document.getElementById("quickLaunchSelect");
            if (hidden) hidden.value = id || "";
        }

        if (search) search.addEventListener("input", applyQuickLaunchSearch);
        if (typeFilter) typeFilter.addEventListener("change", applyQuickLaunchSearch);
        if (sortSelect) sortSelect.addEventListener("change", function () {
            sortQuickLaunch();
            applyQuickLaunchSearch();
        });
        var menu = document.querySelector("#quickLaunchDropdown .dropdown-menu");
        if (menu) {
            menu.addEventListener("click", function (event) {
                var option = event.target.closest(".quick-launch-option");
                if (!option) return;
                selectScenario(option.dataset.id || "", option.dataset.name || "Scenario");
                if (search) {
                    search.value = "";
                    applyQuickLaunchSearch();
                }
                if (dropdownButton && window.bootstrap && bootstrap.Dropdown) {
                    bootstrap.Dropdown.getOrCreateInstance(dropdownButton).hide();
                }
            });
        }
        if (form) {
            form.addEventListener("submit", function (event) {
                if (!choice || !choice.value) event.preventDefault();
            });
        }

        var newScenario = document.getElementById("btnNewScenarioFromRuns");
        if (newScenario) {
            newScenario.addEventListener("click", function () {
                if (typeof window.openScenarioEditor === "function") window.openScenarioEditor(null, "edit");
            });
        }

        document.addEventListener("scenario-editor:changed", function (event) {
            var detail = event.detail || {};
            if (detail.action !== "saved" || !detail.id) return;
            var id = String(detail.id);
            fetch("/Automation/Scenarios/GetQuickLaunchMetadata?id=" + encodeURIComponent(id), { headers: { "Accept": "application/json" } })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(function (scenario) {
                    if (!scenario) return;
                    var label = scenario.name || "Scenario";
                    var type = scenario.type || "Custom";
                    var group = document.querySelector(".quick-launch-group[data-group='" + type + "']");
                    var option = document.querySelector(".quick-launch-option[data-id='" + id + "']");
                    if (!option) {
                        option = document.createElement("button");
                        option.type = "button";
                        option.className = "dropdown-item quick-launch-option";
                        option.dataset.id = id;
                        if (group) group.appendChild(option);
                    }
                    option.dataset.name = label;
                    option.dataset.description = scenario.description || "";
                    option.dataset.method = scenario.method || "";
                    option.dataset.type = type;
                    option.dataset.measures = Array.isArray(scenario.measures) ? scenario.measures.join(" ") : "";
                    option.dataset.updated = String(scenario.updatedAt || Date.now());
                    option.textContent = label + (type === "System" ? " [System]" : "");
                    var hidden = document.getElementById("quickLaunchSelect");
                    if (hidden && !hidden.querySelector("option[value='" + id + "']")) {
                        var hiddenOption = document.createElement("option");
                        hiddenOption.value = id;
                        hidden.appendChild(hiddenOption);
                    }
                    sortQuickLaunch();
                    applyQuickLaunchSearch();
                    selectScenario(id, label);
                })
                .catch(function () { });
        });

        sortQuickLaunch();
        applyQuickLaunchSearch();
    }

    function setLive(text, badge) {
        var node = document.getElementById("liveStatus");
        if (!node) return;
        node.textContent = text;
        node.className = "badge " + badge;
    }

    applyRecentFromLocation();
    writeRecentQuery();
    draw(readStats());
    tick();
    setInterval(tick, 1000);
    bindDashboardActions();

    var live = document.getElementById("liveStatus");
    if (!live || live.getAttribute("data-live") !== "yes" || typeof signalR === "undefined") {
        if (live && live.getAttribute("data-live") === "yes") setLive("Live updates unavailable", "au-badge-muted");
        return;
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/runs")
        .withAutomaticReconnect()
        .build();

    function catchUp() {
        return connection.invoke("SubscribeDashboard").then(function () {
            setLive("Live", "au-badge-active");
            refresh();
        });
    }

    connection.on("dashboardUpdate", refresh);
    connection.onreconnecting(function () { setLive("Reconnecting", "bg-warning text-dark"); });
    connection.onreconnected(function () {
        catchUp().catch(function () { setLive("Live updates unavailable", "au-badge-muted"); });
    });
    connection.onclose(function () { setLive("Live updates unavailable", "au-badge-muted"); });
    refresh();
    connection.start()
        .then(catchUp)
        .catch(function () { setLive("Live updates unavailable", "au-badge-muted"); });
})();

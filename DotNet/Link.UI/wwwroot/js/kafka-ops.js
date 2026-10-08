(function () {
    var colors = ["#28a745", "#dc3545", "#343a40", "#6c757d", "#212529"];
    var charts = {};
    var poll;

    function read() {
        var node = document.getElementById("kafka-ops-chart-data");
        if (!node) return null;
        try {
            return JSON.parse(node.textContent || "");
        } catch (err) {
            return null;
        }
    }

    function bars(canvasId, rows, label) {
        var canvas = document.getElementById(canvasId);
        if (charts[canvasId]) {
            charts[canvasId].destroy();
            delete charts[canvasId];
        }
        if (!canvas || typeof Chart === "undefined" || !rows) return;
        var empty = rows.length === 0;
        canvas.classList.toggle("d-none", empty);
        if (empty) return;
        charts[canvasId] = new Chart(canvas, {
            type: "bar",
            data: {
                labels: rows.map(function (row) { return row.label; }),
                datasets: [{
                    label: label,
                    data: rows.map(function (row) { return row.value; }),
                    backgroundColor: rows.map(function (_, index) { return colors[index % colors.length]; })
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { ticks: { color: "#343a40" } },
                    y: { beginAtZero: true, ticks: { color: "#343a40" } }
                }
            }
        });
    }

    function tips(root) {
        if (!window.bootstrap || !bootstrap.Tooltip) return;
        (root || document).querySelectorAll("button[title], a[aria-label][title]").forEach(function (el) {
            if (el.closest(".lu-section-nav")) return;
            bootstrap.Tooltip.getOrCreateInstance(el);
        });
    }

    function wireConfirm() {
        var input = document.getElementById("plan-confirm");
        var button = document.getElementById("plan-submit");
        if (!input || !button) return;
        var expected = input.getAttribute("data-topic") || "";
        function sync() {
            button.disabled = input.value !== expected;
        }
        input.addEventListener("input", sync);
        sync();
    }

    function paint() {
        var data = read();
        if (data) {
            bars("kafkaRateChart", data.rates, "Messages per second");
            bars("kafkaLagChart", data.lags, "Lag");
        }
        if (typeof window.luPaintTimes === "function") window.luPaintTimes(document.getElementById("kafkaResults") || document);
        tips(document.getElementById("kafkaResults") || document);
        wireConfirm();
        watchRequest();
    }

    function watchRequest() {
        var progress = document.getElementById("kafka-request-progress");
        if (!progress) return;
        var url = progress.getAttribute("data-status-url");
        if (!url || progress.getAttribute("data-watching") === "1") return;
        progress.setAttribute("data-watching", "1");
        if (poll) window.clearInterval(poll);
        poll = window.setInterval(function () { refresh(url); }, 5000);
    }

    function statusText(value) {
        if (typeof value === "string") return value;
        return "";
    }

    async function refresh(url) {
        var progress = document.getElementById("kafka-request-progress");
        if (!progress) return;
        var response = await fetch(url, { headers: { "Accept": "application/json" }, credentials: "same-origin" });
        if (!response.ok) return;
        var body = await response.json();
        var statusName = statusText(body.status);
        var status = document.getElementById("kafka-request-status");
        if (status && statusName) {
            status.textContent = statusName;
            status.className = "badge " + pill(statusName);
        }
        var text = document.getElementById("kafka-request-progress-text");
        if (text && body.progress) text.textContent = body.progress;
        var rows = document.getElementById("kafka-request-groups");
        if (rows && Array.isArray(body.groups)) {
            rows.replaceChildren();
            body.groups.forEach(function (group) {
                var tr = document.createElement("tr");
                ["groupId", "assignedPartitions", "expectedPartitions"].forEach(function (key) {
                    var cell = document.createElement("td");
                    cell.textContent = group[key] == null ? "" : String(group[key]);
                    tr.appendChild(cell);
                });
                var complete = document.createElement("td");
                complete.textContent = group.complete ? "Yes" : "No";
                tr.appendChild(complete);
                rows.appendChild(tr);
            });
        }
        if (typeof window.luPaintTimes === "function") window.luPaintTimes(progress);
        if (statusName === "Done" || statusName === "Failed" || statusName === "Rejected" || statusName === "Cancelled" || statusName === "NeedsAttention")
            window.clearInterval(poll);
    }

    function pill(status) {
        if (status === "Stable" || status === "up" || status === "Done" || status === "Approved") return "au-badge-success";
        if (status === "Failed" || status === "Rejected" || status === "NeedsAttention" || status === "Dead" || status === "offline") return "au-badge-danger";
        if (status === "Cancelled" || status === "Empty") return "au-badge-warning";
        if (status === "Pending" || status === "Executing" || status === "Converging" || status === "Verifying") return "au-badge-active";
        return "au-badge-muted";
    }

    document.addEventListener("au-refreshed", function (event) {
        if (!event.detail || event.detail.id === "kafkaResults") paint();
    });
    paint();
})();

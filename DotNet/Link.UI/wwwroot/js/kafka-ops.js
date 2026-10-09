(function () {
    var barColor = "#6c757d";
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
        if (window.luChartFrame) window.luChartFrame.fit(canvas);
        if (rows.length === 0) {
            if (window.luChartFrame) window.luChartFrame.collapseEmpty(canvasId);
            return;
        }
        if (window.luChartFrame) window.luChartFrame.reveal(canvasId);
        charts[canvasId] = new Chart(canvas, {
            type: "bar",
            data: {
                labels: rows.map(function (row) { return row.label; }),
                datasets: [{
                    label: label,
                    data: rows.map(function (row) { return row.value; }),
                    backgroundColor: barColor
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
        var root = document.getElementById("kafkaResults") || document;
        tips(root);
        wireConfirm();
        bindBusy(root);
        wireResults(root);
        wireProduce();
        focusResult(root);
        watchRequest();
    }

    function wireProduce() {
        if (document.documentElement.getAttribute("data-produce-wired") === "1") {
            syncProduce();
            return;
        }
        document.documentElement.setAttribute("data-produce-wired", "1");
        document.addEventListener("click", function (event) {
            var tab = event.target && event.target.closest ? event.target.closest("[data-msg-tab]") : null;
            if (tab) {
                showMessageTab(tab.getAttribute("data-msg-tab"));
                return;
            }
            var restage = event.target && event.target.closest ? event.target.closest("[data-restage]") : null;
            if (restage) {
                event.preventDefault();
                applyRestage(restage);
                return;
            }
            var paste = event.target && event.target.closest ? event.target.closest("#kafka-paste-message") : null;
            if (paste) {
                event.preventDefault();
                pasteMessage();
            }
        });
        document.addEventListener("input", function (event) {
            var form = event.target && event.target.closest ? event.target.closest("#kafka-produce") : null;
            if (!form) return;
            form.setAttribute("data-produce-dirty", "1");
            if (event.target && event.target.id === "produce-topic") {
                var confirm = document.getElementById("produce-confirm");
                if (confirm) confirm.setAttribute("data-topic", event.target.value || "");
            }
            syncProduce();
        });
        syncProduce();
    }

    function showMessageTab(name) {
        document.querySelectorAll("[data-msg-pane]").forEach(function (pane) {
            pane.hidden = pane.getAttribute("data-msg-pane") !== name;
        });
        document.querySelectorAll("[data-msg-tab]").forEach(function (tab) {
            var on = tab.getAttribute("data-msg-tab") === name;
            tab.classList.toggle("active", on);
            tab.setAttribute("aria-selected", on ? "true" : "false");
        });
    }

    function applyRestage(button) {
        var form = document.getElementById("kafka-produce");
        var node = document.getElementById(button.getAttribute("data-restage") || "");
        if (!form || !node) return;
        var payload;
        try {
            payload = JSON.parse(node.textContent || "");
        } catch (err) {
            return;
        }
        fillProduce(form, payload);
        var topic = button.getAttribute("data-topic") || "";
        var topicBox = form.querySelector("[name='topic']");
        if (topicBox && topic) topicBox.value = topic;
        var confirm = document.getElementById("produce-confirm");
        if (confirm) confirm.setAttribute("data-topic", topicBox ? topicBox.value : topic);
        var stage = button.getAttribute("data-stage") || "";
        if (stage && window.history && history.replaceState) {
            try {
                var url = new URL(window.location.href);
                url.searchParams.set("stage", stage);
                history.replaceState(null, "", url.pathname + url.search + url.hash);
            } catch (err) { /* keep the current address */ }
        }
        syncProduce();
        form.scrollIntoView({ block: "nearest" });
    }

    function pasteMessage() {
        var form = document.getElementById("kafka-produce");
        var summary = document.getElementById("kafka-produce-summary");
        if (!form) return;
        if (!navigator.clipboard || !navigator.clipboard.readText) {
            if (summary) summary.textContent = "Clipboard read is not available. Paste headers, key, and value into the fields.";
            return;
        }
        navigator.clipboard.readText().then(function (text) {
            var payload;
            try {
                payload = JSON.parse(text);
            } catch (err) {
                if (summary) summary.textContent = "That clipboard is not a copied message. Paste headers, key, and value into the fields.";
                return;
            }
            if (!payload || (payload.headers == null && payload.key == null && payload.value == null)) {
                if (summary) summary.textContent = "That clipboard is not a copied message. Paste headers, key, and value into the fields.";
                return;
            }
            fillProduce(form, payload);
            syncProduce();
            form.scrollIntoView({ block: "nearest" });
        }).catch(function () {
            if (summary) summary.textContent = "Clipboard read was blocked. Paste headers, key, and value into the fields.";
        });
    }

    function fillProduce(form, payload) {
        var lines = headerLines(payload.headers);
        var headersBox = form.querySelector("[name='messageHeaders']");
        var keyBox = form.querySelector("[name='messageKey']");
        var valueBox = form.querySelector("[name='messageValue']");
        if (headersBox) headersBox.value = lines.join("\n");
        if (keyBox) keyBox.value = payload.key == null ? "" : String(payload.key);
        if (valueBox) valueBox.value = payload.value == null ? "" : String(payload.value);
        form.setAttribute("data-produce-dirty", "1");
        var summary = document.getElementById("kafka-produce-summary");
        if (summary) summary.textContent = "Review this replica. " + lines.length + " headers. Nothing is produced until you type the topic name.";
    }

    function headerLines(headers) {
        var lines = [];
        if (Array.isArray(headers)) {
            headers.forEach(function (header) {
                if (!header) return;
                lines.push(String(header.name || "") + ": " + String(header.value == null ? "" : header.value));
            });
            return lines;
        }
        if (headers && typeof headers === "object") {
            Object.keys(headers).forEach(function (name) {
                lines.push(name + ": " + String(headers[name] == null ? "" : headers[name]));
            });
        }
        return lines;
    }

    function syncProduce() {
        var input = document.getElementById("produce-confirm");
        var button = document.getElementById("produce-submit");
        var topic = document.getElementById("produce-topic");
        if (!input || !button) return;
        var expected = topic ? topic.value : (input.getAttribute("data-topic") || "");
        button.disabled = input.value !== expected || expected.length === 0;
    }

    function wireResults(root) {
        (root || document).querySelectorAll("[data-lu-result]").forEach(function (panel) {
            if (panel.getAttribute("data-lu-result-bound") === "1") return;
            panel.setAttribute("data-lu-result-bound", "1");
            var id = panel.getAttribute("data-lu-result") || panel.id || "result";
            var key = panel.getAttribute("data-result-key") || panel.getAttribute("data-plan-hash") || "";
            var details = panel.querySelector("details");
            var dismiss = panel.querySelector(".lu-result-dismiss");
            try {
                if (key && window.localStorage.getItem("lu-result-dismiss:" + id) === key) {
                    panel.hidden = true;
                } else if (details && window.localStorage.getItem("lu-result-open:" + id) === "1") {
                    details.open = true;
                }
            } catch (err) {
                /* Storage can be blocked. The line still collapses and dismisses for this page. */
            }
            if (details) {
                details.addEventListener("toggle", function () {
                    try {
                        window.localStorage.setItem("lu-result-open:" + id, details.open ? "1" : "0");
                    } catch (err) {}
                });
            }
            if (dismiss) {
                dismiss.addEventListener("click", function (event) {
                    event.preventDefault();
                    event.stopPropagation();
                    panel.hidden = true;
                    try {
                        if (key) window.localStorage.setItem("lu-result-dismiss:" + id, key);
                    } catch (err) {}
                });
            }
        });
    }

    function bindBusy(root) {
        (root || document).querySelectorAll("form[data-kafka-busy]").forEach(function (form) {
            if (form.getAttribute("data-kafka-busy-bound") === "1") return;
            form.setAttribute("data-kafka-busy-bound", "1");
            form.addEventListener("submit", function (event) {
                var submitter = event.submitter;
                var action = (submitter && submitter.getAttribute("formaction")) || form.getAttribute("action") || "";
                if (action.indexOf("/plan") < 0) return;
                window.setTimeout(function () {
                    form.setAttribute("aria-busy", "true");
                    form.querySelectorAll("button[type='submit'], input[type='submit']").forEach(function (button) {
                        button.disabled = true;
                    });
                    var note = form.querySelector("[data-kafka-busy-note]");
                    if (note) note.hidden = false;
                }, 0);
            });
        });
    }

    function focusResult(root) {
        var panel = (root || document).querySelector("[data-kafka-result]");
        if (!panel || panel.hidden) return;
        var key = panel.id + ":" + (panel.getAttribute("data-plan-hash") || panel.getAttribute("data-result-key") || "");
        try {
            if (window.sessionStorage.getItem("kafka-result-seen") === key) return;
            window.sessionStorage.setItem("kafka-result-seen", key);
        } catch (err) {
            return;
        }
        panel.scrollIntoView({ block: "nearest" });
        if (typeof panel.focus === "function") panel.focus();
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
        if (statusName === "Done" || statusName === "Failed" || statusName === "Rejected" || statusName === "Cancelled" || statusName === "NeedsAttention" || statusName === "TimedOut")
            window.clearInterval(poll);
    }

    function pill(status) {
        if (status === "Stable" || status === "up" || status === "Done" || status === "Approved") return "au-badge-success";
        if (status === "Failed" || status === "Rejected" || status === "NeedsAttention" || status === "TimedOut" || status === "Dead" || status === "offline") return "au-badge-danger";
        if (status === "Cancelled" || status === "Empty") return "au-badge-warning";
        if (status === "Pending" || status === "Executing" || status === "Converging" || status === "Verifying") return "au-badge-active";
        return "au-badge-muted";
    }

    document.addEventListener("au-refreshed", function (event) {
        if (!event.detail || event.detail.id === "kafkaResults") paint();
    });
    paint();
})();

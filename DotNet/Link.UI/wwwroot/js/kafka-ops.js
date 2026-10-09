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
        wireMessageKeyword();
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

    function wireMessageKeyword() {
        if (document.documentElement.getAttribute("data-lu-keyword-wired") !== "1") {
            document.documentElement.setAttribute("data-lu-keyword-wired", "1");
            document.addEventListener("input", function (event) {
                var target = event.target;
                if (!target) return;
                if (target.id === "browse-q" || target.hasAttribute("data-lu-keyword-part"))
                    applyMessageKeyword();
            });
            document.addEventListener("submit", function (event) {
                var form = event.target;
                if (!form || !form.querySelector || !form.querySelector("#browse-q")) return;
                var box = document.getElementById("browse-q");
                var mq = form.querySelector("#browse-mq");
                if (box && mq) mq.value = box.value;
                var header = document.getElementById("browse-header");
                var hq = form.querySelector("#browse-hq");
                if (header && hq) hq.value = header.value;
                var value = document.getElementById("browse-value");
                var vq = form.querySelector("#browse-vq");
                if (value && vq) vq.value = value.value;
            });
            document.addEventListener("click", function (event) {
                var further = event.target && event.target.closest ? event.target.closest("[data-lu-further]") : null;
                if (!further) return;
                event.preventDefault();
                loadFurther(further);
            });
        }
        applyMessageKeyword();
    }

    function keywordParts() {
        var parts = [];
        var box = document.getElementById("browse-q");
        if (box && box.value.trim()) parts.push(box.value.trim().toLowerCase());
        document.querySelectorAll("[data-lu-keyword-part]").forEach(function (el) {
            var value = (el.value || "").trim().toLowerCase();
            if (value) parts.push(value);
        });
        return parts;
    }

    function applyMessageKeyword() {
        var table = document.querySelector("[data-lu-messages]");
        if (!table) return;
        var parts = keywordParts();
        var needle = parts.join("\n");
        var page = parseInt(table.getAttribute("data-lu-page") || "1", 10);
        if (table.getAttribute("data-lu-needle") !== needle) {
            table.setAttribute("data-lu-needle", needle);
            table.setAttribute("data-lu-page", "1");
            page = 1;
        }
        var rows = Array.prototype.slice.call(table.querySelectorAll("tr[data-lu-text]"));
        var matched = [];
        rows.forEach(function (row) {
            var hay = (row.getAttribute("data-lu-text") || "").toLowerCase();
            var ok = parts.every(function (part) { return hay.indexOf(part) >= 0; });
            var detail = row.nextElementSibling;
            var detailRow = detail && detail.classList.contains("lu-msg-row") ? detail : null;
            if (!ok) {
                row.hidden = true;
                if (detailRow) detailRow.hidden = true;
                return;
            }
            matched.push({ row: row, detail: detailRow });
        });
        var client = table.getAttribute("data-lu-client") === "1";
        var size = parseInt(table.getAttribute("data-lu-page-size") || "25", 10);
        if (!size || size < 1) size = 25;
        if (!client) {
            matched.forEach(function (item) {
                item.row.hidden = false;
                if (item.detail) item.detail.hidden = false;
            });
            paintClientPager(table, 1, 1);
            return;
        }
        var pages = Math.max(1, Math.ceil(matched.length / size));
        if (page > pages) page = pages;
        if (page < 1) page = 1;
        table.setAttribute("data-lu-page", String(page));
        matched.forEach(function (item, index) {
            var show = index >= (page - 1) * size && index < page * size;
            item.row.hidden = !show;
            if (item.detail) item.detail.hidden = !show;
        });
        paintClientPager(table, page, pages);
    }

    function paintClientPager(table, page, pages) {
        var host = document.querySelector("[data-lu-client-pager]");
        if (!host) return;
        host.replaceChildren();
        if (!table || table.getAttribute("data-lu-client") !== "1") return;
        var note = document.createElement("span");
        note.className = "small text-muted";
        note.textContent = "Page " + page + " of " + pages;
        host.appendChild(note);
        function go(label, icon, target, enabled) {
            var button = document.createElement("button");
            button.type = "button";
            button.className = "lu-page-step";
            button.disabled = !enabled;
            button.setAttribute("aria-label", label);
            button.title = label;
            var glyph = document.createElement("i");
            glyph.className = "bi " + icon;
            glyph.setAttribute("aria-hidden", "true");
            button.appendChild(glyph);
            button.addEventListener("click", function () {
                table.setAttribute("data-lu-page", String(target));
                table.setAttribute("data-lu-needle", keywordParts().join("\n"));
                applyMessageKeyword();
            });
            host.appendChild(button);
        }
        go("First", "bi-chevron-double-left", 1, page > 1);
        go("Previous", "bi-chevron-left", page - 1, page > 1);
        go("Next", "bi-chevron-right", page + 1, page < pages);
        go("Last", "bi-chevron-double-right", pages, page < pages);
    }

    function loadFurther(link) {
        if (link.getAttribute("data-lu-busy") === "1") return;
        var href = link.getAttribute("href") || "";
        if (!href) return;
        if (!document.querySelector("[data-lu-messages]")) {
            window.location.href = href;
            return;
        }
        link.setAttribute("data-lu-busy", "1");
        var url = href + (href.indexOf("?") >= 0 ? "&" : "?") + "batch=1";
        fetch(url, { headers: { "Accept": "application/json" }, credentials: "same-origin" })
            .then(function (response) {
                if (!response.ok) throw new Error("read failed");
                return response.json();
            })
            .then(function (body) {
                appendMessages(body);
                if (body && body.more && body.resume) link.href = replaceQuery(href, "resume", body.resume);
                else link.hidden = true;
                var scan = document.querySelector("[data-lu-scan]");
                if (scan && body && body.scanned) scan.textContent = "Searched the latest " + body.scanned + " messages.";
                applyMessageKeyword();
            })
            .catch(function () {
                window.location.href = href;
            })
            .finally(function () {
                link.removeAttribute("data-lu-busy");
            });
    }

    function appendMessages(body) {
        var table = document.querySelector("[data-lu-messages]");
        var bodyEl = table ? table.querySelector("tbody") : null;
        if (!table || !bodyEl || !body || !Array.isArray(body.records)) return;
        var seen = {};
        table.querySelectorAll("tr[data-lu-id]").forEach(function (row) {
            seen[row.getAttribute("data-lu-id")] = true;
        });
        body.records.forEach(function (record) {
            var id = String(record.partition) + ":" + String(record.offset);
            if (seen[id]) return;
            seen[id] = true;
            var tr = document.createElement("tr");
            tr.setAttribute("data-lu-text", record.text || "");
            tr.setAttribute("data-lu-id", id);
            function cell(text) {
                var td = document.createElement("td");
                td.textContent = text == null ? "" : String(text);
                return td;
            }
            tr.appendChild(cell(record.partition));
            tr.appendChild(cell(record.offset));
            tr.appendChild(cell(record.time));
            var key = document.createElement("td");
            var code = document.createElement("code");
            code.className = "small";
            code.textContent = record.key || "";
            key.appendChild(code);
            tr.appendChild(key);
            tr.appendChild(cell(record.summary));
            tr.appendChild(cell("—"));
            var open = document.createElement("td");
            var anchor = document.createElement("a");
            anchor.className = "btn btn-sm btn-au-link";
            anchor.href = record.openHref || "#";
            anchor.textContent = "Open";
            open.appendChild(anchor);
            tr.appendChild(open);
            bodyEl.appendChild(tr);
        });
        var note = document.querySelector("[data-lu-loaded]");
        if (note) {
            var count = table.querySelectorAll("tr[data-lu-text]").length;
            note.textContent = count === 1 ? "1 message loaded" : count + " messages loaded";
        }
    }

    function replaceQuery(href, key, value) {
        var hashAt = href.indexOf("#");
        var hash = hashAt >= 0 ? href.slice(hashAt) : "";
        var base = hashAt >= 0 ? href.slice(0, hashAt) : href;
        var q = base.indexOf("?");
        var path = q >= 0 ? base.slice(0, q) : base;
        var params = new URLSearchParams(q >= 0 ? base.slice(q + 1) : "");
        params.set(key, value);
        params.delete("batch");
        return path + "?" + params.toString() + hash;
    }

    document.addEventListener("au-refreshed", function (event) {
        if (!event.detail || event.detail.id === "kafkaResults") paint();
    });
    paint();
})();

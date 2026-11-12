(function () {
    var root = document.getElementById("launchLive");
    if (!root) return;

    function bindCancel(button, form, title, message, details) {
        if (!button || !form) return;
        button.addEventListener("click", function () {
            if (!window.luHoldConfirm) {
                form.submit();
                return;
            }
            window.luHoldConfirm.ask(button, title, message, details, function () { form.submit(); });
        });
    }

    bindCancel(
        document.getElementById("launchCancelButton"),
        document.getElementById("launchCancel"),
        "Cancel this launch",
        "Cancel every EHR that has not finished? Reports that already finished stay as they are.",
        ["Queued reports are not started.", "Reports in progress are cancelled.", "The other EHR systems are cancelled too."]);

    root.querySelectorAll("[data-cancel-batch]").forEach(function (form) {
        if (form.tagName !== "FORM") return;
        bindCancel(
            form.querySelector("[data-cancel-batch-button]"),
            form,
            "Cancel this EHR",
            "Cancel this EHR's reports that have not finished? The other EHR systems keep running.",
            ["Queued reports in this EHR are not started.", "Reports in progress in this EHR are cancelled."]);
    });

    if (root.getAttribute("data-open") !== "yes") return;
    var url = root.getAttribute("data-status-url");
    if (!url) return;

    var timer = 0;
    var ticket = 0;

    function stop() {
        if (timer) clearInterval(timer);
        timer = 0;
        var live = document.getElementById("launchLiveBadge");
        if (live) live.remove();
    }

    function text(id, value) {
        var node = document.getElementById(id);
        if (!node) return;
        node.textContent = value == null || value === "" ? "—" : String(value);
    }

    function paintRun(cell, item) {
        if (!cell) return;
        if (item.runHref) {
            var link = cell.querySelector("a");
            if (!link) {
                cell.textContent = "";
                link = document.createElement("a");
                link.setAttribute("data-item-run", "");
                cell.appendChild(link);
            }
            link.textContent = item.runId || "";
            link.setAttribute("href", item.runHref);
            return;
        }
        cell.textContent = "—";
    }

    function count(box, name, value) {
        if (!box) return;
        var node = box.querySelector('[data-count="' + name + '"]');
        if (node) node.textContent = String(value);
    }

    function apply(page) {
        if (!page || !page.found || !page.launch) return;

        var launch = page.launch;
        text("launchTotal", launch.total);
        text("launchPending", launch.pending);
        text("launchInProgress", launch.inProgress);
        text("launchCompleted", launch.completed);
        text("launchFailed", launch.failed);
        text("launchCancelled", launch.cancelled);

        (launch.groups || []).forEach(function (group) {
            var badge = root.querySelector('[data-ehr-status="' + group.batchId + '"]');
            if (badge) {
                badge.textContent = group.status || "Unknown";
                badge.className = "badge " + (group.pillClass || "au-badge-muted");
            }
            var counts = root.querySelector('[data-ehr-counts="' + group.batchId + '"]');
            count(counts, "cap", group.maxConcurrency);
            count(counts, "pending", group.pending);
            count(counts, "inProgress", group.inProgress);
            count(counts, "completed", group.completed);
            count(counts, "failed", group.failed);
            count(counts, "cancelled", group.cancelled);

            var cancel = root.querySelector('form[data-cancel-batch="' + group.batchId + '"]');
            if (cancel) cancel.classList.toggle("d-none", !group.canCancel);

            (group.items || []).forEach(function (item) {
                var row = root.querySelector('[data-batch="' + group.batchId + '"][data-ordinal="' + item.ordinal + '"]');
                if (!row) return;
                var itemStatus = row.querySelector("[data-item-status]");
                if (itemStatus) {
                    itemStatus.textContent = item.status || "Unknown";
                    itemStatus.className = "badge " + (item.pillClass || "au-badge-muted");
                }
                var error = row.querySelector("[data-item-error]");
                if (error) {
                    var next = item.error || "—";
                    if (error.textContent !== next) error.textContent = next;
                }
                paintRun(row.querySelector("[data-item-run-cell]"), item);
            });
        });

        var launchCancel = document.getElementById("launchCancel");
        if (launchCancel) launchCancel.classList.toggle("d-none", !launch.canCancel);
        if (!launch.open) stop();
    }

    function refresh() {
        var current = ++ticket;
        fetch(url, { headers: { "Accept": "application/json" }, cache: "no-store" })
            .then(function (response) {
                if (current !== ticket) return null;
                if (response.status === 404) {
                    stop();
                    return null;
                }
                if (!response.ok) return null;
                return response.json();
            })
            .then(function (page) {
                if (current !== ticket || !page) return;
                apply(page);
            })
            .catch(function () { });
    }

    refresh();
    timer = setInterval(refresh, 2000);
})();

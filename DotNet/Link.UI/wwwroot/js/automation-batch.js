(function () {
    var root = document.getElementById("batchLive");
    if (!root) return;

    var cancelForm = document.getElementById("batchCancel");
    var cancelButton = document.getElementById("batchCancelButton");
    if (cancelForm && cancelButton) {
        cancelButton.addEventListener("click", function () {
            if (!window.luHoldConfirm) {
                cancelForm.submit();
                return;
            }
            window.luHoldConfirm.ask(
                cancelButton,
                "Cancel this batch",
                "Cancel the reports that have not finished? Reports that already finished stay as they are.",
                ["Queued reports are not started.", "Reports in progress are cancelled."],
                function () { cancelForm.submit(); });
        });
    }

    if (root.getAttribute("data-open") !== "yes") return;
    var url = root.getAttribute("data-status-url");
    if (!url) return;

    var timer = 0;
    var ticket = 0;

    function stop() {
        if (timer) clearInterval(timer);
        timer = 0;
        var live = document.getElementById("batchLiveBadge");
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

    function apply(page) {
        if (!page || !page.found || !page.batch) {
            stop();
            return;
        }

        var batch = page.batch;
        var status = document.getElementById("batchStatus");
        if (status) {
            status.textContent = batch.status || "Unknown";
            status.className = "badge " + (batch.pillClass || "au-badge-muted");
        }
        text("batchEhr", batch.ehrSystem);
        text("batchCap", batch.maxConcurrency);
        text("batchCreated", batch.createdAt);
        text("batchStarted", batch.startedAt);
        text("batchFinished", batch.finishedAt);
        text("countTotal", batch.total);
        text("countPending", batch.pending);
        text("countInProgress", batch.inProgress);
        text("countCompleted", batch.completed);
        text("countFailed", batch.failed);
        text("countCancelled", batch.cancelled);
        if (window.luPaintTimes) window.luPaintTimes(root);

        (batch.groups || []).forEach(function (group) {
            (group.items || []).forEach(function (item) {
                var row = root.querySelector('[data-ordinal="' + item.ordinal + '"]');
                if (!row) return;
                var itemStatus = row.querySelector("[data-item-status]");
                if (itemStatus) {
                    itemStatus.textContent = item.status || "Unknown";
                    itemStatus.className = "badge " + (item.pillClass || "au-badge-muted");
                }
                var error = row.querySelector("[data-item-error]");
                if (error) error.textContent = item.error || "—";
                paintRun(row.querySelector("[data-item-run-cell]"), item);
            });
        });

        if (cancelForm) cancelForm.classList.toggle("d-none", !batch.canCancel);
        if (!batch.open) stop();
    }

    function refresh() {
        var current = ++ticket;
        fetch(url, { headers: { "Accept": "application/json" }, cache: "no-store" })
            .then(function (response) {
                if (!response.ok) {
                    stop();
                    return null;
                }
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

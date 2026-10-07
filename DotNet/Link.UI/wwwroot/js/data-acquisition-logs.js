(function () {
    if (window.__dataAcquisitionLogs) return;
    window.__dataAcquisitionLogs = true;

    var refreshMs = 12000;
    var timer = null;
    var currentUrl = "";

    function root() {
        return document.getElementById("dataAcqLogModal");
    }

    function list() {
        return document.getElementById("dataAcqLogList");
    }

    function busy() {
        var node = list();
        if (node && node.querySelector("input[type=checkbox]:checked")) return true;
        var active = document.activeElement;
        if (active && node && node.contains(active)) return true;
        var search = document.getElementById("daLogSearch");
        return !!(search && active === search);
    }

    function startUrl() {
        var modal = root();
        if (!modal) return "";
        var params = new URLSearchParams();
        var runId = modal.getAttribute("data-run-id") || "";
        var facilityId = modal.getAttribute("data-facility-id") || "";
        if (runId) params.set("runId", runId);
        if (facilityId) params.set("facilityId", facilityId);
        var search = document.getElementById("daLogSearch");
        var term = search ? search.value.trim() : "";
        if (term) params.set("searchTerm", term);
        var base = modal.getAttribute("data-panel-url") || "";
        var query = params.toString();
        return query ? base + "?" + query : base;
    }

    function load(url) {
        var node = list();
        if (!node || !url) return;
        currentUrl = url;
        fetch(url, { headers: { "X-Requested-With": "fetch" }, cache: "no-store" })
            .then(function (res) {
                if (!res.ok) {
                    node.textContent = "Failed to load logs.";
                    return;
                }
                return res.text().then(function (html) {
                    if (currentUrl !== url || !node.isConnected) return;
                    node.innerHTML = html;
                    if (window.luPaintTimes) window.luPaintTimes(node);
                });
            })
            .catch(function () {
                if (currentUrl === url && node.isConnected)
                    node.textContent = "Failed to load logs.";
            });
    }

    document.addEventListener("show.bs.modal", function (event) {
        if (!event.target || event.target.id !== "dataAcqLogModal") return;
        load(startUrl());
        if (timer) clearInterval(timer);
        timer = setInterval(function () {
            if (document.hidden || busy() || !currentUrl) return;
            load(currentUrl);
        }, refreshMs);
    });

    document.addEventListener("hidden.bs.modal", function (event) {
        if (!event.target || event.target.id !== "dataAcqLogModal") return;
        if (timer) clearInterval(timer);
        timer = null;
    });

    document.addEventListener("click", function (event) {
        var link = event.target && event.target.closest
            ? event.target.closest("#dataAcqLogModal a[data-da-panel]")
            : null;
        if (!link || event.defaultPrevented || event.button !== 0) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        load(link.href);
    });

    document.addEventListener("input", function (event) {
        if (!event.target || event.target.id !== "daLogSearch") return;
        var input = event.target;
        clearTimeout(input._daTimer);
        input._daTimer = setTimeout(function () { load(startUrl()); }, 250);
    });
})();

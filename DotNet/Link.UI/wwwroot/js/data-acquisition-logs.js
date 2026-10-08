(function () {
    if (window.__dataAcquisitionLogs) return;
    window.__dataAcquisitionLogs = true;

    var refreshMs = 12000;

    function listOf(host) {
        return host.querySelector("[data-da-list]");
    }

    function checkedKeys(node) {
        return Array.prototype.map.call(node.querySelectorAll("input[type=checkbox][name=ids]:checked"), function (box) {
            return box.value;
        });
    }

    function restoreChecked(node, values) {
        if (!values || !values.length) return;
        Array.prototype.forEach.call(node.querySelectorAll("input[type=checkbox][name=ids]"), function (box) {
            if (values.indexOf(box.value) >= 0) box.checked = true;
        });
    }

    function hold(host) {
        if (document.hidden) return true;
        var active = document.activeElement;
        if (!active || !host.contains(active)) return false;
        var tag = active.tagName;
        return tag === "INPUT" || tag === "SELECT" || tag === "TEXTAREA" || tag === "BUTTON";
    }

    function scopeUrl(host) {
        var params = new URLSearchParams();
        if (host.dataset.runId) params.set("runId", host.dataset.runId);
        if (host.dataset.facilityId) params.set("facilityId", host.dataset.facilityId);
        if (host.dataset.reportId) params.set("reportId", host.dataset.reportId);
        if (host.dataset.patientId) params.set("patientId", host.dataset.patientId);
        var search = host.querySelector("[data-da-search]");
        var term = search ? search.value.trim() : "";
        if (term) params.set("searchTerm", term);
        var base = host.getAttribute("data-panel-url") || "";
        var query = params.toString();
        return query ? base + "?" + query : base;
    }

    function load(host, url) {
        var node = listOf(host);
        if (!node || !url) return;
        host._daUrl = url;
        var held = checkedKeys(node);
        fetch(url, { headers: { "X-Requested-With": "fetch" }, cache: "no-store" })
            .then(function (res) {
                if (!res.ok) {
                    node.textContent = "Failed to load logs.";
                    return;
                }
                return res.text().then(function (html) {
                    if (host._daUrl !== url || !node.isConnected) return;
                    node.innerHTML = html;
                    restoreChecked(node, held);
                    if (window.luPaintTimes) window.luPaintTimes(node);
                    document.dispatchEvent(new CustomEvent("au-refreshed", { detail: { id: node.id || "da-list" } }));
                });
            })
            .catch(function () {
                if (host._daUrl === url && node.isConnected)
                    node.textContent = "Failed to load logs.";
            });
    }

    function watch(host) {
        if (host._daTimer) clearInterval(host._daTimer);
        load(host, scopeUrl(host));
        host._daTimer = setInterval(function () {
            if (hold(host) || !host._daUrl) return;
            load(host, host._daUrl);
        }, refreshMs);
    }

    function stop(host) {
        if (host._daTimer) clearInterval(host._daTimer);
        host._daTimer = null;
    }

    document.addEventListener("show.bs.modal", function (event) {
        var host = event.target && event.target.closest ? event.target.closest("[data-da-host]") : null;
        if (!host || host !== event.target) return;
        watch(host);
    });

    document.addEventListener("hidden.bs.modal", function (event) {
        var host = event.target && event.target.closest ? event.target.closest("[data-da-host]") : null;
        if (!host || host !== event.target) return;
        stop(host);
    });

    document.addEventListener("click", function (event) {
        var link = event.target && event.target.closest ? event.target.closest("[data-da-host] a[data-da-panel]") : null;
        if (!link || event.defaultPrevented || event.button !== 0) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        var host = link.closest("[data-da-host]");
        if (!host) return;
        event.preventDefault();
        load(host, link.href);
    });

    document.addEventListener("input", function (event) {
        var input = event.target;
        if (!input || !input.hasAttribute("data-da-search")) return;
        var host = input.closest("[data-da-host]");
        if (!host) return;
        clearTimeout(input._daTimer);
        input._daTimer = setTimeout(function () { load(host, scopeUrl(host)); }, 250);
    });

    document.querySelectorAll("[data-da-host][data-da-autoload]").forEach(watch);
})();

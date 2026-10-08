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
        if (host.getAttribute("data-header-owns-ids") === "yes") params.set("headerOwnsIds", "true");
        var search = host.querySelector("[data-da-search]");
        var term = search ? search.value.trim() : "";
        if (term) params.set("searchTerm", term);
        var here = window.location.pathname + window.location.search;
        if (here.charAt(0) === "/" && here.indexOf("://") < 0 && here.indexOf("\\") < 0)
            params.set("returnUrl", here);
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

    function toast(message) {
        var host = document.getElementById("auToasts");
        if (!host || !message) return;
        var el = document.createElement("div");
        el.className = "toast au-toast border-0";
        el.setAttribute("role", "status");
        el.innerHTML = '<div class="d-flex"><div class="toast-body"></div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>';
        el.querySelector(".toast-body").textContent = message;
        host.appendChild(el);
        if (window.bootstrap && bootstrap.Toast) {
            var item = bootstrap.Toast.getOrCreateInstance(el, { delay: 4000 });
            el.addEventListener("hidden.bs.toast", function () { el.remove(); });
            item.show();
        }
    }

    function messageFrom(html) {
        var doc = new DOMParser().parseFromString(html, "text/html");
        var alert = doc.querySelector(".alert-success, .alert-warning, .alert-danger");
        return alert ? alert.textContent.replace(/\s+/g, " ").trim() : "";
    }

    // Embedded hosts (the facility modal, a report tab, a run) post log actions
    // in place. The dedicated Logs page has no data-da-host, so it still navigates.
    document.addEventListener("submit", function (event) {
        var form = event.target;
        if (!form || !form.closest || !form.hasAttribute("data-au-save")) return;
        if (!form.closest("[data-da-host]")) return;
        form.removeAttribute("data-au-save");
        form._daSave = true;
    }, true);

    document.addEventListener("submit", function (event) {
        var form = event.target;
        if (!form || !form._daSave) return;
        var host = form.closest("[data-da-host]");
        if (event.defaultPrevented || !host) {
            form.setAttribute("data-au-save", "");
            delete form._daSave;
            return;
        }
        event.preventDefault();
        var submitter = event.submitter;
        var explicit = submitter && submitter.hasAttribute("formaction") ? submitter.formAction : "";
        var action = explicit || form.action;
        var body = submitter ? new FormData(form, submitter) : new FormData(form);
        if (submitter) submitter.disabled = true;
        fetch(action, {
            method: "POST",
            body: body,
            headers: { "X-Requested-With": "fetch" },
            redirect: "follow"
        }).then(function (res) {
            var type = res.headers.get("content-type") || "";
            if (!res.ok || type.indexOf("text/html") === -1) {
                toast(!res.ok
                    ? "Could not save that change. The server returned HTTP " + res.status + "."
                    : "Could not save that change.");
                return;
            }
            return res.text().then(function (html) {
                toast(messageFrom(html) || "Saved.");
                if (host.isConnected) load(host, scopeUrl(host));
            });
        }).catch(function () {
            toast("Could not save that change.");
        }).finally(function () {
            if (form.isConnected) form.setAttribute("data-au-save", "");
            delete form._daSave;
            if (submitter && submitter.isConnected) submitter.disabled = false;
        });
    });

    document.querySelectorAll("[data-da-host][data-da-autoload]").forEach(watch);
})();

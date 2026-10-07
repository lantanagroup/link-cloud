(function () {
    function schedule(form) {
        var wait = Number(form.getAttribute("data-au-filter")) || 300;
        clearTimeout(form._auFilter);
        form._auFilter = setTimeout(function () {
            if (typeof form.requestSubmit === "function") form.requestSubmit();
            else form.submit();
        }, wait);
    }

    document.addEventListener("input", function (event) {
        var form = event.target && event.target.form;
        if (!form || !form.hasAttribute("data-au-filter")) return;
        var page = form.querySelector("input[name=page]");
        if (page) page.value = "1";
        schedule(form);
    });

    document.addEventListener("change", function (event) {
        var form = event.target && event.target.form;
        if (!form || !form.hasAttribute("data-au-filter")) return;
        if (event.target.name === "page") return;
        var page = form.querySelector("input[name=page]");
        if (page && event.target.name !== "pageSize") page.value = "1";
        schedule(form);
    });

    function selectionHeld(node) {
        return !!node.querySelector("input[type=checkbox]:checked");
    }

    function focusedIn(node) {
        var active = document.activeElement;
        if (!active || active === document.body) return false;
        if (node.contains(active)) return true;
        var form = document.querySelector("form[data-au-target='" + node.id + "']");
        return !!(form && form.contains(active));
    }

    function replaceRegion(node, url, push) {
        if (!node || !node.id) return Promise.resolve();
        return fetch(url, { headers: { "X-Requested-With": "fetch" } }).then(function (res) {
            if (!res.ok) return;
            return res.text().then(function (html) {
                if (!node.isConnected) return;
                if (!push && (selectionHeld(node) || focusedIn(node) || document.hidden)) return;
                var doc = new DOMParser().parseFromString(html, "text/html");
                var fresh = doc.getElementById(node.id);
                if (!fresh) return;
                if (fresh.innerHTML !== node.innerHTML) node.innerHTML = fresh.innerHTML;
                if (push) history.replaceState(null, "", url);
                document.dispatchEvent(new CustomEvent("au-refreshed", { detail: { id: node.id } }));
            });
        }).catch(function () { /* leave the current page in place */ });
    }

    document.addEventListener("submit", function (event) {
        var form = event.target;
        if (!form || !form.hasAttribute("data-au-filter")) return;
        if (event.defaultPrevented) return;
        var targetId = form.getAttribute("data-au-target");
        var target = targetId && document.getElementById(targetId);
        if (!target || form.method.toLowerCase() !== "get") return;
        event.preventDefault();
        var params = new URLSearchParams(new FormData(form));
        var url = form.getAttribute("action") || window.location.pathname;
        var query = params.toString();
        var next = query ? url + "?" + query : url;
        replaceRegion(target, next, true);
    });

    document.addEventListener("click", function (event) {
        var link = event.target && event.target.closest ? event.target.closest("a[href]") : null;
        if (!link || event.defaultPrevented || event.button !== 0) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        if (link.target && link.target !== "_self") return;
        var region = link.closest("[data-au-refresh]");
        if (!region) return;
        var url;
        try { url = new URL(link.href, window.location.href); }
        catch (e) { return; }
        if (url.origin !== window.location.origin) return;
        if (url.pathname !== window.location.pathname) return;
        event.preventDefault();
        replaceRegion(region, url.pathname + url.search + url.hash, true);
    });

    function showToast(message) {
        var host = document.getElementById("auToasts");
        if (!host || !message) return;
        var el = document.createElement("div");
        el.className = "toast au-toast border-0";
        el.setAttribute("role", "status");
        el.innerHTML = '<div class="d-flex"><div class="toast-body"></div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>';
        el.querySelector(".toast-body").textContent = message;
        host.appendChild(el);
        if (window.bootstrap && bootstrap.Toast) {
            var toast = bootstrap.Toast.getOrCreateInstance(el, { delay: 4000 });
            el.addEventListener("hidden.bs.toast", function () { el.remove(); });
            toast.show();
        }
    }

    function skipScript(src) {
        return src.indexOf("jquery") >= 0
            || src.indexOf("bootstrap") >= 0
            || src.indexOf("live-region") >= 0
            || src.indexOf("au-data-table") >= 0;
    }

    function activateScripts(root) {
        var scripts = Array.prototype.slice.call(root.querySelectorAll("script"));
        var chain = Promise.resolve();
        scripts.forEach(function (old) {
            var src = old.getAttribute("src") || "";
            if (skipScript(src)) {
                old.remove();
                return;
            }
            chain = chain.then(function () {
                return new Promise(function (resolve) {
                    var fresh = document.createElement("script");
                    if (src) {
                        fresh.src = src;
                        fresh.onload = resolve;
                        fresh.onerror = resolve;
                        old.replaceWith(fresh);
                    } else {
                        fresh.textContent = old.textContent;
                        old.replaceWith(fresh);
                        resolve();
                    }
                });
            });
        });
        return chain;
    }

    function rerunEditorScripts(doc) {
        document.querySelectorAll("script[data-au-rerun]").forEach(function (node) { node.remove(); });
        var scripts = Array.prototype.slice.call(doc.body.querySelectorAll("script"));
        var chain = Promise.resolve();
        scripts.forEach(function (old) {
            if (old.closest(".lu-content")) return;
            var src = old.getAttribute("src") || "";
            var text = old.textContent || "";
            if (skipScript(src)) return;
            if (src.indexOf("configuration-deep-link") < 0
                && text.indexOf("auRefreshPage") < 0
                && text.indexOf("SaveInline") < 0)
                return;
            chain = chain.then(function () {
                return new Promise(function (resolve) {
                    var fresh = document.createElement("script");
                    fresh.setAttribute("data-au-rerun", "1");
                    if (src) {
                        fresh.src = src;
                        fresh.onload = resolve;
                        fresh.onerror = resolve;
                        document.body.appendChild(fresh);
                    } else {
                        fresh.textContent = text;
                        document.body.appendChild(fresh);
                        resolve();
                    }
                });
            });
        });
        return chain;
    }

    function swapContent(doc, url, message, push) {
        var next = doc.querySelector(".lu-content");
        var current = document.querySelector(".lu-content");
        if (!next || !current) {
            if (url) window.location.assign(url);
            return Promise.resolve();
        }
        var y = window.scrollY;
        current.innerHTML = next.innerHTML;
        window.scrollTo(0, y);
        if (push && url && url !== window.location.href) history.pushState(null, "", url);
        if (message) showToast(message);
        return activateScripts(current).then(function () {
            scanRefresh();
            document.dispatchEvent(new CustomEvent("au-refreshed", { detail: { id: "lu-content" } }));
        });
    }

    function applyPage(html, url) {
        var doc = new DOMParser().parseFromString(html, "text/html");
        var next = doc.querySelector(".lu-content");
        var message = "";
        if (next) {
            var success = next.querySelector(".alert-success");
            message = success ? success.textContent.trim() : "";
            if (success) success.remove();
        }
        return swapContent(doc, url, message, true);
    }

    window.auRefreshPage = function (message) {
        return fetch(window.location.href, { headers: { "X-Requested-With": "fetch" } }).then(function (res) {
            if (!res.ok) {
                showToast("Could not refresh this page.");
                return;
            }
            return res.text().then(function (html) {
                var doc = new DOMParser().parseFromString(html, "text/html");
                return swapContent(doc, null, message || "Saved.", false).then(function () {
                    return rerunEditorScripts(doc);
                });
            });
        }).catch(function () {
            showToast("Could not refresh this page.");
        });
    };

    document.addEventListener("submit", function (event) {
        var form = event.target;
        if (!form || !form.hasAttribute("data-au-save")) return;
        if (event.defaultPrevented) return;
        if ((form.method || "").toLowerCase() !== "post") return;
        event.preventDefault();
        var submitter = event.submitter;
        var action = (submitter && submitter.formAction) || form.action;
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
                showToast("Could not save that change.");
                return;
            }
            return res.text().then(function (html) { applyPage(html, res.url); });
        }).catch(function () {
            showToast("Could not save that change.");
        }).finally(function () {
            if (submitter) submitter.disabled = false;
        });
    });

    function refresh(node) {
        if (!node.isConnected) {
            clearInterval(node._auTimer);
            node._auWatch = false;
            return;
        }
        if (document.hidden || focusedIn(node) || selectionHeld(node) || node._auBusy) return;
        node._auBusy = true;
        replaceRegion(node, window.location.href, false).finally(function () { node._auBusy = false; });
    }

    function watch(node) {
        if (node._auWatch) return;
        node._auWatch = true;
        var ms = Number(node.getAttribute("data-au-refresh")) || 12000;
        node._auTimer = setInterval(function () { refresh(node); }, ms);
    }

    function scanRefresh() {
        document.querySelectorAll("[data-au-refresh]").forEach(watch);
    }

    scanRefresh();
    document.addEventListener("au-refreshed", scanRefresh);
})();

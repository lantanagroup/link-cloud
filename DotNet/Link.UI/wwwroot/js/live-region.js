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

    function bulkCount(form, flag) {
        var count = 0;
        Array.prototype.forEach.call(form.querySelectorAll("input[type=checkbox][name=ids]:checked"), function (box) {
            if (box.getAttribute(flag) === "yes") count += 1;
        });
        return count;
    }

    function paintBulk(form) {
        var process = bulkCount(form, "data-can-process");
        var cancel = bulkCount(form, "data-can-cancel");
        var processButton = form.querySelector("[data-au-bulk=process]");
        var cancelButton = form.querySelector("[data-au-bulk=cancel]");
        if (processButton) {
            processButton.disabled = process === 0;
            processButton.textContent = process === 0 ? "Process selected" : "Process " + process + " selected";
        }
        if (cancelButton) {
            cancelButton.disabled = cancel === 0;
            cancelButton.textContent = cancel === 0 ? "Cancel selected" : "Cancel " + cancel + " selected";
        }
        var pageBox = form.querySelector("[data-au-select-page]");
        var enabled = form.querySelectorAll("input[type=checkbox][name=ids]");
        if (pageBox) {
            var checked = form.querySelectorAll("input[type=checkbox][name=ids]:checked").length;
            pageBox.disabled = enabled.length === 0;
            pageBox.checked = enabled.length > 0 && checked === enabled.length;
            pageBox.indeterminate = checked > 0 && checked < enabled.length;
        }
    }

    function wireBulk(form) {
        if (!form || form.getAttribute("data-au-bulk-wired") === "yes") return;
        form.setAttribute("data-au-bulk-wired", "yes");
        form.addEventListener("change", function (event) {
            var target = event.target;
            if (!target || target.type !== "checkbox") return;
            if (target.hasAttribute("data-au-select-page")) {
                var on = target.checked;
                Array.prototype.forEach.call(form.querySelectorAll("input[type=checkbox][name=ids]"), function (box) {
                    box.checked = on;
                });
            }
            paintBulk(form);
        });
        form.addEventListener("click", function (event) {
            var button = event.target && event.target.closest ? event.target.closest("[data-au-bulk]") : null;
            if (!button || button.disabled) return;
            var kind = button.getAttribute("data-au-bulk");
            var count = bulkCount(form, kind === "cancel" ? "data-can-cancel" : "data-can-process");
            var message = kind === "cancel"
                ? "Cancel " + count + " selected logs that are old enough? This cannot be undone."
                : "Process " + count + " selected acquisition logs?";
            if (!window.confirm(message)) event.preventDefault();
        });
        paintBulk(form);
    }

    function wireBulkForms(root) {
        (root || document).querySelectorAll("form#acquisition-logs").forEach(wireBulk);
    }

    function focusedIn(node) {
        var active = document.activeElement;
        if (!active || active === document.body) return false;
        if (node.contains(active)) return true;
        var form = document.querySelector("form[data-au-target='" + node.id + "']");
        return !!(form && form.contains(active));
    }

    function declaredPageUrl(node) {
        var page = node && node.getAttribute ? node.getAttribute("data-au-page-url") : "";
        if (!page) {
            var marked = document.querySelector("[data-au-page-url]");
            page = marked ? marked.getAttribute("data-au-page-url") : "";
        }
        if (!page) return "";
        try {
            var absolute = new URL(page, window.location.href);
            if (absolute.origin !== window.location.origin) return "";
            return absolute.pathname + absolute.search + absolute.hash;
        } catch (e) {
            return "";
        }
    }

    // A dry run posts to an action that has no GET. The address bar then names
    // that action, and a later refresh would receive 405. Use the page URL.
    function regionUrl(node) {
        var explicit = node.getAttribute("data-au-refresh-url");
        if (explicit) return explicit;
        var page = declaredPageUrl(node);
        if (!page) return window.location.href;
        try {
            var here = new URL(window.location.href);
            var target = new URL(page, window.location.href);
            if (here.pathname !== target.pathname) return page;
        } catch (e) { /* keep the address bar */ }
        return window.location.href;
    }

    function alignPostedPage(node) {
        var page = declaredPageUrl(node);
        if (!page || !window.history || !history.replaceState) return;
        try {
            var here = new URL(window.location.href);
            var target = new URL(page, window.location.href);
            if (here.pathname === target.pathname) return;
            history.replaceState(null, "", page);
        } catch (e) { /* leave the address bar */ }
    }

    function replaceRegion(node, url, push) {
        if (!node || !node.id) return Promise.resolve();
        var init = { headers: { "X-Requested-With": "fetch" } };
        if (node.hasAttribute("data-au-refresh-url")) init.cache = "no-store";
        return fetch(url, init).then(function (res) {
            if (!res.ok) return;
            return res.text().then(function (html) {
                if (!node.isConnected) return;
                if (!push && (focusedIn(node) || document.hidden || node.querySelector("#kafka-produce.lu-produce-hold, [data-produce-dirty='1']"))) return;
                var held = checkedKeys(node);
                var doc = new DOMParser().parseFromString(html, "text/html");
                var fresh = doc.getElementById(node.id);
                if (!fresh) return;
                if (fresh.innerHTML !== node.innerHTML) node.innerHTML = fresh.innerHTML;
                restoreChecked(node, held);
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

    // Bootstrap appends its backdrop to document.body. A dialog left inside the
    // scrolling shell paints under that backdrop, so dialogs live on the body.
    function liftDialogs() {
        document.querySelectorAll(".modal, .offcanvas").forEach(function (node) {
            var id = node.id;
            if (id) {
                document.querySelectorAll("body > [data-lu-lifted]").forEach(function (old) {
                    if (old !== node && old.id === id) old.remove();
                });
            }
            if (node.parentElement !== document.body)
                document.body.appendChild(node);
            node.setAttribute("data-lu-lifted", "1");
        });
    }

    function dropLiftedDialogs() {
        document.querySelectorAll("body > [data-lu-lifted]").forEach(function (node) {
            node.remove();
        });
        document.querySelectorAll(".modal-backdrop, .offcanvas-backdrop").forEach(function (node) {
            node.remove();
        });
        document.body.classList.remove("modal-open");
        document.body.style.removeProperty("overflow");
        document.body.style.removeProperty("padding-right");
    }

    function swapContent(doc, url, message, push) {
        var next = doc.querySelector(".lu-content");
        var current = document.querySelector(".lu-content");
        if (!next || !current) {
            if (url) window.location.assign(url);
            return Promise.resolve();
        }
        dropLiftedDialogs();
        var scroller = document.querySelector(".lu-main") || document.scrollingElement;
        var y = scroller ? scroller.scrollTop : window.scrollY;
        current.innerHTML = next.innerHTML;
        if (scroller) scroller.scrollTop = y;
        else window.scrollTo(0, y);
        if (push && url && url !== window.location.href) history.pushState(null, "", url);
        if (message) showToast(message);
        return activateScripts(current).then(function () {
            liftDialogs();
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
        // A submit button with no formaction reports the current document URL.
        // Use that URL only when the button actually sets formaction.
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
                showToast(!res.ok
                    ? "Could not save that change. The server returned HTTP " + res.status + "."
                    : "Could not save that change.");
                return;
            }
            if (form.getAttribute("data-au-save") === "navigate") {
                try {
                    var nextUrl = new URL(res.url, window.location.href);
                    if (nextUrl.origin === window.location.origin && nextUrl.pathname !== window.location.pathname) {
                        window.location.assign(nextUrl.pathname + nextUrl.search + nextUrl.hash);
                        return;
                    }
                } catch (e) { /* keep the validation page inline */ }
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
        if (document.hidden || focusedIn(node) || node._auBusy) return;
        node._auBusy = true;
        replaceRegion(node, regionUrl(node), false).finally(function () { node._auBusy = false; });
    }

    function watch(node) {
        if (node._auWatch) return;
        node._auWatch = true;
        alignPostedPage(node);
        var ms = Number(node.getAttribute("data-au-refresh")) || 12000;
        if (node.hasAttribute("data-au-refresh-url")) {
            setTimeout(function () { refresh(node); }, 0);
        }
        node._auTimer = setInterval(function () { refresh(node); }, ms);
    }

    function scanRefresh() {
        document.querySelectorAll("[data-au-refresh]").forEach(watch);
    }

    liftDialogs();
    scanRefresh();
    wireBulkForms(document);
    document.addEventListener("au-refreshed", function () {
        liftDialogs();
        scanRefresh();
        wireBulkForms(document);
    });

    document.addEventListener("click", function (event) {
        var copy = event.target && event.target.closest ? event.target.closest("[data-lu-copy]") : null;
        if (copy) {
            event.preventDefault();
            var value = copy.getAttribute("data-lu-copy") || "";
            var shown = copy.parentElement && copy.parentElement.querySelector
                ? copy.parentElement.querySelector(".lu-facility-id, .lu-clip")
                : null;
            var shownText = shown && shown.textContent ? shown.textContent.trim() : "";
            if (shownText && shownText !== "—" && shownText !== "-") value = shownText;
            var icon = copy.querySelector("i");
            var label = copy.getAttribute("aria-label") || "Copy";
            var done = function () {
                if (icon) {
                    icon.classList.remove("bi-copy");
                    icon.classList.add("bi-check2");
                }
                copy.setAttribute("title", "Copied");
                copy.setAttribute("aria-label", "Copied");
                setTimeout(function () {
                    if (icon) {
                        icon.classList.remove("bi-check2");
                        icon.classList.add("bi-copy");
                    }
                    copy.setAttribute("title", label);
                    copy.setAttribute("aria-label", label);
                }, 1200);
            };
            if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(value).then(done);
            return;
        }
        var resubmit = event.target && event.target.closest ? event.target.closest("[data-resubmit-open]") : null;
        if (resubmit) {
            var dialog = document.getElementById("resubmitDialog");
            var form = document.getElementById("resubmitForm");
            if (dialog && form && window.bootstrap && window.bootstrap.Modal) {
                form.querySelectorAll("[data-resubmit-copy]").forEach(function (node) { node.remove(); });
                var fields = resubmit.parentElement && resubmit.parentElement.querySelector("[data-resubmit-fields]");
                if (fields) {
                    fields.querySelectorAll("input").forEach(function (input) {
                        var clone = input.cloneNode(true);
                        clone.setAttribute("data-resubmit-copy", "1");
                        form.appendChild(clone);
                    });
                }
                var summary = document.getElementById("resubmitSummary");
                if (summary) {
                    summary.textContent = "Report " + (resubmit.getAttribute("data-report-id") || "")
                        + " for facility " + (resubmit.getAttribute("data-facility-id") || "")
                        + ". " + (resubmit.getAttribute("data-period") || "");
                }
                window.bootstrap.Modal.getOrCreateInstance(dialog).show();
            }
            return;
        }
        var opener = event.target && event.target.closest ? event.target.closest("[data-lu-reveal]") : null;
        if (!opener) return;
        var panel = document.getElementById(opener.getAttribute("data-lu-reveal") || "");
        if (!panel) return;
        event.preventDefault();
        panel.classList.remove("d-none");
        var focus = panel.querySelector("input:not([type='hidden']), select, textarea");
        if (focus) focus.focus();
    });

    document.addEventListener("click", function (event) {
        var closer = event.target && event.target.closest ? event.target.closest("[data-lu-cancel]") : null;
        if (!closer) return;
        event.preventDefault();
        var panel = closer.closest("[data-lu-panel]");
        var named = closer.getAttribute("data-lu-cancel");
        if (named) panel = document.getElementById(named) || panel;
        if (!panel) return;
        panel.querySelectorAll("input, textarea, select").forEach(function (field) {
            if (field.type === "hidden" || field.type === "file") return;
            if (field.tagName === "SELECT") {
                Array.prototype.forEach.call(field.options, function (option) {
                    option.selected = option.defaultSelected;
                });
                return;
            }
            if (field.type === "checkbox" || field.type === "radio") {
                field.checked = field.defaultChecked;
                return;
            }
            field.value = field.defaultValue;
        });
        if (panel.hasAttribute("data-lu-hide-on-cancel")) panel.classList.add("d-none");
    });

    var instantPattern = /\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:\d{2}| UTC)/g;
    var utcTitlePattern = /^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} UTC(?:; \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} UTC)*$/;
    var localFormat = new Intl.DateTimeFormat(undefined, {
        year: "numeric",
        month: "short",
        day: "numeric",
        hour: "numeric",
        minute: "2-digit"
    });

    function utcTitle(date) {
        function part(value) { return String(value).padStart(2, "0"); }
        return date.getUTCFullYear() + "-" + part(date.getUTCMonth() + 1) + "-" + part(date.getUTCDate())
            + " " + part(date.getUTCHours()) + ":" + part(date.getUTCMinutes()) + ":" + part(date.getUTCSeconds()) + " UTC";
    }

    function parseInstant(token) {
        var body = token.slice(-4) === " UTC" ? token.slice(0, -4) : token;
        if (!/Z$|[+-]\d{2}:\d{2}$/.test(body)) body += "Z";
        body = body.replace(" ", "T");
        if (/T\d{2}:\d{2}Z$/.test(body)) body = body.replace("Z", ":00Z");
        else if (/T\d{2}:\d{2}[+-]\d{2}:\d{2}$/.test(body)) body = body.replace(/([+-]\d{2}:\d{2})$/, ":00$1");
        return new Date(body);
    }

    function paintTimes(root) {
        var scope = root && root.nodeType === 1 ? root : (document.querySelector(".lu-content") || document.body);
        if (!scope) return;
        var walker = document.createTreeWalker(scope, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                var parent = node.parentElement;
                if (!parent) return NodeFilter.FILTER_REJECT;
                if (parent.closest("script, style, textarea, pre, code, input, title, [data-lu-wall]")) return NodeFilter.FILTER_REJECT;
                return NodeFilter.FILTER_ACCEPT;
            }
        });
        var nodes = [];
        while (walker.nextNode()) nodes.push(walker.currentNode);
        nodes.forEach(function (node) {
            var text = node.nodeValue || "";
            if (text.length > 180 || text.indexOf("{") >= 0 || text.indexOf("\"") >= 0) return;
            instantPattern.lastIndex = 0;
            if (!instantPattern.test(text)) return;
            var titles = [];
            instantPattern.lastIndex = 0;
            var next = text.replace(instantPattern, function (token) {
                var date = parseInstant(token);
                if (isNaN(date.getTime())) return token;
                titles.push(utcTitle(date));
                return localFormat.format(date);
            });
            if (next === text || !titles.length) return;
            node.nodeValue = next;
            var parent = node.parentElement;
            if (!parent) return;
            var title = parent.getAttribute("title") || "";
            if (!title || utcTitlePattern.test(title)) parent.setAttribute("title", titles.join("; "));
        });
    }

    window.luPaintTimes = paintTimes;
    window.luLocalInstant = function (value) {
        var date = value instanceof Date ? value : parseInstant(String(value || ""));
        if (!date || isNaN(date.getTime())) return { text: value == null ? "" : String(value), title: "" };
        return { text: localFormat.format(date), title: utcTitle(date) };
    };
    function wireDismiss(root) {
        var scope = root && root.querySelectorAll ? root : document;
        scope.querySelectorAll(".alert-success, .alert-danger, .alert-warning").forEach(function (alert) {
            if (alert.querySelector(".lu-dismiss, .btn-close")) return;
            if (alert.closest(".modal, .toast, .offcanvas")) return;
            if (/display\s*:\s*none/i.test(alert.getAttribute("style") || "")) return;
            if (/(Error|Errors|Warning|Warnings|Banner)$/.test(alert.id || "")) return;
            var button = document.createElement("button");
            button.type = "button";
            button.className = "lu-dismiss";
            button.setAttribute("aria-label", "Dismiss");
            button.innerHTML = "<span aria-hidden=\"true\">\u00d7</span>";
            button.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopPropagation();
                alert.hidden = true;
            });
            alert.classList.add("lu-alert");
            alert.appendChild(button);
        });
    }

    document.addEventListener("au-refreshed", function (event) {
        var id = event.detail && event.detail.id;
        var node = id ? document.getElementById(id) : null;
        paintTimes(node);
        wireDismiss(node || document);
    });
    paintTimes();
    wireDismiss(document);
})();

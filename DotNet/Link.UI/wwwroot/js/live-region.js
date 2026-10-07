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

    function regionUrl(node) {
        return node.getAttribute("data-au-refresh-url") || window.location.href;
    }

    function replaceRegion(node, url, push) {
        if (!node || !node.id) return Promise.resolve();
        var init = { headers: { "X-Requested-With": "fetch" } };
        if (node.hasAttribute("data-au-refresh-url")) init.cache = "no-store";
        return fetch(url, init).then(function (res) {
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
        if (document.hidden || focusedIn(node) || selectionHeld(node) || node._auBusy) return;
        node._auBusy = true;
        replaceRegion(node, regionUrl(node), false).finally(function () { node._auBusy = false; });
    }

    function watch(node) {
        if (node._auWatch) return;
        node._auWatch = true;
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
    document.addEventListener("au-refreshed", function () {
        liftDialogs();
        scanRefresh();
    });

    document.addEventListener("click", function (event) {
        var copy = event.target && event.target.closest ? event.target.closest("[data-lu-copy]") : null;
        if (copy) {
            event.preventDefault();
            var value = copy.getAttribute("data-lu-copy") || "";
            var shown = copy.parentElement && copy.parentElement.querySelector
                ? copy.parentElement.querySelector(".lu-facility-id, .lu-clip")
                : null;
            if (shown && shown.textContent && shown.textContent.trim()) value = shown.textContent.trim();
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
    document.addEventListener("au-refreshed", function (event) {
        var id = event.detail && event.detail.id;
        paintTimes(id ? document.getElementById(id) : null);
    });
    paintTimes();
})();

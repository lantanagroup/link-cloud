(function () {
    if (window.__facilitySaveBound) return;
    window.__facilitySaveBound = true;

    var saveOrder = [
        "SaveFacility",
        "SaveCensus",
        "SaveQueryDispatch",
        "SaveFhirQuery",
        "SaveFhirList",
        "SaveQueryPlan",
        "SaveReportingOrg",
        "SaveSftp",
        "SaveOperation",
        "SaveOperationSequence"
    ];
    var removeOrder = [
        "DeleteOperation",
        "DeleteOperationSequence",
        "DeleteSftp",
        "DeleteReportingOrg",
        "DeleteQueryPlan",
        "DeleteFhirList",
        "DeleteFhirQuery",
        "DeleteQueryDispatch",
        "DeleteCensus"
    ];
    var immediate = {
        Remove: true,
        TestSftp: true,
        TestSavedSftp: true,
        TestOperation: true,
        ImportExtensionUrls: true,
        ImportVendorExtensions: true,
        DeleteSftpCredentials: true
    };
    var collapsible = {
        SaveCensus: true,
        SaveQueryDispatch: true,
        SaveFhirQuery: true,
        SaveFhirList: true,
        SaveQueryPlan: true,
        SaveReportingOrg: true,
        SaveSftp: true
    };
    var labels = {
        SaveFacility: "Facility",
        SaveCensus: "Census",
        DeleteCensus: "Census",
        SaveQueryDispatch: "Query dispatch",
        DeleteQueryDispatch: "Query dispatch",
        SaveFhirQuery: "FHIR query",
        DeleteFhirQuery: "FHIR query",
        SaveFhirList: "FHIR list",
        DeleteFhirList: "FHIR list",
        SaveQueryPlan: "Query plan",
        DeleteQueryPlan: "Query plan",
        SaveReportingOrg: "Reporting organization",
        DeleteReportingOrg: "Reporting organization",
        SaveSftp: "SFTP",
        DeleteSftp: "SFTP",
        SaveOperation: "Normalization operation",
        DeleteOperation: "Normalization operation",
        SaveOperationSequence: "Operation sequence",
        DeleteOperationSequence: "Operation sequence"
    };
    var panels = {
        SaveCensus: "censusPanel",
        DeleteCensus: "censusPanel",
        SaveQueryDispatch: "dispatchPanel",
        DeleteQueryDispatch: "dispatchPanel",
        SaveFhirQuery: "fhirQueryPanel",
        DeleteFhirQuery: "fhirQueryPanel",
        SaveFhirList: "fhirListPanel",
        DeleteFhirList: "fhirListPanel",
        SaveQueryPlan: "queryPlanPanel",
        DeleteQueryPlan: "queryPlanPanel",
        SaveReportingOrg: "reportingOrgPanel",
        DeleteReportingOrg: "reportingOrgPanel",
        SaveSftp: "sftpPanel",
        DeleteSftp: "sftpPanel",
        SaveOperation: "normalizationPanel",
        DeleteOperation: "normalizationPanel",
        SaveOperationSequence: "normalizationPanel",
        DeleteOperationSequence: "normalizationPanel"
    };

    var saving = false;
    var leaving = false;

    function root() {
        return document.getElementById("facilityEditor");
    }

    function actionName(form) {
        try {
            var parts = new URL(form.action, window.location.href).pathname.split("/").filter(Boolean);
            return parts.length >= 2 ? parts[1] : "";
        } catch (e) {
            return "";
        }
    }

    function inputValue(form, name) {
        var field = form.querySelector("input[name='" + name + "']");
        return field ? field.value : "";
    }

    function isDirty() {
        var editor = root();
        if (!editor) return false;
        return !!editor.querySelector("[data-facility-dirty='true'], [data-facility-force='true']");
    }

    function refreshFlag() {
        var flag = document.getElementById("facilityDirtyFlag");
        var button = document.getElementById("facilitySaveButton");
        var dirty = isDirty();
        if (flag) {
            flag.textContent = dirty ? "Unsaved changes" : "No unsaved changes";
            flag.classList.toggle("lu-dirty", dirty);
        }
        if (button) button.disabled = !dirty || saving;
    }

    function markDirty(form) {
        form.setAttribute("data-facility-dirty", "true");
        refreshFlag();
    }

    function showResults(lines) {
        var host = document.getElementById("facilitySaveResults");
        if (!host) return;
        host.textContent = "";
        lines.forEach(function (line) {
            var p = document.createElement("p");
            p.className = "mb-1";
            p.textContent = line;
            host.appendChild(p);
        });
    }

    function sectionItem(form) {
        return form.closest(".accordion-item");
    }

    function ensureSlot(item) {
        if (!item) return null;
        var header = item.querySelector(":scope > .accordion-header");
        if (!header) return null;
        var slot = header.querySelector(":scope > .lu-section-action");
        if (!slot) {
            slot = document.createElement("div");
            slot.className = "lu-section-action";
            header.appendChild(slot);
        }
        return slot;
    }

    function setExpanded(item, open) {
        if (!item) return;
        var panel = item.querySelector(":scope > .accordion-collapse");
        var toggle = item.querySelector(":scope > .accordion-header > .accordion-button");
        if (panel && window.bootstrap && window.bootstrap.Collapse) {
            var instance = window.bootstrap.Collapse.getOrCreateInstance(panel, { toggle: false });
            if (open) instance.show();
            else instance.hide();
        } else if (panel) {
            panel.classList.toggle("show", open);
        }
        if (toggle) {
            toggle.classList.toggle("collapsed", !open);
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
        }
    }

    function sectionDirty(item) {
        return !!(item && item.querySelector("[data-facility-dirty='true'], [data-facility-force='true']"));
    }

    function undoButton(onClick) {
        var undo = document.createElement("button");
        undo.type = "button";
        undo.className = "btn btn-sm btn-outline-light";
        undo.textContent = "Undo";
        undo.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
            onClick();
        });
        return undo;
    }

    function stagedFlag(text) {
        var flag = document.createElement("span");
        flag.className = "lu-staged-flag";
        flag.textContent = text;
        return flag;
    }

    function collapseEmpty(form) {
        form.classList.add("d-none");
        var body = form.closest(".accordion-body") || form.parentElement;
        if (body) {
            body.querySelectorAll(":scope > .alert-info").forEach(function (alert) {
                alert.classList.add("d-none");
            });
        }
        var extras = [];
        var node = form.nextElementSibling;
        while (node) {
            if (node.matches && node.matches("form[data-au-save]") && actionName(node) === "TestSftp") {
                node.classList.add("d-none");
                extras.push(node);
            }
            node = node.nextElementSibling;
        }
        var item = sectionItem(form);
        setExpanded(item, false);
        var slot = ensureSlot(item);
        if (!slot) return;

        function showAdd() {
            slot.textContent = "";
            var add = document.createElement("button");
            add.type = "button";
            add.className = "btn btn-sm btn-success";
            add.textContent = "+ Add";
            add.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopPropagation();
                form.classList.remove("d-none");
                extras.forEach(function (extra) { extra.classList.remove("d-none"); });
                var submit = form.querySelector("button[type='submit'], input[type='submit']");
                if (submit) submit.classList.add("d-none");
                form.setAttribute("data-facility-dirty", "true");
                form.setAttribute("data-facility-force", "true");
                setExpanded(item, true);
                showUndo();
                refreshFlag();
                var focus = form.querySelector("input:not([type='hidden']), select, textarea");
                if (focus) focus.focus();
            });
            slot.appendChild(add);
        }

        function showUndo() {
            slot.textContent = "";
            slot.appendChild(stagedFlag("New"));
            slot.appendChild(undoButton(function () {
                form.classList.add("d-none");
                extras.forEach(function (extra) { extra.classList.add("d-none"); });
                form.removeAttribute("data-facility-dirty");
                form.removeAttribute("data-facility-force");
                setExpanded(item, false);
                showAdd();
                refreshFlag();
            }));
        }

        showAdd();
    }

    function saveNameFor(action) {
        return action.indexOf("Delete") === 0 ? "Save" + action.substring(6) : "";
    }

    function applySkip(deleteForm) {
        var saveName = saveNameFor(deleteForm.getAttribute("data-facility-action") || "");
        if (!saveName) return;
        var delId = inputValue(deleteForm, "operationId");
        root().querySelectorAll("form[data-facility-action='" + saveName + "']").forEach(function (save) {
            if (delId) {
                var editId = inputValue(save, "OperationId");
                if (editId !== delId) return;
            }
            save.setAttribute("data-facility-skip", "true");
        });
    }

    function syncSkip(action) {
        var saveName = saveNameFor(action);
        if (!saveName) return;
        var editor = root();
        editor.querySelectorAll("form[data-facility-action='" + saveName + "']").forEach(function (save) {
            save.removeAttribute("data-facility-skip");
        });
        editor.querySelectorAll("form[data-facility-action='" + action + "'][data-facility-force='true']").forEach(applySkip);
    }

    function wireRowRemove(form, submit) {
        var action = form.getAttribute("data-facility-action");
        var rest = (submit.textContent || "").replace(/^Delete\s+/i, "").trim();
        var label = rest || labels[action] || "this row";
        submit.type = "button";
        submit.className = "btn btn-sm btn-danger";
        submit.textContent = "Delete";
        submit.addEventListener("click", function () {
            if (!window.confirm("Delete " + label + "? It is deleted when you save the facility.")) return;
            form.setAttribute("data-facility-force", "true");
            applySkip(form);
            var note = document.createElement("div");
            note.className = "alert alert-warning mt-2 lu-remove-note";
            note.appendChild(document.createTextNode("Staged for removal. "));
            note.appendChild(undoButton(function () {
                form.removeAttribute("data-facility-force");
                syncSkip(action);
                note.remove();
                refreshFlag();
            }));
            form.appendChild(note);
            refreshFlag();
        });
    }

    function wireHeaderDelete(form, submit) {
        var action = form.getAttribute("data-facility-action");
        var label = labels[action] || "this section";
        submit.classList.add("d-none");
        form.classList.add("d-none");
        var item = sectionItem(form);
        var slot = ensureSlot(item);
        if (!slot) return;

        function showDelete() {
            slot.textContent = "";
            var button = document.createElement("button");
            button.type = "button";
            button.className = "btn btn-sm btn-danger";
            button.textContent = "Delete";
            button.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopPropagation();
                if (!window.confirm("Delete " + label + "? It is deleted when you save the facility.")) return;
                form.setAttribute("data-facility-force", "true");
                applySkip(form);
                showStaged();
                refreshFlag();
            });
            slot.appendChild(button);
        }

        function showStaged() {
            slot.textContent = "";
            slot.appendChild(stagedFlag("Staged for removal"));
            slot.appendChild(undoButton(function () {
                form.removeAttribute("data-facility-force");
                syncSkip(action);
                showDelete();
                refreshFlag();
            }));
        }

        showDelete();
    }

    function wireRemove(form, submit) {
        var action = form.getAttribute("data-facility-action");
        if (action === "DeleteOperation" || action === "DeleteOperationSequence") {
            wireRowRemove(form, submit);
            return;
        }
        wireHeaderDelete(form, submit);
    }

    function withPlanType(url) {
        var select = document.getElementById("queryPlanType");
        if (select && select.value) url.searchParams.set("planType", select.value);
        return url;
    }

    function swapItem(url, itemId) {
        var item = document.getElementById(itemId);
        if (!item) return Promise.resolve(false);
        item.setAttribute("aria-busy", "true");
        return fetch(url, { headers: { "X-Requested-With": "fetch" }, credentials: "same-origin" })
            .then(function (res) {
                if (!res.ok) throw new Error(String(res.status));
                return res.text();
            })
            .then(function (html) {
                var doc = new DOMParser().parseFromString(html, "text/html");
                var fresh = doc.getElementById(itemId);
                if (!fresh) throw new Error("missing");
                item.replaceWith(fresh);
                history.replaceState(null, "", url);
                fresh.querySelectorAll("form[data-au-save]").forEach(classify);
                if (itemId === "normalizationPanel") decorateNormalization();
                wirePlanSelect(fresh);
                return true;
            })
            .catch(function () {
                var current = document.getElementById(itemId);
                if (current) current.removeAttribute("aria-busy");
                showResults(["That section could not be opened. The rest of your edits are still here."]);
                return false;
            });
    }

    function decorateNormalization() {
        var item = document.getElementById("normalizationPanel");
        if (!item) return;
        if (item.querySelector("form[data-facility-action='DeleteOperation']")) return;
        if (item.querySelector("#normalization-editor")) return;
        if (!item.querySelector("a[data-facility-swap]")) return;
        var slot = ensureSlot(item);
        if (!slot || slot.childElementCount > 0) return;
        var add = document.createElement("button");
        add.type = "button";
        add.className = "btn btn-sm btn-success";
        add.textContent = "+ Add";
        add.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
            setExpanded(item, true);
        });
        slot.appendChild(add);
    }

    function wirePlanSelect(scope) {
        var select = (scope || document).querySelector("#queryPlanType");
        if (!select || select.getAttribute("data-wired") === "1") return;
        select.setAttribute("data-wired", "1");
        select.setAttribute("data-current", select.value);
        select.addEventListener("change", function () {
            var item = select.closest(".accordion-item");
            if (!item || !item.id) return;
            if (sectionDirty(item) && !window.confirm("This query plan has unsaved changes. Switch type without saving?")) {
                select.value = select.getAttribute("data-current");
                return;
            }
            var url = new URL(window.location.href);
            url.searchParams.set("planType", select.value);
            swapItem(url.toString(), item.id).then(function (ok) {
                if (!ok && select.isConnected) select.value = select.getAttribute("data-current");
            });
        });
    }

    function classify(form) {
        var action = actionName(form);
        form.setAttribute("data-facility-action", action);
        if (immediate[action] || (saveOrder.indexOf(action) < 0 && removeOrder.indexOf(action) < 0)) {
            form.setAttribute("data-facility-immediate", "1");
            return;
        }
        var submit = form.querySelector("button[type='submit'], input[type='submit']");
        var label = submit ? (submit.textContent || "").trim() : "";
        if (collapsible[action] && label.indexOf("Add ") === 0) collapseEmpty(form);
        else if (action.indexOf("Save") === 0 && submit) {
            submit.classList.add("d-none");
            var wrap = submit.parentElement;
            if (wrap && wrap.classList.contains("d-flex") && wrap.querySelectorAll("button, a.btn, input[type='submit']").length === 1)
                wrap.classList.add("d-none");
        }
        if (action.indexOf("Delete") === 0 && submit) wireRemove(form, submit);
        form.addEventListener("input", function () { markDirty(form); });
        form.addEventListener("change", function () { markDirty(form); });
    }

    function init() {
        var editor = root();
        if (!editor || editor.getAttribute("data-facility-ready") === "1") return;
        if (!document.getElementById("facilitySaveBar")) return;
        editor.setAttribute("data-facility-ready", "1");
        editor.querySelectorAll("form[data-au-save]").forEach(classify);
        decorateNormalization();
        wirePlanSelect(editor);
        var save = document.getElementById("facilitySaveButton");
        var cancel = document.getElementById("facilityCancelButton");
        if (save) save.addEventListener("click", saveAll);
        if (cancel) cancel.addEventListener("click", function () {
            if (isDirty() && !window.confirm("This facility has unsaved changes. Leave without saving?")) return;
            leaving = true;
            window.location.reload();
        });
        refreshFlag();
    }

    function failureText(doc, action) {
        if (action === "SaveFacility") {
            var box = doc.getElementById("facilityFormError");
            return box ? box.textContent.replace(/\s+/g, " ").trim() : "";
        }
        var panel = panels[action] && doc.getElementById(panels[action]);
        if (!panel) return "";
        var warn = panel.querySelector(".alert-warning");
        return warn ? warn.textContent.replace(/\s+/g, " ").trim() : "";
    }

    function postForm(form) {
        return fetch(form.action, {
            method: "POST",
            body: new FormData(form),
            headers: { "X-Requested-With": "fetch" },
            redirect: "follow"
        }).then(function (res) {
            var type = res.headers.get("content-type") || "";
            if (!res.ok || type.indexOf("text/html") === -1) {
                return { ok: false, detail: "The server returned HTTP " + res.status + "." };
            }
            return res.text().then(function (html) {
                var doc = new DOMParser().parseFromString(html, "text/html");
                var success = doc.querySelector(".alert-success");
                if (success && success.textContent.trim()) return { ok: true };
                var detail = failureText(doc, form.getAttribute("data-facility-action"));
                return { ok: false, detail: detail || "The server did not confirm this change." };
            });
        }).catch(function () {
            return { ok: false, detail: "The request did not complete." };
        });
    }

    function markSaved(form) {
        var action = form.getAttribute("data-facility-action") || "";
        if (action.indexOf("Delete") === 0) {
            var saveName = saveNameFor(action);
            var delId = inputValue(form, "operationId");
            root().querySelectorAll("form[data-facility-action='" + saveName + "']").forEach(function (save) {
                if (save.getAttribute("data-facility-skip") !== "true") return;
                if (delId && inputValue(save, "OperationId") !== delId) return;
                save.classList.add("d-none");
            });
            form.classList.add("d-none");
        }
        form.removeAttribute("data-facility-dirty");
        form.removeAttribute("data-facility-force");
        form.removeAttribute("data-facility-skip");
        if (action.indexOf("Save") === 0) {
            ["censusExists", "queryDispatchExists", "Exists"].forEach(function (name) {
                var field = form.querySelector("input[name='" + name + "']");
                if (field) field.value = "true";
            });
        }
    }

    function pending(names) {
        var editor = root();
        var list = [];
        names.forEach(function (name) {
            editor.querySelectorAll("form[data-facility-action='" + name + "']").forEach(function (form) {
                if (form.getAttribute("data-facility-skip") === "true") return;
                if (form.getAttribute("data-facility-immediate") === "1") return;
                var forced = form.getAttribute("data-facility-force") === "true";
                var dirty = form.getAttribute("data-facility-dirty") === "true";
                if (name.indexOf("Delete") === 0) {
                    if (forced) list.push(form);
                } else if (dirty || forced) {
                    list.push(form);
                }
            });
        });
        return list;
    }

    function saveAll() {
        if (saving || !isDirty()) return;
        saving = true;
        refreshFlag();
        showResults([]);
        var forms = pending(saveOrder).concat(pending(removeOrder));
        var saved = [];
        var index = 0;

        function finish() {
            saving = false;
            leaving = true;
            var refresh = window.auRefreshPage
                ? window.auRefreshPage("Facility saved.")
                : Promise.reject();
            Promise.resolve(refresh).then(function () {
                leaving = false;
                refreshFlag();
            }).catch(function () {
                window.location.reload();
            });
        }

        function fail(form, detail) {
            saving = false;
            refreshFlag();
            var label = labels[form.getAttribute("data-facility-action")] || "Section";
            var lines = [];
            lines.push(saved.length ? "Saved: " + saved.join(", ") + "." : "No earlier section was saved.");
            lines.push(label + " could not be saved. " + detail);
            lines.push("This section still has your edits. Later sections were not saved.");
            showResults(lines);
        }

        function next() {
            if (index >= forms.length) {
                finish();
                return;
            }
            var form = forms[index++];
            postForm(form).then(function (result) {
                if (!result.ok) {
                    fail(form, result.detail);
                    return;
                }
                markSaved(form);
                saved.push(labels[form.getAttribute("data-facility-action")] || "Section");
                refreshFlag();
                next();
            });
        }

        if (!forms.length) {
            saving = false;
            refreshFlag();
            return;
        }
        next();
    }

    document.addEventListener("submit", function (event) {
        var editor = root();
        if (!editor || editor.getAttribute("data-facility-ready") !== "1" || !editor.contains(event.target)) return;
        var form = event.target;
        if (!form || !form.getAttribute) return;
        if ((form.method || "").toLowerCase() === "get") {
            var swapId = form.getAttribute("data-facility-swap");
            if (swapId) {
                event.preventDefault();
                event.stopPropagation();
                var swapTarget = document.getElementById(swapId);
                if (sectionDirty(swapTarget) && !window.confirm("This section has unsaved changes. Continue and discard them?")) return;
                var url = new URL(form.action, window.location.href);
                new FormData(form).forEach(function (value, key) {
                    if (value) url.searchParams.set(key, value);
                });
                swapItem(withPlanType(url).toString(), swapId);
                return;
            }
            if (!isDirty()) return;
            if (!window.confirm("This facility has unsaved changes. Leave without saving?")) {
                event.preventDefault();
                event.stopPropagation();
            } else {
                leaving = true;
            }
            return;
        }
        if (form.getAttribute("data-facility-immediate") === "1") return;
        var action = form.getAttribute("data-facility-action");
        if (!action) return;
        event.preventDefault();
        event.stopPropagation();
        if (action.indexOf("Delete") !== 0) markDirty(form);
    }, true);

    document.addEventListener("click", function (event) {
        var link = event.target && event.target.closest ? event.target.closest("a[data-facility-swap]") : null;
        var editor = root();
        if (!link || !editor || editor.getAttribute("data-facility-ready") !== "1" || !editor.contains(link)) return;
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        if (link.target && link.target !== "_self") return;
        event.preventDefault();
        event.stopPropagation();
        var swapId = link.getAttribute("data-facility-swap");
        var swapTarget = document.getElementById(swapId);
        if (sectionDirty(swapTarget) && !window.confirm("This section has unsaved changes. Continue and discard them?")) return;
        var url;
        try { url = new URL(link.href, window.location.href); }
        catch (e) { return; }
        swapItem(withPlanType(url).toString(), swapId);
    }, true);

    document.addEventListener("click", function (event) {
        var editor = root();
        if (!editor || editor.getAttribute("data-facility-ready") !== "1" || !isDirty()) return;
        var link = event.target && event.target.closest ? event.target.closest("a[href]") : null;
        if (!link || event.button !== 0) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        if (link.target && link.target !== "_self") return;
        var url;
        try { url = new URL(link.href, window.location.href); }
        catch (e) { return; }
        if (url.origin !== window.location.origin) return;
        if (url.pathname === window.location.pathname && url.search === window.location.search) return;
        if (!window.confirm("This facility has unsaved changes. Leave without saving?")) {
            event.preventDefault();
            event.stopPropagation();
            return;
        }
        leaving = true;
    }, true);

    window.addEventListener("beforeunload", function (event) {
        if (!isDirty() || leaving) return;
        event.preventDefault();
        event.returnValue = "";
    });

    document.addEventListener("show.bs.tab", function (event) {
        var editor = root();
        if (!editor || !editor.contains(event.target) || !isDirty()) return;
        if (!window.confirm("This facility has unsaved changes. Switch tabs without saving?")) {
            event.preventDefault();
        }
    });

    document.addEventListener("au-refreshed", function () { init(); });
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();

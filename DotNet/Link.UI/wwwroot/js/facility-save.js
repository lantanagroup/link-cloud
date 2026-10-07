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
        var row = document.createElement("div");
        row.className = "d-flex justify-content-between align-items-center gap-2 lu-section-empty";
        var note = document.createElement("span");
        note.className = "text-muted";
        note.textContent = "Not configured";
        var add = document.createElement("button");
        add.type = "button";
        add.className = "btn btn-sm btn-success";
        add.textContent = "+ Add";
        add.addEventListener("click", function () {
            form.classList.remove("d-none");
            extras.forEach(function (extra) { extra.classList.remove("d-none"); });
            var submit = form.querySelector("button[type='submit'], input[type='submit']");
            if (submit) submit.classList.add("d-none");
            form.setAttribute("data-facility-dirty", "true");
            form.setAttribute("data-facility-force", "true");
            row.remove();
            refreshFlag();
            var focus = form.querySelector("input:not([type='hidden']), select, textarea");
            if (focus) focus.focus();
        });
        row.appendChild(note);
        row.appendChild(add);
        if (body) body.insertBefore(row, form);
        else form.parentNode.insertBefore(row, form);
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

    function wireRemove(form, submit) {
        var action = form.getAttribute("data-facility-action");
        var rest = (submit.textContent || "").replace(/^Delete\s+/i, "").trim();
        var label = rest || labels[action] || "this section";
        submit.type = "button";
        submit.className = "btn btn-sm btn-danger";
        submit.textContent = "Remove";
        submit.addEventListener("click", function () {
            if (!window.confirm("Remove " + label + "? It is deleted when you save the facility.")) return;
            form.setAttribute("data-facility-force", "true");
            applySkip(form);
            var note = document.createElement("div");
            note.className = "alert alert-warning mt-2 lu-remove-note";
            note.appendChild(document.createTextNode("This will be removed when you save the facility. "));
            var undo = document.createElement("button");
            undo.type = "button";
            undo.className = "btn btn-sm btn-outline-secondary";
            undo.textContent = "Undo";
            undo.addEventListener("click", function () {
                form.removeAttribute("data-facility-force");
                syncSkip(action);
                note.remove();
                refreshFlag();
            });
            note.appendChild(undo);
            form.appendChild(note);
            refreshFlag();
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

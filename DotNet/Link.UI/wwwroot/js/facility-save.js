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
        "SaveNotification",
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
        "DeleteCensus",
        "DeleteNotification"
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
        SaveFhirQuery: true,
        SaveFhirList: true,
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
        SaveNotification: "Notification email",
        DeleteNotification: "Notification email",
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
        SaveNotification: "notificationPanel",
        DeleteNotification: "notificationPanel",
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
        undo.className = "btn btn-sm btn-au-close";
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
        var body = form.closest(".accordion-body") || form.parentElement;
        if (body && body.querySelector(":scope > .alert-warning")) {
            var blocked = form.querySelector("button[type='submit'], input[type='submit']");
            if (blocked) blocked.classList.add("d-none");
            setExpanded(sectionItem(form), true);
            return;
        }
        form.classList.add("d-none");
        if (body) {
            body.querySelectorAll(":scope > .au-note").forEach(function (alert) {
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
        var delId = inputValue(deleteForm, "operationId") || inputValue(deleteForm, "type");
        root().querySelectorAll("form[data-facility-action='" + saveName + "']").forEach(function (save) {
            if (delId) {
                var editId = inputValue(save, "OperationId") || inputValue(save, "Type");
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
        var rest = (submit.textContent || "").replace(/^Delete\s*/i, "").trim();
        var label = rest || labels[action] || "this row";
        submit.type = "button";
        submit.className = "btn btn-sm btn-danger lu-icon-btn";
        submit.innerHTML = '<i class="bi bi-trash" aria-hidden="true"></i>';
        submit.title = "Delete";
        submit.setAttribute("aria-label", "Delete " + label);
        submit.addEventListener("click", function () {
            if (!window.confirm("Delete " + label + "? It is deleted when you save the facility.")) return;
            form.setAttribute("data-facility-force", "true");
            applySkip(form);
            var row = form.closest("[data-collection-row]");
            if (row) row.classList.add("lu-row-staged");
            var note = document.createElement("div");
            note.className = "alert alert-warning mt-2 lu-remove-note";
            note.appendChild(document.createTextNode("Staged for removal. "));
            note.appendChild(undoButton(function () {
                form.removeAttribute("data-facility-force");
                syncSkip(action);
                if (row) row.classList.remove("lu-row-staged");
                note.remove();
                refreshFlag();
                updateCounts();
            }));
            form.appendChild(note);
            refreshFlag();
            updateCounts();
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
        if (action === "DeleteQueryDispatch" || action === "DeleteNotification") {
            submit.classList.add("d-none");
            form.classList.add("d-none");
            form.setAttribute("data-collection-clear", "true");
            return;
        }
        if (action === "DeleteOperation" || action === "DeleteOperationSequence" || action === "DeleteQueryPlan") {
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
                decorateAll();
                wirePlanSelect(fresh);
                refreshFlag();
                return true;
            })
            .catch(function () {
                var current = document.getElementById(itemId);
                if (current) current.removeAttribute("aria-busy");
                showResults(["That section could not be opened. The rest of your edits are still here."]);
                return false;
            });
    }

    function updateCounts() {
        var editor = root();
        if (!editor) return;
        var planBadge = editor.querySelector("#queryPlanCount");
        if (planBadge) {
            var plans = editor.querySelectorAll("[data-plan-type]:not(.lu-row-staged)").length;
            var addingPlan = editor.querySelector("#queryPlanEditor[data-plan-edit='add']:not(.d-none)");
            planBadge.textContent = String(plans + (addingPlan ? 1 : 0));
        }
        var normBadge = editor.querySelector("#normalizationCount");
        if (normBadge) {
            var ops = editor.querySelectorAll("[data-operation-row]:not(.lu-row-staged)").length;
            var normEditor = editor.querySelector("#normalization-editor");
            var opId = normEditor && normEditor.querySelector("input[name='OperationId']");
            normBadge.textContent = String(ops + (normEditor && opId && !opId.value ? 1 : 0));
        }
        var dispatchBadge = editor.querySelector("#dispatchCount");
        if (dispatchBadge) dispatchBadge.textContent = String(dispatchKept(editor));
        var emailBadge = editor.querySelector("#notificationCount");
        if (emailBadge) emailBadge.textContent = String(emailKept(editor));
        var dispatchEmpty = editor.querySelector("#dispatchEmpty");
        if (dispatchEmpty) dispatchEmpty.classList.toggle("d-none", dispatchKept(editor) > 0);
        var emailEmpty = editor.querySelector("#notificationEmpty");
        if (emailEmpty) emailEmpty.classList.toggle("d-none", emailKept(editor) > 0);
    }

    function dispatchKept(editor) {
        var kept = 0;
        editor.querySelectorAll("[data-dispatch-row]").forEach(function (row) {
            if (row.classList.contains("d-none") || row.classList.contains("lu-row-staged")) return;
            var eventField = row.querySelector("input[name$='.Event']");
            var duration = row.querySelector("input[name$='.Duration']");
            var has = (eventField && eventField.value.trim()) || (duration && duration.value.trim());
            if (has || row.getAttribute("data-dispatch-new") === "true") kept++;
        });
        return kept;
    }

    function emailKept(editor) {
        var kept = 0;
        editor.querySelectorAll("[data-email-row]").forEach(function (row) {
            if (!row.classList.contains("lu-row-staged") && !row.classList.contains("d-none")) kept++;
        });
        return kept;
    }

    function syncEmails(form) {
        form = form || document.querySelector("#facilityEditor form[data-facility-action='SaveNotification']");
        if (!form) return;
        var box = form.querySelector("#notificationEmails");
        if (!box) return;
        var lines = [];
        form.querySelectorAll("[data-email-row]").forEach(function (row) {
            if (row.classList.contains("lu-row-staged")) return;
            var input = row.querySelector("[data-email-input]");
            if (input && input.value.trim()) lines.push(input.value.trim());
        });
        box.value = lines.join("\n");
    }

    function headerAdd(item, onClick) {
        var slot = ensureSlot(item);
        if (!slot || slot.querySelector("[data-collection-add]")) return null;
        var add = document.createElement("button");
        add.type = "button";
        add.className = "btn btn-sm btn-success";
        add.textContent = "+ Add";
        add.setAttribute("data-collection-add", "1");
        add.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
            onClick();
        });
        slot.appendChild(add);
        return add;
    }

    function decorateNormalization() {
        var item = document.getElementById("normalizationPanel");
        if (!item || item.getAttribute("data-vendor") === "true") return;
        if (!item.querySelector("#normalizationTypes") && !item.querySelector("[data-operation-row]")) return;
        if (!item.querySelector("#normalization-editor")) {
            headerAdd(item, function () {
                var types = item.querySelector("#normalizationTypes");
                if (types) types.classList.remove("d-none");
                setExpanded(item, true);
                var link = types && types.querySelector("a, button");
                if (link) link.focus();
            });
        }
        updateCounts();
    }

    function decorateQueryPlans() {
        var item = document.getElementById("queryPlanItem");
        if (!item) return;
        headerAdd(item, function () {
            var types = ["Discharge", "Daily", "Weekly", "Monthly"];
            var present = {};
            item.querySelectorAll("[data-plan-type]").forEach(function (row) {
                if (!row.classList.contains("lu-row-staged")) present[row.getAttribute("data-plan-type")] = true;
            });
            var missing = "";
            types.forEach(function (type) { if (!missing && !present[type]) missing = type; });
            if (!missing) {
                showResults(["All four query plan types already exist."]);
                return;
            }
            if (sectionDirty(item) && !window.confirm("This query plan has unsaved changes. Continue and discard them?")) return;
            var url = new URL(window.location.href);
            url.searchParams.set("planType", missing);
            url.searchParams.set("planEdit", "add");
            swapItem(url.toString(), "queryPlanItem");
        });
        var undo = item.querySelector("#queryPlanAddUndo");
        if (undo && undo.getAttribute("data-wired") !== "1") {
            undo.setAttribute("data-wired", "1");
            undo.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopPropagation();
                var url = new URL(window.location.href);
                url.searchParams.delete("planEdit");
                url.searchParams.delete("planType");
                swapItem(url.toString(), "queryPlanItem");
            });
        }
        var editor = item.querySelector("#queryPlanEditor[data-plan-edit='add']");
        if (editor && !editor.classList.contains("d-none")) {
            var form = editor.querySelector("form[data-facility-action='SaveQueryPlan']");
            if (form && form.getAttribute("data-plan-armed") !== "1") {
                form.setAttribute("data-plan-armed", "1");
                form.setAttribute("data-facility-force", "true");
                form.setAttribute("data-facility-dirty", "true");
                refreshFlag();
            }
        }
        updateCounts();
    }

    function cloneDispatchRow(form) {
        var rows = form.querySelectorAll("[data-dispatch-row]");
        var last = rows[rows.length - 1];
        if (!last) return null;
        var copy = last.cloneNode(true);
        var next = rows.length;
        copy.classList.remove("d-none", "lu-row-staged");
        copy.setAttribute("data-dispatch-blank", "false");
        copy.setAttribute("data-dispatch-new", "true");
        copy.querySelectorAll("[name]").forEach(function (field) {
            field.name = field.name.replace(/Schedules\[\d+\]/, "Schedules[" + next + "]");
            if (field.type === "checkbox") field.checked = false;
            else field.value = "";
        });
        copy.querySelectorAll("[id]").forEach(function (field) {
            field.id = field.id.replace(/-\d+$/, "-" + next);
        });
        copy.querySelectorAll("label[for]").forEach(function (label) {
            label.htmlFor = label.htmlFor.replace(/-\d+$/, "-" + next);
        });
        var note = copy.querySelector(".lu-remove-note");
        if (note) note.remove();
        var remove = copy.querySelector("[data-row-remove]");
        if (remove) remove.removeAttribute("data-wired");
        last.after(copy);
        wireDispatchRemove(copy.querySelector("[data-row-remove]"));
        return copy;
    }

    function wireDispatchRemove(button) {
        if (!button || button.getAttribute("data-wired") === "1") return;
        button.setAttribute("data-wired", "1");
        button.addEventListener("click", function () {
            var row = button.closest("[data-dispatch-row]");
            var form = button.closest("form");
            if (!row || !form) return;
            if (!window.confirm("Delete this dispatch schedule? It is removed when you save the facility.")) return;
            if (row.getAttribute("data-dispatch-new") === "true") {
                row.remove();
                if (!form.querySelector("[data-dispatch-new='true'], .lu-row-staged") && form.getAttribute("data-facility-edited") !== "1")
                    form.removeAttribute("data-facility-dirty");
                else
                    markDirty(form);
                refreshFlag();
                updateCounts();
                return;
            }
            var wasDirty = form.getAttribute("data-facility-dirty") === "true";
            var box = row.querySelector("input[type='checkbox'][name$='.Remove']");
            if (box) box.checked = true;
            row.classList.add("lu-row-staged");
            var note = document.createElement("div");
            note.className = "alert alert-warning mt-2 lu-remove-note";
            note.appendChild(document.createTextNode("Staged for removal. "));
            note.appendChild(undoButton(function () {
                if (box) box.checked = false;
                row.classList.remove("lu-row-staged");
                note.remove();
                var pending = form.querySelector(".lu-row-staged, [data-dispatch-new='true']");
                if (!pending && !wasDirty && form.getAttribute("data-facility-edited") !== "1")
                    form.removeAttribute("data-facility-dirty");
                refreshFlag();
                updateCounts();
            }));
            row.appendChild(note);
            markDirty(form);
            updateCounts();
        });
    }

    function decorateDispatch() {
        var item = document.getElementById("dispatchItem");
        if (!item || !item.querySelector("form[data-facility-action='SaveQueryDispatch']")) return;
        headerAdd(item, function () {
            var form = item.querySelector("form[data-facility-action='SaveQueryDispatch']");
            setExpanded(item, true);
            var blank = form.querySelector("[data-dispatch-blank='true'].d-none");
            var row = blank || cloneDispatchRow(form);
            if (blank) {
                blank.classList.remove("d-none");
                blank.setAttribute("data-dispatch-blank", "false");
                blank.setAttribute("data-dispatch-new", "true");
            }
            updateCounts();
            var focus = row && row.querySelector("input:not([type='hidden']):not([type='checkbox'])");
            if (focus) focus.focus();
        });
        item.querySelectorAll("[data-row-remove]").forEach(wireDispatchRemove);
        updateCounts();
    }

    function wireEmailRemove(button) {
        if (!button || button.getAttribute("data-wired") === "1") return;
        button.setAttribute("data-wired", "1");
        button.addEventListener("click", function () {
            var row = button.closest("[data-email-row]");
            var form = button.closest("form");
            if (!row || !form) return;
            if (!window.confirm("Delete this address? It is removed when you save the facility.")) return;
            if (row.getAttribute("data-email-new") === "true") {
                row.remove();
            } else {
                var wasDirty = form.getAttribute("data-facility-dirty") === "true";
                row.classList.add("lu-row-staged");
                var note = document.createElement("div");
                note.className = "small text-warning lu-remove-note";
                note.textContent = "Staged for removal.";
                var undo = undoButton(function () {
                    row.classList.remove("lu-row-staged");
                    note.remove();
                    syncEmails(form);
                    var pending = form.querySelector(".lu-row-staged, [data-email-new='true']");
                    if (!pending && !wasDirty && form.getAttribute("data-facility-edited") !== "1")
                        form.removeAttribute("data-facility-dirty");
                    refreshFlag();
                    updateCounts();
                });
                note.appendChild(document.createTextNode(" "));
                note.appendChild(undo);
                row.appendChild(note);
            }
            syncEmails(form);
            if (!form.querySelector(".lu-row-staged, [data-email-new='true']") && form.getAttribute("data-facility-edited") !== "1")
                form.removeAttribute("data-facility-dirty");
            else
                markDirty(form);
            refreshFlag();
            updateCounts();
        });
    }

    function decorateNotification() {
        var item = document.getElementById("notificationItem");
        var form = item && item.querySelector("form[data-facility-action='SaveNotification']");
        if (!item || !form) return;
        headerAdd(item, function () {
            var host = form.querySelector("#notificationRows");
            setExpanded(item, true);
            var row = document.createElement("div");
            row.className = "row g-2 align-items-center mb-2";
            row.setAttribute("data-email-row", "");
            row.setAttribute("data-collection-row", "");
            row.setAttribute("data-email-new", "true");
            row.innerHTML = '<div class="col"><input class="form-control" data-email-input aria-label="Email address" maxlength="254" /></div>'
                + '<div class="col-auto"><button type="button" class="btn btn-sm btn-danger lu-icon-btn" data-email-remove title="Delete" aria-label="Delete"><i class="bi bi-trash" aria-hidden="true"></i></button></div>';
            var box = form.querySelector("#notificationEmails");
            if (box) box.before(row);
            else host.appendChild(row);
            wireEmailRemove(row.querySelector("[data-email-remove]"));
            markDirty(form);
            updateCounts();
            var focus = row.querySelector("input");
            if (focus) focus.focus();
        });
        form.querySelectorAll("[data-email-remove]").forEach(wireEmailRemove);
        if (form.getAttribute("data-email-wired") !== "1") {
            form.setAttribute("data-email-wired", "1");
            form.addEventListener("input", function () { syncEmails(form); });
        }
        updateCounts();
    }

    function reconcileEmptied() {
        var editor = root();
        if (!editor) return;
        syncEmails();
        var dispatchForm = editor.querySelector("form[data-facility-action='SaveQueryDispatch']");
        var dispatchDelete = editor.querySelector("form[data-facility-action='DeleteQueryDispatch']");
        if (dispatchForm && dispatchDelete) {
            var exists = (dispatchForm.querySelector("input[name='queryDispatchExists']") || {}).value === "true";
            var dirty = dispatchForm.getAttribute("data-facility-dirty") === "true";
            if (exists && dirty && dispatchKept(editor) === 0) {
                dispatchForm.setAttribute("data-facility-skip", "true");
                dispatchDelete.setAttribute("data-facility-force", "true");
            }
        }
        var emailForm = editor.querySelector("form[data-facility-action='SaveNotification']");
        var emailDelete = editor.querySelector("form[data-facility-action='DeleteNotification']");
        if (emailForm && emailDelete) {
            var id = emailForm.querySelector("input[name='Id']");
            var emailExists = !!(id && id.value.trim());
            var emailDirty = emailForm.getAttribute("data-facility-dirty") === "true";
            if (emailExists && emailDirty && emailKept(editor) === 0) {
                emailForm.setAttribute("data-facility-skip", "true");
                emailDelete.setAttribute("data-facility-force", "true");
            }
        }
    }

    function decorateAll() {
        decorateNormalization();
        decorateQueryPlans();
        decorateDispatch();
        decorateNotification();
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
        var action = form.getAttribute("data-facility-action") || actionName(form);
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
        form.addEventListener("input", function () {
            form.setAttribute("data-facility-edited", "1");
            markDirty(form);
        });
        form.addEventListener("change", function () { markDirty(form); });
    }

    function init() {
        var editor = root();
        if (!editor || editor.getAttribute("data-facility-ready") === "1") return;
        if (!document.getElementById("facilitySaveBar")) return;
        editor.setAttribute("data-facility-ready", "1");
        editor.querySelectorAll("form[data-au-save]").forEach(classify);
        decorateAll();
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
        if (form.getAttribute("data-facility-action") === "SaveNotification") syncEmails(form);
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
        reconcileEmptied();
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
        event.stopImmediatePropagation();
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

(function (global) {
    var RESOURCE_TYPES = [
        'Observation', 'Condition', 'Procedure', 'MedicationRequest',
        'MedicationAdministration', 'Coverage', 'ServiceRequest', 'Specimen', 'DiagnosticReport'
    ];
    var ACH = 'NhsnAcuteCareHospitalMonthlyInitialPopulation';
    var ACH_DAILY = 'NhsnAcuteCareHospitalDailyInitialPopulation';
    var HYPO = 'NhsnGlycemicControlHypoglycemicInitialPopulation';
    var ipRules = {
        achClasses: ['IMP', 'ACUTE', 'NONAC', 'SS', 'EMER', 'OBSENC'],
        hypoClasses: ['IMP', 'ACUTE', 'NONAC', 'SS'],
        statuses: ['in-progress', 'finished', 'triaged', 'onleave', 'entered-in-error'],
        diabetesMedicationCodes: ['1116635', '311040'],
        hypoScenarioIds: []
    };

    var catalog = null;
    var mode = 'library';
    var readOnly = false;
    var enablePickerAdd = false;
    var options = {
        catalogUrl: '/PatientConfigurations/Catalog',
        seedUrl: '/PatientConfigurations/SeedFromProfile'
    };
    var seeding = false;
    var lastSeededIntent = null;

    function $(id) { return document.getElementById(id); }
    function val(id) { var el = $(id); return el ? el.value : ''; }
    function setVal(id, value) { var el = $(id); if (el) el.value = value == null ? '' : String(value); }
    function emptyToNull(s) { return s && String(s).trim() ? String(s).trim() : null; }
    function esc(s) {
        return String(s ?? '').replace(/[&<>"']/g, function (ch) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[ch];
        });
    }

    function readBool(id) {
        var v = val(id);
        if (v === 'true') return true;
        if (v === 'false') return false;
        return null;
    }
    function writeBool(id, value) {
        if (value === true) setVal(id, 'true');
        else if (value === false) setVal(id, 'false');
        else setVal(id, '');
    }

    function parseIntOrNull(id) {
        var v = val(id);
        if (v === '' || v == null) return null;
        var n = parseInt(v, 10);
        return Number.isFinite(n) ? n : null;
    }

    function findCatalogItem(list, code) {
        if (!list || !code) return null;
        var key = String(code);
        return list.find(function (i) { return String(i.code) === key; }) || null;
    }

    function storedLabels(host) {
        if (!host) return {};
        try { return JSON.parse(host.dataset.selectedLabels || '{}'); } catch { return {}; }
    }

    function displayFor(host, code) {
        var hit = findCatalogItem(catalogItemsFor(host), code);
        if (hit && hit.display) return hit.display;
        var labels = storedLabels(host);
        if (labels[code]) return labels[code];
        return code;
    }

    function normalizeMode(value) {
        var raw = String(value || 'Inherit');
        if (raw === '1' || raw.toLowerCase() === 'replace') return 'Replace';
        if (raw === '2' || raw.toLowerCase() === 'append') return 'Append';
        return 'Inherit';
    }

    function pickerHost(id) { return $(id); }

    function selectedCodes(host) {
        if (!host) return [];
        try { return JSON.parse(host.dataset.selected || '[]'); } catch { return []; }
    }

    function setSelectedCodes(host, codes) {
        if (!host) return;
        host.dataset.selected = JSON.stringify(codes || []);
        refreshPickerToggle(host);
        host.querySelectorAll('.pc-picker-choice').forEach(function (el) {
            var on = (codes || []).indexOf(el.value) >= 0;
            if (el.type === 'checkbox' || el.type === 'radio') el.checked = on;
        });
    }

    function refreshPickerToggle(host) {
        if (!host) return;
        var toggle = host.querySelector('.pc-picker-toggle');
        if (!toggle) return;
        var codes = selectedCodes(host);
        if (!codes.length) {
            toggle.textContent = 'None';
            return;
        }
        if (host.dataset.multiple === 'true') {
            toggle.textContent = codes.length === 1
                ? displayFor(host, codes[0])
                : codes.length + ' selected';
            return;
        }
        toggle.textContent = displayFor(host, codes[0]);
    }

    function catalogItemsFor(host) {
        if (!catalog || !host) return [];
        return catalog[host.dataset.catalog] || [];
    }

    function pickerItems(host) {
        var items = catalogItemsFor(host).slice();
        var seen = {};
        items.forEach(function (i) { seen[String(i.code)] = true; });
        selectedCodes(host).forEach(function (code) {
            if (seen[code]) return;
            seen[code] = true;
            items.unshift({ code: code, display: displayFor(host, code) });
        });
        return items;
    }

    function pickerQuery(host, query) {
        var searchEl = host && host.querySelector('.pc-picker-search');
        return query != null ? query : (searchEl ? searchEl.value : '');
    }

    function pickerResultsHtml(host, query) {
        var items = pickerItems(host);
        var multiple = host.dataset.multiple === 'true';
        var selected = selectedCodes(host);
        var q = String(query || '').trim();
        var qLower = q.toLowerCase();
        var matches = !qLower ? items : items.filter(function (item) {
            return String(item.display || '').toLowerCase().indexOf(qLower) >= 0
                || String(item.code || '').toLowerCase().indexOf(qLower) >= 0;
        });
        var cap = 150;
        var shown = matches.slice(0, cap);
        selected.forEach(function (code) {
            if (shown.some(function (i) { return String(i.code) === String(code); })) return;
            var hit = items.find(function (i) { return String(i.code) === String(code); });
            if (hit) shown.unshift(hit);
        });
        var list = shown.map(function (item) {
            var code = String(item.code);
            var checked = selected.indexOf(code) >= 0 ? ' checked' : '';
            var input = multiple
                ? '<input class="form-check-input pc-picker-choice mt-1" type="checkbox" value="' + esc(code) + '"' + checked + ' />'
                : '<input class="form-check-input pc-picker-choice mt-1" type="radio" name="' + esc(host.id) + '_choice" value="' + esc(code) + '"' + checked + ' />';
            var extra = item.incomplete ? ' <span class="text-muted">needs details</span>' : '';
            return '<label class="pc-picker-item"><span>' + input + '</span><span>' + esc(item.display) +
                ' <span class="text-muted">(' + esc(item.code) + ')</span>' + extra + '</span></label>';
        }).join('');
        if (!shown.length)
            list = '<div class="text-muted small px-1 py-1">No matches in the catalog.</div>';
        else if (matches.length > cap)
            list += '<div class="text-muted small px-1 pt-1">Showing ' + cap + ' of ' + matches.length + '. Type more to narrow.</div>';
        var exact = q && items.some(function (i) { return String(i.code).toLowerCase() === qLower; });
        var add = enablePickerAdd && q && !exact
            ? '<button type="button" class="btn btn-sm btn-outline-primary w-100 mt-2 pc-picker-add">Add “' + esc(q) + '”</button>'
            : '';
        return { list: list, add: add };
    }

    function applyPickerResults(host, query) {
        if (!host) return;
        var html = pickerResultsHtml(host, query);
        var listEl = host.querySelector('.pc-picker-list');
        var addEl = host.querySelector('.pc-picker-add-slot');
        if (listEl) listEl.innerHTML = html.list;
        if (addEl) addEl.innerHTML = html.add;
    }

    function pickerIsOpen(host) {
        var menu = host && host.querySelector('.dropdown-menu');
        return !!(menu && menu.classList.contains('show'));
    }

    function showPicker(host) {
        var toggle = host && host.querySelector('.pc-picker-toggle');
        if (!toggle || typeof bootstrap === 'undefined' || !bootstrap.Dropdown) return;
        bootstrap.Dropdown.getOrCreateInstance(toggle).show();
    }

    function renderPicker(host, query) {
        if (!host || !catalog) return;
        var keepQuery = pickerQuery(host, query);
        var placeholder = host.dataset.placeholder || 'Search…';
        var keepOpen = pickerIsOpen(host);
        if (host.querySelector('.pc-picker-menu')) {
            var searchEl = host.querySelector('.pc-picker-search');
            if (query != null && searchEl)
                searchEl.value = query;
            applyPickerResults(host, keepQuery);
            refreshPickerToggle(host);
            if (keepOpen) showPicker(host);
            return;
        }

        var html = pickerResultsHtml(host, keepQuery);
        host.innerHTML =
            '<div class="dropdown">' +
                '<button type="button" class="btn btn-sm btn-outline-secondary dropdown-toggle w-100 pc-picker-toggle" data-bs-toggle="dropdown" data-bs-auto-close="outside"></button>' +
                '<div class="dropdown-menu p-2 pc-picker-menu">' +
                    '<input type="text" class="form-control form-control-sm mb-2 pc-picker-search" autocomplete="off" placeholder="' + esc(placeholder) + '" value="' + esc(keepQuery) + '" />' +
                    '<div class="mb-1"><button type="button" class="btn btn-link btn-sm p-0 pc-picker-clear">Clear</button></div>' +
                    '<div class="pc-picker-list">' + html.list + '</div>' +
                    '<div class="pc-picker-add-slot">' + html.add + '</div>' +
                '</div>' +
            '</div>';
        refreshPickerToggle(host);
        if (keepOpen) showPicker(host);
    }

    function verificationToken() {
        var t = document.querySelector('input[name="__RequestVerificationToken"]');
        return t ? t.value : '';
    }

    function rememberCatalogItem(kind, row) {
        if (!catalog || !kind || !row || !row.code) return;
        var list = catalog[kind] || [];
        var found = list.some(function (i) { return String(i.code) === String(row.code); });
        if (!found) {
            list.push(row);
            catalog[kind] = list;
        }
        return row;
    }

    function addPickerCode(host, query) {
        var kind = host && host.dataset.catalog;
        if (!kind || !query) return;
        var url = options.lookupUrl || '/PatientConfigurations/LookupCode';
        fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': verificationToken() },
            body: JSON.stringify({ kind: kind, code: query, save: true })
        }).then(function (r) {
            if (!r.ok) throw new Error('Could not add that code.');
            return r.json();
        }).then(function (row) {
            rememberCatalogItem(kind, row);
            var labels = storedLabels(host);
            labels[String(row.code)] = row.display || row.code;
            host.dataset.selectedLabels = JSON.stringify(labels);
            var selected = selectedCodes(host);
            if (host.dataset.multiple === 'true') {
                if (selected.indexOf(String(row.code)) < 0) selected.push(String(row.code));
            } else {
                selected = [String(row.code)];
            }
            host.dataset.selected = JSON.stringify(selected);
            renderPicker(host, '');
            updateSectionBadges();
        }).catch(function () {
            var row = { code: query, display: query };
            rememberCatalogItem(kind, row);
            var selected = host.dataset.multiple === 'true' ? selectedCodes(host).concat([query]) : [query];
            host.dataset.selected = JSON.stringify(selected);
            renderPicker(host, '');
            updateSectionBadges();
        });
    }

    function bindPickerEvents(root) {
        function stopPickerSearchBubble(e) {
            if (!e.target.classList.contains('pc-picker-search')) return;
            e.stopPropagation();
        }
        root.addEventListener('keydown', function (e) {
            if (!e.target.classList.contains('pc-picker-search')) return;
            e.stopPropagation();
            if (e.key === 'Enter')
                e.preventDefault();
        });
        root.addEventListener('keyup', stopPickerSearchBubble);
        root.addEventListener('input', function (e) {
            if (!e.target.classList.contains('pc-picker-search')) return;
            e.stopPropagation();
            applyPickerResults(e.target.closest('.pc-picker'), e.target.value);
        });
        root.addEventListener('change', function (e) {
            var host = e.target.closest('.pc-picker');
            if (!host) return;
            if (!e.target.classList.contains('pc-picker-choice')) return;
            var multiple = host.dataset.multiple === 'true';
            var codes;
            if (multiple) {
                codes = Array.prototype.map.call(host.querySelectorAll('.pc-picker-choice:checked'), function (el) { return el.value; });
            } else {
                codes = e.target.checked ? [e.target.value] : [];
            }
            host.dataset.selected = JSON.stringify(codes);
            refreshPickerToggle(host);
            updateSectionBadges();
        });
        root.addEventListener('click', function (e) {
            if (e.target.classList.contains('pc-picker-search'))
                e.stopPropagation();
            var add = enablePickerAdd && e.target.closest('.pc-picker-add');
            if (add) {
                e.stopPropagation();
                var host = add.closest('.pc-picker');
                var search = host && host.querySelector('.pc-picker-search');
                addPickerCode(host, search && search.value);
                return;
            }
            var clear = e.target.closest('.pc-picker-clear');
            if (!clear) return;
            e.stopPropagation();
            var hostClear = clear.closest('.pc-picker');
            setSelectedCodes(hostClear, []);
            applyPickerResults(hostClear, pickerQuery(hostClear));
            updateSectionBadges();
        });
    }

    function renderClinicalProfileSelect(selectedId) {
        var host = $('PcClinicalProfile');
        if (!host || !catalog) return;
        var current = selectedId != null ? selectedId : host.value;
        var opts = '<option value="">Select a clinical profile…</option>' +
            catalog.scenarios.map(function (s) {
                return '<option value="' + esc(s.id) + '">' + esc(s.display) + ' (' + esc(s.icd) + ')</option>';
            }).join('');
        host.innerHTML = opts;
        if (current) host.value = current;
    }

    function renderTypeCounts() {
        var host = $('pcTypeCounts');
        if (!host) return;
        host.innerHTML = RESOURCE_TYPES.map(function (t) {
            return '<div class="col-md-4"><label class="form-label small">' + t + '</label>' +
                '<input type="number" min="0" class="form-control form-control-sm pc-type-count" data-type="' + t + '" placeholder="Default mix" /></div>';
        }).join('');
    }

    function renderAllPickers() {
        ['PcConditionPicker', 'PcObservationPicker', 'PcProcedurePicker',
            'PcPrimaryDxPicker', 'PcMedAdminPicker', 'PcServiceRequestPicker',
            'PcSpecimenPicker', 'PcDiagnosticReportPicker'].forEach(function (id) {
            renderPicker($(id));
        });
    }

    function paletteFromPicker(host, kind) {
        if (!host) return null;
        var codes = selectedCodes(host);
        if (!codes.length) return [];
        var items = catalogItemsFor(host);
        return codes.map(function (code) {
            var hit = findCatalogItem(items, code);
            var display = displayFor(host, code);
            if (kind === 'obs') {
                return {
                    loincCode: code,
                    loincDisplay: display,
                    type: hit && hit.category ? hit.category : 'laboratory',
                    unit: hit && hit.unit ? hit.unit : null,
                    unitCode: hit && hit.unit ? hit.unit : null,
                    minValue: hit ? hit.normLow : null,
                    maxValue: hit ? hit.normHigh : null
                };
            }
            var item = { code: code, display: display };
            if (hit && hit.category) item.category = hit.category;
            return item;
        });
    }

    function singleFromPicker(host) {
        var codes = selectedCodes(host);
        if (!codes.length) return { code: null, display: null };
        return { code: codes[0], display: displayFor(host, codes[0]) };
    }

    function loadPaletteIntoPicker(host, list, codeKey) {
        if (!host) return;
        var labels = storedLabels(host);
        var codes = (list || []).map(function (i) {
            if (i == null) return null;
            if (typeof i === 'string' || typeof i === 'number') return String(i);
            var code = i[codeKey] || i.code || i.loincCode;
            var display = i.display || i.loincDisplay;
            if (code && display) labels[String(code)] = display;
            return code;
        }).filter(Boolean).map(String);
        host.dataset.selected = JSON.stringify(codes);
        host.dataset.selectedLabels = JSON.stringify(labels);
        renderPicker(host);
    }

    function inSet(list, value) {
        if (value == null || value === '') return false;
        var v = String(value).toLowerCase();
        return (list || []).some(function (x) { return String(x).toLowerCase() === v; });
    }

    function measureShortName(id) {
        if (id === ACH) return 'ACH';
        if (id === ACH_DAILY) return 'ACH Daily';
        if (id === HYPO) return 'Hypo';
        return id;
    }

    function predictQualification(inputs) {
        inputs = inputs || {};
        var encounterClass = inputs.encounterClass || 'IMP';
        var status = inputs.encounterStatus || 'finished';
        var statusOk = !status || inSet(ipRules.statuses, status);
        var achIp = statusOk && inSet(ipRules.achClasses, encounterClass);
        var hypoIp = statusOk && inSet(ipRules.hypoClasses, encounterClass);
        var insulin = inputs.includeHypoglycemicInsulin;
        if (insulin == null)
            insulin = inSet(ipRules.hypoScenarioIds, inputs.clinicalScenarioId);
        var diabetesMed = inSet(ipRules.diabetesMedicationCodes, inputs.medicationAdministrationRxNorm);
        var hypoClinical = !!insulin || diabetesMed;
        var achReason = achIp
            ? 'Encounter class ' + encounterClass + ' is an ACH initial-population class.'
            : 'Encounter class ' + encounterClass + ' is not an ACH initial-population class.';
        var hypoReason = !hypoIp
            ? 'Encounter class ' + encounterClass + ' is not a Hypoglycemic inpatient class.'
            : !hypoClinical
                ? 'No antidiabetic (hypoglycemic insulin) medication on this configuration.'
                : 'Inpatient class plus antidiabetic medication.';
        var hypoQ = hypoIp && hypoClinical;
        var measures = {};
        measures[ACH] = achIp ? 'Qualifying' : 'NonQualifying';
        measures[ACH_DAILY] = achIp ? 'Qualifying' : 'NonQualifying';
        measures[HYPO] = hypoQ ? 'Qualifying' : 'NonQualifying';
        return {
            measures: measures,
            items: [
                { id: ACH, qualifying: achIp, reason: achReason },
                { id: ACH_DAILY, qualifying: achIp, reason: achReason },
                { id: HYPO, qualifying: hypoQ, reason: hypoReason }
            ]
        };
    }

    function renderQualificationBadges(prediction, selectedFamilies, censusPlaces) {
        var families = selectedFamilies && selectedFamilies.length
            ? selectedFamilies
            : [ACH, ACH_DAILY, HYPO];
        if (!families.length)
            return '<span class="text-muted small">Select measures</span>';
        var items = (prediction && prediction.items) || [];
        var html = items.filter(function (item) { return families.indexOf(item.id) >= 0; }).map(function (item) {
            var icon = item.qualifying
                ? '<i class="bi bi-check-circle-fill text-success" aria-hidden="true"></i>'
                : '<i class="bi bi-x-circle-fill text-danger" aria-hidden="true"></i>';
            var word = item.qualifying ? 'qualifies' : 'does not qualify';
            return '<span class="d-inline-flex align-items-center gap-1 me-2" title="' + esc(item.reason) + '">'
                + icon + ' <span class="small">' + esc(measureShortName(item.id)) + '</span>'
                + '<span class="visually-hidden"> ' + word + '</span></span>';
        }).join('');
        if (censusPlaces === false)
            html += ' <span class="d-inline-flex align-items-center gap-1 text-muted" title="Admit/discharge pattern is outside the report window"><i class="bi bi-slash-circle" aria-hidden="true"></i> <span class="small">Out of window</span></span>';
        return html || '<span class="text-muted small">Select a configuration</span>';
    }

    var reportPeriod = { start: null, end: null };
    var DEFAULT_STAY_PATTERN = 'AdmittedBeforePeriodRemainsInpatientAfterPeriod';

    function stayPatterns() {
        return (catalog && catalog.stayPatterns) || [];
    }

    function renderStayPatternSelect(selected) {
        var sel = $('PcStayPattern');
        if (!sel) return;
        var current = selected || sel.value || DEFAULT_STAY_PATTERN;
        var opts = stayPatterns();
        if (!opts.length) {
            opts = [{ value: DEFAULT_STAY_PATTERN, label: 'Before -> Remains after', hint: 'Admitted before report period; remains inpatient after report period.', expectedInReport: true }];
        }
        sel.innerHTML = opts.map(function (p) {
            return '<option value="' + esc(p.value) + '" title="' + esc(p.hint || p.label) + '"'
                + (p.value === current ? ' selected' : '') + '>' + esc(p.label) + '</option>';
        }).join('');
        if (current) sel.value = current;
    }

    function parsePeriodDate(value, endOfDay) {
        if (!value) return null;
        var raw = String(value).trim();
        if (!raw) return null;
        if (/^\d{4}-\d{2}-\d{2}$/.test(raw))
            return new Date(raw + (endOfDay ? 'T23:59:59Z' : 'T00:00:00Z'));
        var d = new Date(raw);
        return isNaN(d.getTime()) ? null : d;
    }

    function computeStayWindow(pattern, startIso, endIso, seed) {
        var rs = parsePeriodDate(startIso, false);
        var re = parsePeriodDate(endIso, true);
        if (!rs || !re || re <= rs) return null;
        var totalMinutes = Math.max(1, Math.round((re - rs) / 60000));
        var admissionOffsetMinutes = Math.max(5, Math.round(totalMinutes * 0.20));
        var dischargeOffsetMinutes = Math.max(admissionOffsetMinutes + 30, Math.round(totalMinutes * 0.75));
        var jitter = Math.abs((seed || 0) % 20);
        function addMinutes(d, n) { return new Date(d.getTime() + n * 60000); }
        var inPeriodStart = addMinutes(rs, Math.min(totalMinutes - 1, admissionOffsetMinutes + jitter));
        var inPeriodEnd = addMinutes(rs, Math.min(totalMinutes - 1, dischargeOffsetMinutes + jitter));
        if (inPeriodEnd <= inPeriodStart) inPeriodEnd = addMinutes(inPeriodStart, 30);
        var hours = (re - rs) / 3600000;
        var boundaryPadMs = hours >= 12 ? 6 * 3600000 : Math.max(60, totalMinutes / 6) * 60000;
        var name = pattern || DEFAULT_STAY_PATTERN;
        if (name === 'AdmittedBeforePeriodRemainsInpatientAfterPeriod')
            return { start: new Date(rs.getTime() - boundaryPadMs), end: new Date(re.getTime() + boundaryPadMs) };
        if (name === 'AdmittedBeforePeriodDischargedDuringPeriod')
            return { start: new Date(rs.getTime() - boundaryPadMs), end: inPeriodEnd };
        if (name === 'AdmittedDuringPeriodRemainsInpatientAfterPeriod')
            return { start: inPeriodStart, end: new Date(re.getTime() + boundaryPadMs) };
        if (name === 'AdmittedDuringPeriodDischargedDuringPeriod')
            return { start: inPeriodStart, end: inPeriodEnd };
        if (name === 'AdmittedAndDischargedBeforePeriod')
            return { start: new Date(rs.getTime() - boundaryPadMs - 6 * 3600000), end: addMinutes(rs, -60) };
        if (name === 'AdmittedAndDischargedAfterPeriod')
            return { start: addMinutes(re, 60), end: new Date(re.getTime() + boundaryPadMs + 6 * 3600000) };
        return { start: new Date(rs.getTime() - boundaryPadMs), end: new Date(re.getTime() + boundaryPadMs) };
    }

    function formatStayStamp(d) {
        if (!d || isNaN(d.getTime())) return '';
        function pad(n) { return n < 10 ? '0' + n : '' + n; }
        return d.getUTCFullYear() + '-' + pad(d.getUTCMonth() + 1) + '-' + pad(d.getUTCDate())
            + ' ' + pad(d.getUTCHours()) + ':' + pad(d.getUTCMinutes()) + ' UTC';
    }

    function patternExpectedInReport(pattern) {
        var hit = stayPatterns().find(function (p) { return p.value === pattern; });
        if (hit && typeof hit.expectedInReport === 'boolean') return hit.expectedInReport;
        return pattern !== 'AdmittedAndDischargedBeforePeriod' && pattern !== 'AdmittedAndDischargedAfterPeriod';
    }

    function refreshStayPreview() {
        var datesEl = $('pcStayPreviewDates');
        var hintEl = $('pcStayPreviewHint');
        if (!datesEl) return;
        var pattern = val('PcStayPattern') || DEFAULT_STAY_PATTERN;
        var hit = stayPatterns().find(function (p) { return p.value === pattern; });
        var hint = hit && hit.hint ? hit.hint : '';
        var window = computeStayWindow(pattern, reportPeriod.start, reportPeriod.end, 0);
        if (window) {
            datesEl.textContent = 'Admit ' + formatStayStamp(window.start) + '  →  Discharge ' + formatStayStamp(window.end);
            var minutes = Math.max(1, Math.round((window.end - window.start) / 60000));
            setVal('PcDuration', minutes);
            if (hintEl) hintEl.textContent = hint + (patternExpectedInReport(pattern) ? '' : ' This stay is outside the report window.');
        } else {
            datesEl.textContent = 'Admit and discharge dates are placed against the scenario report period.';
            if (hintEl) hintEl.textContent = hint || 'Set a report period on the scenario to see concrete dates.';
        }
        refreshPredicted();
    }

    function setReportPeriod(start, end) {
        reportPeriod.start = start || null;
        reportPeriod.end = end || null;
        refreshStayPreview();
    }

    function collectQualificationInputs() {
        var med = singleFromPicker($('PcMedAdminPicker'));
        return {
            encounterClass: emptyToNull(val('PcEncClass')),
            encounterStatus: emptyToNull(val('PcEncStatus')),
            includeHypoglycemicInsulin: readBool('PcHypoInsulin'),
            medicationAdministrationRxNorm: med.code,
            clinicalScenarioId: emptyToNull(val('PcClinicalProfile'))
        };
    }

    function refreshPredicted() {
        var host = $('pcPredictedBadges');
        if (!host) return;
        var pattern = val('PcStayPattern') || DEFAULT_STAY_PATTERN;
        host.innerHTML = renderQualificationBadges(
            predictQualification(collectQualificationInputs()),
            null,
            patternExpectedInReport(pattern));
    }

    function syncVolumeMode() {
        var exact = $('PcVolumeExact') && $('PcVolumeExact').checked;
        var host = $('pcTypeCounts');
        if (host) host.hidden = !exact;
        if (host) host.querySelectorAll('input').forEach(function (el) { el.disabled = readOnly || !exact; });
        ['PcResMin', 'PcResMax'].forEach(function (id) {
            var el = $(id);
            if (el) el.disabled = readOnly;
        });
    }

    function sectionHasValue(ids, pickerIds) {
        for (var i = 0; i < (ids || []).length; i++) {
            var el = $(ids[i]);
            if (!el) continue;
            if (el.type === 'checkbox') { if (el.checked !== el.defaultChecked) return true; continue; }
            if (el.value) return true;
        }
        for (var j = 0; j < (pickerIds || []).length; j++) {
            var host = $(pickerIds[j]);
            if (!host) continue;
            if (selectedCodes(host).length) return true;
        }
        return false;
    }

    function updateSectionBadges() {
        function setBadge(id, on) {
            var el = $(id);
            if (!el) return;
            el.textContent = on ? 'From profile' : 'Empty';
            el.className = 'pc-section-badge ' + (on ? 'bg-primary text-white' : 'bg-light text-muted border');
        }
        setBadge('pcBadgeDemo', sectionHasValue(['PcGender', 'PcMinAge', 'PcMaxAge']));
        setBadge('pcBadgeEnc', sectionHasValue(['PcEncClass', 'PcEncStatus', 'PcStayPattern', 'PcDischarge', 'PcHospitalization']));
        refreshStayPreview();
        setBadge('pcBadgeClinical', sectionHasValue(
            ['PcAllergy', 'PcLabs', 'PcHypoInsulin', 'PcConditionMeds', 'PcSpreadObs', 'PcAdditionalConditions'],
            ['PcPrimaryDxPicker', 'PcConditionPicker', 'PcObservationPicker', 'PcProcedurePicker', 'PcMedAdminPicker', 'PcServiceRequestPicker', 'PcSpecimenPicker', 'PcDiagnosticReportPicker']));
        var exact = $('PcVolumeExact') && $('PcVolumeExact').checked;
        var anyCount = false;
        document.querySelectorAll('.pc-type-count').forEach(function (el) { if (el.value !== '') anyCount = true; });
        var mixBadge = $('pcBadgeMix');
        if (mixBadge) {
            if (exact && anyCount) {
                mixBadge.textContent = 'Exact counts';
                mixBadge.className = 'pc-section-badge bg-primary text-white';
            } else {
                var min = val('PcResMin') || '50';
                var max = val('PcResMax') || min;
                mixBadge.textContent = min + '–' + max;
                mixBadge.className = 'pc-section-badge bg-primary text-white';
            }
        }
        refreshPredicted();
    }

    function resetSection(name) {
        if (lastSeededIntent) {
            applyIntent(lastSeededIntent, name);
            updateSectionBadges();
            return;
        }
        if (name === 'demo') {
            setVal('PcGender', ''); setVal('PcMinAge', ''); setVal('PcMaxAge', '');
        } else if (name === 'enc') {
            setVal('PcEncClass', ''); setVal('PcEncStatus', ''); setVal('PcStayPattern', DEFAULT_STAY_PATTERN);
            setVal('PcDischarge', ''); setVal('PcHospitalization', '');
        } else if (name === 'clinical') {
            ['PcAllergy', 'PcLabs', 'PcHypoInsulin', 'PcConditionMeds', 'PcSpreadObs', 'PcAdditionalConditions'].forEach(function (id) { setVal(id, ''); });
            ['PcPrimaryDxPicker', 'PcConditionPicker', 'PcObservationPicker', 'PcProcedurePicker', 'PcMedAdminPicker', 'PcServiceRequestPicker', 'PcSpecimenPicker', 'PcDiagnosticReportPicker'].forEach(function (id) {
                var host = $(id);
                if (!host) return;
                setSelectedCodes(host, []);
                renderPicker(host);
            });
        } else if (name === 'mix') {
            if ($('PcVolumeExact')) $('PcVolumeExact').checked = false;
            setVal('PcResMin', 50);
            setVal('PcResMax', 100);
            document.querySelectorAll('.pc-type-count').forEach(function (el) { el.value = ''; });
            syncVolumeMode();
        }
        updateSectionBadges();
    }

    function collectIntent() {
        var condHost = $('PcConditionPicker');
        var obsHost = $('PcObservationPicker');
        var procHost = $('PcProcedurePicker');
        var dx = singleFromPicker($('PcPrimaryDxPicker'));
        var med = singleFromPicker($('PcMedAdminPicker'));
        var sr = singleFromPicker($('PcServiceRequestPicker'));
        var spec = singleFromPicker($('PcSpecimenPicker'));
        var dr = singleFromPicker($('PcDiagnosticReportPicker'));
        var counts = {};
        var anyCounts = false;
        if ($('PcVolumeExact') && $('PcVolumeExact').checked) {
            document.querySelectorAll('.pc-type-count').forEach(function (el) {
                if (el.value !== '') { counts[el.dataset.type] = parseInt(el.value, 10); anyCounts = true; }
            });
        }
        var conditionPalette = paletteFromPicker(condHost, 'coded');
        var observationPalette = paletteFromPicker(obsHost, 'obs');
        var procedurePalette = paletteFromPicker(procHost, 'coded');

        return {
            gender: emptyToNull(val('PcGender')),
            minAge: parseIntOrNull('PcMinAge'),
            maxAge: parseIntOrNull('PcMaxAge'),
            encounterClass: emptyToNull(val('PcEncClass')),
            encounterStatus: emptyToNull(val('PcEncStatus')),
            durationMinutes: parseIntOrNull('PcDuration'),
            dischargeDisposition: emptyToNull(val('PcDischarge')),
            includeHospitalization: readBool('PcHospitalization'),
            primaryConditionSnomed: dx.code,
            primaryConditionDisplay: dx.display,
            conditionPalette: conditionPalette,
            conditionPaletteMode: 'Replace',
            additionalConditionCount: parseIntOrNull('PcAdditionalConditions'),
            generateLabWork: readBool('PcLabs'),
            observationPalette: observationPalette,
            observationPaletteMode: 'Replace',
            spreadObservationsAcrossEncounter: readBool('PcSpreadObs'),
            includeAllergy: readBool('PcAllergy'),
            includeConditionDrivenMedications: readBool('PcConditionMeds'),
            includeHypoglycemicInsulin: readBool('PcHypoInsulin'),
            resourceTypeCounts: anyCounts ? counts : null,
            procedurePalette: procedurePalette,
            procedurePaletteMode: 'Replace',
            medicationAdministrationRxNorm: med.code,
            medicationAdministrationDisplay: med.display,
            serviceRequestLoinc: sr.code,
            serviceRequestDisplay: sr.display,
            specimenTypeCode: spec.code,
            specimenTypeDisplay: spec.display,
            diagnosticReportLoinc: dr.code,
            diagnosticReportDisplay: dr.display
        };
    }

    function collect() {
        var story = emptyToNull(val('PcClinicalProfile'));
        return {
            id: val('PcId') || (global.crypto && crypto.randomUUID ? crypto.randomUUID() : null),
            name: val('PcName'),
            description: val('PcDescription'),
            isSystem: val('PcIsSystem') === 'true',
            clinicalScenarioIds: story ? [story] : [],
            resourcesPerPatientMin: parseInt(val('PcResMin') || '50', 10),
            resourcesPerPatientMax: parseInt(val('PcResMax') || '100', 10),
            scheduledInpatientPattern: val('PcStayPattern') || DEFAULT_STAY_PATTERN,
            intent: collectIntent()
        };
    }

    function populate(model) {
        model = model || {};
        setVal('PcId', model.id || '');
        setVal('PcIsSystem', model.isSystem ? 'true' : 'false');
        setVal('PcName', model.name || '');
        setVal('PcDescription', model.description || '');
        setVal('PcResMin', model.resourcesPerPatientMin || 50);
        setVal('PcResMax', model.resourcesPerPatientMax || 100);
        renderStayPatternSelect(model.scheduledInpatientPattern || DEFAULT_STAY_PATTERN);
        var storyId = (model.clinicalScenarioIds && model.clinicalScenarioIds[0]) || model.clinicalScenarioId || '';
        renderClinicalProfileSelect(storyId);
        setVal('PcClinicalProfile', storyId);
        var intent = model.intent || {};
        applyIntent(intent);
        lastSeededIntent = intent.primaryConditionSnomed || (intent.observationPalette && intent.observationPalette.length)
            ? intent
            : null;
        updateSectionBadges();
        setReadOnly(readOnly);
        var hasCodes = lastSeededIntent != null;
        if (storyId && !hasCodes && !readOnly)
            return seedFromProfile(storyId, { suggestName: !model.name });
        return Promise.resolve();
    }

    function applyIntent(intent, only) {
        intent = intent || {};
        function want(section) { return !only || only === section; }
        if (want('demo')) {
            setVal('PcGender', intent.gender || 'random');
            setVal('PcMinAge', intent.minAge);
            setVal('PcMaxAge', intent.maxAge);
        }
        if (want('enc')) {
            setVal('PcEncClass', intent.encounterClass || '');
            setVal('PcEncStatus', intent.encounterStatus || '');
            setVal('PcDischarge', intent.dischargeDisposition || '');
            writeBool('PcHospitalization', intent.includeHospitalization);
        }
        if (want('clinical')) {
            writeBool('PcAllergy', intent.includeAllergy);
            writeBool('PcLabs', intent.generateLabWork);
            writeBool('PcHypoInsulin', intent.includeHypoglycemicInsulin);
            writeBool('PcConditionMeds', intent.includeConditionDrivenMedications);
            writeBool('PcSpreadObs', intent.spreadObservationsAcrossEncounter);
            setVal('PcAdditionalConditions', intent.additionalConditionCount);
            loadPaletteIntoPicker($('PcPrimaryDxPicker'), intent.primaryConditionSnomed
                ? [{ code: intent.primaryConditionSnomed, display: intent.primaryConditionDisplay }]
                : [], 'code');
            loadPaletteIntoPicker($('PcConditionPicker'), intent.conditionPalette, 'code');
            loadPaletteIntoPicker($('PcObservationPicker'), intent.observationPalette, 'loincCode');
            loadPaletteIntoPicker($('PcProcedurePicker'), intent.procedurePalette, 'code');
            loadPaletteIntoPicker($('PcMedAdminPicker'), intent.medicationAdministrationRxNorm
                ? [{ code: intent.medicationAdministrationRxNorm, display: intent.medicationAdministrationDisplay }]
                : [], 'code');
            loadPaletteIntoPicker($('PcServiceRequestPicker'), intent.serviceRequestLoinc
                ? [{ code: intent.serviceRequestLoinc, display: intent.serviceRequestDisplay }]
                : [], 'code');
            loadPaletteIntoPicker($('PcSpecimenPicker'), intent.specimenTypeCode
                ? [{ code: intent.specimenTypeCode, display: intent.specimenTypeDisplay }]
                : [], 'code');
            loadPaletteIntoPicker($('PcDiagnosticReportPicker'), intent.diagnosticReportLoinc
                ? [{ code: intent.diagnosticReportLoinc, display: intent.diagnosticReportDisplay }]
                : [], 'code');
        }
        if (want('mix')) {
            var counts = intent.resourceTypeCounts || {};
            var anyCounts = Object.keys(counts).length > 0;
            if ($('PcVolumeExact')) $('PcVolumeExact').checked = anyCounts;
            document.querySelectorAll('.pc-type-count').forEach(function (el) {
                el.value = counts[el.dataset.type] != null ? counts[el.dataset.type] : '';
            });
            syncVolumeMode();
        }
    }

    function seedFromProfile(scenarioId, opts) {
        opts = opts || {};
        if (!scenarioId || !options.seedUrl) return Promise.resolve();
        var resources = parseInt(val('PcResMin') || '50', 10);
        var url = options.seedUrl
            + '?scenarioId=' + encodeURIComponent(scenarioId)
            + '&resources=' + encodeURIComponent(resources);
        seeding = true;
        return fetch(url).then(function (r) {
            if (!r.ok) throw new Error('Could not load that Clinical Profile.');
            return r.json();
        }).then(function (seed) {
            if (opts.suggestName && seed.display && !emptyToNull(val('PcName')))
                setVal('PcName', seed.display);
            lastSeededIntent = seed.intent || {};
            applyIntent(lastSeededIntent);
            if (seed.exampleResourceCounts) {
                document.querySelectorAll('.pc-type-count').forEach(function (el) {
                    var n = seed.exampleResourceCounts[el.dataset.type];
                    if (n != null) el.placeholder = String(n);
                });
            }
            updateSectionBadges();
        }).finally(function () { seeding = false; });
    }

    function setMode(next) {
        mode = next === 'cohort' ? 'cohort' : 'library';
        var library = document.querySelectorAll('.pc-library-only');
        var cohort = document.querySelectorAll('.pc-cohort-only');
        library.forEach(function (el) { el.style.display = mode === 'library' ? '' : 'none'; });
        cohort.forEach(function (el) { el.style.display = mode === 'cohort' ? '' : 'none'; });
    }

    function setBanner(text) {
        var wrap = $('pcCohortBanner');
        var el = $('pcCohortBannerText');
        if (el) el.textContent = text || '';
        if (wrap) wrap.hidden = !text;
    }

    function setReadOnly(value) {
        readOnly = !!value;
        var root = $('pcEditorRoot');
        if (!root) return;
        root.querySelectorAll('input,select,textarea,button').forEach(function (el) {
            if (el.classList.contains('accordion-button')
                || el.classList.contains('pc-picker-toggle')
                || el.classList.contains('pc-picker-search')
                || el.classList.contains('pc-picker-clear')
                || el.classList.contains('au-info-toggle')) {
                el.disabled = false;
                return;
            }
            if (el.id === 'PcId' || el.id === 'PcIsSystem') return;
            el.disabled = readOnly;
        });
        if (readOnly) {
            document.querySelectorAll('.pc-section-reset').forEach(function (el) { el.disabled = true; });
        }
        syncVolumeMode();
        refreshPredicted();
    }

    function ensureCatalog() {
        if (catalog) return Promise.resolve(catalog);
        return fetch(options.catalogUrl).then(function (r) { return r.json(); }).then(function (c) {
            catalog = c;
            if (c && c.ipClassification) ipRules = c.ipClassification;
            renderClinicalProfileSelect();
            renderStayPatternSelect();
            renderTypeCounts();
            renderAllPickers();
            syncVolumeMode();
            refreshStayPreview();
            return catalog;
        });
    }

    function init(opts) {
        options = Object.assign({
            catalogUrl: '/PatientConfigurations/Catalog',
            seedUrl: '/PatientConfigurations/SeedFromProfile',
            lookupUrl: '/PatientConfigurations/LookupCode'
        }, opts || {});
        var root = $('pcEditorRoot');
        if (!root || root.dataset.pcBound === 'true') return;
        root.dataset.pcBound = 'true';
        bindPickerEvents(root);
        root.addEventListener('change', function (e) {
            if (e.target.id === 'PcVolumeExact') syncVolumeMode();
            if (e.target.id === 'PcClinicalProfile' && !seeding && !readOnly) {
                var id = e.target.value;
                if (id) seedFromProfile(id, { suggestName: true });
            }
            updateSectionBadges();
        });
        root.addEventListener('input', function () { updateSectionBadges(); });
        root.addEventListener('click', function (e) {
            var btn = e.target.closest('.pc-section-reset');
            if (!btn || readOnly) return;
            resetSection(btn.dataset.section);
        });
        setMode(mode);
    }

    if (!document.documentElement.dataset.auHelpBound) {
        document.documentElement.dataset.auHelpBound = 'true';
        document.addEventListener('click', function (e) {
            var btn = e.target.closest('.au-info-toggle');
            if (!btn) return;
            e.preventDefault();
            e.stopPropagation();
            var id = btn.getAttribute('data-help');
            var panel = id ? document.getElementById(id) : null;
            if (!panel) return;
            var willOpen = panel.hasAttribute('hidden');
            var scope = btn.closest('.au-help-scope') || document;
            if (willOpen) {
                scope.querySelectorAll('.au-help-text:not([hidden])').forEach(function (el) {
                    if (el !== panel) el.setAttribute('hidden', '');
                });
                scope.querySelectorAll('.au-info-toggle[aria-expanded="true"]').forEach(function (el) {
                    if (el !== btn) el.setAttribute('aria-expanded', 'false');
                });
            }
            panel.toggleAttribute('hidden', !willOpen);
            btn.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
        });
    }

    global.PatientConfigurationEditor = {
        init: init,
        ensureCatalog: ensureCatalog,
        populate: populate,
        collect: collect,
        collectIntent: collectIntent,
        seedFromProfile: seedFromProfile,
        setMode: setMode,
        setBanner: setBanner,
        setReadOnly: setReadOnly,
        updateSectionBadges: updateSectionBadges,
        predictQualification: predictQualification,
        renderQualificationBadges: renderQualificationBadges,
        setReportPeriod: setReportPeriod,
        refreshStayPreview: refreshStayPreview,
        computeStayWindow: computeStayWindow,
        patternExpectedInReport: patternExpectedInReport,
        measureFamilies: { ACH: ACH, ACH_DAILY: ACH_DAILY, HYPO: HYPO }
    };
})(window);

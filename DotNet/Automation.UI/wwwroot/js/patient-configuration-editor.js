(function (global) {
    var RESOURCE_TYPES = [
        'Observation', 'Condition', 'Procedure', 'MedicationRequest',
        'MedicationAdministration', 'Coverage', 'ServiceRequest', 'Specimen', 'DiagnosticReport'
    ];
    var ACH = 'NhsnAcuteCareHospitalMonthlyInitialPopulation';
    var ACH_DAILY = 'NhsnAcuteCareHospitalDailyInitialPopulation';
    var HYPO = 'NhsnGlycemicControlHypoglycemicInitialPopulation';

    var catalog = null;
    var mode = 'library';
    var readOnly = false;
    var options = {
        catalogUrl: '/PatientConfigurations/Catalog'
    };

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
        var items = catalogItemsFor(host);
        var codes = selectedCodes(host);
        if (!codes.length) {
            toggle.textContent = host.dataset.multiple === 'true' ? 'Story default' : 'From story pack';
            return;
        }
        if (host.dataset.multiple === 'true') {
            toggle.textContent = codes.length === 1
                ? labelFor(items, codes[0])
                : codes.length + ' selected';
            return;
        }
        toggle.textContent = labelFor(items, codes[0]);
    }

    function catalogItemsFor(host) {
        if (!catalog || !host) return [];
        return catalog[host.dataset.catalog] || [];
    }

    function labelFor(items, code) {
        var hit = findCatalogItem(items, code);
        return hit ? (hit.display + ' (' + hit.code + ')') : code;
    }

    function renderPicker(host) {
        if (!host || !catalog) return;
        var items = catalogItemsFor(host);
        var multiple = host.dataset.multiple === 'true';
        var withMode = host.dataset.withMode === 'true';
        var placeholder = host.dataset.placeholder || 'Search…';
        var selected = selectedCodes(host);
        var modeVal = normalizeMode(host.dataset.mode);

        var modeHtml = withMode
            ? '<select class="form-select form-select-sm pc-picker-mode">' +
                '<option value="Inherit"' + (modeVal === 'Inherit' ? ' selected' : '') + '>Inherit story pack</option>' +
                '<option value="Replace"' + (modeVal === 'Replace' ? ' selected' : '') + '>Replace story pack</option>' +
                '<option value="Append"' + (modeVal === 'Append' ? ' selected' : '') + '>Append to story pack</option>' +
              '</select>'
            : '';

        var list = items.map(function (item) {
            var code = String(item.code);
            var checked = selected.indexOf(code) >= 0 ? ' checked' : '';
            var input = multiple
                ? '<input class="form-check-input pc-picker-choice mt-1" type="checkbox" value="' + esc(code) + '"' + checked + ' />'
                : '<input class="form-check-input pc-picker-choice mt-1" type="radio" name="' + esc(host.id) + '_choice" value="' + esc(code) + '"' + checked + ' />';
            return '<label class="pc-picker-item"><span>' + input + '</span><span>' + esc(item.display) +
                ' <span class="text-muted">(' + esc(item.code) + ')</span></span></label>';
        }).join('');

        host.innerHTML =
            '<div class="d-flex gap-1 align-items-stretch">' +
                (modeHtml ? '<div style="min-width:160px;">' + modeHtml + '</div>' : '') +
                '<div class="dropdown flex-grow-1">' +
                    '<button type="button" class="btn btn-sm btn-outline-secondary dropdown-toggle w-100 pc-picker-toggle" data-bs-toggle="dropdown" data-bs-auto-close="outside"></button>' +
                    '<div class="dropdown-menu p-2 pc-picker-menu">' +
                        '<input type="search" class="form-control form-control-sm mb-2 pc-picker-search" placeholder="' + esc(placeholder) + '" />' +
                        (multiple ? '<div class="d-flex justify-content-between mb-1"><button type="button" class="btn btn-link btn-sm p-0 pc-picker-clear">Clear</button></div>' : '<div class="mb-1"><button type="button" class="btn btn-link btn-sm p-0 pc-picker-clear">Use story default</button></div>') +
                        '<div class="pc-picker-list">' + list + '</div>' +
                    '</div>' +
                '</div>' +
            '</div>';
        refreshPickerToggle(host);
    }

    function bindPickerEvents(root) {
        root.addEventListener('input', function (e) {
            if (!e.target.classList.contains('pc-picker-search')) return;
            var q = e.target.value.toLowerCase();
            var list = e.target.closest('.dropdown-menu').querySelectorAll('.pc-picker-item');
            list.forEach(function (row) {
                row.style.display = !q || row.textContent.toLowerCase().indexOf(q) >= 0 ? '' : 'none';
            });
        });
        root.addEventListener('change', function (e) {
            var host = e.target.closest('.pc-picker');
            if (!host) return;
            if (e.target.classList.contains('pc-picker-mode')) {
                host.dataset.mode = e.target.value;
                if (e.target.value !== 'Inherit' && !selectedCodes(host).length) {
                    // keep inherit-or-empty until the user picks codes
                }
                updateSectionBadges();
                return;
            }
            if (!e.target.classList.contains('pc-picker-choice')) return;
            var multiple = host.dataset.multiple === 'true';
            var codes;
            if (multiple) {
                codes = Array.prototype.map.call(host.querySelectorAll('.pc-picker-choice:checked'), function (el) { return el.value; });
            } else {
                codes = e.target.checked ? [e.target.value] : [];
            }
            host.dataset.selected = JSON.stringify(codes);
            if (codes.length && host.dataset.withMode === 'true' && normalizeMode(host.dataset.mode) === 'Inherit') {
                host.dataset.mode = 'Replace';
                var modeEl = host.querySelector('.pc-picker-mode');
                if (modeEl) modeEl.value = 'Replace';
            }
            refreshPickerToggle(host);
            updateSectionBadges();
        });
        root.addEventListener('click', function (e) {
            var clear = e.target.closest('.pc-picker-clear');
            if (!clear) return;
            var host = clear.closest('.pc-picker');
            setSelectedCodes(host, []);
            if (host.dataset.withMode === 'true') {
                host.dataset.mode = 'Inherit';
                var modeEl = host.querySelector('.pc-picker-mode');
                if (modeEl) modeEl.value = 'Inherit';
            }
            updateSectionBadges();
        });
    }

    function renderStories() {
        var host = $('PcStories');
        if (!host || !catalog) return;
        host.innerHTML = catalog.scenarios.map(function (s) {
            return '<div class="form-check"><label class="form-check-label small"><input class="form-check-input pc-story" type="checkbox" value="' + esc(s.id) + '" /> '
                + esc(s.display) + ' <span class="text-muted">(' + esc(s.icd) + ')</span></label></div>';
        }).join('');
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
        if (!codes.length) return null;
        var items = catalogItemsFor(host);
        return codes.map(function (code) {
            var hit = findCatalogItem(items, code);
            if (kind === 'obs') {
                return {
                    loincCode: code,
                    loincDisplay: hit ? hit.display : code,
                    type: hit && hit.category ? hit.category : 'laboratory',
                    unit: hit && hit.unit ? hit.unit : null,
                    unitCode: hit && hit.unit ? hit.unit : null,
                    minValue: hit ? hit.normLow : null,
                    maxValue: hit ? hit.normHigh : null
                };
            }
            var item = { code: code, display: hit ? hit.display : code };
            if (hit && hit.category) item.category = hit.category;
            return item;
        });
    }

    function singleFromPicker(host) {
        var codes = selectedCodes(host);
        if (!codes.length) return { code: null, display: null };
        var hit = findCatalogItem(catalogItemsFor(host), codes[0]);
        return { code: codes[0], display: hit ? hit.display : codes[0] };
    }

    function loadPaletteIntoPicker(host, list, codeKey) {
        if (!host) return;
        var codes = (list || []).map(function (i) { return i[codeKey] || i.code || i.loincCode; }).filter(Boolean);
        host.dataset.selected = JSON.stringify(codes);
        renderPicker(host);
    }

    function collectMeasureEligibilities() {
        var q = val('PcQualification') === 'NonQualifying' ? 'NonQualifying' : 'Qualifying';
        var map = {};
        map[ACH] = $('PcMeasureAch') && $('PcMeasureAch').checked && q === 'Qualifying' ? 'Qualifying' : 'NonQualifying';
        map[ACH_DAILY] = $('PcMeasureDaily') && $('PcMeasureDaily').checked && q === 'Qualifying' ? 'Qualifying' : 'NonQualifying';
        map[HYPO] = $('PcMeasureHypo') && $('PcMeasureHypo').checked && q === 'Qualifying' ? 'Qualifying' : 'NonQualifying';
        return map;
    }

    function populateMeasureEligibilities(map, qualification) {
        var q = qualification === 'NonQualifying' ? 'NonQualifying' : 'Qualifying';
        function on(key) {
            var v = map && map[key];
            return q === 'Qualifying' && (v === 'Qualifying' || v === 0 || v === '0');
        }
        if ($('PcMeasureAch')) $('PcMeasureAch').checked = on(ACH);
        if ($('PcMeasureDaily')) $('PcMeasureDaily').checked = on(ACH_DAILY);
        if ($('PcMeasureHypo')) $('PcMeasureHypo').checked = on(HYPO);
        syncMeasureChecks();
    }

    function syncMeasureChecks() {
        var nq = val('PcQualification') === 'NonQualifying';
        ['PcMeasureAch', 'PcMeasureDaily', 'PcMeasureHypo'].forEach(function (id) {
            var el = $(id);
            if (!el) return;
            el.disabled = readOnly || nq;
            if (nq) el.checked = false;
        });
        if (!nq && $('PcMeasureHypo') && $('PcMeasureHypo').checked && $('PcMeasureAch') && !$('PcMeasureAch').checked)
            $('PcMeasureAch').checked = true;
    }

    function syncVolumeMode() {
        var exact = $('PcVolumeExact') && $('PcVolumeExact').checked;
        var host = $('pcTypeCounts');
        if (host) host.style.opacity = exact ? '1' : '.55';
        if (host) host.querySelectorAll('input').forEach(function (el) { el.disabled = readOnly || !exact; });
        ['PcResMin', 'PcResMax'].forEach(function (id) {
            var el = $(id);
            if (el) el.disabled = readOnly || exact;
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
            if (host.dataset.withMode === 'true' && normalizeMode(host.dataset.mode) !== 'Inherit') return true;
        }
        return false;
    }

    function updateSectionBadges() {
        function setBadge(id, on) {
            var el = $(id);
            if (!el) return;
            el.textContent = on ? 'Customized' : 'Using story defaults';
            el.className = 'pc-section-badge ' + (on ? 'bg-primary text-white' : 'bg-light text-muted border');
        }
        setBadge('pcBadgeDemo', sectionHasValue(['PcGender', 'PcMinAge', 'PcMaxAge']));
        setBadge('pcBadgeEnc', sectionHasValue(['PcEncClass', 'PcEncStatus', 'PcDuration', 'PcDischarge', 'PcHospitalization']));
        setBadge('pcBadgeClinical', sectionHasValue(
            ['PcAllergy', 'PcLabs', 'PcHypoInsulin', 'PcConditionMeds', 'PcSpreadObs', 'PcAdditionalConditions'],
            ['PcPrimaryDxPicker', 'PcConditionPicker', 'PcObservationPicker', 'PcProcedurePicker', 'PcMedAdminPicker', 'PcServiceRequestPicker', 'PcSpecimenPicker', 'PcDiagnosticReportPicker']));
        var exact = $('PcVolumeExact') && $('PcVolumeExact').checked;
        var anyCount = false;
        document.querySelectorAll('.pc-type-count').forEach(function (el) { if (el.value !== '') anyCount = true; });
        setBadge('pcBadgeMix', exact || anyCount);
    }

    function resetSection(name) {
        if (name === 'demo') {
            setVal('PcGender', ''); setVal('PcMinAge', ''); setVal('PcMaxAge', '');
        } else if (name === 'enc') {
            setVal('PcEncClass', ''); setVal('PcEncStatus', ''); setVal('PcDuration', '');
            setVal('PcDischarge', ''); setVal('PcHospitalization', '');
        } else if (name === 'clinical') {
            ['PcAllergy', 'PcLabs', 'PcHypoInsulin', 'PcConditionMeds', 'PcSpreadObs', 'PcAdditionalConditions'].forEach(function (id) { setVal(id, ''); });
            ['PcPrimaryDxPicker', 'PcConditionPicker', 'PcObservationPicker', 'PcProcedurePicker', 'PcMedAdminPicker', 'PcServiceRequestPicker', 'PcSpecimenPicker', 'PcDiagnosticReportPicker'].forEach(function (id) {
                var host = $(id);
                if (!host) return;
                host.dataset.mode = 'Inherit';
                setSelectedCodes(host, []);
                renderPicker(host);
            });
        } else if (name === 'mix') {
            if ($('PcVolumeTotal')) $('PcVolumeTotal').checked = true;
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
        var conditionMode = condHost ? normalizeMode(condHost.dataset.mode) : 'Inherit';
        var observationMode = obsHost ? normalizeMode(obsHost.dataset.mode) : 'Inherit';
        var procedureMode = procHost ? normalizeMode(procHost.dataset.mode) : 'Inherit';
        var conditionPalette = paletteFromPicker(condHost, 'coded');
        var observationPalette = paletteFromPicker(obsHost, 'obs');
        var procedurePalette = paletteFromPicker(procHost, 'coded');
        if (conditionPalette && conditionMode === 'Inherit') conditionMode = 'Replace';
        if (observationPalette && observationMode === 'Inherit') observationMode = 'Replace';
        if (procedurePalette && procedureMode === 'Inherit') procedureMode = 'Replace';

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
            conditionPaletteMode: conditionMode,
            additionalConditionCount: parseIntOrNull('PcAdditionalConditions'),
            generateLabWork: readBool('PcLabs'),
            observationPalette: observationPalette,
            observationPaletteMode: observationMode,
            spreadObservationsAcrossEncounter: readBool('PcSpreadObs'),
            includeAllergy: readBool('PcAllergy'),
            includeConditionDrivenMedications: readBool('PcConditionMeds'),
            includeHypoglycemicInsulin: readBool('PcHypoInsulin'),
            resourceTypeCounts: anyCounts ? counts : null,
            procedurePalette: procedurePalette,
            procedurePaletteMode: procedureMode,
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
        var stories = Array.prototype.map.call(document.querySelectorAll('.pc-story:checked'), function (cb) { return cb.value; });
        var allStories = document.querySelectorAll('.pc-story').length;
        if (stories.length === allStories) stories = [];
        return {
            id: val('PcId') || (global.crypto && crypto.randomUUID ? crypto.randomUUID() : null),
            name: val('PcName'),
            description: val('PcDescription'),
            isSystem: val('PcIsSystem') === 'true',
            cohortQualification: val('PcQualification') || 'Qualifying',
            measureEligibilities: collectMeasureEligibilities(),
            clinicalScenarioIds: stories,
            resourcesPerPatientMin: parseInt(val('PcResMin') || '50', 10),
            resourcesPerPatientMax: parseInt(val('PcResMax') || '100', 10),
            intent: collectIntent()
        };
    }

    function populate(model) {
        model = model || {};
        setVal('PcId', model.id || '');
        setVal('PcIsSystem', model.isSystem ? 'true' : 'false');
        setVal('PcName', model.name || '');
        setVal('PcDescription', model.description || '');
        setVal('PcQualification', model.cohortQualification || 'Qualifying');
        setVal('PcResMin', model.resourcesPerPatientMin || 50);
        setVal('PcResMax', model.resourcesPerPatientMax || 100);
        var selectedStories = new Set(model.clinicalScenarioIds || []);
        document.querySelectorAll('.pc-story').forEach(function (cb) {
            cb.checked = selectedStories.size === 0 || selectedStories.has(cb.value);
        });
        populateMeasureEligibilities(model.measureEligibilities || {}, model.cohortQualification);
        var intent = model.intent || {};
        setVal('PcGender', intent.gender || '');
        setVal('PcMinAge', intent.minAge);
        setVal('PcMaxAge', intent.maxAge);
        setVal('PcEncClass', intent.encounterClass || '');
        setVal('PcEncStatus', intent.encounterStatus || '');
        setVal('PcDuration', intent.durationMinutes);
        setVal('PcDischarge', intent.dischargeDisposition || '');
        writeBool('PcHospitalization', intent.includeHospitalization);
        writeBool('PcAllergy', intent.includeAllergy);
        writeBool('PcLabs', intent.generateLabWork);
        writeBool('PcHypoInsulin', intent.includeHypoglycemicInsulin);
        writeBool('PcConditionMeds', intent.includeConditionDrivenMedications);
        writeBool('PcSpreadObs', intent.spreadObservationsAcrossEncounter);
        setVal('PcAdditionalConditions', intent.additionalConditionCount);
        loadPaletteIntoPicker($('PcPrimaryDxPicker'), intent.primaryConditionSnomed ? [{ code: intent.primaryConditionSnomed }] : [], 'code');
        var condHost = $('PcConditionPicker');
        if (condHost) condHost.dataset.mode = normalizeMode(intent.conditionPaletteMode);
        loadPaletteIntoPicker(condHost, intent.conditionPalette, 'code');
        var obsHost = $('PcObservationPicker');
        if (obsHost) obsHost.dataset.mode = normalizeMode(intent.observationPaletteMode);
        loadPaletteIntoPicker(obsHost, intent.observationPalette, 'loincCode');
        var procHost = $('PcProcedurePicker');
        if (procHost) procHost.dataset.mode = normalizeMode(intent.procedurePaletteMode);
        loadPaletteIntoPicker(procHost, intent.procedurePalette, 'code');
        loadPaletteIntoPicker($('PcMedAdminPicker'), intent.medicationAdministrationRxNorm ? [{ code: intent.medicationAdministrationRxNorm }] : [], 'code');
        loadPaletteIntoPicker($('PcServiceRequestPicker'), intent.serviceRequestLoinc ? [{ code: intent.serviceRequestLoinc }] : [], 'code');
        loadPaletteIntoPicker($('PcSpecimenPicker'), intent.specimenTypeCode ? [{ code: intent.specimenTypeCode }] : [], 'code');
        loadPaletteIntoPicker($('PcDiagnosticReportPicker'), intent.diagnosticReportLoinc ? [{ code: intent.diagnosticReportLoinc }] : [], 'code');
        var counts = intent.resourceTypeCounts || {};
        var anyCounts = Object.keys(counts).length > 0;
        if ($('PcVolumeExact')) $('PcVolumeExact').checked = anyCounts;
        if ($('PcVolumeTotal')) $('PcVolumeTotal').checked = !anyCounts;
        document.querySelectorAll('.pc-type-count').forEach(function (el) {
            el.value = counts[el.dataset.type] != null ? counts[el.dataset.type] : '';
        });
        syncVolumeMode();
        syncMeasureChecks();
        updateSectionBadges();
        setReadOnly(readOnly);
    }

    function setMode(next) {
        mode = next === 'cohort' ? 'cohort' : 'library';
        var library = document.querySelectorAll('.pc-library-only');
        var cohort = document.querySelectorAll('.pc-cohort-only');
        library.forEach(function (el) { el.style.display = mode === 'library' ? '' : 'none'; });
        cohort.forEach(function (el) { el.style.display = mode === 'cohort' ? '' : 'none'; });
    }

    function setBanner(text) {
        var el = $('pcCohortBannerText');
        if (el) el.textContent = text || '';
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
        syncMeasureChecks();
        syncVolumeMode();
    }

    function ensureCatalog() {
        if (catalog) return Promise.resolve(catalog);
        return fetch(options.catalogUrl).then(function (r) { return r.json(); }).then(function (c) {
            catalog = c;
            renderStories();
            renderTypeCounts();
            renderAllPickers();
            return catalog;
        });
    }

    function init(opts) {
        options = Object.assign({ catalogUrl: '/PatientConfigurations/Catalog' }, opts || {});
        var root = $('pcEditorRoot');
        if (!root || root.dataset.pcBound === 'true') return;
        root.dataset.pcBound = 'true';
        bindPickerEvents(root);
        root.addEventListener('change', function (e) {
            if (e.target.id === 'PcQualification') syncMeasureChecks();
            if (e.target.id === 'PcMeasureHypo' && e.target.checked && $('PcMeasureAch')) $('PcMeasureAch').checked = true;
            if (e.target.id === 'PcMeasureAch' && !e.target.checked && $('PcMeasureHypo')) $('PcMeasureHypo').checked = false;
            if (e.target.id === 'PcVolumeTotal' || e.target.id === 'PcVolumeExact') syncVolumeMode();
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
        setMode: setMode,
        setBanner: setBanner,
        setReadOnly: setReadOnly,
        updateSectionBadges: updateSectionBadges
    };
})(window);

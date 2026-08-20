(function (global) {
    function token() {
        const t = document.querySelector('input[name="__RequestVerificationToken"]');
        return t ? t.value : '';
    }

    async function postJson(url, body) {
        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token()
            },
            body: JSON.stringify(body || {})
        });
        const payload = await res.json().catch(() => ({}));
        if (!res.ok)
            throw new Error(payload.error || payload.Error || ('Server ' + res.status));
        return payload;
    }

    function esc(s) {
        return String(s ?? '').replace(/[&<>"']/g, ch => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[ch]));
    }

    function pick(obj, camel, pascal, fallback) {
        if (!obj) return fallback;
        if (obj[camel] !== undefined && obj[camel] !== null) return obj[camel];
        if (obj[pascal] !== undefined && obj[pascal] !== null) return obj[pascal];
        return fallback;
    }

    const OP_TYPE_LABELS = {
        CopyLocation: 'Copy identifiers',
        CopyProperty: 'Copy a field',
        CopyLocationAliasToTypeIteratively: 'Copy aliases',
        ConditionalTransform: 'Conditional update',
        CodeMap: 'Translate codes',
        RemoveExtensions: 'Remove extra fields'
    };

    function friendlyCondition(path) {
        const text = String(path || '');
        const identifier = text.match(/identifier\.where\(\s*system\s*=\s*'([^']+)'/i);
        if (identifier)
            return 'Has an identifier from ' + identifier[1];
        const typeAndCode = text.match(/type\.coding\.where\(\s*system\s*=\s*'([^']+)'\s+and\s+code\s*=\s*'([^']+)'/i);
        if (typeAndCode)
            return 'Has location type ' + typeAndCode[2];
        const typeOnly = text.match(/type\.coding\.where\(\s*system\s*=\s*'([^']+)'/i);
        if (typeOnly)
            return 'Has a location type from ' + typeOnly[1];
        return text;
    }

    function ensureModal() {
        let modal = document.getElementById('cfgGenModal');
        if (modal) return modal;
        modal = document.createElement('div');
        modal.id = 'cfgGenModal';
        modal.setAttribute('role', 'dialog');
        modal.setAttribute('aria-modal', 'true');
        modal.setAttribute('aria-labelledby', 'cfgGenTitle');
        modal.style.cssText = 'display:none;position:fixed;inset:0;z-index:1070;overflow-y:auto;';
        modal.innerHTML = `
            <div style="position:absolute;inset:0;background:rgba(0,0,0,.45);" data-cfggen-close></div>
            <div style="min-height:100%;display:flex;align-items:center;justify-content:center;padding:1.25rem;position:relative;">
                <div class="au-card au-cfggen-dialog">
                    <div class="card-header au-cfggen-header">
                        <div>
                            <span class="au-cfggen-kicker">From uploaded patients</span>
                            <div class="au-cfggen-title" id="cfgGenTitle">Suggest configuration</div>
                            <div class="au-cfggen-sub">Two choices: how locations are recognized, then how the data is cleaned.</div>
                        </div>
                        <button type="button" class="btn btn-sm btn-outline-light" data-cfggen-close>Close</button>
                    </div>
                    <div class="au-cfggen-steps" id="cfgGenSteps" hidden>
                        <button type="button" class="au-cfggen-step" data-goto="1"><span class="au-cfggen-step-num">1</span><span>What we found</span></button>
                        <button type="button" class="au-cfggen-step" data-goto="2"><span class="au-cfggen-step-num">2</span><span>Location matching</span></button>
                        <button type="button" class="au-cfggen-step" data-goto="3"><span class="au-cfggen-step-num">3</span><span>Data cleanup</span></button>
                    </div>
                    <div class="alert alert-danger au-cfggen-error py-2 small" id="cfgGenError"></div>
                    <div class="au-cfggen-body" id="cfgGenBody"></div>
                    <div class="au-cfggen-footer" id="cfgGenFooter" hidden></div>
                </div>
            </div>`;
        document.body.appendChild(modal);
        modal.querySelectorAll('[data-cfggen-close]').forEach(btn => {
            btn.addEventListener('click', () => { modal.style.display = 'none'; });
        });
        modal.querySelector('#cfgGenSteps').addEventListener('click', ev => {
            const btn = ev.target.closest('[data-goto]');
            if (btn && typeof modal._cfgGenGo === 'function')
                modal._cfgGenGo(Number(btn.getAttribute('data-goto')));
        });
        document.addEventListener('keydown', ev => {
            if (ev.key === 'Escape' && modal.style.display !== 'none')
                modal.style.display = 'none';
        });
        return modal;
    }

    function showError(message) {
        const box = document.getElementById('cfgGenError');
        if (!box) return;
        if (!message) {
            box.style.display = 'none';
            box.textContent = '';
            return;
        }
        box.textContent = message;
        box.style.display = 'block';
    }

    function showLoading(message) {
        const modal = ensureModal();
        document.getElementById('cfgGenSteps').hidden = true;
        document.getElementById('cfgGenFooter').hidden = true;
        showError('');
        document.getElementById('cfgGenBody').innerHTML = `
            <div class="au-cfggen-loading">
                <div class="spinner-border text-secondary mb-3" role="status" aria-hidden="true"></div>
                <div>${esc(message || 'Reading uploaded patients…')}</div>
            </div>`;
        modal.style.display = '';
    }

    function renderReuseCards(items, kindHint, emptyText) {
        if (!items || !items.length)
            return `<p class="small text-muted mb-3">${esc(emptyText)}</p>`;

        const ranked = items.slice().sort((a, b) =>
            (pick(b, 'score', 'Score', 0) - pick(a, 'score', 'Score', 0)));
        const [best, ...rest] = ranked;

        function card(item, recommended) {
            const rec = pick(item, 'recommendation', 'Recommendation', '');
            const isReuse = rec === 'Reuse';
            const isSystem = String(pick(item, 'kind', 'Kind', '')).toLowerCase().includes('system');
            const score = Math.round(100 * (pick(item, 'score', 'Score', 0)));
            const action = isReuse
                ? 'Use this one'
                : (isSystem ? 'Make a custom copy' : 'Add the missing pieces');
            return `
                <div class="au-cfggen-choice ${recommended ? 'is-recommended' : ''}">
                    <div class="au-cfggen-choice-head">
                        <div>
                            <div class="fw-semibold">${esc(pick(item, 'name', 'Name', ''))}</div>
                            <div class="small text-muted mt-1">${esc(pick(item, 'reason', 'Reason', ''))}</div>
                        </div>
                        <span class="badge ${isReuse ? 'au-badge-success' : 'au-badge-warning'}">${esc(isReuse ? 'Ready to use' : 'Needs a few additions')}</span>
                    </div>
                    <div class="au-cfggen-choice-actions d-flex align-items-center gap-2 flex-wrap">
                        <button type="button" class="btn btn-sm ${recommended && isReuse ? 'btn-au-action' : 'btn-outline-secondary'} cfggen-reuse"
                            data-kind="${esc(kindHint)}"
                            data-id="${esc(pick(item, 'id', 'Id', ''))}"
                            data-name="${esc(pick(item, 'name', 'Name', ''))}"
                            data-rec="${esc(rec)}"
                            data-system="${isSystem ? 'true' : 'false'}">${esc(action)}</button>
                        <span class="small text-muted">${score}% match</span>
                    </div>
                </div>`;
        }

        let html = card(best, true);
        if (rest.length) {
            html += `<details class="au-cfggen-details mb-3">
                <summary>Other existing options (${rest.length})</summary>
                <div class="mt-2">${rest.map(item => card(item, false)).join('')}</div>
            </details>`;
        }
        return html;
    }

    function renderProposal(proposal, urls, hooks) {
        const modal = ensureModal();
        const body = document.getElementById('cfgGenBody');
        const footer = document.getElementById('cfgGenFooter');
        const stepsEl = document.getElementById('cfgGenSteps');
        const fp = pick(proposal, 'fingerprint', 'Fingerprint', {});
        const orm = pick(proposal, 'orm', 'Orm', {});
        const norm = pick(proposal, 'normalization', 'Normalization', {});
        const summary = pick(proposal, 'summary', 'Summary', []);
        const ormNotes = pick(orm, 'notes', 'Notes', []);
        const normNotes = pick(norm, 'notes', 'Notes', []);
        const ormConds = pick(orm, 'conditions', 'Conditions', []);
        const ops = pick(norm, 'operations', 'Operations', []);
        const refinedOrmId = pick(proposal, 'refinedOrmId', 'RefinedOrmId', null);
        const refinedSuiteId = pick(proposal, 'refinedSuiteId', 'RefinedSuiteId', null);
        const combined = pick(proposal, 'combinedWithPrior', 'CombinedWithPrior', false);
        const namePrefix = String(hooks.namePrefix || '').trim();
        const defaultOrmName = pick(orm, 'suggestedName', 'SuggestedName', '')
            || (namePrefix ? namePrefix + ' location matching' : '');
        const defaultSuiteName = pick(norm, 'suggestedSuiteName', 'SuggestedSuiteName', '')
            || (namePrefix ? namePrefix + ' cleanup' : '');
        const patientCount = pick(fp, 'patientCount', 'PatientCount', 0);
        const locationCount = pick(fp, 'locationCount', 'LocationCount', 0);
        const identifiers = pick(fp, 'locationIdentifiers', 'LocationIdentifiers', []) || [];
        const extensions = pick(fp, 'extensions', 'Extensions', []) || [];

        let step = 1;
        let busy = false;

        function setBusy(on) {
            busy = on;
            modal.querySelectorAll('button, input').forEach(el => {
                if (el.hasAttribute('data-cfggen-close')) return;
                el.disabled = on;
            });
        }

        function selectedOps() {
            return Array.from(body.querySelectorAll('.cfggen-op-box'))
                .filter(cb => cb.checked)
                .map(cb => ops[Number(cb.getAttribute('data-idx'))])
                .filter(Boolean);
        }

        function resolveName(inputId, kind, fallback) {
            const input = document.getElementById(inputId);
            let name = input ? String(input.value || '').trim() : '';
            if (!name && namePrefix)
                name = `${namePrefix} ${kind}`.trim();
            if (!name && fallback)
                name = String(fallback).trim();
            return name;
        }

        async function saveOrm(updateExisting) {
            const name = resolveName('cfgGenOrmName', 'location matching', defaultOrmName);
            if (!name) {
                showError('Give this location map a name so you can find it later.');
                document.getElementById('cfgGenOrmName')?.focus();
                return;
            }
            showError('');
            const payload = {
                proposal: Object.assign({}, orm, {
                    suggestedName: name,
                    SuggestedName: name
                })
            };
            if (updateExisting && refinedOrmId)
                payload.updateExistingId = refinedOrmId;
            const saved = await postJson(urls.applyOrm, payload);
            hooks.onSavedOrm?.(saved.id || saved.Id, proposal);
            hooks.onSelectOrm?.(saved.id || saved.Id, saved.name || saved.Name);
            go(3);
        }

        async function saveNorm(updateExisting) {
            const name = resolveName('cfgGenSuiteName', 'cleanup', defaultSuiteName);
            if (!name) {
                showError('Give this cleanup suite a name so you can find it later.');
                document.getElementById('cfgGenSuiteName')?.focus();
                return;
            }
            showError('');
            const chosen = selectedOps();
            const payload = {
                proposal: Object.assign({}, norm, {
                    suggestedSuiteName: name,
                    SuggestedSuiteName: name,
                    suggestedSequenceName: name + ' sequence',
                    SuggestedSequenceName: name + ' sequence',
                    operations: chosen,
                    Operations: chosen
                })
            };
            if (updateExisting && refinedSuiteId)
                payload.updateExistingSuiteId = refinedSuiteId;
            const saved = await postJson(urls.applyNorm, payload);
            hooks.onSavedSuite?.(saved.id || saved.Id, proposal);
            hooks.onSelectSuite?.(saved.id || saved.Id, saved.name || saved.Name);
            modal.style.display = 'none';
        }

        function findingsHtml() {
            return `
                ${combined ? '<div class="alert alert-info py-2 small">Includes every patient analyzed in this session, not just the last upload.</div>' : ''}
                <p class="au-cfggen-intro">We looked at the uploaded patients and found the pieces needed to recognize hospital locations and clean the FHIR data.</p>
                <div class="au-cfggen-stats">
                    <div class="au-stat-tile"><div class="au-stat-value">${esc(patientCount)}</div><div class="au-stat-label">Patients</div></div>
                    <div class="au-stat-tile"><div class="au-stat-value">${esc(locationCount)}</div><div class="au-stat-label">Locations</div></div>
                    <div class="au-stat-tile"><div class="au-stat-value">${esc(identifiers.length)}</div><div class="au-stat-label">Identifier kinds</div></div>
                    <div class="au-stat-tile"><div class="au-stat-value">${esc(extensions.length)}</div><div class="au-stat-label">Extra fields</div></div>
                </div>
                ${summary.length ? `<ul class="small text-muted mb-0">${summary.map(s => `<li>${esc(s)}</li>`).join('')}</ul>` : ''}`;
        }

        function ormHtml() {
            const reuse = pick(orm, 'reuse', 'Reuse', []);
            return `
                <p class="au-cfggen-intro">The run needs to know which locations belong to this hospital. We can reuse a map that already covers this upload, or save a new one from the identifiers we found.</p>
                <h6 class="small text-uppercase text-muted">Use an existing map</h6>
                ${renderReuseCards(reuse, 'orm', 'None of your existing maps cover this upload well. Create a new one below.')}
                <h6 class="small text-uppercase text-muted mt-3">Or create a new map</h6>
                <div class="au-cfggen-choice">
                    <label class="form-label small mb-1" for="cfgGenOrmName">Name</label>
                    <input class="form-control mb-2" id="cfgGenOrmName" value="${esc(defaultOrmName)}" placeholder="e.g. Memorial Hospital locations" />
                    ${ormNotes.length ? `<ul class="small text-muted mb-2">${ormNotes.map(n => `<li>${esc(n)}</li>`).join('')}</ul>` : ''}
                    <div class="small mb-2">We will match locations that:</div>
                    <ul class="small mb-0">${ormConds.map(c => `<li>${esc(friendlyCondition(pick(c, 'fhirPath', 'FhirPath', '')))}</li>`).join('') || '<li class="text-muted">No location rules were found.</li>'}</ul>
                    <details class="au-cfggen-details">
                        <summary>Technical match rules</summary>
                        <pre class="au-cfggen-pre">${esc(ormConds.map(c => pick(c, 'fhirPath', 'FhirPath', '')).join('\n')) || '(none)'}</pre>
                    </details>
                    <div class="au-cfggen-choice-actions d-flex flex-wrap gap-2">
                        <button type="button" class="btn btn-sm btn-au-action" id="cfgGenSaveOrm" ${ormConds.length ? '' : 'disabled'}>
                            ${refinedOrmId ? 'Update this map' : 'Save and use this map'}
                        </button>
                        ${refinedOrmId ? '<button type="button" class="btn btn-sm btn-outline-secondary" id="cfgGenSaveOrmNew">Save as a new map</button>' : ''}
                    </div>
                </div>`;
        }

        function normHtml() {
            const reuse = pick(norm, 'reuse', 'Reuse', []);
            const opRows = ops.map((op, idx) => {
                const type = pick(op, 'operationType', 'OperationType', '');
                const name = pick(op, 'suggestedName', 'SuggestedName', '');
                const desc = pick(op, 'suggestedDescription', 'SuggestedDescription', '');
                const reuseName = pick(op, 'reuseOperationName', 'ReuseOperationName', '');
                return `<label class="au-cfggen-op">
                    <input type="checkbox" class="form-check-input mt-1 cfggen-op-box" data-idx="${idx}" checked />
                    <span>
                        <span class="au-cfggen-op-type">${esc(OP_TYPE_LABELS[type] || type)}</span>
                        <div class="fw-semibold">${esc(name)}</div>
                        ${desc ? `<div class="small text-muted">${esc(desc)}</div>` : ''}
                        ${reuseName ? `<div class="small text-muted">Reuses ${esc(reuseName)}</div>` : ''}
                    </span>
                </label>`;
            }).join('');

            return `
                <p class="au-cfggen-intro">Cleanup operations reshape the uploaded FHIR so later pipeline steps can use it. Reuse a suite that already covers this data, or save the operations we found.</p>
                <h6 class="small text-uppercase text-muted">Use an existing suite</h6>
                ${renderReuseCards(reuse, 'suite', 'No existing suite covers the cleanup this upload needs. Create one below.')}
                <h6 class="small text-uppercase text-muted mt-3">Or create / extend a suite</h6>
                <div class="au-cfggen-choice">
                    <label class="form-label small mb-1" for="cfgGenSuiteName">Name</label>
                    <input class="form-control mb-2" id="cfgGenSuiteName" value="${esc(defaultSuiteName)}" placeholder="e.g. Memorial Hospital cleanup" />
                    ${normNotes.length ? `<ul class="small text-muted mb-2">${normNotes.map(n => `<li>${esc(n)}</li>`).join('')}</ul>` : ''}
                    <div class="small text-muted mb-1">${ops.length ? 'Include these operations' : 'Nothing new to add — an existing suite already covers this upload.'}</div>
                    ${opRows || ''}
                    <div class="au-cfggen-choice-actions d-flex flex-wrap gap-2 mt-2">
                        <button type="button" class="btn btn-sm btn-au-action" id="cfgGenSaveNorm" ${ops.length || refinedSuiteId ? '' : 'disabled'}>
                            ${refinedSuiteId ? 'Update this suite' : 'Save and use this suite'}
                        </button>
                        ${refinedSuiteId ? '<button type="button" class="btn btn-sm btn-outline-secondary" id="cfgGenSaveNormNew">Save as a new suite</button>' : ''}
                    </div>
                </div>`;
        }

        function footerHtml() {
            if (step === 1)
                return `<span></span><button type="button" class="btn btn-au-action" data-cfggen-next>Continue</button>`;
            if (step === 2)
                return `<button type="button" class="btn btn-outline-secondary" data-cfggen-back>Back</button>
                    <button type="button" class="btn btn-outline-secondary" data-cfggen-skip>Skip for now</button>`;
            return `<button type="button" class="btn btn-outline-secondary" data-cfggen-back>Back</button>
                <button type="button" class="btn btn-outline-secondary" data-cfggen-done>I'm done</button>`;
        }

        function paint() {
            showError('');
            stepsEl.hidden = false;
            footer.hidden = false;
            stepsEl.querySelectorAll('.au-cfggen-step').forEach(el => {
                const n = Number(el.getAttribute('data-goto'));
                el.classList.toggle('is-active', n === step);
                el.classList.toggle('is-done', n < step);
                if (n === step)
                    el.setAttribute('aria-current', 'step');
                else
                    el.removeAttribute('aria-current');
            });
            body.innerHTML = step === 1 ? findingsHtml() : (step === 2 ? ormHtml() : normHtml());
            footer.innerHTML = footerHtml();
            bindStep();
        }

        function bindStep() {
            footer.querySelector('[data-cfggen-next]')?.addEventListener('click', () => go(step + 1));
            footer.querySelector('[data-cfggen-back]')?.addEventListener('click', () => go(step - 1));
            footer.querySelector('[data-cfggen-skip]')?.addEventListener('click', () => go(3));
            footer.querySelector('[data-cfggen-done]')?.addEventListener('click', () => { modal.style.display = 'none'; });

            const saveOrmBtn = document.getElementById('cfgGenSaveOrm');
            if (saveOrmBtn) {
                saveOrmBtn.addEventListener('click', async () => {
                    try {
                        setBusy(true);
                        await saveOrm(!!refinedOrmId);
                    } catch (err) {
                        showError(err.message);
                    } finally {
                        setBusy(false);
                    }
                });
            }
            const saveOrmNew = document.getElementById('cfgGenSaveOrmNew');
            if (saveOrmNew) {
                saveOrmNew.addEventListener('click', async () => {
                    try {
                        setBusy(true);
                        await saveOrm(false);
                    } catch (err) {
                        showError(err.message);
                    } finally {
                        setBusy(false);
                    }
                });
            }
            const saveNormBtn = document.getElementById('cfgGenSaveNorm');
            if (saveNormBtn) {
                saveNormBtn.addEventListener('click', async () => {
                    try {
                        setBusy(true);
                        await saveNorm(!!refinedSuiteId);
                    } catch (err) {
                        showError(err.message);
                    } finally {
                        setBusy(false);
                    }
                });
            }
            const saveNormNew = document.getElementById('cfgGenSaveNormNew');
            if (saveNormNew) {
                saveNormNew.addEventListener('click', async () => {
                    try {
                        setBusy(true);
                        await saveNorm(false);
                    } catch (err) {
                        showError(err.message);
                    } finally {
                        setBusy(false);
                    }
                });
            }

            body.querySelectorAll('.cfggen-reuse').forEach(btn => {
                btn.addEventListener('click', async () => {
                    if (busy) return;
                    const id = btn.getAttribute('data-id');
                    const name = btn.getAttribute('data-name');
                    const rec = btn.getAttribute('data-rec');
                    const kind = (btn.getAttribute('data-kind') || '').toLowerCase();
                    try {
                        setBusy(true);
                        if (kind.includes('orm')) {
                            if (rec === 'Extend')
                                await hooks.onExtendOrm?.(id, proposal);
                            else
                                hooks.onSelectOrm?.(id, name);
                            go(3);
                        } else {
                            if (rec === 'Extend')
                                await hooks.onExtendSuite?.(id, proposal);
                            else
                                hooks.onSelectSuite?.(id, name);
                            modal.style.display = 'none';
                        }
                    } catch (err) {
                        showError(err.message || 'Could not apply that choice.');
                    } finally {
                        setBusy(false);
                    }
                });
            });
        }

        function go(next) {
            step = Math.min(3, Math.max(1, next));
            paint();
        }

        modal._cfgGenGo = go;
        paint();
        modal.style.display = '';
        return proposal;
    }

    async function analyzeAndShow(urls, request, hooks) {
        showLoading('Reading uploaded patients…');
        try {
            const proposal = await postJson(urls.analyze, request);
            renderProposal(proposal, urls, hooks || {});
            return proposal;
        } catch (err) {
            showError(err.message || 'Could not analyze the uploaded patients.');
            document.getElementById('cfgGenBody').innerHTML = `
                <p class="au-cfggen-intro mb-0">We could not build a suggestion from this upload. Close this window, check the patient data, and try again.</p>`;
            document.getElementById('cfgGenFooter').hidden = true;
            throw err;
        }
    }

    async function readFilesAsSources(fileList) {
        const files = Array.from(fileList || []).filter(Boolean);
        const sources = [];
        for (const file of files) {
            sources.push({ source: 'Bundle', bundleJson: await file.text() });
        }
        return sources;
    }

    global.BundleConfigGeneration = {
        analyzeAndShow,
        postJson,
        readFilesAsSources
    };
})(window);

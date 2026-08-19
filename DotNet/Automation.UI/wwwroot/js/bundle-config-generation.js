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

    function ensureModal() {
        let modal = document.getElementById('cfgGenModal');
        if (modal) return modal;
        modal = document.createElement('div');
        modal.id = 'cfgGenModal';
        modal.style.cssText = 'display:none;position:fixed;inset:0;z-index:1070;overflow-y:auto;';
        modal.innerHTML = `
            <div style="position:absolute;inset:0;background:rgba(0,0,0,.45);" data-cfggen-close></div>
            <div style="min-height:100%;display:flex;align-items:center;justify-content:center;padding:2rem;position:relative;">
                <div class="au-card" style="width:100%;max-width:920px;margin:0;max-height:90vh;display:flex;flex-direction:column;">
                    <div class="card-header d-flex justify-content-between align-items-center">
                        <span><i class="bi bi-magic me-2"></i>Suggest from patient data</span>
                        <button type="button" class="btn btn-sm btn-outline-light" data-cfggen-close>Close</button>
                    </div>
                    <div class="card-body" id="cfgGenBody" style="overflow:auto;"></div>
                </div>
            </div>`;
        document.body.appendChild(modal);
        modal.querySelectorAll('[data-cfggen-close]').forEach(btn => {
            btn.addEventListener('click', () => { modal.style.display = 'none'; });
        });
        return modal;
    }

    function reuseBlock(title, items, kindHint) {
        if (!items || !items.length)
            return `<div class="mb-3"><h6 class="text-uppercase text-muted small">${esc(title)}</h6><div class="text-muted small">No existing template covers this upload well enough to reuse. A new one will be drafted below.</div></div>`;
        return `<div class="mb-3"><h6 class="text-uppercase text-muted small">${esc(title)}</h6>` +
            items.map(item => {
                const rec = pick(item, 'recommendation', 'Recommendation', '');
                const isReuse = rec === 'Reuse';
                const isSystem = String(pick(item, 'kind', 'Kind', '')).toLowerCase().includes('system');
                return `
                <div class="border rounded p-2 mb-2">
                    <div class="d-flex justify-content-between gap-2">
                        <div>
                            <strong>${esc(pick(item, 'name', 'Name', ''))}</strong>
                            <span class="badge au-badge-muted ms-1">${esc(pick(item, 'kind', 'Kind', ''))}</span>
                            <span class="badge ${isReuse ? 'au-badge-success' : 'au-badge-warning'} ms-1">${esc(rec)}</span>
                        </div>
                        <span class="small text-muted">${Math.round(100 * (pick(item, 'score', 'Score', 0)))}%</span>
                    </div>
                    <div class="small text-muted mt-1">${esc(pick(item, 'reason', 'Reason', ''))}</div>
                    <div class="mt-2">
                        <button type="button" class="btn btn-sm ${isReuse ? 'btn-success' : 'btn-outline-secondary'} cfggen-reuse"
                            data-kind="${esc(kindHint)}"
                            data-id="${esc(pick(item, 'id', 'Id', ''))}"
                            data-name="${esc(pick(item, 'name', 'Name', ''))}"
                            data-rec="${esc(rec)}"
                            data-system="${isSystem ? 'true' : 'false'}">
                            ${isReuse ? 'Use this existing one' : (isSystem ? 'Clone and extend into a custom copy' : 'Extend this with new matches')}
                        </button>
                    </div>
                </div>`;
            }).join('') + '</div>';
    }

    function renderProposal(proposal, urls, hooks) {
        const modal = ensureModal();
        const body = document.getElementById('cfgGenBody');
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
        const selectOrmLabel = hooks.selectOrmLabel || 'Save new ORM and select it';
        const selectSuiteLabel = hooks.selectSuiteLabel || 'Save new suite and select it';
        const updateOrmLabel = refinedOrmId ? 'Update the map we are refining' : selectOrmLabel;
        const updateSuiteLabel = refinedSuiteId ? 'Update the suite we are refining' : selectSuiteLabel;
        const namePrefix = String(hooks.namePrefix || '').trim();
        const defaultOrmName = pick(orm, 'suggestedName', 'SuggestedName', '')
            || (namePrefix ? namePrefix + ' ORM' : '');
        const defaultSuiteName = pick(norm, 'suggestedSuiteName', 'SuggestedSuiteName', '')
            || (namePrefix ? namePrefix + ' Normalization' : '');

        body.innerHTML = `
            ${combined ? '<div class="alert alert-info py-2 small">This proposal includes previously analyzed patients in this session so every upload can pass.</div>' : ''}
            <div class="small text-muted mb-3">${summary.map(esc).join('<br/>')}</div>
            ${reuseBlock('Reuse an existing Organization Resource Map', pick(orm, 'reuse', 'Reuse', []), 'orm')}
            ${reuseBlock('Reuse an existing Normalization Suite', pick(norm, 'reuse', 'Reuse', []), 'suite')}
            <div class="mb-4">
                <h6 class="text-uppercase text-muted small">New / extended ORM</h6>
                <label class="form-label small mb-1" for="cfgGenOrmName">Name</label>
                <input class="form-control form-control-sm mb-2" id="cfgGenOrmName" value="${esc(defaultOrmName)}" placeholder="Required — e.g. My Scenario ORM" />
                <ul class="small mb-2">${ormNotes.map(n => `<li>${esc(n)}</li>`).join('') || '<li class="text-muted">No ORM notes.</li>'}</ul>
                <pre class="small bg-light border rounded p-2" style="max-height:8rem;overflow:auto;">${esc(ormConds.map(c => pick(c, 'fhirPath', 'FhirPath', '')).join('\n')) || '(no conditions)'}</pre>
                <div class="d-flex flex-wrap gap-2">
                    <button type="button" class="btn btn-sm btn-au-action" id="cfgGenSaveOrm" ${ormConds.length ? '' : 'disabled'}>${esc(updateOrmLabel)}</button>
                    ${refinedOrmId ? `<button type="button" class="btn btn-sm btn-outline-secondary" id="cfgGenSaveOrmNew">${esc(selectOrmLabel)}</button>` : ''}
                </div>
            </div>
            <div>
                <h6 class="text-uppercase text-muted small">New / extended Normalization Suite</h6>
                <label class="form-label small mb-1" for="cfgGenSuiteName">Suite name</label>
                <input class="form-control form-control-sm mb-2" id="cfgGenSuiteName" value="${esc(defaultSuiteName)}" placeholder="Required — e.g. My Scenario Normalization" />
                <ul class="small mb-2">${normNotes.map(n => `<li>${esc(n)}</li>`).join('') || '<li class="text-muted">No normalization notes.</li>'}</ul>
                <div class="small mb-2">
                    ${ops.map((op, idx) => {
                        const type = pick(op, 'operationType', 'OperationType', '');
                        const name = pick(op, 'suggestedName', 'SuggestedName', '');
                        const reuseName = pick(op, 'reuseOperationName', 'ReuseOperationName', '');
                        return `<label class="d-flex align-items-start gap-2 mb-1">
                            <input type="checkbox" class="form-check-input mt-1 cfggen-op" data-idx="${idx}" checked />
                            <span><strong>${esc(type)}</strong> — ${esc(name)}${reuseName ? ' <span class="text-muted">(reuses ' + esc(reuseName) + ')</span>' : ''}</span>
                        </label>`;
                    }).join('') || '<div class="text-muted">No new operations to add.</div>'}
                </div>
                <div class="d-flex flex-wrap gap-2">
                    <button type="button" class="btn btn-sm btn-au-action" id="cfgGenSaveNorm" ${ops.length || refinedSuiteId ? '' : 'disabled'}>${esc(updateSuiteLabel)}</button>
                    ${refinedSuiteId ? `<button type="button" class="btn btn-sm btn-outline-secondary" id="cfgGenSaveNormNew">${esc(selectSuiteLabel)}</button>` : ''}
                </div>
            </div>`;

        body.querySelectorAll('.cfggen-reuse').forEach(btn => {
            btn.addEventListener('click', () => {
                const id = btn.getAttribute('data-id');
                const name = btn.getAttribute('data-name');
                const rec = btn.getAttribute('data-rec');
                const kind = (btn.getAttribute('data-kind') || '').toLowerCase();
                if (kind.includes('orm')) {
                    if (rec === 'Extend')
                        hooks.onExtendOrm?.(id, proposal);
                    else
                        hooks.onSelectOrm?.(id, name);
                } else if (rec === 'Extend') {
                    hooks.onExtendSuite?.(id, proposal);
                } else {
                    hooks.onSelectSuite?.(id, name);
                }
                modal.style.display = 'none';
            });
        });

        function selectedOps() {
            const chosen = [];
            body.querySelectorAll('.cfggen-op').forEach(cb => {
                if (cb.checked)
                    chosen.push(ops[Number(cb.getAttribute('data-idx'))]);
            });
            return chosen;
        }

        function resolveName(inputId, kind, fallback) {
            const input = document.getElementById(inputId);
            let name = input ? String(input.value || '').trim() : '';
            if (!name && namePrefix)
                name = `${namePrefix} ${kind}`.trim();
            if (!name && fallback)
                name = String(fallback).trim();
            if (!name) {
                name = String(window.prompt(`Name this ${kind}. This is required so generated maps stay identifiable.`) || '').trim();
                if (input) input.value = name;
            }
            return name;
        }

        async function saveOrm(updateExisting) {
            const name = resolveName('cfgGenOrmName', 'ORM', defaultOrmName);
            if (!name) {
                alert('An Organization Resource Map name is required.');
                return;
            }
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
            modal.style.display = 'none';
        }

        async function saveNorm(updateExisting) {
            const name = resolveName('cfgGenSuiteName', 'Normalization suite', defaultSuiteName);
            if (!name) {
                alert('A Normalization Suite name is required.');
                return;
            }
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

        const saveOrmBtn = document.getElementById('cfgGenSaveOrm');
        if (saveOrmBtn) {
            saveOrmBtn.addEventListener('click', async () => {
                try {
                    saveOrmBtn.disabled = true;
                    await saveOrm(!!refinedOrmId);
                } catch (err) {
                    alert(err.message);
                } finally {
                    saveOrmBtn.disabled = false;
                }
            });
        }

        const saveOrmNew = document.getElementById('cfgGenSaveOrmNew');
        if (saveOrmNew) {
            saveOrmNew.addEventListener('click', async () => {
                try {
                    saveOrmNew.disabled = true;
                    await saveOrm(false);
                } catch (err) {
                    alert(err.message);
                } finally {
                    saveOrmNew.disabled = false;
                }
            });
        }

        const saveNormBtn = document.getElementById('cfgGenSaveNorm');
        if (saveNormBtn) {
            saveNormBtn.addEventListener('click', async () => {
                try {
                    saveNormBtn.disabled = true;
                    await saveNorm(!!refinedSuiteId);
                } catch (err) {
                    alert(err.message);
                } finally {
                    saveNormBtn.disabled = false;
                }
            });
        }

        const saveNormNew = document.getElementById('cfgGenSaveNormNew');
        if (saveNormNew) {
            saveNormNew.addEventListener('click', async () => {
                try {
                    saveNormNew.disabled = true;
                    await saveNorm(false);
                } catch (err) {
                    alert(err.message);
                } finally {
                    saveNormNew.disabled = false;
                }
            });
        }

        modal.style.display = '';
        return proposal;
    }

    async function analyzeAndShow(urls, request, hooks) {
        const proposal = await postJson(urls.analyze, request);
        renderProposal(proposal, urls, hooks || {});
        return proposal;
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

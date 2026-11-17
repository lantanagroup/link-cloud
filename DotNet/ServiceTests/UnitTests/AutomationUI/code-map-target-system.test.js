const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const {
    hslocTargetSystemUrl,
    applyTargetSystemField,
    lockTargetSystemInput,
    targetSystemDraft,
    targetSystemForBuild,
    normalizeCodeSystemMapsForSave,
    targetSystemForSave
} = require(path.join(
    __dirname,
    '../../../Automation.UI/wwwroot/js/code-map-target-system.js'));

function fakeInput(value) {
    const attrs = new Map();
    return {
        value,
        readOnly: false,
        disabled: false,
        dataset: {},
        setAttribute(name, attrValue) { attrs.set(name, attrValue); },
        removeAttribute(name) { attrs.delete(name); },
        getAttribute(name) { return attrs.has(name) ? attrs.get(name) : null; }
    };
}

test('editing an HSLOC map shows Target System read-only at the NHSN URL', () => {
    const input = fakeInput('http://example.org/old');
    const state = applyTargetSystemField(input, 'HSLOCMap', 'http://example.org/old');

    assert.equal(state.readOnly, true);
    assert.equal(input.readOnly, true);
    assert.equal(input.disabled, true);
    assert.equal(input.getAttribute('aria-readonly'), 'true');
    assert.equal(input.value, hslocTargetSystemUrl);
});

test('saving an HSLOC map does not change Target System', () => {
    const saved = normalizeCodeSystemMapsForSave('HSLOCMap', [
        {
            sourceSystem: 'urn:local',
            targetSystem: 'http://example.org/changed',
            codeMaps: { ICU: { code: '1025-6', display: 'Trauma Critical Care' } }
        }
    ]);

    assert.equal(saved[0].targetSystem, hslocTargetSystemUrl);
    assert.equal(saved[0].sourceSystem, 'urn:local');
    assert.equal(targetSystemForSave('HSLOCMap', ''), hslocTargetSystemUrl);
});

test('creating an HSLOC map starts locked to the NHSN URL', () => {
    const input = fakeInput('');
    applyTargetSystemField(input, 'HSLOCMap', '');

    assert.equal(input.readOnly, true);
    assert.equal(input.disabled, true);
    assert.equal(input.value, hslocTargetSystemUrl);
});

test('creating and editing a CodeMap still accepts the entered Target System', () => {
    const created = fakeInput('');
    applyTargetSystemField(created, 'CodeMap', '');
    assert.equal(created.readOnly, false);
    assert.equal(created.disabled, false);
    assert.equal(created.value, '');
    assert.equal(created.getAttribute('aria-readonly'), null);

    const edited = fakeInput('http://example.org/old');
    applyTargetSystemField(edited, 'CodeMap', 'http://example.org/codes');
    assert.equal(edited.readOnly, false);
    assert.equal(edited.disabled, false);
    assert.equal(edited.value, 'http://example.org/codes');

    const saved = normalizeCodeSystemMapsForSave('CodeMap', [
        { sourceSystem: 'urn:local', targetSystem: '  http://example.org/codes  ' }
    ]);
    assert.equal(saved[0].targetSystem, 'http://example.org/codes');
});

test('switching a CodeMap to HSLOC and back keeps the draft target', () => {
    const input = fakeInput('http://example.org/codes');

    lockTargetSystemInput(input, 'HSLOCMap');
    assert.equal(input.readOnly, true);
    assert.equal(input.disabled, true);
    assert.equal(input.value, hslocTargetSystemUrl);
    assert.equal(targetSystemDraft(input), 'http://example.org/codes');

    lockTargetSystemInput(input, 'HSLOCMap');
    assert.equal(targetSystemDraft(input), 'http://example.org/codes');

    lockTargetSystemInput(input, 'CodeMap');
    assert.equal(input.readOnly, false);
    assert.equal(input.disabled, false);
    assert.equal(input.value, 'http://example.org/codes');
    assert.equal(input.dataset.codeMapTarget, undefined);

    const savedWhileHsloc = normalizeCodeSystemMapsForSave('HSLOCMap', [
        { sourceSystem: 'urn:local', targetSystem: 'http://example.org/codes' }
    ]);
    assert.equal(savedWhileHsloc[0].targetSystem, hslocTargetSystemUrl);
});

test('an empty Code Map target switched to HSLOC Map and back restores empty', () => {
    const input = fakeInput('');

    lockTargetSystemInput(input, 'HSLOCMap');
    assert.equal(input.value, hslocTargetSystemUrl);
    assert.equal(targetSystemDraft(input), '');

    lockTargetSystemInput(input, 'CodeMap');
    assert.equal(input.value, '');
    assert.equal(input.readOnly, false);
    assert.equal(input.disabled, false);
    assert.equal(input.dataset.codeMapTarget, undefined);
});

test('an HSLOC Map card with an empty prior target builds and saves the HSLOC URL', () => {
    const input = fakeInput('');
    lockTargetSystemInput(input, 'HSLOCMap');

    assert.equal(targetSystemDraft(input), '');
    assert.equal(targetSystemForBuild('HSLOCMap', input), hslocTargetSystemUrl);

    const saved = normalizeCodeSystemMapsForSave('HSLOCMap', [
        {
            sourceSystem: 'urn:local',
            targetSystem: targetSystemForBuild('HSLOCMap', input)
        }
    ]);
    assert.equal(saved[0].targetSystem, hslocTargetSystemUrl);

    const codeMap = fakeInput('');
    assert.equal(targetSystemForBuild('CodeMap', codeMap), '');
});

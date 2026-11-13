const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { parseTestRailCaseId } = require(path.join(
    __dirname,
    '../../../Automation.UI/wwwroot/js/testrail-case-id.js'));

test('accepts a positive integer and a C prefix', () => {
    assert.deepEqual(parseTestRailCaseId('123'), { ok: true, id: 123 });
    assert.deepEqual(parseTestRailCaseId('C123'), { ok: true, id: 123 });
    assert.deepEqual(parseTestRailCaseId('c123'), { ok: true, id: 123 });
    assert.deepEqual(parseTestRailCaseId(' c0007 '), { ok: true, id: 7 });
    assert.deepEqual(parseTestRailCaseId(123), { ok: true, id: 123 });
});

test('blank is optional', () => {
    assert.deepEqual(parseTestRailCaseId(''), { ok: true, id: null });
    assert.deepEqual(parseTestRailCaseId('   '), { ok: true, id: null });
    assert.deepEqual(parseTestRailCaseId(null), { ok: true, id: null });
    assert.deepEqual(parseTestRailCaseId(undefined), { ok: true, id: null });
});

test('rejects zero, negative, and junk', () => {
    for (const value of ['0', 'C0', '00', '-1', 'C-1', 'abc', 'C', '12.5', 'C123x', '2147483648', ' 12 3 ']) {
        const parsed = parseTestRailCaseId(value);
        assert.equal(parsed.ok, false, String(value));
        assert.equal(parsed.id, null, String(value));
    }
});

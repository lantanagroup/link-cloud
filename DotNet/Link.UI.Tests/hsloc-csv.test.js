const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const {
    hasHslocHeader,
    readHeaderSample,
    createHslocFileChecker,
    formatError,
    emptyFileError,
    unreadableError,
    headerSampleBytes
} = require(path.join(__dirname, '../Link.UI/wwwroot/js/hsloc-csv.js'));

const goodHeader = 'CDCCode,ShortDescription,HSLOCCode,LongDescription';
const cdcDownload = 'Concept Code,Concept Name,Code System\n1027-2,Medical Critical Care,HSLOC';

function record() {
    const state = { errors: [], checking: [], clears: 0, lastError: undefined };
    state.hooks = {
        onError(message) {
            state.errors.push(message);
            state.lastError = message;
        },
        onChecking(checking) {
            state.checking.push(checking);
        },
        onClear() {
            state.clears += 1;
        }
    };
    return state;
}

test('accepts a formatted CSV with headers in any order and case', async () => {
    const text = '\uFEFF"hsloccode", longdescription,CDCCODE,ShortDescription\r\n1027-2,Medical,IN:ACUTE:CC:M,Medical';
    assert.equal(hasHslocHeader(text), true);
    const seen = record();
    await createHslocFileChecker(seen.hooks).select(new File([text], 'hsloc.csv'));
    assert.equal(seen.lastError, '');
    assert.equal(seen.clears, 0);
    assert.equal(seen.checking.includes(true), true);
    assert.equal(seen.checking.at(-1), false);
});

test('rejects the unformatted CDC download with a format explanation', async () => {
    assert.equal(hasHslocHeader(cdcDownload), false);
    const seen = record();
    await createHslocFileChecker(seen.hooks).select(new File([cdcDownload], 'hsloc-cdc.csv'));
    assert.equal(seen.lastError, formatError);
    assert.match(formatError, /must be reformatted/);
    assert.match(formatError, /CDCCode, ShortDescription, HSLOCCode, LongDescription/);
    assert.equal(seen.clears, 1);
    assert.equal(seen.checking.at(-1), false);
});

test('validates only the header row', () => {
    assert.equal(hasHslocHeader(goodHeader), true);
    assert.equal(hasHslocHeader(goodHeader + '\nnot,a,valid,row'), true);
    assert.equal(hasHslocHeader('Concept Code,Concept Name,Code System\n' + goodHeader), false);
    assert.equal(hasHslocHeader('CDCCode,ShortDescription,HSLOCCode'), false);
    assert.equal(hasHslocHeader(goodHeader + ',Extra'), false);
    assert.equal(hasHslocHeader('CDCCode,CDCCode,HSLOCCode,LongDescription'), false);
    assert.equal(hasHslocHeader(''), false);
});

test('rejects empty files and non-CSV files', async () => {
    for (const file of [new File([], 'empty.csv'), new File(['invalid'], 'invalid.txt')]) {
        const seen = record();
        await createHslocFileChecker(seen.hooks).select(file);
        assert.equal(seen.lastError, emptyFileError);
        assert.equal(emptyFileError, 'Select a non-empty CSV file.');
        assert.equal(seen.clears, 1);
        assert.equal(seen.checking.includes(true), false);
    }
});

test('reports a file that cannot be read', async () => {
    const file = {
        name: 'hsloc.csv',
        size: 12,
        slice() {
            return { text() { return Promise.reject(new Error('denied')); } };
        }
    };
    const seen = record();
    await createHslocFileChecker(seen.hooks).select(file);
    assert.equal(seen.lastError, unreadableError);
    assert.equal(unreadableError, 'Unable to read the selected file.');
    assert.equal(seen.clears, 1);
    assert.equal(seen.checking.at(-1), false);
});

test('reads only the first 64 KiB when checking the header', async () => {
    assert.equal(headerSampleBytes, 64 * 1024);
    const payload = 'A'.repeat(headerSampleBytes + 1024);
    const sample = await readHeaderSample(new File([payload], 'big.csv'));
    assert.equal(sample.length, headerSampleBytes);
    assert.equal(sample, payload.slice(0, headerSampleBytes));

    const header = goodHeader + '\nIN:ACUTE:CC:M,Medical Critical Care,1027-2,Medical Critical Care\n';
    const seen = record();
    await createHslocFileChecker(seen.hooks).select(new File([header + 'x'.repeat(80 * 1024)], 'hsloc.csv'));
    assert.equal(seen.lastError, '');
    assert.equal(seen.clears, 0);
});

test('a newer selection wins over an in-flight read', async () => {
    let release;
    const gate = new Promise((resolve) => { release = resolve; });
    const slow = {
        name: 'slow.csv',
        size: 20,
        slice() {
            return { text: () => gate.then(() => goodHeader + '\n') };
        }
    };
    const seen = record();
    const checker = createHslocFileChecker(seen.hooks);
    const first = checker.select(slow);
    await checker.select(new File([cdcDownload], 'cdc.csv'));
    release();
    await first;
    assert.equal(seen.lastError, formatError);
    assert.equal(seen.clears, 1);
    assert.equal(seen.checking.at(-1), false);
});

// Browser check for the HSLOC Replace form. The normalization service still parses the CSV.
// Only the first 64 KiB is read, so a large file is not loaded just to inspect the header.
(function (root, factory) {
    var api = factory();
    if (typeof module === 'object' && module.exports)
        module.exports = api;
    if (typeof document !== 'undefined')
        api.install(document);
    root.hslocCsv = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
    var headerSampleBytes = 64 * 1024;
    var columnNames = ['CDCCode', 'ShortDescription', 'HSLOCCode', 'LongDescription'];
    var emptyFileError = 'Select a non-empty CSV file.';
    var unreadableError = 'Unable to read the selected file.';
    var formatError = 'This file is not in the required HSLOC format. The CSV downloaded from the CDC website ' +
        'must be reformatted before upload so its header row contains exactly these columns: ' +
        columnNames.join(', ') + '.';

    function hasHslocHeader(text) {
        var firstLine = String(text == null ? '' : text).replace(/^\uFEFF/, '').split(/\r?\n/, 1)[0] || '';
        var headers = firstLine.split(',').map(function (header) {
            return header.trim().replace(/^"(.*)"$/, '$1').trim().toLowerCase();
        });
        var required = columnNames.map(function (name) { return name.toLowerCase(); });
        var seen = {};
        for (var i = 0; i < headers.length; i++) {
            if (seen[headers[i]]) return false;
            seen[headers[i]] = true;
        }
        if (headers.length !== required.length) return false;
        for (var n = 0; n < required.length; n++) {
            if (headers.indexOf(required[n]) < 0) return false;
        }
        return true;
    }

    function quickFileError(file) {
        var name = file && file.name ? String(file.name) : '';
        var size = file && typeof file.size === 'number' ? file.size : 0;
        if (!name.toLowerCase().endsWith('.csv') || size === 0)
            return emptyFileError;
        return '';
    }

    function readHeaderSample(file) {
        return file.slice(0, headerSampleBytes).text();
    }

    function createHslocFileChecker(hooks) {
        var fileCheck = 0;
        function select(file) {
            var check = ++fileCheck;
            hooks.onError('');
            hooks.onChecking(false);
            if (!file) return Promise.resolve();
            var quick = quickFileError(file);
            if (quick) {
                hooks.onError(quick);
                hooks.onClear();
                return Promise.resolve();
            }
            hooks.onChecking(true);
            return Promise.resolve().then(function () {
                return readHeaderSample(file);
            }).then(function (text) {
                if (check !== fileCheck) return;
                hooks.onChecking(false);
                if (!hasHslocHeader(text)) {
                    hooks.onError(formatError);
                    hooks.onClear();
                }
            }).catch(function () {
                if (check !== fileCheck) return;
                hooks.onChecking(false);
                hooks.onError(unreadableError);
                hooks.onClear();
            });
        }
        return { select: select };
    }

    // The shared save handler posts this form with fetch. Enter in a version field can submit
    // while the header read is still running, so a disabled Replace button must cancel that submit.
    function bindHslocReplaceForm(doc) {
        var input = doc.getElementById('csv');
        var button = doc.getElementById('hsloc-replace');
        if (!input || !button || !input.form || button.form !== input.form) return;
        if (input.getAttribute('data-hsloc-bound') === '1') return;
        input.setAttribute('data-hsloc-bound', '1');
        var error = doc.getElementById('file-error');
        var checker = createHslocFileChecker({
            onError: function (message) {
                if (!error) return;
                error.textContent = message || '';
                if (message) error.removeAttribute('hidden');
                else error.setAttribute('hidden', '');
            },
            onChecking: function (checking) {
                button.disabled = !!checking;
            },
            onClear: function () {
                input.value = '';
            }
        });
        input.addEventListener('change', function () {
            var selected = input.files && input.files.length ? input.files[0] : null;
            checker.select(selected);
        });
        input.form.addEventListener('submit', function (event) {
            if (button.disabled) event.preventDefault();
        }, true);
    }

    function install(doc) {
        bindHslocReplaceForm(doc);
        if (doc.readyState === 'loading')
            doc.addEventListener('DOMContentLoaded', function () { bindHslocReplaceForm(doc); });
    }

    return {
        hasHslocHeader: hasHslocHeader,
        readHeaderSample: readHeaderSample,
        createHslocFileChecker: createHslocFileChecker,
        formatError: formatError,
        emptyFileError: emptyFileError,
        unreadableError: unreadableError,
        headerSampleBytes: headerSampleBytes,
        install: install
    };
});

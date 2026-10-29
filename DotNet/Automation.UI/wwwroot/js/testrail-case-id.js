// Parses a TestRail case id. Blank is unset. "123" and "C123" both store 123.
// Keep the accepted forms in step with Automation.UI/Models/TestRailCaseIdParser.cs.
(function (root, factory) {
    var api = factory();
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    }
    root.parseTestRailCaseId = api.parseTestRailCaseId;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
    var maxCaseId = 2147483647;

    function parseTestRailCaseId(value) {
        if (value == null)
            return { ok: true, id: null };

        var text = String(value).trim();
        if (text === '')
            return { ok: true, id: null };

        var match = /^[Cc]?(\d+)$/.exec(text);
        if (!match)
            return { ok: false, id: null };

        var digits = match[1].replace(/^0+/, '');
        if (digits === '' || digits.length > 10)
            return { ok: false, id: null };

        var id = Number(digits);
        if (!Number.isSafeInteger(id) || id > maxCaseId)
            return { ok: false, id: null };

        return { ok: true, id: id };
    }

    return { parseTestRailCaseId: parseTestRailCaseId };
});

// HSLOC Map target system matches Admin UI: shown, not editable, always the NHSN HSLOC URL.
// Other code maps keep an editable target system on create and on edit.
// Keep the URL in step with MappingTargetSystems.HslocUrl.
(function (root, factory) {
    var api = factory();
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    }
    root.hslocTargetSystemUrl = api.hslocTargetSystemUrl;
    root.isHslocMapOperation = api.isHslocMapOperation;
    root.targetSystemField = api.targetSystemField;
    root.applyTargetSystemField = api.applyTargetSystemField;
    root.targetSystemForSave = api.targetSystemForSave;
    root.normalizeCodeSystemMapsForSave = api.normalizeCodeSystemMapsForSave;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
    var hslocTargetSystemUrl = 'https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html';

    function isHslocMapOperation(operationType) {
        return String(operationType || '') === 'HSLOCMap';
    }

    function targetSystemField(operationType, existingValue) {
        if (isHslocMapOperation(operationType))
            return { readOnly: true, value: hslocTargetSystemUrl };

        return {
            readOnly: false,
            value: existingValue == null ? '' : String(existingValue)
        };
    }

    function applyTargetSystemField(input, operationType, existingValue) {
        var state = targetSystemField(operationType, existingValue);
        input.readOnly = state.readOnly;
        input.disabled = state.readOnly;
        input.value = state.value;
        if (state.readOnly)
            input.setAttribute('aria-readonly', 'true');
        else
            input.removeAttribute('aria-readonly');
        return state;
    }

    function targetSystemForSave(operationType, submittedValue) {
        if (isHslocMapOperation(operationType))
            return hslocTargetSystemUrl;
        return submittedValue == null ? '' : String(submittedValue).trim();
    }

    function normalizeCodeSystemMapsForSave(operationType, maps) {
        return (maps || []).map(function (map) {
            var copy = Object.assign({}, map);
            var submitted = Object.prototype.hasOwnProperty.call(copy, 'targetSystem')
                ? copy.targetSystem
                : copy.TargetSystem;
            var saved = targetSystemForSave(operationType, submitted);
            if (Object.prototype.hasOwnProperty.call(copy, 'targetSystem')
                || !Object.prototype.hasOwnProperty.call(copy, 'TargetSystem'))
                copy.targetSystem = saved;
            if (Object.prototype.hasOwnProperty.call(copy, 'TargetSystem'))
                copy.TargetSystem = saved;
            return copy;
        });
    }

    return {
        hslocTargetSystemUrl: hslocTargetSystemUrl,
        isHslocMapOperation: isHslocMapOperation,
        targetSystemField: targetSystemField,
        applyTargetSystemField: applyTargetSystemField,
        targetSystemForSave: targetSystemForSave,
        normalizeCodeSystemMapsForSave: normalizeCodeSystemMapsForSave
    };
});

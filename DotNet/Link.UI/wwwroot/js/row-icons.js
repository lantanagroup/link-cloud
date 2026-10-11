// Row actions in table cells are icon-only buttons; the label moves to the tooltip and the accessible name.
(function () {
    var selector = "table > tbody > tr > td .btn.btn-sm:not(.lu-cell-copy)";

    function iconize(button) {
        if (button.classList.contains("lu-row-icon") || !button.querySelector(":scope > i.bi")) return;
        var label = (button.textContent || "").replace(/\s+/g, " ").trim();
        if (!label) return;
        Array.prototype.slice.call(button.childNodes).forEach(function (node) {
            if (node.nodeType === 3 && node.nodeValue.trim()) {
                var span = document.createElement("span");
                span.textContent = node.nodeValue.trim();
                button.replaceChild(span, node);
            }
        });
        if (!button.getAttribute("title")) button.setAttribute("title", label);
        if (!button.getAttribute("aria-label")) button.setAttribute("aria-label", label);
        button.classList.add("lu-row-icon");
    }

    function scan(root) {
        (root.querySelectorAll ? root : document).querySelectorAll(selector).forEach(iconize);
    }

    function start() {
        scan(document);
        new MutationObserver(function (changes) {
            changes.forEach(function (change) {
                change.addedNodes.forEach(function (node) { if (node.nodeType === 1) scan(node); });
            });
        }).observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
    else start();
})();

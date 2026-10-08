/* Keeps a client-side filter in the query string and restores it on browser back. */
(function () {
    function fields() {
        return document.querySelectorAll("[data-lu-query]");
    }

    function apply(push) {
        var params = new URLSearchParams(window.location.search);
        fields().forEach(function (el) {
            var key = el.getAttribute("data-lu-query") || "q";
            if (!params.has(key)) return;
            var next = params.get(key) || "";
            if (el.value === next) return;
            el.value = next;
            el.dispatchEvent(new Event("input", { bubbles: true }));
        });
        void push;
    }

    function write(el) {
        var key = el.getAttribute("data-lu-query") || "q";
        var params = new URLSearchParams(window.location.search);
        var value = String(el.value || "").trim();
        if (value) params.set(key, value);
        else params.delete(key);
        var next = params.toString();
        var url = window.location.pathname + (next ? "?" + next : "") + window.location.hash;
        var current = window.location.pathname + window.location.search + window.location.hash;
        if (url !== current) history.replaceState(history.state, "", url);
    }

    document.addEventListener("input", function (event) {
        var el = event.target && event.target.closest ? event.target.closest("[data-lu-query]") : null;
        if (!el || event.isTrusted === false) return;
        write(el);
    });

    window.addEventListener("popstate", function () { apply(false); });

    if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", function () { apply(false); });
    else
        apply(false);
})();

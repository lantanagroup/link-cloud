(function () {
    function schedule(form) {
        var wait = Number(form.getAttribute("data-au-filter")) || 300;
        clearTimeout(form._auFilter);
        form._auFilter = setTimeout(function () {
            if (typeof form.requestSubmit === "function") form.requestSubmit();
            else form.submit();
        }, wait);
    }

    document.addEventListener("input", function (event) {
        var form = event.target && event.target.form;
        if (!form || !form.hasAttribute("data-au-filter")) return;
        var page = form.querySelector("input[name=page]");
        if (page) page.value = "1";
        schedule(form);
    });

    document.addEventListener("change", function (event) {
        var form = event.target && event.target.form;
        if (!form || !form.hasAttribute("data-au-filter")) return;
        if (event.target.name === "page") return;
        var page = form.querySelector("input[name=page]");
        if (page && event.target.name !== "pageSize") page.value = "1";
        schedule(form);
    });

    document.addEventListener("submit", function (event) {
        var form = event.target;
        if (!form || !form.hasAttribute("data-au-filter")) return;
        var targetId = form.getAttribute("data-au-target");
        var target = targetId && document.getElementById(targetId);
        if (!target || form.method.toLowerCase() !== "get") return;
        event.preventDefault();
        var params = new URLSearchParams(new FormData(form));
        var url = form.getAttribute("action") || window.location.pathname;
        var query = params.toString();
        var next = query ? url + "?" + query : url;
        replaceRegion(target, next, true);
    });

    function replaceRegion(node, url, push) {
        return fetch(url, { headers: { "X-Requested-With": "fetch" } }).then(function (res) {
            if (!res.ok) return;
            return res.text().then(function (html) {
                var doc = new DOMParser().parseFromString(html, "text/html");
                var fresh = doc.getElementById(node.id);
                if (!fresh) return;
                if (fresh.innerHTML !== node.innerHTML) node.innerHTML = fresh.innerHTML;
                if (push) history.replaceState(null, "", url);
                document.dispatchEvent(new CustomEvent("au-refreshed", { detail: { id: node.id } }));
            });
        }).catch(function () { /* leave the current page in place */ });
    }

    document.querySelectorAll("[data-au-refresh]").forEach(function (node) {
        var ms = Number(node.getAttribute("data-au-refresh")) || 12000;
        setInterval(function () {
            if (document.hidden) return;
            if (node.contains(document.activeElement)) return;
            var form = document.querySelector("form[data-au-target='" + node.id + "']");
            if (form && form.contains(document.activeElement)) return;
            replaceRegion(node, window.location.href, false);
        }, ms);
    });
})();

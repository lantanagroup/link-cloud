// One delegated handler for every data table. The button is created once and moved
// onto the hovered or focused cell. Touch devices never get it.
(function () {
    if (window.matchMedia("(hover: none), (pointer: coarse)").matches) return;

    var button = document.createElement("button");
    button.type = "button";
    button.className = "btn btn-sm lu-icon-btn lu-copy lu-cell-copy";
    button.innerHTML = '<i class="bi bi-copy" aria-hidden="true"></i><span class="visually-hidden">Copy</span>';
    button.setAttribute("aria-label", "Copy");
    button.title = "Copy";

    var host = null;
    var copied = "";
    var timer = 0;

    function tableCell(node) {
        var el = node && node.nodeType === 1 ? node : node && node.parentElement;
        if (!el || !el.closest) return null;
        if (el.closest(".lu-cell-copy")) return host;
        var td = el.closest("td");
        if (!td || td.closest("[data-nocopy]")) return null;
        var table = td.closest("table");
        if (!table) return null;
        if (!table.classList.contains("table")
            && !table.classList.contains("au-table")
            && !table.classList.contains("au-cohort-summary-table")) return null;
        return td;
    }

    function explicitCopy(td) {
        if (td.hasAttribute("data-copy")) return (td.getAttribute("data-copy") || "").trim();
        var marked = td.querySelector("[data-copy]");
        return marked ? (marked.getAttribute("data-copy") || "").trim() : null;
    }

    function blank(value) {
        return !value || value === "—" || value === "-" || value === "–";
    }

    function valueOf(td) {
        if (td.querySelector("input[type='checkbox'], input[type='radio'], button:not(.lu-cell-copy), a.btn")) return "";
        var exact = explicitCopy(td);
        if (exact !== null) return exact;
        var probe = td.cloneNode(true);
        probe.querySelectorAll(".lu-cell-copy, .visually-hidden, .au-info-icon, .badge").forEach(function (node) {
            node.remove();
        });
        var rest = (probe.textContent || "").replace(/\s+/g, " ").trim();
        if (td.querySelector(".badge") && !rest) return "";
        return rest;
    }

    function resetIcon() {
        var icon = button.querySelector("i");
        if (icon) {
            icon.classList.remove("bi-check2");
            icon.classList.add("bi-copy");
        }
        button.setAttribute("aria-label", "Copy");
        button.title = "Copy";
    }

    function clearHost() {
        if (timer) {
            clearTimeout(timer);
            timer = 0;
        }
        if (host) host.classList.remove("lu-copy-host");
        host = null;
        copied = "";
        if (button.parentElement) button.parentElement.removeChild(button);
        resetIcon();
    }

    function hideSoon() {
        if (timer) return;
        timer = setTimeout(function () {
            timer = 0;
            var active = document.activeElement;
            if (host && (host.contains(active) || host.matches(":hover"))) return;
            clearHost();
        }, 30);
    }

    function textOverflows(td) {
        var nodes = td.querySelectorAll(".lu-clip, .lu-labeled-id, code");
        for (var i = 0; i < nodes.length; i++) {
            if (nodes[i].scrollWidth > nodes[i].clientWidth) return true;
        }
        return false;
    }

    function show(td) {
        if (td === host) return;
        var next = valueOf(td);
        if (blank(next)) {
            if (host) clearHost();
            return;
        }
        if (host) host.classList.remove("lu-copy-host");
        host = td;
        copied = next;
        resetIcon();
        host.classList.add("lu-copy-host");
        if (!host.hasAttribute("title") && (host.scrollWidth > host.clientWidth || textOverflows(host)))
            host.setAttribute("title", next);
        host.appendChild(button);
        place(host);
    }

    // The icon sits at the cell's right edge, inside the cell. When a short value fills a narrow cell and leaves no
    // room there, it sits just after the text instead, so it never covers the value. Either way it takes no layout space.
    function place(td) {
        button.classList.remove("lu-copy-after");
        button.style.removeProperty("--lu-copy-left");
        if (td.scrollWidth > td.clientWidth || textOverflows(td)) return;
        var icon = button.getBoundingClientRect();
        var right = 0;
        var walker = document.createTreeWalker(td, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                if (!node.nodeValue.trim()) return NodeFilter.FILTER_REJECT;
                var parent = node.parentElement;
                if (!parent || parent.closest(".lu-cell-copy, .visually-hidden, [hidden]")) return NodeFilter.FILTER_REJECT;
                return NodeFilter.FILTER_ACCEPT;
            }
        });
        var range = document.createRange();
        for (var node = walker.nextNode(); node; node = walker.nextNode()) {
            range.selectNodeContents(node);
            Array.prototype.forEach.call(range.getClientRects(), function (rect) {
                if (rect.width > 0) right = Math.max(right, rect.right);
            });
        }
        if (!right) return;
        var cell = td.getBoundingClientRect();
        if (icon.left - right >= 3) return;
        button.classList.add("lu-copy-after");
        button.style.setProperty("--lu-copy-left", Math.round(right - cell.left + 4) + "px");
    }

    function copyText() {
        if (!copied || !navigator.clipboard || !navigator.clipboard.writeText) return;
        var value = copied;
        navigator.clipboard.writeText(value).then(function () {
            var icon = button.querySelector("i");
            if (icon) {
                icon.classList.remove("bi-copy");
                icon.classList.add("bi-check2");
            }
            button.setAttribute("aria-label", "Copied");
            button.title = "Copied";
        }, function () { });
    }

    function onDocument(event) {
        if (event.type === "click") {
            if (event.target === button || button.contains(event.target)) {
                event.preventDefault();
                copyText();
            }
            return;
        }
        if (event.type === "mouseout" || event.type === "focusout") {
            var next = event.relatedTarget;
            if (next && host && (next === button || host.contains(next))) return;
            if (!next) hideSoon();
            else if (host && !host.contains(next)) clearHost();
            return;
        }
        var td = tableCell(event.target);
        if (td) show(td);
        else if (host && event.target !== button && !host.contains(event.target)) clearHost();
    }

    ["mouseover", "mouseout", "focusin", "focusout", "click"].forEach(function (type) {
        document.addEventListener(type, onDocument);
    });
})();

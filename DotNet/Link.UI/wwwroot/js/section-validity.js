/* Marks the section that owns a validation error and opens it. */
(function () {
    function isItem(node) {
        return !!(node && node.classList && node.classList.contains("accordion-item"));
    }

    function isCard(node) {
        return !!(node && node.classList && (node.classList.contains("card") || node.classList.contains("au-cohort-card")));
    }

    function buttonOf(item) {
        return item ? item.querySelector(":scope > .accordion-header .accordion-button") : null;
    }

    function headingOf(card) {
        if (!card) return null;
        return card.querySelector(":scope > .card-header") || card.querySelector(":scope > .card-body .fw-semibold, :scope > .fw-semibold");
    }

    function resolve(root) {
        if (!root) return null;
        if (isItem(root)) {
            var ownButton = buttonOf(root);
            return ownButton ? { item: root, heading: ownButton, accordion: true } : null;
        }
        var item = root.closest ? root.closest(".accordion-item") : null;
        if (item) {
            var accordionButton = buttonOf(item);
            if (accordionButton) return { item: item, heading: accordionButton, accordion: true };
        }
        var card = isCard(root) ? root : (root.closest ? root.closest(".card, .au-cohort-card") : null);
        var heading = headingOf(card);
        if (!card || !heading) return null;
        return { item: card, heading: heading, accordion: false };
    }

    function ensureWarning(heading, message) {
        var mark = heading.querySelector(":scope > .lu-section-warning");
        if (!mark) {
            mark = document.createElement("span");
            mark.className = "lu-section-warning";
            mark.innerHTML = '<i class="bi bi-exclamation-triangle" aria-hidden="true"></i> <span class="visually-hidden"></span>';
            heading.appendChild(mark);
        }
        var text = mark.querySelector(".visually-hidden");
        if (text) text.textContent = message || "This section has a validation error.";
    }

    function openItem(item) {
        var panel = item.querySelector(":scope > .accordion-collapse");
        var toggle = buttonOf(item);
        if (panel && window.bootstrap && window.bootstrap.Collapse) {
            window.bootstrap.Collapse.getOrCreateInstance(panel, { toggle: false }).show();
        } else if (panel) {
            panel.classList.add("show");
        }
        if (toggle) {
            toggle.classList.remove("collapsed");
            toggle.setAttribute("aria-expanded", "true");
        }
    }

    function mark(root, message) {
        var target = resolve(root);
        if (!target) return;
        target.heading.classList.add("lu-section-invalid");
        target.heading.setAttribute("aria-invalid", "true");
        ensureWarning(target.heading, message);
        if (target.accordion) openItem(target.item);
    }

    function clear(root) {
        var target = resolve(root);
        if (!target) return;
        target.heading.classList.remove("lu-section-invalid");
        target.heading.removeAttribute("aria-invalid");
        var markEl = target.heading.querySelector(":scope > .lu-section-warning");
        if (markEl) markEl.remove();
    }

    function ownedError(section) {
        var nodes = section.querySelectorAll(".is-invalid, .alert-warning.lu-section-error");
        for (var i = 0; i < nodes.length; i++) {
            var owner = nodes[i].closest(".accordion-item") || nodes[i].closest(".card, .au-cohort-card");
            if (owner === section) return nodes[i];
        }
        return null;
    }

    function messageOf(node) {
        var alert = node && node.classList && node.classList.contains("alert-warning")
            ? node
            : (node && node.closest ? node.closest(".alert-warning.lu-section-error") : null);
        if (!alert && node && node.parentElement) alert = node.parentElement.querySelector(".alert-warning.lu-section-error");
        var text = alert && alert.textContent ? alert.textContent.trim() : "";
        return text || "This section has a validation error.";
    }

    function sync(scope) {
        var root = scope && scope.querySelectorAll ? scope : document;
        root.querySelectorAll(".accordion-item").forEach(function (item) {
            var error = ownedError(item);
            if (error) mark(item, messageOf(error));
            else clear(item);
        });
        root.querySelectorAll(".card, .au-cohort-card").forEach(function (card) {
            if (card.closest(".accordion-item")) return;
            var error = ownedError(card);
            if (error) mark(card, messageOf(error));
            else clear(card);
        });
    }

    window.luSectionValidity = { mark: mark, clear: clear, sync: sync };

    function start() { sync(document); }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
    else start();
})();

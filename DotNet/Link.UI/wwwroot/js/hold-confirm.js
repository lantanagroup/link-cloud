// Shared hold-to-confirm, ported from Automation.UI's Purge + Replace FHIR hold:
// pointer or keyboard (Space/Enter) hold, progress fill, cancel on release, Esc closes.
// Any submit button with data-hold='{"title","action","details","fields","risks","confirm","label"}' opens it;
// a completed hold writes "confirm" into the form's hidden [data-hold-target] input and submits with that button.
(function () {
    "use strict";
    var modalEl, modal, btn, fill, hint, holdMs = 1000;
    var pending = null, holdStart = 0, timer = null, raf = 0, done = false, keyHold = false;
    var IDLE = "Press and hold the button for 1 second to confirm.";

    function el(id) { return document.getElementById(id); }
    function setPct(p) { fill.style.width = p + "%"; fill.setAttribute("aria-valuenow", String(p)); }

    function stop(reset) {
        if (timer) { clearInterval(timer); timer = null; }
        if (raf) { cancelAnimationFrame(raf); raf = 0; }
        if (reset && !done) { setPct(0); hint.textContent = IDLE; btn.classList.remove("is-holding"); }
    }

    function complete() {
        if (done) return;
        done = true;
        stop(false);
        setPct(100);
        btn.classList.remove("btn-danger", "is-holding");
        btn.classList.add("btn-success");
        hint.textContent = "Confirmed.";
        var job = pending;
        setTimeout(function () {
            modal.hide();
            if (job) submit(job);
        }, 400);
    }

    function tick() {
        if (done) return;
        var pct = Math.min(100, Math.round(((Date.now() - holdStart) / holdMs) * 100));
        setPct(pct);
        if (pct >= 100) complete();
    }

    function start(ev) {
        if (ev) ev.preventDefault();
        if (done || !pending) return;
        holdStart = Date.now();
        stop(false);
        btn.classList.add("is-holding");
        hint.textContent = "Keep holding...";
        // Interval for throttled timers, rAF for smooth fill (as in Automation.UI).
        timer = setInterval(tick, 30);
        (function frame() { if (done) return; tick(); if (!done) raf = requestAnimationFrame(frame); })();
    }

    function cancel(ev) {
        if (ev) ev.preventDefault();
        if (done) return;
        stop(true);
    }

    function submit(job) {
        // A live refresh can swap the page while the modal is open; find the same button again.
        if (!job.button.isConnected) {
            var again = Array.prototype.find.call(document.querySelectorAll("button[data-hold]"), function (b) {
                return b.getAttribute("data-hold") === job.raw && (b.getAttribute("value") || "") === job.value;
            });
            if (!again) return;
            job.button = again;
        }
        if (typeof job.onDone === "function") {
            var done = job.onDone;
            job.onDone = null;
            done();
            return;
        }
        var form = job.button.form;
        if (!form) return;
        var target = form.querySelector("[data-hold-target]");
        if (target && job.spec.confirm) target.value = job.spec.confirm;
        job.button.setAttribute("data-hold-passed", "1");
        try {
            if (form.requestSubmit) form.requestSubmit(job.button);
            else job.button.click();
        } finally {
            setTimeout(function () { job.button.removeAttribute("data-hold-passed"); }, 0);
        }
    }

    function text(tag, value, cls) { var n = document.createElement(tag); n.textContent = value; if (cls) n.className = cls; return n; }

    function open(button, spec, onDone) {
        pending = { button: button, spec: spec, raw: button.getAttribute("data-hold") || "", value: button.getAttribute("value") || "", onDone: onDone || null };
        done = false;
        stop(true);
        btn.classList.remove("btn-success");
        btn.classList.add("btn-danger");
        el("lu-hold-title").textContent = spec.title || "Confirm";
        el("lu-hold-action").textContent = spec.action || "";
        el("lu-hold-label").textContent = spec.label || "Hold to confirm";
        var dl = el("lu-hold-details");
        dl.replaceChildren();
        var rows = (spec.details || []).slice();
        (spec.fields || []).forEach(function (f) {
            var input = button.form && button.form.elements[f[0]];
            var value = input ? (input.value || "").trim() : "";
            rows.push([f[1], value || "(empty)"]);
        });
        rows.forEach(function (r) { dl.appendChild(text("dt", r[0], "col-sm-4")); dl.appendChild(text("dd", r[1], "col-sm-8 text-break")); });
        dl.hidden = rows.length === 0;
        var ul = el("lu-hold-risks");
        ul.replaceChildren();
        (spec.risks || []).forEach(function (r) { ul.appendChild(text("li", r)); });
        el("lu-hold-risk-box").hidden = !(spec.risks || []).length;
        modal.show();
    }

    function wire() {
        modalEl = el("lu-hold-modal");
        if (!modalEl || !window.bootstrap) return;
        modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        btn = el("lu-hold-confirm"); fill = el("lu-hold-fill"); hint = el("lu-hold-hint");
        holdMs = parseInt(modalEl.getAttribute("data-hold-ms") || "1000", 10) || 1000;
        IDLE = "Press and hold the button for " + (holdMs / 1000) + " second" + (holdMs === 1000 ? "" : "s") + " to confirm.";
        btn.addEventListener("pointerdown", start);
        btn.addEventListener("pointerup", cancel);
        btn.addEventListener("pointerleave", cancel);
        btn.addEventListener("pointercancel", cancel);
        btn.addEventListener("contextmenu", function (ev) { ev.preventDefault(); });
        btn.addEventListener("keydown", function (ev) {
            if (ev.key !== " " && ev.key !== "Enter") return;
            ev.preventDefault();
            if (ev.repeat || keyHold) return;
            keyHold = true; start();
        });
        btn.addEventListener("keyup", function (ev) {
            if (ev.key !== " " && ev.key !== "Enter") return;
            ev.preventDefault();
            keyHold = false; cancel();
        });
        btn.addEventListener("blur", function () { keyHold = false; cancel(); });
        modalEl.addEventListener("shown.bs.modal", function () { btn.focus(); });
        modalEl.addEventListener("hidden.bs.modal", function () { stop(true); keyHold = false; if (!done) pending = null; });
        // Capture phase so page scripts never see an unconfirmed click.
        document.addEventListener("click", function (ev) {
            var b = ev.target && ev.target.closest ? ev.target.closest("button[data-hold]") : null;
            if (!b || b.disabled || b.getAttribute("data-hold-passed") === "1") return;
            var spec;
            try { spec = JSON.parse(b.getAttribute("data-hold") || "{}"); } catch (e) { spec = {}; }
            ev.preventDefault();
            ev.stopPropagation();
            open(b, spec);
        }, true);
    }

    function ask(anchor, title, action, risks, yes) {
        var node = anchor && typeof anchor.getAttribute === "function" ? anchor : document.body;
        if (!modal) wire();
        if (!modal) return;
        open(node, { title: title || "Confirm", action: action || "", details: [], fields: [], risks: risks || [], confirm: "", label: "Hold to confirm" }, yes);
    }

    window.luHoldConfirm = {
        open: function (button, spec, onDone) { if (!modal) wire(); if (modal) open(button, spec, onDone); },
        ask: ask
    };
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", wire); else wire();
})();

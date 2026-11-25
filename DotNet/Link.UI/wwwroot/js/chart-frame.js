/* Shared chart box: the size class owns the height, and an empty series becomes one line. */
(function () {
    function canvasOf(canvasId) {
        return document.getElementById(canvasId);
    }

    function boxOf(canvas) {
        if (!canvas) return null;
        return canvas.closest(".lu-chart, .lu-chart-donut") || canvas.parentElement;
    }

    function noteOf(canvasId, box) {
        var note = document.getElementById(canvasId + "-empty");
        if (note || !box || !box.parentElement) return note;
        note = document.createElement("p");
        note.id = canvasId + "-empty";
        note.className = "lu-chart-empty text-muted small mb-0";
        note.textContent = "No data yet.";
        box.insertAdjacentElement("afterend", note);
        return note;
    }

    function collapseEmpty(canvasId, withNote) {
        var canvas = canvasOf(canvasId);
        var box = boxOf(canvas);
        if (box) box.classList.add("d-none");
        if (withNote === false) return;
        var note = noteOf(canvasId, box);
        if (note) note.classList.remove("d-none");
    }

    function reveal(canvasId) {
        var canvas = canvasOf(canvasId);
        var box = boxOf(canvas);
        if (box) box.classList.remove("d-none");
        if (canvas) canvas.classList.remove("d-none");
        var note = document.getElementById(canvasId + "-empty");
        if (note) note.classList.add("d-none");
    }

    function fit(canvas) {
        if (!canvas || !canvas.parentElement) return;
        var parent = canvas.parentElement;
        if (parent.classList.contains("lu-chart") || parent.classList.contains("lu-chart-donut") || parent.dataset.luChart)
            return;
        var box = document.createElement("div");
        box.className = "lu-chart";
        box.dataset.luChart = "1";
        parent.insertBefore(box, canvas);
        box.appendChild(canvas);
    }

    window.luChartFrame = {
        collapseEmpty: collapseEmpty,
        reveal: reveal,
        fit: fit
    };
})();

/* Patient resource explorer and the on-demand type graph. Clusters only; identifiers stay paged. */
(function () {
    var graphUrl = "";
    var colors = ["#343a40", "#28a745", "#ffc107", "#dc3545", "#6f42c1", "#111111", "#fd7e14", "#545c64", "#adb5bd"];
    var summaryAbort = null;
    var pageAbort = null;
    var state = null;

    function patient() {
        return window.luManifestPatient || null;
    }

    function number(value) {
        return Number(value || 0).toLocaleString();
    }

    function note(parent, text) {
        var paragraph = document.createElement("p");
        paragraph.className = "mb-0 text-muted";
        paragraph.textContent = text;
        parent.appendChild(paragraph);
    }

    function endpoint(extra) {
        var row = patient();
        var url = graphUrl + (graphUrl.indexOf("?") >= 0 ? "&" : "?") + "patientId=" + encodeURIComponent(row && row.id || "");
        Object.keys(extra || {}).forEach(function (key) {
            if (extra[key] === undefined || extra[key] === null || extra[key] === "") return;
            url += "&" + key + "=" + encodeURIComponent(extra[key]);
        });
        return url;
    }

    function pager(host, page, pages, total, onPage) {
        var bar = document.createElement("div");
        bar.className = "lu-graph-pager";
        function step(label, icon, enabled, next) {
            var control = document.createElement("button");
            control.type = "button";
            control.className = "lu-page-step";
            control.setAttribute("aria-label", label);
            control.title = label;
            control.disabled = !enabled;
            var mark = document.createElement("i");
            mark.className = "bi " + icon;
            mark.setAttribute("aria-hidden", "true");
            control.appendChild(mark);
            control.addEventListener("click", function () {
                if (enabled) onPage(next);
            });
            bar.appendChild(control);
        }
        step("First", "bi-chevron-double-left", page > 1, 1);
        step("Previous", "bi-chevron-left", page > 1, page - 1);
        var place = document.createElement("span");
        place.className = "small text-muted";
        place.textContent = page + " / " + (pages || 1);
        bar.appendChild(place);
        step("Next", "bi-chevron-right", page < pages, page + 1);
        step("Last", "bi-chevron-double-right", page < pages, pages);
        host.appendChild(bar);
    }

    function idCell(label, value) {
        var cell = document.createElement("div");
        if (window.luLabeledId) cell.innerHTML = window.luLabeledId(label, value || "");
        else cell.textContent = value || "—";
        return cell;
    }

    window.luResourceExplorer = {
        render: function (row, url) {
            var host = document.getElementById("manifest-patient-refs");
            if (!host) return;
            host.replaceChildren();
            var resources = (row && row.resources) || [];
            var refs = (row && row.refs) || [];
            if (!resources.length && !refs.length) {
                note(host, "No resource counts were recorded.");
                return;
            }

            var search = document.createElement("input");
            search.type = "search";
            search.className = "form-control form-control-sm lu-explorer-search";
            search.placeholder = "Search types or identifiers";
            search.setAttribute("aria-label", "Search resources");
            host.appendChild(search);

            var list = document.createElement("div");
            host.appendChild(list);
            var text = "";
            var openName = "";

            function groups() {
                var map = new Map();
                resources.forEach(function (item) {
                    var name = item.name || "—";
                    map.set(name, { name: name, count: Number(item.count || 0), refs: [] });
                });
                refs.forEach(function (item) {
                    var name = item.type || "—";
                    var group = map.get(name) || { name: name, count: 0, refs: [] };
                    group.refs.push(item);
                    if (!group.count) group.count = group.refs.length;
                    map.set(name, group);
                });
                return Array.from(map.values()).sort(function (left, right) { return right.count - left.count || left.name.localeCompare(right.name); });
            }

            function draw() {
                list.replaceChildren();
                var rows = groups().filter(function (group) {
                    if (!text) return true;
                    if (group.name.toLowerCase().indexOf(text) >= 0) return true;
                    return group.refs.some(function (item) { return (item.id || "").toLowerCase().indexOf(text) >= 0; });
                });
                if (!rows.length) {
                    note(list, "Nothing on this page matches.");
                    return;
                }
                var max = rows.reduce(function (peak, group) { return Math.max(peak, group.count); }, 1);
                rows.forEach(function (group) {
                    var open = openName === group.name;
                    var button = document.createElement("button");
                    button.type = "button";
                    button.className = "lu-type-toggle";
                    button.setAttribute("aria-expanded", open ? "true" : "false");
                    var chevron = document.createElement("i");
                    chevron.className = "bi " + (open ? "bi-chevron-down" : "bi-chevron-right");
                    chevron.setAttribute("aria-hidden", "true");
                    var label = document.createElement("span");
                    label.textContent = group.name;
                    var count = document.createElement("span");
                    count.className = "lu-type-count";
                    count.textContent = number(group.count);
                    button.appendChild(chevron);
                    button.appendChild(label);
                    button.appendChild(count);
                    var bar = document.createElement("span");
                    bar.className = "lu-type-bar";
                    bar.style.gridColumn = "1 / -1";
                    var fill = document.createElement("span");
                    fill.style.width = Math.max(8, Math.round(Math.sqrt(group.count / max) * 100)) + "%";
                    fill.style.background = colors[rows.indexOf(group) % colors.length];
                    bar.appendChild(fill);
                    button.appendChild(bar);
                    button.addEventListener("click", function () {
                        openName = open ? "" : group.name;
                        draw();
                    });
                    list.appendChild(button);
                    if (open) list.appendChild(openGroup(group));
                });
            }

            function openGroup(group) {
                var panel = document.createElement("div");
                panel.className = "lu-type-panel";
                if (url) loadPage(panel, group, 1);
                else localPage(panel, group, 1);
                return panel;
            }

            function localPage(panel, group, page) {
                panel.replaceChildren();
                var matched = group.refs.filter(function (item) {
                    return !text || (item.id || "").toLowerCase().indexOf(text) >= 0 || group.name.toLowerCase().indexOf(text) >= 0;
                });
                var size = 25;
                var pages = Math.max(1, Math.ceil(matched.length / size));
                var current = Math.min(page, pages);
                var slice = matched.slice((current - 1) * size, current * size);
                if (!slice.length) note(panel, group.count ? number(group.count) + " counted. No identifiers are on this list." : "No identifiers are on this list.");
                else {
                    if (matched.length > size) pager(panel, current, pages, matched.length, function (next) { localPage(panel, group, next); });
                    slice.forEach(function (item) { panel.appendChild(idCell("Resource", item.id || "")); });
                    if (matched.length < group.count) {
                        var quiet = document.createElement("p");
                        quiet.className = "small text-muted mb-0 mt-2";
                        quiet.textContent = number(matched.length) + " identifiers on this list.";
                        panel.appendChild(quiet);
                    }
                }
            }

            function loadPage(panel, group, page) {
                panel.replaceChildren();
                note(panel, "Loading " + group.name + "…");
                fetch(endpoint({ part: "page", type: group.name, q: text, page: page, pageSize: 25 }))
                    .then(function (response) { return response.ok ? response.json() : response.json().then(function (body) { throw new Error(body.detail || "The list was refused."); }); })
                    .then(function (body) {
                        panel.replaceChildren();
                        var records = body.records || [];
                        var meta = body.metadata || {};
                        if (!records.length) note(panel, "No identifiers match.");
                        var total = Number(meta.totalCount || records.length);
                        var pages = Number(meta.totalPages || 1);
                        if (pages > 1) pager(panel, Number(meta.pageNumber || page), pages, total, function (next) { loadPage(panel, group, next); });
                        records.forEach(function (item) { panel.appendChild(idCell("Resource", item.id || "")); });
                        if (total < group.count) {
                            var quiet = document.createElement("p");
                            quiet.className = "small text-muted mb-0 mt-2";
                            quiet.textContent = number(total) + " identifiers. " + number(group.count) + " counted.";
                            panel.appendChild(quiet);
                        }
                    })
                    .catch(function (error) {
                        panel.replaceChildren();
                        note(panel, error.message || "The list was refused.");
                    });
            }

            var timer = 0;
            search.addEventListener("input", function () {
                text = search.value.trim().toLowerCase();
                window.clearTimeout(timer);
                timer = window.setTimeout(draw, 120);
            });
            draw();
        }
    };

    function panel() { return document.getElementById("manifest-resource-graph"); }

    function setStatus(text, ratio, mode) {
        mode = mode || (text ? "busy" : "idle");
        var status = document.getElementById("manifest-graph-status");
        var bar = document.getElementById("manifest-graph-bar");
        var wrap = document.getElementById("manifest-graph-progress");
        var count = document.getElementById("manifest-graph-count");
        var cancel = document.getElementById("manifest-graph-cancel");
        var busy = mode === "busy";
        if (cancel) cancel.hidden = !busy;
        if (wrap) wrap.hidden = !busy;
        if (bar && busy) bar.style.width = Math.max(4, Math.min(100, ratio || 0)) + "%";
        if (status) {
            status.hidden = mode === "settled" || !text;
            status.textContent = mode === "settled" ? "" : (text || "");
        }
        if (count && (mode === "settled" || mode === "idle")) count.textContent = mode === "settled" ? (text || "") : "";
    }

    function showError(text) {
        var box = document.getElementById("manifest-graph-error");
        var label = document.getElementById("manifest-graph-error-text");
        if (label) label.textContent = text || "The graph was refused.";
        if (box) box.hidden = !text;
    }

    window.luResourceGraph = {
        mount: function (url, palette) {
            graphUrl = url || "";
            if (palette && palette.length) colors = palette;
            var button = document.getElementById("manifest-interactive");
            var bar = document.getElementById("manifest-interactive-bar");
            if (button) button.hidden = !graphUrl;
            if (bar) bar.hidden = !graphUrl;
            if (button && !button.dataset.bound) {
                button.dataset.bound = "1";
                button.addEventListener("click", open);
            }
            var cancel = document.getElementById("manifest-graph-cancel");
            if (cancel && !cancel.dataset.bound) {
                cancel.dataset.bound = "1";
                cancel.addEventListener("click", function () { abort(); setStatus("Cancelled.", 0, "note"); });
            }
            var dismiss = document.getElementById("manifest-graph-dismiss");
            if (dismiss && !dismiss.dataset.bound) {
                dismiss.dataset.bound = "1";
                dismiss.addEventListener("click", close);
            }
        },
        close: close
    };

    function abort() {
        if (summaryAbort) summaryAbort.abort();
        if (pageAbort) pageAbort.abort();
        summaryAbort = null;
        pageAbort = null;
    }

    function close() {
        abort();
        state = null;
        var box = panel();
        if (box) box.hidden = true;
        var body = document.getElementById("manifest-graph-body");
        if (body) body.hidden = true;
        showError("");
        setStatus("", 0);
        var button = document.getElementById("manifest-interactive");
        if (button) button.disabled = false;
    }

    function open() {
        var row = patient();
        if (!row || !graphUrl) return;
        abort();
        summaryAbort = new AbortController();
        var box = panel();
        if (box) {
            box.hidden = false;
            box.scrollIntoView({ block: "nearest" });
        }
        var body = document.getElementById("manifest-graph-body");
        if (body) body.hidden = true;
        showError("");
        setStatus("Reading resources…", 8, "busy");
        var button = document.getElementById("manifest-interactive");
        if (button) button.disabled = true;
        readSummary(summaryAbort.signal)
            .then(function (done) {
                if (button) button.disabled = false;
                if (!done) return;
                if (done.error && !done.total) {
                    showError(done.error);
                    setStatus("", 0, "idle");
                    return;
                }
                state = {
                    patientId: row.id,
                    total: Number(done.total || 0),
                    truncated: !!done.truncated,
                    types: (done.types || []).map(function (type, index) {
                        return { name: type.name, count: Number(type.count || 0), color: colors[index % colors.length] };
                    }),
                    selected: null,
                    page: null,
                    node: null,
                    panX: 0,
                    panY: 0,
                    zoom: 1
                };
                var body = document.getElementById("manifest-graph-body");
                if (body) body.hidden = false;
                var line = number(state.total) + " resources";
                if (state.truncated) line = "Stopped at " + number(state.total);
                setStatus(line, 100, "settled");
                bindCanvas();
                draw();
                showTypes();
            })
            .catch(function (error) {
                if (button) button.disabled = false;
                if (error.name === "AbortError") return;
                showError(error.message || "The graph was refused.");
                setStatus("", 0, "idle");
            });
    }

    async function readSummary(signal) {
        var response = await fetch(endpoint({ part: "summary" }), { signal: signal, headers: { Accept: "application/x-ndjson" } });
        if (!response.ok) {
            var problem = {};
            try { problem = await response.json(); } catch (ignore) { problem = {}; }
            throw new Error(problem.detail || "The graph was refused.");
        }
        var reader = response.body.getReader();
        var decoder = new TextDecoder();
        var buffer = "";
        var done = null;
        while (true) {
            var chunk = await reader.read();
            if (chunk.done) break;
            buffer += decoder.decode(chunk.value, { stream: true });
            var lines = buffer.split("\n");
            buffer = lines.pop();
            lines.forEach(function (line) { done = takeLine(line) || done; });
        }
        if (buffer.trim()) done = takeLine(buffer) || done;
        return done;
    }

    function takeLine(line) {
        if (!line || !line.trim()) return null;
        var payload = JSON.parse(line);
        if (payload.kind === "progress") {
            var read = Number(payload.read || 0);
            var cap = Number(payload.cap || read || 1);
            setStatus("Reading resources… " + number(read), read * 100 / cap, "busy");
        }
        return payload.kind === "done" ? payload : null;
    }

    function bindCanvas() {
        var canvas = document.getElementById("manifest-graph-canvas");
        if (!canvas || canvas.dataset.bound) return;
        canvas.dataset.bound = "1";
        var drag = null;
        canvas.addEventListener("pointerdown", function (event) {
            drag = { x: event.clientX, y: event.clientY, panX: state ? state.panX : 0, panY: state ? state.panY : 0, moved: false };
            canvas.setPointerCapture(event.pointerId);
        });
        canvas.addEventListener("pointermove", function (event) {
            if (!state) return;
            if (!drag) {
                if (!state.center) return;
                var point = canvasPoint(event);
                var over = state.types.some(function (type) {
                    return Math.hypot(point.x - type.x, point.y - type.y) <= type.r + 4;
                });
                canvas.style.cursor = over ? "pointer" : "grab";
                return;
            }
            var dx = event.clientX - drag.x;
            var dy = event.clientY - drag.y;
            if (Math.abs(dx) + Math.abs(dy) > 4) drag.moved = true;
            state.panX = drag.panX + dx;
            state.panY = drag.panY + dy;
            draw();
        });
        canvas.addEventListener("pointerup", function (event) {
            if (!drag || !state) return;
            if (!drag.moved) selectAt(event);
            drag = null;
        });
        canvas.addEventListener("wheel", function (event) {
            if (!state) return;
            event.preventDefault();
            state.zoom = Math.min(2.4, Math.max(0.6, state.zoom * (event.deltaY > 0 ? 0.92 : 1.08)));
            draw();
        }, { passive: false });
    }

    function layout(width, height) {
        var count = Math.max(1, state.types.length);
        var peak = state.types.reduce(function (max, type) { return Math.max(max, type.count); }, 1);
        var maxR = count > 8 ? 22 : 26;
        state.types.forEach(function (type, index) {
            var ratio = Math.sqrt(type.count / peak);
            type.r = 11 + (maxR - 11) * ratio;
            type.angle = -Math.PI / 2 + index * Math.PI * 2 / count;
        });
        var widest = state.types.reduce(function (max, type) { return Math.max(max, type.r); }, 11);
        var cx = width / 2;
        var cy = height / 2;
        var orbit = Math.min(width, height) / 2 - widest - 78;
        orbit = Math.max(78, orbit);
        state.types.forEach(function (type) {
            type.x = cx + Math.cos(type.angle) * orbit;
            type.y = cy + Math.sin(type.angle) * orbit;
        });
        return { cx: cx, cy: cy, orbit: orbit };
    }

    function drawLabel(ctx, type, width, height) {
        var ux = Math.cos(type.angle);
        var uy = Math.sin(type.angle);
        ctx.font = "12px system-ui, sans-serif";
        var name = type.name;
        var countText = number(type.count);
        var wide = Math.max(ctx.measureText(name).width, ctx.measureText(countText).width);
        var x = type.x + ux * (type.r + 18);
        var y = type.y + uy * (type.r + 26);
        var align = Math.abs(ux) < 0.45 ? "center" : (ux > 0 ? "left" : "right");
        var left = align === "left" ? x : align === "right" ? x - wide : x - wide / 2;
        if (left < 8) {
            align = "left";
            x = 8;
        } else if (left + wide > width - 8) {
            align = "right";
            x = width - 8;
        }
        y = Math.max(14, Math.min(height - 14, y));
        ctx.textAlign = align;
        ctx.textBaseline = "middle";
        ctx.fillStyle = "#1a1a1a";
        ctx.fillText(name, x, y - 7);
        ctx.font = "11px system-ui, sans-serif";
        ctx.fillStyle = "#5c656d";
        ctx.fillText(countText, x, y + 8);
    }

    function draw() {
        var canvas = document.getElementById("manifest-graph-canvas");
        if (!canvas || !state) return;
        var rect = canvas.getBoundingClientRect();
        var dpr = window.devicePixelRatio || 1;
        var width = Math.max(320, rect.width || 640);
        var height = 420;
        canvas.width = Math.round(width * dpr);
        canvas.height = Math.round(height * dpr);
        var ctx = canvas.getContext("2d");
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, width, height);
        ctx.fillStyle = "#fbfbfb";
        ctx.fillRect(0, 0, width, height);
        ctx.save();
        ctx.translate(width / 2 + state.panX, height / 2 + state.panY);
        ctx.scale(state.zoom, state.zoom);
        ctx.translate(-width / 2, -height / 2);
        var center = layout(width, height);
        state.center = center;
        state.types.forEach(function (type) {
            var quiet = state.selected && state.selected.name !== type.name;
            ctx.globalAlpha = quiet ? 0.35 : 1;
            ctx.strokeStyle = "rgba(17,17,17,.16)";
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(center.cx, center.cy);
            ctx.lineTo(type.x, type.y);
            ctx.stroke();
            ctx.globalAlpha = 1;
        });
        ctx.beginPath();
        ctx.fillStyle = "#111";
        ctx.arc(center.cx, center.cy, 28, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = "#fff";
        ctx.font = "600 11px system-ui, sans-serif";
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillText("Patient", center.cx, center.cy);
        state.types.forEach(function (type) {
            var selected = state.selected && state.selected.name === type.name;
            var quiet = state.selected && !selected;
            ctx.globalAlpha = quiet ? 0.4 : 1;
            ctx.beginPath();
            ctx.fillStyle = type.color;
            ctx.arc(type.x, type.y, type.r, 0, Math.PI * 2);
            ctx.fill();
            ctx.lineWidth = selected ? 3 : 1.5;
            ctx.strokeStyle = selected ? "#111" : "rgba(255,255,255,.85)";
            ctx.stroke();
            ctx.globalAlpha = 1;
        });
        state.types.forEach(function (type) { drawLabel(ctx, type, width, height); });
        ctx.restore();
        canvas.setAttribute("aria-label", "Resource graph, " + number(state.total) + " resources");
    }

    function canvasPoint(event) {
        var canvas = document.getElementById("manifest-graph-canvas");
        var rect = canvas.getBoundingClientRect();
        var width = rect.width;
        var height = 420;
        var x = event.clientX - rect.left;
        var y = event.clientY - rect.top;
        x = (x - width / 2 - state.panX) / state.zoom + width / 2;
        y = (y - height / 2 - state.panY) / state.zoom + height / 2;
        return { x: x, y: y };
    }

    function selectAt(event) {
        if (!state) return;
        var point = canvasPoint(event);
        var hit = state.types.find(function (type) {
            return Math.hypot(point.x - type.x, point.y - type.y) <= type.r + 4;
        });
        if (hit) chooseType(hit.name, 1);
    }

    function showTypes() {
        var aside = document.getElementById("manifest-graph-aside");
        if (!aside || !state) return;
        aside.replaceChildren();
        var heading = document.createElement("p");
        heading.className = "small text-muted mb-2";
        heading.textContent = "Types";
        aside.appendChild(heading);
        state.types.forEach(function (type) {
            var button = document.createElement("button");
            button.type = "button";
            button.className = "lu-type-toggle";
            var swatch = document.createElement("span");
            swatch.className = "lu-donut-swatch";
            swatch.style.background = type.color;
            var label = document.createElement("span");
            label.textContent = type.name;
            var count = document.createElement("span");
            count.className = "lu-type-count";
            count.textContent = number(type.count);
            button.appendChild(swatch);
            button.appendChild(label);
            button.appendChild(count);
            button.addEventListener("click", function () { chooseType(type.name, 1); });
            aside.appendChild(button);
        });
    }

    function chooseType(name, page) {
        if (!state) return;
        state.selected = state.types.find(function (type) { return type.name === name; }) || null;
        state.node = null;
        if (pageAbort) pageAbort.abort();
        pageAbort = new AbortController();
        var signal = pageAbort.signal;
        fetch(endpoint({ part: "page", type: name, page: page, pageSize: 25 }), { signal: signal })
            .then(function (response) { return response.json(); })
            .then(function (body) {
                if (!state || signal.aborted) return;
                state.page = body.records || [];
                state.pageMeta = body.metadata || {};
                draw();
                showPage(name);
            })
            .catch(function (error) {
                if (error.name === "AbortError") return;
                showError(error.message || "The list was refused.");
            });
    }

    function showPage(name) {
        var aside = document.getElementById("manifest-graph-aside");
        if (!aside || !state) return;
        aside.replaceChildren();
        var back = document.createElement("button");
        back.type = "button";
        back.className = "lu-page-step mb-2";
        back.setAttribute("aria-label", "Back to types");
        back.title = "Back to types";
        var icon = document.createElement("i");
        icon.className = "bi bi-chevron-left";
        icon.setAttribute("aria-hidden", "true");
        back.appendChild(icon);
        back.addEventListener("click", function () {
            state.selected = null;
            state.page = null;
            state.node = null;
            draw();
            showTypes();
        });
        var title = document.createElement("span");
        title.className = "ms-2";
        title.textContent = name;
        var head = document.createElement("div");
        head.className = "d-flex align-items-center mb-2";
        head.appendChild(back);
        head.appendChild(title);
        aside.appendChild(head);
        var meta = state.pageMeta || {};
        var pages = Number(meta.totalPages || 1);
        if (pages > 1) {
            pager(aside, Number(meta.pageNumber || 1), pages, Number(meta.totalCount || 0), function (next) {
                chooseType(name, next);
            });
        }
        (state.page || []).forEach(function (node) {
            var row = document.createElement("div");
            row.className = "lu-type-row";
            row.appendChild(idCell("Resource", node.id));
            var open = document.createElement("button");
            open.type = "button";
            open.className = "btn btn-sm btn-au-link lu-icon-btn";
            open.title = "Open";
            open.setAttribute("aria-label", "Open resource");
            var eye = document.createElement("i");
            eye.className = "bi bi-eye";
            eye.setAttribute("aria-hidden", "true");
            open.appendChild(eye);
            open.addEventListener("click", function () { chooseNode(node); });
            row.appendChild(open);
            aside.appendChild(row);
        });
    }

    function chooseNode(node) {
        state.node = node;
        draw();
        fetch(endpoint({ part: "raw", type: node.type, id: node.id }))
            .then(function (response) { return response.ok ? response.json() : response.json().then(function (body) { throw new Error(body.detail || "The resource was refused."); }); })
            .then(function (body) { showNode(body); })
            .catch(function (error) { showError(error.message || "The resource was refused."); });
    }

    function showNode(body) {
        var aside = document.getElementById("manifest-graph-aside");
        if (!aside) return;
        aside.replaceChildren();
        var back = document.createElement("button");
        back.type = "button";
        back.className = "lu-page-step mb-2";
        back.setAttribute("aria-label", "Back to " + (body.type || "type"));
        back.title = "Back";
        var icon = document.createElement("i");
        icon.className = "bi bi-chevron-left";
        icon.setAttribute("aria-hidden", "true");
        back.appendChild(icon);
        back.addEventListener("click", function () { showPage(body.type); });
        aside.appendChild(back);
        var tabs = document.createElement("div");
        tabs.className = "lu-graph-tabs";
        tabs.setAttribute("role", "tablist");
        var details = document.createElement("div");
        var raw = document.createElement("div");
        raw.hidden = true;
        function tab(label, selected, show) {
            var button = document.createElement("button");
            button.type = "button";
            button.textContent = label;
            button.setAttribute("role", "tab");
            button.setAttribute("aria-selected", selected ? "true" : "false");
            button.addEventListener("click", function () {
                details.hidden = show !== details;
                raw.hidden = show !== raw;
                tabs.querySelectorAll("button").forEach(function (item) {
                    item.setAttribute("aria-selected", item === button ? "true" : "false");
                });
            });
            tabs.appendChild(button);
        }
        tab("Details", true, details);
        tab("Raw", false, raw);
        aside.appendChild(tabs);
        details.appendChild(line("Type", body.type || "—"));
        details.appendChild(idCell("Resource", body.id || ""));
        var refs = body.refs || [];
        if (!refs.length) note(details, "No references were recorded.");
        else refs.forEach(function (item) {
            var text = String(item || "");
            var slash = text.indexOf("/");
            if (slash > 0 && slash < text.length - 1) details.appendChild(idCell(text.slice(0, slash), text.slice(slash + 1)));
            else details.appendChild(line("Reference", text));
        });
        if (body.note) note(details, body.note);
        var pre = document.createElement("pre");
        pre.className = "lu-graph-raw mb-2";
        pre.textContent = body.json || body.note || "No JSON was kept for this resource.";
        var copy = document.createElement("button");
        copy.type = "button";
        copy.className = "btn btn-sm btn-au-link lu-icon-btn lu-copy";
        copy.setAttribute("data-lu-copy", body.json || "");
        copy.title = "Copy resource";
        copy.setAttribute("aria-label", "Copy resource");
        var mark = document.createElement("i");
        mark.className = "bi bi-copy";
        mark.setAttribute("aria-hidden", "true");
        copy.appendChild(mark);
        if (!body.json) copy.disabled = true;
        raw.appendChild(copy);
        raw.appendChild(pre);
        aside.appendChild(details);
        aside.appendChild(raw);
    }

    function line(label, value) {
        var paragraph = document.createElement("p");
        paragraph.className = "mb-1";
        var name = document.createElement("span");
        name.className = "text-muted";
        name.textContent = label + " ";
        paragraph.appendChild(name);
        paragraph.appendChild(document.createTextNode(value || "—"));
        return paragraph;
    }
})();

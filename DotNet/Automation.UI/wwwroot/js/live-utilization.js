(function () {
    var ramMaxBytes = 2 * 1024 * 1024 * 1024;
    var historyLimit = 24;
    var history = {};
    var selectedKey = null;
    var userPinned = false;
    var timer = null;
    var inFlight = false;
    var lastServices = [];

    function $(id) { return document.getElementById(id); }

    function loadPref(key, fallback) {
        try {
            var v = localStorage.getItem('au.livePulse.' + key);
            return v == null ? fallback : v;
        } catch (e) { return fallback; }
    }
    function savePref(key, value) {
        try { localStorage.setItem('au.livePulse.' + key, value); } catch (e) { /* ignore */ }
    }

    var view = loadPref('view', 'compact');
    var filter = loadPref('filter', 'all');
    var sortMode = loadPref('sort', 'heat');

    function formatPercent(percent) {
        if (percent == null || Number.isNaN(percent)) return '—';
        if (percent < 0.1) return '< 0.1%';
        if (percent < 10) return percent.toFixed(1) + '%';
        return Math.round(percent) + '%';
    }
    function formatRam(bytes) {
        if (bytes == null || bytes <= 0) return '—';
        if (bytes >= 1024 * 1024 * 1024) return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
        return Math.round(bytes / (1024 * 1024)) + ' MB';
    }
    function formatApi(ms) {
        if (ms == null || Number.isNaN(ms)) return '—';
        if (ms >= 1000) return (ms / 1000).toFixed(1) + ' sec';
        if (ms < 1) return '< 1 ms';
        return Math.round(ms) + ' ms';
    }
    function heatColor(pct) {
        if (pct >= 0.8) return 'var(--au-danger)';
        if (pct >= 0.5) return '#e38b00';
        return 'var(--au-success)';
    }
    function cpuPct(svc) {
        return svc.cpuPercent == null ? 0 : Math.max(0, Math.min(1, svc.cpuPercent / 100));
    }
    function ramPct(svc) {
        return svc.memoryBytes == null ? 0 : Math.max(0, Math.min(1, svc.memoryBytes / ramMaxBytes));
    }
    function heat(svc) {
        return Math.max(cpuPct(svc), ramPct(svc));
    }
    function isHot(svc) { return heat(svc) >= 0.5; }
    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }
    function shortName(name) {
        return String(name || '').replace('Data Acquisition', 'DA').replace('Measure Evaluation', 'Eval');
    }

    function remember(services) {
        services.forEach(function (svc) {
            var key = svc.key;
            if (!history[key]) history[key] = [];
            history[key].push(heat(svc));
            if (history[key].length > historyLimit) history[key].shift();
        });
    }

    function sparkPath(points) {
        if (!points || !points.length) return '';
        var w = 160, h = 36;
        var step = points.length === 1 ? 0 : w / (points.length - 1);
        return points.map(function (p, i) {
            var x = points.length === 1 ? w / 2 : i * step;
            var y = h - (Math.max(0, Math.min(1, p)) * (h - 4)) - 2;
            return (i === 0 ? 'M' : 'L') + x.toFixed(1) + ',' + y.toFixed(1);
        }).join(' ');
    }

    function visibleServices(services) {
        var list = services.slice();
        if (filter === 'pipeline')
            list = list.filter(function (s) { return s.group === 'pipeline'; });
        else if (filter === 'hot')
            list = list.filter(isHot);
        if (sortMode === 'heat')
            list.sort(function (a, b) { return heat(b) - heat(a); });
        else
            list.sort(function (a, b) { return String(a.name).localeCompare(String(b.name)); });
        return list;
    }

    function setSeg(group, value) {
        document.querySelectorAll('[data-pulse-group="' + group + '"]').forEach(function (btn) {
            btn.classList.toggle('is-on', btn.getAttribute('data-pulse-value') === value);
        });
    }

    function metersHtml(svc) {
        var c = cpuPct(svc);
        var r = ramPct(svc);
        return '<div class="au-pulse-meters" aria-hidden="true">' +
            '<div class="au-pulse-bar" title="CPU"><i style="--h:' + (c * 100).toFixed(1) + '%;--c:' + heatColor(c) + ';"></i></div>' +
            '<div class="au-pulse-bar" title="RAM"><i style="--h:' + (r * 100).toFixed(1) + '%;--c:' + heatColor(r) + ';"></i></div>' +
            '</div>';
    }

    function hMeter(label, pct, readout) {
        return '<div class="au-pulse-h-meter">' +
            '<span>' + label + '</span>' +
            '<div class="au-pulse-h-track"><div class="au-pulse-h-fill" style="--h:' + (pct * 100).toFixed(1) + '%;--c:' + heatColor(pct) + ';"></div></div>' +
            '<strong>' + escapeHtml(readout) + '</strong>' +
            '</div>';
    }

    function renderDetail(svc) {
        var host = $('livePulseDetail');
        if (!host) return;
        if (!svc) {
            host.innerHTML = '<p class="au-pulse-detail-empty mb-0">Click a service to pin CPU, RAM, and API p95. Hot services glow. Compact view keeps the rest of Performance in sight.</p>';
            return;
        }
        var points = history[svc.key] || [];
        var path = sparkPath(points);
        host.innerHTML =
            '<div class="au-pulse-detail-grid">' +
                '<div>' +
                    '<div class="d-flex justify-content-between align-items-baseline gap-2 mb-2">' +
                        '<div class="fw-semibold">' + escapeHtml(svc.name || svc.key) + '</div>' +
                        '<div class="small text-muted">' + (svc.group === 'pipeline' ? 'Pipeline' : 'Platform') + ' · API p95 ' + escapeHtml(formatApi(svc.apiP95Ms)) + '</div>' +
                    '</div>' +
                    hMeter('CPU', cpuPct(svc), formatPercent(svc.cpuPercent)) +
                    hMeter('RAM', ramPct(svc), formatRam(svc.memoryBytes)) +
                '</div>' +
                '<div>' +
                    '<div class="small text-muted mb-1">Heat over the last ~2 min</div>' +
                    (path
                        ? '<svg class="au-pulse-spark" viewBox="0 0 160 36" aria-hidden="true"><path d="' + path + '" fill="none" stroke="currentColor" stroke-width="2"></path></svg>'
                        : '<div class="small text-muted">Collecting samples…</div>') +
                '</div>' +
            '</div>';
    }

    function render(payload) {
        var status = $('liveUtilizationStatus');
        var strip = $('livePulseStrip');
        var expanded = $('livePulseExpanded');
        if (!payload || payload.reachable === false) {
            if (status) status.textContent = 'Prometheus unreachable';
            if (strip) strip.innerHTML = '<div class="small px-2 py-2" style="color:rgba(255,255,255,.6)">Prometheus is not reachable. Pulse will retry while this page stays open.</div>';
            renderDetail(null);
            return;
        }
        var services = payload.services || [];
        lastServices = services;
        remember(services);
        if (status) {
            var sampled = payload.sampledAt ? new Date(payload.sampledAt) : new Date();
            var hotCount = services.filter(isHot).length;
            status.textContent = 'Live · ' + sampled.toLocaleTimeString() + ' · ' + hotCount + ' hot · not saved';
        }
        var hottest = services.slice().sort(function (a, b) { return heat(b) - heat(a); })[0];
        var hottestEl = $('livePulseHottest');
        if (hottestEl) {
            hottestEl.textContent = hottest && isHot(hottest)
                ? 'Hottest: ' + (hottest.name || hottest.key) + ' · CPU ' + formatPercent(hottest.cpuPercent) + ' · RAM ' + formatRam(hottest.memoryBytes)
                : 'All services inside the 50% heat line';
        }
        if (!userPinned) {
            if (hottest && isHot(hottest)) selectedKey = hottest.key;
            else selectedKey = null;
        }
        var shown = visibleServices(services);
        if (strip) {
            if (!shown.length) {
                strip.innerHTML = '<div class="small px-2 py-2" style="color:rgba(255,255,255,.6)">Nothing in this filter.</div>';
            } else {
                strip.innerHTML = shown.map(function (svc) {
                    var cls = 'au-pulse-cell';
                    if (svc.group === 'pipeline') cls += ' is-pipeline';
                    if (isHot(svc)) cls += ' is-hot';
                    if (svc.key === selectedKey) cls += ' is-selected';
                    return '<button type="button" class="' + cls + '" data-pulse-key="' + escapeHtml(svc.key) + '" title="' +
                        escapeHtml((svc.name || svc.key) + ' · CPU ' + formatPercent(svc.cpuPercent) + ' · RAM ' + formatRam(svc.memoryBytes) + ' · API p95 ' + formatApi(svc.apiP95Ms)) + '">' +
                        metersHtml(svc) +
                        '<div class="au-pulse-label">' + escapeHtml(shortName(svc.name || svc.key)) + '</div>' +
                        '<div class="au-pulse-read">' + escapeHtml(formatPercent(svc.cpuPercent)) + '</div>' +
                        '</button>';
                }).join('');
            }
        }
        if (expanded) {
            expanded.hidden = view !== 'expanded';
            if (view === 'expanded') {
                expanded.innerHTML = shown.map(function (svc) {
                    var cls = 'au-pulse-mini';
                    if (isHot(svc)) cls += ' is-hot';
                    if (svc.key === selectedKey) cls += ' is-selected';
                    return '<button type="button" class="' + cls + '" data-pulse-key="' + escapeHtml(svc.key) + '">' +
                        '<div class="au-pulse-mini-name">' + escapeHtml(svc.name || svc.key) + '</div>' +
                        hMeter('CPU', cpuPct(svc), formatPercent(svc.cpuPercent)) +
                        hMeter('RAM', ramPct(svc), formatRam(svc.memoryBytes)) +
                        '<div class="small text-muted">API p95 ' + escapeHtml(formatApi(svc.apiP95Ms)) + '</div>' +
                        '</button>';
                }).join('');
            }
        }
        if (strip) strip.hidden = view === 'expanded';
        var selected = services.filter(function (s) { return s.key === selectedKey; })[0] || null;
        renderDetail(selected);
        setSeg('view', view);
        setSeg('filter', filter);
        setSeg('sort', sortMode);
    }

    function findService(key) {
        return lastServices.filter(function (s) { return s.key === key; })[0] || null;
    }

    async function poll() {
        var card = $('liveUtilizationCard');
        if (!card || inFlight || document.hidden) return;
        inFlight = true;
        try {
            var response = await fetch(card.getAttribute('data-url'), { headers: { Accept: 'application/json' }, cache: 'no-store' });
            if (!response.ok) {
                render({ reachable: false, services: [] });
                return;
            }
            render(await response.json());
        } catch (e) {
            render({ reachable: false, services: [] });
        } finally {
            inFlight = false;
        }
    }

    function start() {
        if (timer) return;
        poll();
        timer = setInterval(poll, 5000);
    }
    function stop() {
        if (!timer) return;
        clearInterval(timer);
        timer = null;
    }

    function bind() {
        var card = $('liveUtilizationCard');
        if (!card || card.dataset.pulseBound === 'true') return;
        card.dataset.pulseBound = 'true';
        card.addEventListener('click', function (e) {
            var seg = e.target.closest('[data-pulse-group]');
            if (seg) {
                var group = seg.getAttribute('data-pulse-group');
                var value = seg.getAttribute('data-pulse-value');
                if (group === 'view') { view = value; savePref('view', view); }
                if (group === 'filter') { filter = value; savePref('filter', filter); }
                if (group === 'sort') { sortMode = value; savePref('sort', sortMode); }
                render({ reachable: true, sampledAt: new Date().toISOString(), services: lastServices });
                return;
            }
            var cell = e.target.closest('[data-pulse-key]');
            if (!cell) return;
            var key = cell.getAttribute('data-pulse-key');
            userPinned = true;
            selectedKey = selectedKey === key ? null : key;
            if (!selectedKey) userPinned = false;
            render({ reachable: true, sampledAt: new Date().toISOString(), services: lastServices });
            void findService;
        });
        document.addEventListener('visibilitychange', function () {
            if (document.hidden) stop();
            else start();
        });
        window.addEventListener('pagehide', stop);
        start();
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', bind);
    else
        bind();
})();

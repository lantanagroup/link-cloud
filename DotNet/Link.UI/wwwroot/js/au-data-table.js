(function () {
    function searchHay(row) {
        if (row.dataset.search) return String(row.dataset.search);
        var keys = ['name', 'description', 'profile', 'type', 'method', 'measures', 'family', 'measureid', 'ehr', 'stay', 'seed', 'optype', 'resources', 'contents'];
        var parts = keys.map(function (k) { return row.dataset[k]; }).filter(Boolean);
        if (parts.length) return parts.join(' ');
        return row.innerText || '';
    }

    function unique(values) {
        var seen = {};
        var out = [];
        values.forEach(function (v) {
            if (!v || seen[v]) return;
            seen[v] = true;
            out.push(v);
        });
        return out;
    }

    function rowsOf(table) {
        return Array.prototype.slice.call(table.querySelectorAll('tbody tr')).filter(function (row) {
            return !row.classList.contains('row-group-header')
                && row.querySelector('td')
                && !row.querySelector('td[colspan]');
        });
    }

    function cellText(row, index) {
        var cell = row.children[index];
        return cell ? (cell.innerText || cell.textContent || '').trim() : '';
    }

    function rowValue(row, key, colIndex) {
        if (key && row.dataset[key] != null && row.dataset[key] !== '')
            return row.dataset[key];
        return cellText(row, colIndex);
    }

    function compare(a, b, numeric) {
        if (numeric) {
            var na = parseFloat(String(a).replace(/[^0-9.\-]+/g, ''));
            var nb = parseFloat(String(b).replace(/[^0-9.\-]+/g, ''));
            if (Number.isFinite(na) && Number.isFinite(nb) && na !== nb) return na - nb;
        }
        return String(a || '').localeCompare(String(b || ''), undefined, { numeric: true, sensitivity: 'base' });
    }

    function apply(shell) {
        var table = shell._auTable;
        if (!table) return;
        var q = (shell._auSearch || '').trim().toLowerCase();
        var type = shell._auType || '';
        var extra = shell._auExtra || {};
        var body = table.tBodies[0];
        var list = rowsOf(table);
        var visible = 0;

        list.forEach(function (row) {
            var hay = searchHay(row).toLowerCase();
            var ok = !q || hay.indexOf(q) >= 0;
            if (ok && type && (row.dataset.type || '') !== type) ok = false;
            Object.keys(extra).forEach(function (key) {
                if (!ok) return;
                var want = extra[key];
                if (!want) return;
                if ((row.dataset[key] || '') !== want) ok = false;
            });
            row.classList.toggle('au-row-hidden', !ok);
            if (ok) visible += 1;
        });

        if (shell._auSortKey) {
            var key = shell._auSortKey;
            var dir = shell._auSortDir === 'asc' ? 1 : -1;
            var numeric = shell._auSortNumeric;
            var colIndex = shell._auSortIndex;
            list.sort(function (a, b) {
                return dir * compare(rowValue(a, key, colIndex), rowValue(b, key, colIndex), numeric);
            });
            list.forEach(function (row) { body.appendChild(row); });
        }

        var count = shell.querySelector('.au-table-count');
        if (count) {
            var total = list.length;
            count.textContent = visible === total
                ? total + (total === 1 ? ' row' : ' rows')
                : 'Showing ' + visible + ' of ' + total;
        }

        var empty = shell.querySelector('.au-table-empty');
        if (empty)
            empty.hidden = !(list.length > 0 && visible === 0);
    }

    function ensureToolbar(shell, table) {
        var existing = shell.querySelector('.au-table-toolbar');
        if (existing) {
            shell._auToolbar = existing;
            return existing;
        }
        var bar = document.createElement('div');
        bar.className = 'au-table-toolbar';
        bar.innerHTML =
            '<input type="search" class="form-control form-control-sm au-table-search" placeholder="Filter this table…" aria-label="Filter this table" />' +
            '<select class="form-select form-select-sm au-table-type" hidden aria-label="Filter by type"><option value="">All</option></select>' +
            '<span class="au-table-count"></span>';
        shell.insertBefore(bar, shell.firstChild);
        shell._auToolbar = bar;
        return bar;
    }

    function fillTypeFilter(shell, table) {
        var select = shell.querySelector('.au-table-type');
        if (!select) return;
        var types = unique(rowsOf(table).map(function (row) { return row.dataset.type; }));
        if (types.length < 2) {
            select.hidden = true;
            return;
        }
        var current = select.value;
        select.innerHTML = '<option value="">All</option>' + types.map(function (t) {
            return '<option value="' + t.replace(/"/g, '&quot;') + '">' + t + '</option>';
        }).join('');
        select.value = current;
        select.hidden = false;
    }

    function pageParams() {
        return new URLSearchParams(window.location.search);
    }

    // The first table on a page keeps q/type/sort/dir. Any later table prefixes
    // those keys with its id so one table's search does not reload into the others.
    function queryPrefix(shell) {
        var shells = Array.prototype.slice.call(document.querySelectorAll('[data-au-table]'));
        var index = shells.indexOf(shell);
        if (index <= 0) return '';
        var table = shell._auTable || shell.querySelector('table');
        var id = table && table.id ? table.id : ('t' + index);
        return id + '_';
    }

    function readQuery(shell) {
        var params = pageParams();
        var prefix = queryPrefix(shell);
        function get(key) { return params.get(prefix + key) || ''; }
        var dir = get('dir');
        return {
            q: get('q'),
            type: get('type'),
            sort: get('sort'),
            dir: dir === 'asc' ? 'asc' : (dir === 'desc' ? 'desc' : '')
        };
    }

    function writeQuery(shell) {
        var params = pageParams();
        var prefix = queryPrefix(shell);
        function put(key, value) {
            if (value) params.set(prefix + key, value);
            else params.delete(prefix + key);
        }
        put('q', (shell._auSearch || '').trim());
        put('type', shell._auType || '');
        put('sort', shell._auSortKey || '');
        put('dir', shell._auSortKey ? (shell._auSortDir || '') : '');
        var next = params.toString();
        var url = window.location.pathname + (next ? '?' + next : '') + window.location.hash;
        var current = window.location.pathname + window.location.search + window.location.hash;
        if (url !== current) history.replaceState(history.state, '', url);
    }

    function markSort(shell, key, dir) {
        var table = shell._auTable;
        if (!table) return;
        Array.prototype.forEach.call(table.querySelectorAll('thead th[data-sort]'), function (other) {
            other.removeAttribute('data-sort-dir');
            if (key && other.getAttribute('data-sort') === key) {
                other.setAttribute('data-sort-dir', dir);
                shell._auSortIndex = other.cellIndex;
                shell._auSortNumeric = other.getAttribute('data-sort-type') === 'number' || key === 'updated';
            }
        });
    }

    function restoreQuery(shell) {
        var saved = readQuery(shell);
        shell._auSearch = saved.q;
        shell._auType = saved.type;
        var search = shell.querySelector('.au-table-search');
        if (search) search.value = saved.q;
        var typeSel = shell.querySelector('.au-table-type');
        if (typeSel && saved.type) {
            typeSel.value = saved.type;
            shell._auType = typeSel.value;
        }
        if (saved.sort) {
            shell._auSortKey = saved.sort;
            shell._auSortDir = saved.dir || 'asc';
            markSort(shell, shell._auSortKey, shell._auSortDir);
        }
    }

    function enhance(shell) {
        if (shell.dataset.auTableReady === 'true') return;
        var table = shell.matches('table') ? shell : shell.querySelector('table');
        if (!table) return;
        shell.dataset.auTableReady = 'true';
        if (shell === table) {
            var wrap = document.createElement('div');
            wrap.setAttribute('data-au-table', '');
            table.parentNode.insertBefore(wrap, table);
            wrap.appendChild(table);
            enhance(wrap);
            return;
        }
        shell._auTable = table;
        var toolbar = ensureToolbar(shell, table);
        fillTypeFilter(shell, table);

        var search = toolbar.querySelector('.au-table-search');
        if (search) {
            search.addEventListener('input', function () {
                shell._auSearch = search.value;
                apply(shell);
                writeQuery(shell);
            });
        }
        var typeSel = toolbar.querySelector('.au-table-type');
        if (typeSel) {
            typeSel.addEventListener('change', function () {
                shell._auType = typeSel.value;
                apply(shell);
                writeQuery(shell);
            });
        }

        if (!shell.querySelector('.au-table-empty')) {
            var empty = document.createElement('div');
            empty.className = 'au-table-empty text-center text-muted py-4';
            empty.hidden = true;
            empty.textContent = 'No matching rows.';
            table.parentNode.appendChild(empty);
        }

        Array.prototype.forEach.call(table.querySelectorAll('thead th[data-sort]'), function (th, i) {
            if (th.querySelector('a.au-sort-header')) return;
            th.addEventListener('click', function (e) {
                if (e.target.closest('button, a, input, label, .au-info-toggle')) return;
                var key = th.getAttribute('data-sort');
                var numeric = th.getAttribute('data-sort-type') === 'number' || key === 'updated';
                if (shell._auSortKey === key)
                    shell._auSortDir = shell._auSortDir === 'desc' ? 'asc' : 'desc';
                else {
                    shell._auSortKey = key;
                    shell._auSortDir = th.getAttribute('data-sort-default') || 'asc';
                }
                shell._auSortNumeric = numeric;
                shell._auSortIndex = th.cellIndex;
                Array.prototype.forEach.call(table.querySelectorAll('thead th[data-sort]'), function (other) {
                    other.removeAttribute('data-sort-dir');
                });
                th.setAttribute('data-sort-dir', shell._auSortDir);
                apply(shell);
                writeQuery(shell);
            });
            void i;
        });

        restoreQuery(shell);
        apply(shell);
    }

    function scan(root) {
        (root || document).querySelectorAll('[data-au-table]').forEach(enhance);
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', function () { scan(document); });
    else
        scan(document);

    if (document.body) {
        new MutationObserver(function (records) {
            records.forEach(function (rec) {
                rec.addedNodes.forEach(function (node) {
                    if (!(node instanceof Element)) return;
                    if (node.matches('[data-au-table]')) enhance(node);
                    node.querySelectorAll && node.querySelectorAll('[data-au-table]').forEach(enhance);
                });
            });
        }).observe(document.body, { childList: true, subtree: true });
    }

    window.addEventListener('popstate', function () {
        document.querySelectorAll('[data-au-table]').forEach(function (shell) {
            if (shell.dataset.auTableReady !== 'true') return;
            restoreQuery(shell);
            apply(shell);
        });
    });

    window.AuDataTable = { scan: scan, apply: apply };
})();

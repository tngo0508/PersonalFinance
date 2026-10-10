// Spreadsheet Transactions modal on the Google Drive page.
// Server values come from data- attributes on #transactionsModal (data-transactions-url, data-connection-id).
(function ($) {
    'use strict';

    $(function () {
        const $modal = $('#transactionsModal');
        if ($modal.length === 0) return;

        // Same series colors as the budget report (validated pair: slot 1 blue, slot 2 orange)
        const TX_COLORS = { spend: '#eb6834', grid: '#e1e0d9', axis: '#c3c2b7', muted: '#898781' };
        const TOP_CATEGORY_COUNT = 6;
        const DAY_MS = 24 * 60 * 60 * 1000;

        let txTable = null;
        let txChart = null;
        let currentTx = null;
        let lastTxRequest = null;
        let txRequestSeq = 0;
        const filters = { type: '', category: '', search: '' };

        function formatCurrency(num) {
            const n = Number(num) || 0;
            const formatted = '$' + Math.abs(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            return n < 0 ? '-' + formatted : formatted;
        }

        // Whole dollars for tiles and prose where cents are noise
        function formatMoney(num) {
            const n = Number(num) || 0;
            const formatted = '$' + Math.abs(n).toLocaleString('en-US', { maximumFractionDigits: 0 });
            return n < 0 ? '-' + formatted : formatted;
        }

        function formatCompactCurrency(val) {
            const abs = Math.abs(val);
            if (abs >= 1000000) return '$' + (abs / 1000000).toFixed(1).replace(/\.0$/, '') + 'M';
            if (abs >= 1000) return '$' + (abs / 1000).toFixed(1).replace(/\.0$/, '') + 'K';
            return '$' + abs.toLocaleString('en-US');
        }

        function escapeHtml(value) {
            return String(value === null || value === undefined ? '' : value)
                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
        }

        function toDate(iso) {
            if (!iso) return null;
            const d = new Date(iso);
            return isNaN(d) ? null : d;
        }

        function formatDate(iso, withYear) {
            const d = toDate(iso);
            if (!d) return '';
            return d.toLocaleDateString('en-US', withYear === false
                ? { month: 'short', day: 'numeric' }
                : { year: 'numeric', month: 'short', day: 'numeric' });
        }

        function dayKey(d) {
            return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
        }

        function isIncome(tx) {
            return tx.type === 'Income';
        }

        function plural(n, word) {
            return n + ' ' + word + (n === 1 ? '' : 's');
        }

        function showTxState(state) {
            $('#txLoadingState').toggleClass('d-none', state !== 'loading');
            $('#txErrorState').toggleClass('d-none', state !== 'error');
            $('#txContent').toggleClass('d-none', state !== 'content');
            $('#btnExportTx').prop('disabled', state !== 'content');
        }

        function destroyTxViews() {
            if (txTable) {
                txTable.destroy();
                txTable = null;
            }
            if (txChart) {
                txChart.destroy();
                txChart = null;
            }
            $('#txTable tbody').empty();
        }

        // ---------- Summary ----------

        function categoryTotals(expenses) {
            const map = {};
            expenses.forEach(function (t) {
                const key = t.category || 'Uncategorized';
                if (!map[key]) map[key] = { name: key, total: 0, count: 0 };
                map[key].total += t.amount;
                map[key].count += 1;
            });
            return Object.keys(map).map(function (k) { return map[k]; }).sort(function (a, b) { return b.total - a.total; });
        }

        function renderSummary(data) {
            const txs = data.transactions;
            const expenses = txs.filter(function (t) { return !isIncome(t); });
            const incomes = txs.filter(isIncome);
            const first = toDate(data.firstDate);
            const last = toDate(data.lastDate);
            const days = first && last ? Math.round((last - first) / DAY_MS) + 1 : 0;
            const net = data.totalIncome - data.totalExpenses;

            $('#txKpiExpenses').text(formatMoney(data.totalExpenses));
            $('#txKpiExpensesSub').text(days > 1
                ? plural(expenses.length, 'purchase') + ' · avg ' + formatMoney(data.totalExpenses / days) + '/day'
                : plural(expenses.length, 'purchase'));
            $('#txKpiIncome').text(formatMoney(data.totalIncome));
            $('#txKpiIncomeSub').text(plural(incomes.length, 'deposit'));
            $('#txKpiNet').text(formatMoney(net));
            $('#txKpiNetSub').text(data.totalIncome > 0
                ? (net >= 0 ? 'Kept ' + Math.round(net / data.totalIncome * 100) + '% of income' : 'Spent more than came in')
                : 'Income minus spending');
            $('#txKpiCount').text(txs.length.toLocaleString('en-US'));
            $('#txKpiRange').text(first && last ? formatDate(data.firstDate) + ' – ' + formatDate(data.lastDate) : 'No dates recorded');

            // Highlights
            const facts = [];
            const largest = expenses.slice().sort(function (a, b) { return b.amount - a.amount; })[0];
            if (largest) {
                facts.push(['Largest expense', escapeHtml(largest.description || largest.category || 'Expense') + ' · ' + formatMoney(largest.amount) +
                    (largest.date ? ' on ' + formatDate(largest.date, false) : '')]);
            }
            const cats = categoryTotals(expenses);
            if (cats.length && data.totalExpenses > 0) {
                facts.push(['Top category', escapeHtml(cats[0].name) + ' · ' + Math.round(cats[0].total / data.totalExpenses * 100) + '% of spending']);
                const frequent = cats.slice().sort(function (a, b) { return b.count - a.count || b.total - a.total; })[0];
                if (frequent.count > 1) {
                    facts.push(['Most frequent', escapeHtml(frequent.name) + ' · ' + plural(frequent.count, 'purchase')]);
                }
            }
            $('#txInsights').html(facts.map(function (f) {
                return '<li><span class="bgt-facts-label">' + f[0] + '</span><span class="bgt-facts-value">' + f[1] + '</span></li>';
            }).join('')).toggleClass('d-none', facts.length === 0);

            renderCategoryRank(cats, data.totalExpenses);
            renderTimeline(expenses, first, last);

            // Category filter options
            const allCategories = Array.from(new Set(txs.map(function (t) { return t.category; }).filter(Boolean))).sort();
            const $select = $('#txCategoryFilter').empty().append('<option value="">All categories</option>');
            allCategories.forEach(function (c) {
                $select.append($('<option>').val(c).text(c));
            });
        }

        // Ranked list of spending categories; the tail folds into "Other" so the list stays scannable
        function renderCategoryRank(cats, totalExpenses) {
            const $list = $('#txCategoryBars');
            if (cats.length === 0 || !(totalExpenses > 0)) {
                $list.html('<div class="text-muted small">No spending recorded.</div>');
                return;
            }
            let rows = cats.slice(0, TOP_CATEGORY_COUNT);
            const rest = cats.slice(TOP_CATEGORY_COUNT);
            if (rest.length) {
                rows = rows.concat([{
                    name: 'Other (' + rest.length + ')',
                    total: rest.reduce(function (s, c) { return s + c.total; }, 0),
                    count: rest.reduce(function (s, c) { return s + c.count; }, 0),
                    isOther: true
                }]);
            }
            const max = rows.reduce(function (m, c) { return Math.max(m, c.total); }, 0) || 1;
            $list.html(rows.map(function (c) {
                const share = Math.round(c.total / totalExpenses * 100);
                const inner = '<span class="bgt-rank-head"><span class="bgt-rank-name">' + escapeHtml(c.name) + '</span>' +
                    '<span class="bgt-rank-value">' + formatMoney(c.total) + ' <small>' + share + '%</small></span></span>' +
                    '<span class="bgt-rank-track"><span class="bgt-rank-fill" style="width:' + (c.total / max * 100).toFixed(1) + '%"></span></span>';
                // "Other" groups several categories, so it isn't a filter target
                return c.isOther
                    ? '<div class="bgt-rank-row bgt-rank-row--static">' + inner + '</div>'
                    : '<button type="button" class="bgt-rank-row" data-category="' + escapeHtml(c.name === 'Uncategorized' ? '' : c.name) + '"' +
                      ' aria-pressed="false" title="Show only ' + escapeHtml(c.name) + '">' + inner + '</button>';
            }).join(''));
        }

        // Spending per day for ranges up to two months, otherwise per month
        function renderTimeline(expenses, first, last) {
            const dated = expenses.filter(function (t) { return toDate(t.date); });
            const $box = $('#txTimelineChart').closest('.bgt-chart-box');
            $box.find('.bgt-chart-empty').remove();
            if (dated.length === 0 || !first || !last) {
                $('#txTimelineSub').text('');
                $box.append('<div class="bgt-chart-empty">No dated spending to chart.</div>');
                return;
            }

            const byDay = (last - first) / DAY_MS <= 62;
            const buckets = [];
            const index = {};
            if (byDay) {
                for (let d = new Date(first.getFullYear(), first.getMonth(), first.getDate()); d <= last; d = new Date(d.getTime() + DAY_MS)) {
                    const key = dayKey(d);
                    index[key] = buckets.length;
                    buckets.push({ label: d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }), total: 0, count: 0 });
                }
            } else {
                for (let d = new Date(first.getFullYear(), first.getMonth(), 1); d <= last; d = new Date(d.getFullYear(), d.getMonth() + 1, 1)) {
                    const key = d.getFullYear() + '-' + d.getMonth();
                    index[key] = buckets.length;
                    buckets.push({ label: d.toLocaleDateString('en-US', { month: 'short', year: 'numeric' }), total: 0, count: 0 });
                }
            }
            dated.forEach(function (t) {
                const d = toDate(t.date);
                const key = byDay ? dayKey(d) : d.getFullYear() + '-' + d.getMonth();
                const b = buckets[index[key]];
                if (b) { b.total += t.amount; b.count += 1; }
            });

            const peak = buckets.reduce(function (a, b) { return b.total > a.total ? b : a; });
            $('#txTimelineSub').text((byDay ? 'Daily' : 'Monthly') + ' totals · highest: ' + peak.label + ' (' + formatMoney(peak.total) + ')');

            txChart = new Chart(document.getElementById('txTimelineChart').getContext('2d'), {
                type: 'bar',
                data: {
                    labels: buckets.map(function (b) { return b.label; }),
                    datasets: [{
                        label: 'Spending',
                        data: buckets.map(function (b) { return b.total; }),
                        backgroundColor: TX_COLORS.spend,
                        borderRadius: 4,
                        borderSkipped: 'start',
                        maxBarThickness: 28,
                        categoryPercentage: 0.8,
                        barPercentage: 0.9
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: { display: false },
                        tooltip: {
                            backgroundColor: '#0b0b0b',
                            padding: 10,
                            cornerRadius: 8,
                            callbacks: {
                                label: function (ctx) { return ' Spent ' + formatCurrency(ctx.parsed.y); },
                                afterLabel: function (ctx) {
                                    const n = buckets[ctx.dataIndex].count;
                                    return n ? ' ' + plural(n, 'purchase') : ' No purchases';
                                }
                            }
                        }
                    },
                    scales: {
                        x: {
                            grid: { display: false },
                            border: { color: TX_COLORS.axis },
                            ticks: { color: TX_COLORS.muted, maxRotation: 0, autoSkip: true, autoSkipPadding: 12 }
                        },
                        y: {
                            beginAtZero: true,
                            grid: { color: TX_COLORS.grid },
                            border: { display: false },
                            ticks: { color: TX_COLORS.muted, maxTicksLimit: 5, callback: function (v) { return formatCompactCurrency(v); } }
                        }
                    }
                }
            });
        }

        // ---------- Table & filters ----------

        function exactPattern(value) {
            return value ? '^' + DataTable.util.escapeRegex(value) + '$' : '';
        }

        function applyFilters() {
            if (!txTable) return;
            txTable.column(3).search(exactPattern(filters.type), true, false);
            txTable.column(2).search(exactPattern(filters.category), true, false);
            txTable.search(filters.search).draw();

            $('.tx-type-filter').each(function () {
                const active = String($(this).data('type') || '') === filters.type;
                $(this).toggleClass('active', active).attr('aria-pressed', String(active));
            });
            $('#txCategoryFilter').val(filters.category);
            $('#txCategoryBars .bgt-rank-row[data-category]').each(function () {
                const active = filters.category !== '' && $(this).attr('data-category') === filters.category;
                $(this).toggleClass('active', active).attr('aria-pressed', String(active));
            });
            $('#txClearFilters').toggleClass('d-none', !filters.type && !filters.category && !filters.search);
        }

        // Totals for whatever the filters currently show
        function renderFilterSummary() {
            if (!txTable || !currentTx) return;
            const rows = txTable.rows({ search: 'applied' }).data().toArray();
            const spent = rows.filter(function (t) { return !isIncome(t); }).reduce(function (s, t) { return s + t.amount; }, 0);
            const earned = rows.filter(isIncome).reduce(function (s, t) { return s + t.amount; }, 0);
            const total = currentTx.transactions.length;
            const parts = [
                rows.length === total ? 'Showing all ' + plural(total, 'transaction') : 'Showing ' + rows.length + ' of ' + total,
                'Spending <strong>' + formatCurrency(spent) + '</strong>'
            ];
            if (earned > 0) parts.push('Income <strong>' + formatCurrency(earned) + '</strong>');
            $('#txFilterSummary').html(parts.join('<span aria-hidden="true"> · </span>'));
        }

        function renderTable(transactions) {
            txTable = $('#txTable').DataTable({
                data: transactions,
                pageLength: 25,
                lengthMenu: [[10, 25, 50, 100, -1], [10, 25, 50, 100, 'All']],
                order: [[0, 'desc']],
                autoWidth: false,
                columns: [
                    {
                        data: 'date',
                        className: 'text-nowrap text-muted text-start',
                        render: function (value, type, row) {
                            if (type === 'sort' || type === 'type') return value || '';
                            return escapeHtml(formatDate(value) || row.dateText || '—');
                        }
                    },
                    {
                        data: 'description',
                        render: function (value, type, row) {
                            if (type !== 'display') return value || '';
                            // Category repeats under the description on narrow screens, where its column is hidden
                            return '<div class="fw-semibold">' + escapeHtml(value || '—') + '</div>' +
                                '<div class="small text-muted d-md-none">' + escapeHtml(row.category || 'Uncategorized') + '</div>';
                        }
                    },
                    {
                        data: 'category',
                        className: 'd-none d-md-table-cell',
                        render: function (value, type) {
                            if (type !== 'display') return value || '';
                            return '<span class="bgt-chip">' + escapeHtml(value || 'Uncategorized') + '</span>';
                        }
                    },
                    { data: 'type', visible: false },
                    {
                        data: 'amount',
                        className: 'text-end text-nowrap fw-semibold',
                        render: function (value, type, row) {
                            if (type !== 'display') return value;
                            return isIncome(row)
                                ? '<span class="bgt-amount-in">+' + formatCurrency(value) + '</span>'
                                : formatCurrency(value);
                        }
                    }
                ],
                language: {
                    emptyTable: 'No transactions found.',
                    zeroRecords: 'No transactions match your filters.',
                    info: '_START_–_END_ of _TOTAL_',
                    infoEmpty: '',
                    infoFiltered: '',
                    lengthMenu: 'Show _MENU_'
                },
                // Search and filters live in the toolbar above the table
                dom: "<'row'<'col-12'tr>>" +
                     "<'row align-items-center mt-3 small text-muted'<'col-sm-12 col-md-4'i><'col-sm-12 col-md-3'l><'col-sm-12 col-md-5'p>>",
                drawCallback: renderFilterSummary
            });
        }

        function resetFilters() {
            filters.type = '';
            filters.category = '';
            filters.search = '';
            $('#txSearch').val('');
        }

        // ---------- Loading ----------

        function loadTransactions(request) {
            lastTxRequest = request;
            const seq = ++txRequestSeq;
            destroyTxViews();
            currentTx = null;
            resetFilters();

            $('#txModalFileName').text(request.fileName).attr('title', request.fileName);
            $('#txModalDataSource').empty();
            $('#txSheetName').text('');
            $('#btnTxToBudget')
                .toggleClass('d-none', !request.isBudget)
                .attr({ 'data-file-id': request.fileId, 'data-file-name': request.fileName })
                .data({ fileId: request.fileId, fileName: request.fileName });
            showTxState('loading');

            $.ajax({
                url: $modal.data('transactions-url'),
                method: 'GET',
                data: { fileId: request.fileId, fileName: request.fileName, connectionId: request.connectionId },
                success: function (data) {
                    if (seq !== txRequestSeq) return; // a newer request superseded this one

                    if (!data || !data.transactions || data.transactions.length === 0) {
                        $('#txErrorMessage').text((data && data.errorMessage) || 'Make sure the spreadsheet has a sheet with Date and Amount column headers.');
                        showTxState('error');
                        return;
                    }

                    currentTx = data;
                    const sourceClass = data.isLiveSpreadsheetData ? 'bgt-source--live' : 'bgt-source--sample';
                    $('#txModalDataSource').html('<span class="bgt-source ' + sourceClass + '">' + escapeHtml(data.dataSource || 'Google Spreadsheet') + '</span>');
                    $('#txSheetName').text(data.sheetName ? 'Read from sheet: ' + data.sheetName : '');

                    showTxState('content');
                    renderSummary(data);
                    renderTable(data.transactions);
                    applyFilters();
                },
                error: function (xhr) {
                    if (seq !== txRequestSeq) return;
                    const message = xhr.responseJSON && xhr.responseJSON.error;
                    $('#txErrorMessage').text(message || 'Check that the drive is still shared, then try again.');
                    showTxState('error');
                }
            });
        }

        // "Transactions" buttons on the page, plus the link from the budget report
        $(document).on('click', '.btn-sheet-transactions', function () {
            const $btn = $(this);
            loadTransactions({
                fileName: $btn.attr('data-file-name'),
                fileId: $btn.attr('data-file-id'),
                isBudget: $btn.attr('data-is-budget') === 'true',
                connectionId: $modal.data('connection-id')
            });
        });

        $('#btnTxRetry').on('click', function () {
            if (lastTxRequest) loadTransactions(lastTxRequest);
        });

        $(document).on('click', '.tx-type-filter', function () {
            filters.type = String($(this).data('type') || '');
            applyFilters();
        });

        $('#txCategoryFilter').on('change', function () {
            filters.category = $(this).val();
            applyFilters();
        });

        // Clicking a ranked category toggles it as the filter
        $(document).on('click', '#txCategoryBars .bgt-rank-row[data-category]', function () {
            const category = $(this).attr('data-category');
            filters.category = filters.category === category ? '' : category;
            applyFilters();
        });

        let searchTimer = null;
        $('#txSearch').on('input', function () {
            const value = $(this).val().trim();
            clearTimeout(searchTimer);
            searchTimer = setTimeout(function () {
                filters.search = value;
                applyFilters();
            }, 150);
        });

        $('#txClearFilters').on('click', function () {
            resetFilters();
            applyFilters();
        });

        // Export the rows the current filters show
        $('#btnExportTx').on('click', function () {
            if (!txTable || !currentTx) return;
            const rows = [['Date', 'Description', 'Category', 'Type', 'Amount']];
            txTable.rows({ search: 'applied', order: 'applied' }).data().toArray().forEach(function (t) {
                const d = toDate(t.date);
                rows.push([d ? dayKey(d) : (t.dateText || ''), t.description, t.category, isIncome(t) ? 'Income' : 'Spending', t.amount.toFixed(2)]);
            });
            const csv = rows.map(function (r) {
                return r.map(function (v) {
                    const text = String(v === null || v === undefined ? '' : v);
                    return /[",\n]/.test(text) ? '"' + text.replace(/"/g, '""') + '"' : text;
                }).join(',');
            }).join('\r\n');
            // BOM so Excel opens the file as UTF-8
            const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' });
            const link = document.createElement('a');
            link.href = URL.createObjectURL(blob);
            link.download = String(currentTx.fileName || 'transactions').replace(/\.[^.]+$/, '') + ' - transactions.csv';
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(function () { URL.revokeObjectURL(link.href); }, 1000);
        });

        // Tables and charts built during the open animation measure zero width; re-measure once visible
        $modal.on('shown.bs.modal', function () {
            if (txTable) txTable.columns.adjust();
            if (txChart) txChart.resize();
        });

        $modal.on('hidden.bs.modal', function () {
            txRequestSeq++;
            destroyTxViews();
        });
    });
})(jQuery);

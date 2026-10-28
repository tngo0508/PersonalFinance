/* global PFI18n */
// Spreadsheet Transactions modal on the Google Drive page.
// Server values come from data- attributes on #transactionsModal (data-transactions-url, data-connection-id).
(function ($) {
    'use strict';

    $(() => {
        const $modal = $('#transactionsModal');
        if ($modal.length === 0) { return; }

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
        // The spreadsheet's currency, chosen when its folder was connected (ADR 0016); read from each result.
        let currentCurrency = 'USD';

        // Reference to shared utilities
        const PF = window.PFUtils;

        function formatCurrency (num) {
            return PFI18n.formatMoney(Number(num) || 0, currentCurrency);
        }

        // Whole units for tiles and prose where minor units are noise
        function formatMoney (num) {
            return PFI18n.formatMoney(Number(num) || 0, currentCurrency, { whole: true });
        }

        function formatCompactCurrency (val) {
            return PFI18n.formatCompactMoney(val, currentCurrency);
        }

        function escapeHtml (value) {
            return PF.escapeHtml(value);
        }

        function toDate (iso) {
            if (!iso) { return null; }
            const d = new Date(iso);
            return isNaN(d) ? null : d;
        }

        function formatDate (iso, withYear) {
            const d = toDate(iso);
            if (!d) { return ''; }
            return PFI18n.formatDate(d, withYear === false
                ? { month: 'short', day: 'numeric' }
                : { year: 'numeric', month: 'short', day: 'numeric' });
        }

        function dayKey (d) {
            return `${d.getFullYear() }-${ String(d.getMonth() + 1).padStart(2, '0') }-${ String(d.getDate()).padStart(2, '0')}`;
        }

        function isIncome (tx) {
            return tx.type === 'Income';
        }

        // "3 purchases", "1 deposit", ... in the page language
        function plural (n, key) {
            return PFI18n.plural(`GoogleDrive.Js.Tx.${ key }`, n);
        }

        function formatPercent (value) {
            return PFI18n.t('GoogleDrive.Js.Percent', { value: PFI18n.formatNumber(value) });
        }

        function sourceLabel (data) {
            return PFI18n.t(data.isLiveSpreadsheetData ? 'GoogleDrive.Js.Source.Live' : 'GoogleDrive.Js.Source.Sample');
        }

        function showTxState (state) {
            $('#txLoadingState').toggleClass('d-none', state !== 'loading');
            $('#txErrorState').toggleClass('d-none', state !== 'error');
            $('#txContent').toggleClass('d-none', state !== 'content');
            $('#btnExportTx').prop('disabled', state !== 'content');
        }

        function destroyTxViews () {
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

        // Totals per category; key is the category as stored ('' when none), name is what the page shows
        function categoryTotals (expenses) {
            const map = {};
            expenses.forEach((t) => {
                const key = t.category || '';
                if (!map[key]) { map[key] = { key: key, name: key || PFI18n.t('GoogleDrive.Js.Tx.Uncategorized'), total: 0, count: 0 }; }
                map[key].total += t.amount;
                map[key].count += 1;
            });
            return Object.keys(map).map((k) => { return map[k]; }).sort((a, b) => { return b.total - a.total; });
        }

        function renderSummary (data) {
            const txs = data.transactions;
            const expenses = txs.filter((t) => { return !isIncome(t); });
            const incomes = txs.filter(isIncome);
            const first = toDate(data.firstDate);
            const last = toDate(data.lastDate);
            const days = first && last ? Math.round((last - first) / DAY_MS) + 1 : 0;
            const net = data.totalIncome - data.totalExpenses;

            $('#txKpiExpenses').text(formatMoney(data.totalExpenses));
            $('#txKpiExpensesSub').text(days > 1
                ? PFI18n.t('GoogleDrive.Js.Tx.PurchasesPerDay', { purchases: plural(expenses.length, 'Purchases'), amount: formatMoney(data.totalExpenses / days) })
                : plural(expenses.length, 'Purchases'));
            $('#txKpiIncome').text(formatMoney(data.totalIncome));
            $('#txKpiIncomeSub').text(plural(incomes.length, 'Deposits'));
            $('#txKpiNet').text(formatMoney(net));
            $('#txKpiNetSub').text(data.totalIncome > 0
                ? (net >= 0
                    ? PFI18n.t('GoogleDrive.Js.Tx.KeptShare', { percent: formatPercent(Math.round(net / data.totalIncome * 100)) })
                    : PFI18n.t('GoogleDrive.Js.Tx.SpentMoreThanIncome'))
                : PFI18n.t('GoogleDrive.Js.IncomeMinusSpending'));
            $('#txKpiCount').text(PFI18n.formatNumber(txs.length));
            $('#txKpiRange').text(first && last
                ? PFI18n.t('GoogleDrive.Js.Tx.Range', { from: formatDate(data.firstDate), to: formatDate(data.lastDate) })
                : PFI18n.t('GoogleDrive.Js.Tx.NoDates'));

            // Highlights
            const facts = [];
            const largest = expenses.slice().sort((a, b) => { return b.amount - a.amount; })[0];
            if (largest) {
                const largestArgs = {
                    name: largest.description || largest.category || PFI18n.t('GoogleDrive.Js.Tx.Expense'),
                    amount: formatMoney(largest.amount)
                };
                if (largest.date) { largestArgs.date = formatDate(largest.date, false); }
                facts.push([PFI18n.t('GoogleDrive.Js.Tx.LargestExpense'),
                    PFI18n.t(largest.date ? 'GoogleDrive.Js.Tx.LargestValueOn' : 'GoogleDrive.Js.Tx.LargestValue', largestArgs)]);
            }
            const cats = categoryTotals(expenses);
            if (cats.length && data.totalExpenses > 0) {
                facts.push([PFI18n.t('GoogleDrive.Js.Tx.TopCategory'), PFI18n.t('GoogleDrive.Js.Tx.TopCategoryValue', {
                    name: cats[0].name,
                    percent: formatPercent(Math.round(cats[0].total / data.totalExpenses * 100))
                })]);
                const frequent = cats.slice().sort((a, b) => { return b.count - a.count || b.total - a.total; })[0];
                if (frequent.count > 1) {
                    facts.push([PFI18n.t('GoogleDrive.Js.Tx.MostFrequent'),
                        PFI18n.t('GoogleDrive.Js.Tx.FrequentValue', { name: frequent.name, purchases: plural(frequent.count, 'Purchases') })]);
                }
            }
            // Facts are plain text (names are the user's own), so both parts are escaped
            $('#txInsights').html(facts.map((f) => {
                return `<li><span class="bgt-facts-label">${ escapeHtml(f[0]) }</span><span class="bgt-facts-value">${ escapeHtml(f[1]) }</span></li>`;
            }).join('')).toggleClass('d-none', facts.length === 0);

            renderCategoryRank(cats, data.totalExpenses);
            renderTimeline(expenses, first, last);

            // Category filter options
            const allCategories = Array.from(new Set(txs.map((t) => { return t.category; }).filter(Boolean))).sort();
            const $select = $('#txCategoryFilter').empty().append($('<option value="">').text(PFI18n.t('GoogleDrive.Js.Tx.AllCategories')));
            allCategories.forEach((c) => {
                $select.append($('<option>').val(c).text(c));
            });
        }

        // Ranked list of spending categories; the tail folds into "Other" so the list stays scannable. The rows are content:
        // the All categories select is the one category filter.
        function renderCategoryRank (cats, totalExpenses) {
            const $list = $('#txCategoryBars');
            if (cats.length === 0 || !(totalExpenses > 0)) {
                $list.html($('<div class="text-muted small">').text(PFI18n.t('GoogleDrive.Js.Tx.NoSpending')));
                return;
            }
            let rows = cats.slice(0, TOP_CATEGORY_COUNT);
            const rest = cats.slice(TOP_CATEGORY_COUNT);
            if (rest.length) {
                rows = rows.concat([{
                    name: PFI18n.t('GoogleDrive.Js.Tx.OtherCount', { count: PFI18n.formatNumber(rest.length) }),
                    total: rest.reduce((s, c) => { return s + c.total; }, 0),
                    count: rest.reduce((s, c) => { return s + c.count; }, 0)
                }]);
            }
            const max = rows.reduce((m, c) => { return Math.max(m, c.total); }, 0) || 1;
            $list.html(rows.map((c) => {
                const share = Math.round(c.total / totalExpenses * 100);
                return '<div class="bgt-rank-row">' +
                    `<span class="bgt-rank-head"><span class="bgt-rank-name">${ escapeHtml(c.name) }</span>` +
                    `<span class="bgt-rank-value">${ formatMoney(c.total) } <small>${ formatPercent(share) }</small></span></span>` +
                    `<span class="bgt-rank-track"><span class="bgt-rank-fill" style="width:${ (c.total / max * 100).toFixed(1) }%"></span></span>` +
                    '</div>';
            }).join(''));
        }

        // Spending per day for ranges up to two months, otherwise per month
        function renderTimeline (expenses, first, last) {
            const dated = expenses.filter((t) => { return toDate(t.date); });
            const $box = $('#txTimelineChart').closest('.bgt-chart-box');
            $box.find('.bgt-chart-empty').remove();
            if (dated.length === 0 || !first || !last) {
                $('#txTimelineSub').text('');
                $box.append($('<div class="bgt-chart-empty">').text(PFI18n.t('GoogleDrive.Js.Tx.NoDatedSpending')));
                return;
            }

            const byDay = (last - first) / DAY_MS <= 62;
            const buckets = [];
            const index = {};
            if (byDay) {
                for (let d = new Date(first.getFullYear(), first.getMonth(), first.getDate()); d <= last; d = new Date(d.getTime() + DAY_MS)) {
                    const key = dayKey(d);
                    index[key] = buckets.length;
                    buckets.push({ label: PFI18n.formatDate(d, { month: 'short', day: 'numeric' }), total: 0, count: 0 });
                }
            } else {
                for (let d = new Date(first.getFullYear(), first.getMonth(), 1); d <= last; d = new Date(d.getFullYear(), d.getMonth() + 1, 1)) {
                    const key = `${d.getFullYear() }-${ d.getMonth()}`;
                    index[key] = buckets.length;
                    buckets.push({ label: PFI18n.formatDate(d, { month: 'short', year: 'numeric' }), total: 0, count: 0 });
                }
            }
            dated.forEach((t) => {
                const d = toDate(t.date);
                const key = byDay ? dayKey(d) : `${d.getFullYear() }-${ d.getMonth()}`;
                const b = buckets[index[key]];
                if (b) { b.total += t.amount; b.count += 1; }
            });

            const peak = buckets.reduce((a, b) => { return b.total > a.total ? b : a; });
            $('#txTimelineSub').text(PFI18n.t(byDay ? 'GoogleDrive.Js.Tx.TimelineDaily' : 'GoogleDrive.Js.Tx.TimelineMonthly',
                { label: peak.label, amount: formatMoney(peak.total) }));

            txChart = new Chart(document.getElementById('txTimelineChart').getContext('2d'), {
                type: 'bar',
                data: {
                    labels: buckets.map((b) => { return b.label; }),
                    datasets: [{
                        label: PFI18n.t('GoogleDrive.Js.Spending'),
                        data: buckets.map((b) => { return b.total; }),
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
                                label: function (ctx) { return ` ${ PFI18n.t('GoogleDrive.Js.Tx.TooltipSpent', { amount: formatCurrency(ctx.parsed.y) }) }`; },
                                afterLabel: function (ctx) {
                                    const n = buckets[ctx.dataIndex].count;
                                    return ` ${ n ? plural(n, 'Purchases') : PFI18n.t('GoogleDrive.Js.Tx.NoPurchases') }`;
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

        function exactPattern (value) {
            return value ? `^${ DataTable.util.escapeRegex(value) }$` : '';
        }

        function applyFilters () {
            if (!txTable) { return; }
            txTable.column(3).search(exactPattern(filters.type), true, false);
            txTable.column(2).search(exactPattern(filters.category), true, false);
            txTable.search(filters.search).draw();

            $('.tx-type-filter').each(function () {
                const active = String($(this).data('type') || '') === filters.type;
                $(this).toggleClass('active', active).attr('aria-pressed', String(active));
            });
            $('#txCategoryFilter').val(filters.category);
            $('#txClearFilters').toggleClass('d-none', !filters.type && !filters.category && !filters.search);
        }

        // Totals for whatever the filters currently show
        function renderFilterSummary () {
            if (!txTable || !currentTx) { return; }
            const rows = txTable.rows({ search: 'applied' }).data().toArray();
            const spent = rows.filter((t) => { return !isIncome(t); }).reduce((s, t) => { return s + t.amount; }, 0);
            const earned = rows.filter(isIncome).reduce((s, t) => { return s + t.amount; }, 0);
            const total = currentTx.transactions.length;
            const parts = [
                escapeHtml(rows.length === total
                    ? PFI18n.plural('GoogleDrive.Js.Tx.ShowingAll', total)
                    : PFI18n.t('GoogleDrive.Js.Tx.ShowingSome', { shown: PFI18n.formatNumber(rows.length), total: PFI18n.formatNumber(total) })),
                // Templates are the app's own text; only the amount is markup
                PFI18n.t('GoogleDrive.Js.Tx.SummarySpending', { amount: `<strong>${ escapeHtml(formatCurrency(spent)) }</strong>` })
            ];
            if (earned > 0) { parts.push(PFI18n.t('GoogleDrive.Js.Tx.SummaryIncome', { amount: `<strong>${ escapeHtml(formatCurrency(earned)) }</strong>` })); }
            $('#txFilterSummary').html(parts.join('<span aria-hidden="true"> · </span>'));
        }

        function renderTable (transactions) {
            txTable = $('#txTable').DataTable({
                data: transactions,
                pageLength: 25,
                order: [[0, 'desc']],
                autoWidth: false,
                columns: [
                    {
                        data: 'date',
                        className: 'text-nowrap text-muted text-start',
                        render: function (value, type, row) {
                            if (type === 'sort' || type === 'type') { return value || ''; }
                            return escapeHtml(formatDate(value) || row.dateText || '—');
                        }
                    },
                    {
                        data: 'description',
                        render: function (value, type, row) {
                            if (type !== 'display') { return value || ''; }
                            // Category repeats under the description on narrow screens, where its column is hidden
                            return `<div class="fw-semibold">${ escapeHtml(value || '—') }</div>` +
                                `<div class="small text-muted d-md-none">${ escapeHtml(row.category || PFI18n.t('GoogleDrive.Js.Tx.Uncategorized')) }</div>`;
                        }
                    },
                    {
                        data: 'category',
                        className: 'd-none d-md-table-cell',
                        render: function (value, type) {
                            if (type !== 'display') { return value || ''; }
                            return `<span class="bgt-chip">${ escapeHtml(value || PFI18n.t('GoogleDrive.Js.Tx.Uncategorized')) }</span>`;
                        }
                    },
                    { data: 'type', visible: false },
                    {
                        data: 'amount',
                        className: 'text-end text-nowrap fw-semibold',
                        render: function (value, type, row) {
                            if (type !== 'display') { return value; }
                            return isIncome(row)
                                ? `<span class="bgt-amount-in">+${ formatCurrency(value) }</span>`
                                : formatCurrency(value);
                        }
                    }
                ],
                // DataTables fills in its own _START_, _END_ and _TOTAL_ tokens
                language: {
                    emptyTable: PFI18n.t('GoogleDrive.Js.Tx.TableEmpty'),
                    zeroRecords: PFI18n.t('GoogleDrive.Js.Tx.TableNoMatches'),
                    info: PFI18n.t('GoogleDrive.Js.Tx.TableInfo', { start: '_START_', end: '_END_', total: '_TOTAL_' }),
                    infoEmpty: '',
                    infoFiltered: ''
                },
                // Search and filters live in the toolbar above the table, so only table, info and paging here
                dom: "<'row'<'col-12'tr>>" +
                     "<'row align-items-center mt-3 small text-muted'<'col-sm-12 col-md-5'i><'col-sm-12 col-md-7 d-flex justify-content-md-end'p>>",
                drawCallback () {
                    // A pager with one page offers nothing to click, so it shows only when the rows span pages
                    const api = this.api();
                    $(api.table().container()).find('.dt-paging').toggleClass('d-none', api.page.info().pages <= 1);
                    renderFilterSummary();
                }
            });
        }

        function resetFilters () {
            filters.type = '';
            filters.category = '';
            filters.search = '';
            $('#txSearch').val('');
        }

        // ---------- Loading ----------

        function loadTransactions (request) {
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
                    if (seq !== txRequestSeq) { return; } // a newer request superseded this one

                    if (!data || !data.transactions || data.transactions.length === 0) {
                        const message = (data && data.errorMessage) || PFI18n.t('GoogleDrive.Js.Tx.NoneFound', { sheet: 'Transactions', date: 'Date', amount: 'Amount' });
                        $('#txErrorMessage').text(message);
                        showTxState('error');
                        return;
                    }

                    currentTx = data;
                    currentCurrency = data.currencyCode || 'USD';
                    const sourceClass = data.isLiveSpreadsheetData ? 'bgt-source--live' : 'bgt-source--sample';
                    $('#txModalDataSource').html(`<span class="bgt-source ${ sourceClass }">${ escapeHtml(sourceLabel(data)) }</span>`);
                    $('#txSheetName').text(data.sheetName ? PFI18n.t('GoogleDrive.Js.Tx.ReadFromSheet', { sheet: data.sheetName }) : '');

                    showTxState('content');
                    renderSummary(data);
                    renderTable(data.transactions);
                    applyFilters();
                },
                error: function (xhr) {
                    if (seq !== txRequestSeq) { return; }
                    let message = PFI18n.t('GoogleDrive.Js.Tx.ReadFailed');
                    if (xhr.responseJSON && xhr.responseJSON.error) {
                        message = xhr.responseJSON.error;
                    } else if (xhr.status === 0) {
                        message = PFI18n.t('GoogleDrive.Js.RequestInterrupted');
                    } else if (xhr.status >= 500) {
                        message = PFI18n.t('GoogleDrive.Js.Tx.ServerError');
                    }
                    $('#txErrorMessage').text(message);
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

        $('#btnTxRetry').on('click', () => {
            if (lastTxRequest) { loadTransactions(lastTxRequest); }
        });

        $(document).on('click', '.tx-type-filter', function () {
            filters.type = String($(this).data('type') || '');
            applyFilters();
        });

        $('#txCategoryFilter').on('change', function () {
            filters.category = $(this).val();
            applyFilters();
        });

        let searchTimer = null;
        $('#txSearch').on('input', function () {
            const value = $(this).val().trim();
            clearTimeout(searchTimer);
            searchTimer = setTimeout(() => {
                filters.search = value;
                applyFilters();
            }, 150);
        });

        $('#txClearFilters').on('click', () => {
            resetFilters();
            applyFilters();
        });

        // Export the rows the current filters show
        $('#btnExportTx').on('click', () => {
            if (!txTable || !currentTx) { return; }
            // Headers and type in the page language; dates and amounts stay machine-readable (ISO date, dot decimal)
            const rows = [[
                PFI18n.t('GoogleDrive.Js.Csv.Date'), PFI18n.t('GoogleDrive.Js.Csv.Description'), PFI18n.t('GoogleDrive.Js.Csv.Category'),
                PFI18n.t('GoogleDrive.Js.Csv.Type'), PFI18n.t('GoogleDrive.Js.Csv.Amount')
            ]];
            txTable.rows({ search: 'applied', order: 'applied' }).data().toArray().forEach((t) => {
                const d = toDate(t.date);
                rows.push([d ? dayKey(d) : (t.dateText || ''), t.description, t.category,
                    PFI18n.t(isIncome(t) ? 'GoogleDrive.Js.Income' : 'GoogleDrive.Js.Spending'), t.amount.toFixed(2)]);
            });
            const csv = rows.map((r) => {
                return r.map((v) => {
                    const text = String(v === null || v === undefined ? '' : v);
                    return /[",\n]/.test(text) ? `"${ text.replace(/"/g, '""') }"` : text;
                }).join(',');
            }).join('\r\n');
            // BOM so Excel opens the file as UTF-8
            const blob = new Blob([`\ufeff${ csv}`], { type: 'text/csv;charset=utf-8' });
            const link = document.createElement('a');
            link.href = URL.createObjectURL(blob);
            link.download = PFI18n.t('GoogleDrive.Js.Tx.CsvFileName', { name: String(currentTx.fileName || 'transactions').replace(/\.[^.]+$/, '') });
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => { URL.revokeObjectURL(link.href); }, 1000);
        });

        // Tables and charts built during the open animation measure zero width; re-measure once visible
        $modal.on('shown.bs.modal', () => {
            if (txTable) { txTable.columns.adjust(); }
            if (txChart) { txChart.resize(); }
        });

        $modal.on('hidden.bs.modal', () => {
            txRequestSeq++;
            destroyTxViews();
        });
    });
})(jQuery);

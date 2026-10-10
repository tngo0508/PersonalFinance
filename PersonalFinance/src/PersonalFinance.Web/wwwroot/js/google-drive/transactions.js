// Spreadsheet Transactions modal on the Google Drive page.
// Server values come from data- attributes on #transactionsModal (data-transactions-url, data-connection-id).
(function ($) {
    'use strict';

    $(function () {
        const $modal = $('#transactionsModal');
        if ($modal.length === 0) return;

        let txTable = null;
        let lastTxRequest = null;
        let txRequestSeq = 0;

        function formatCurrency(num) {
            const n = Number(num) || 0;
            const formatted = '$' + Math.abs(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            return n < 0 ? '-' + formatted : formatted;
        }

        function escapeHtml(value) {
            return String(value === null || value === undefined ? '' : value)
                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
        }

        function formatDate(iso) {
            if (!iso) return '';
            const d = new Date(iso);
            return isNaN(d) ? '' : d.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
        }

        function isIncome(tx) {
            return tx.type === 'Income';
        }

        function showTxState(state) {
            $('#txLoadingState').toggleClass('d-none', state !== 'loading');
            $('#txErrorState').toggleClass('d-none', state !== 'error');
            $('#txContent').toggleClass('d-none', state !== 'content');
        }

        function destroyTxTable() {
            if (txTable) {
                txTable.destroy();
                txTable = null;
            }
            $('#txTable tbody').empty();
        }

        function renderSummary(data) {
            const txs = data.transactions;
            const expenseCount = txs.filter(function (t) { return !isIncome(t); }).length;
            const incomeCount = txs.length - expenseCount;
            const net = data.totalIncome - data.totalExpenses;

            $('#txKpiCount').text(txs.length.toLocaleString('en-US'));
            $('#txKpiRange').text(data.firstDate && data.lastDate
                ? formatDate(data.firstDate) + ' – ' + formatDate(data.lastDate)
                : '');
            $('#txKpiExpenses').text(formatCurrency(data.totalExpenses));
            $('#txKpiExpensesSub').text(expenseCount + (expenseCount === 1 ? ' transaction' : ' transactions'));
            $('#txKpiIncome').text(formatCurrency(data.totalIncome));
            $('#txKpiIncomeSub').text(incomeCount + (incomeCount === 1 ? ' transaction' : ' transactions'));
            $('#txKpiNet').text(formatCurrency(net));

            const categories = Array.from(new Set(txs.map(function (t) { return t.category; }).filter(Boolean))).sort();
            const $select = $('#txCategoryFilter').empty().append('<option value="">All categories</option>');
            categories.forEach(function (c) {
                $select.append($('<option>').val(c).text(c));
            });

            $('.tx-type-filter').removeClass('active').attr('aria-pressed', 'false')
                .filter('[data-type=""]').addClass('active').attr('aria-pressed', 'true');
        }

        function renderTable(transactions) {
            destroyTxTable();
            txTable = $('#txTable').DataTable({
                data: transactions,
                pageLength: 25,
                lengthMenu: [[10, 25, 50, 100, -1], [10, 25, 50, 100, 'All']],
                order: [[0, 'desc']],
                autoWidth: false,
                columns: [
                    {
                        data: 'date',
                        className: 'text-nowrap',
                        render: function (value, type, row) {
                            if (type === 'sort' || type === 'type') return value || '';
                            return escapeHtml(formatDate(value) || row.dateText || '-');
                        }
                    },
                    {
                        data: 'description',
                        render: function (value, type) {
                            return type === 'display' ? escapeHtml(value || '-') : (value || '');
                        }
                    },
                    {
                        data: 'category',
                        render: function (value, type) {
                            return type === 'display' ? escapeHtml(value || 'Uncategorized') : (value || '');
                        }
                    },
                    {
                        data: 'type',
                        render: function (value, type) {
                            if (type !== 'display') return value;
                            return value === 'Income'
                                ? '<span class="text-nowrap"><span class="bgt-swatch bgt-swatch--income" aria-hidden="true"></span>Income</span>'
                                : '<span class="text-nowrap"><span class="bgt-swatch bgt-swatch--spend" aria-hidden="true"></span>Spending</span>';
                        }
                    },
                    {
                        data: 'amount',
                        className: 'text-end text-nowrap fw-semibold',
                        render: function (value, type, row) {
                            if (type !== 'display') return value;
                            return isIncome(row)
                                ? '<span class="text-success">+' + formatCurrency(value) + '</span>'
                                : formatCurrency(value);
                        }
                    }
                ],
                language: {
                    search: '',
                    searchPlaceholder: 'Search transactions...',
                    emptyTable: 'No transactions found.',
                    zeroRecords: 'No transactions match your filters.'
                },
                dom: "<'row mb-2'<'col-sm-12 col-md-6'l><'col-sm-12 col-md-6'f>>" +
                     "<'row'<'col-sm-12'tr>>" +
                     "<'row mt-3'<'col-sm-12 col-md-5'i><'col-sm-12 col-md-7'p>>"
            });
        }

        function exactColumnSearch(columnIndex, value) {
            if (!txTable) return;
            const pattern = value ? '^' + DataTable.util.escapeRegex(value) + '$' : '';
            txTable.column(columnIndex).search(pattern, true, false).draw();
        }

        function loadTransactions(request) {
            lastTxRequest = request;
            const seq = ++txRequestSeq;
            destroyTxTable();

            $('#txModalFileName').text(request.fileName).attr('title', request.fileName);
            $('#txModalDataSource').empty();
            $('#txSheetName').text('');
            showTxState('loading');

            $.ajax({
                url: $modal.data('transactions-url'),
                method: 'GET',
                data: request,
                success: function (data) {
                    if (seq !== txRequestSeq) return; // a newer request superseded this one

                    if (!data || !data.transactions || data.transactions.length === 0) {
                        $('#txErrorMessage').text((data && data.errorMessage) || 'Make sure the spreadsheet has a sheet with Date and Amount column headers.');
                        showTxState('error');
                        return;
                    }

                    const sourceClass = data.isLiveSpreadsheetData ? 'bgt-source--live' : 'bgt-source--sample';
                    $('#txModalDataSource').html('<span class="bgt-source ' + sourceClass + '">' + escapeHtml(data.dataSource || 'Google Spreadsheet') + '</span>');
                    $('#txSheetName').text(data.sheetName ? 'Read from sheet: ' + data.sheetName : '');

                    renderSummary(data);
                    showTxState('content');
                    renderTable(data.transactions);
                },
                error: function (xhr) {
                    if (seq !== txRequestSeq) return;
                    const message = xhr.responseJSON && xhr.responseJSON.error;
                    $('#txErrorMessage').text(message || 'Check that the drive is still shared, then try again.');
                    showTxState('error');
                }
            });
        }

        // Click Handler for "Transactions" button on DataTables & Grid
        $(document).on('click', '.btn-sheet-transactions', function () {
            loadTransactions({
                fileName: $(this).data('file-name'),
                fileId: $(this).data('file-id'),
                connectionId: $modal.data('connection-id')
            });
        });

        $('#btnTxRetry').on('click', function () {
            if (lastTxRequest) loadTransactions(lastTxRequest);
        });

        $(document).on('click', '.tx-type-filter', function () {
            $('.tx-type-filter').removeClass('active').attr('aria-pressed', 'false');
            $(this).addClass('active').attr('aria-pressed', 'true');
            exactColumnSearch(3, $(this).data('type'));
        });

        $('#txCategoryFilter').on('change', function () {
            exactColumnSearch(2, $(this).val());
        });

        // Tables built during the open animation measure zero-width columns; re-measure once visible
        $modal.on('shown.bs.modal', function () {
            if (txTable) txTable.columns.adjust();
        });

        $modal.on('hidden.bs.modal', function () {
            txRequestSeq++;
            destroyTxTable();
        });
    });
})(jQuery);

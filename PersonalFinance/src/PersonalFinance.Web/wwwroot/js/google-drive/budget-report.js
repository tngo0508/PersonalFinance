// Monthly Budget Summary modal on the Google Drive page.
// Server values come from data- attributes on #monthlyBudgetModal (data-report-url, data-connection-id).
(function ($) {
    'use strict';

    $(function () {
        const $modal = $('#monthlyBudgetModal');
        if ($modal.length === 0) return;

        // ==========================================
        // Monthly Budget Summary Report & Charts Logic
        // ==========================================
        const BUDGET_COLORS = {
            income: '#2a78d6',
            spend: '#eb6834',
            negative: '#e34948',
            grid: '#e1e0d9',
            axis: '#c3c2b7',
            muted: '#898781'
        };
        let budgetMainChartInstance = null;
        let budgetNetChartInstance = null;
        let currentBudgetData = null;
        let currentSelectedMonth = 0;
        let budgetPeriods = [];
        let lastBudgetRequest = null;
        let budgetRequestSeq = 0;
        let budgetTableUserToggled = false;

        function formatCurrency(num) {
            if (num === null || num === undefined || isNaN(num)) return '$0.00';
            const n = Number(num);
            const formatted = '$' + Math.abs(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            return n < 0 ? '-' + formatted : formatted;
        }

        // Whole-dollar format for prose and tiles where cents are noise
        function formatMoney(num) {
            const n = Number(num) || 0;
            const formatted = '$' + Math.abs(n).toLocaleString('en-US', { maximumFractionDigits: 0 });
            return n < 0 ? '-' + formatted : formatted;
        }

        function formatCompactCurrency(val) {
            const abs = Math.abs(val);
            const sign = val < 0 ? '-' : '';
            if (abs >= 1000000) return sign + '$' + (abs / 1000000).toFixed(1).replace(/\.0$/, '') + 'M';
            if (abs >= 1000) return sign + '$' + (abs / 1000).toFixed(1).replace(/\.0$/, '') + 'K';
            return sign + '$' + abs.toLocaleString('en-US');
        }

        function escapeHtml(value) {
            return String(value === null || value === undefined ? '' : value)
                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
        }

        function hasValue(v) {
            return v !== null && v !== undefined;
        }

        // Status always ships as icon + label, never color alone
        function statusPill(tone, label) {
            const icon = tone === 'good' ? '&#10003;' : (tone === 'warning' ? '!' : '&#10005;');
            return '<span class="bgt-status bgt-status--' + tone + '"><span class="bgt-status-icon" aria-hidden="true">' + icon + '</span>' + escapeHtml(label) + '</span>';
        }

        function monthStatusTone(status) {
            if (status === 'Under Budget') return 'good';
            if (status === 'On Track') return 'warning';
            return 'critical';
        }

        function monthStatusLabel(status) {
            if (status === 'Under Budget') return 'Under budget';
            if (status === 'On Track') return 'On track (up to 5% over)';
            return 'Over budget';
        }

        function yearStatus(data) {
            const planned = data.totalAnnualBudgetedExpenses;
            if (planned > 0) {
                if (data.totalAnnualExpenses <= planned) return 'Under Budget';
                if (data.totalAnnualExpenses <= planned * 1.05) return 'On Track';
                return 'Over Budget';
            }
            return data.totalAnnualSavings >= 0 ? 'Under Budget' : 'Over Budget';
        }

        function categoryTone(c) {
            if (!(c.budgetedAmount > 0)) return 'none';
            if (c.actualAmount <= c.budgetedAmount * 0.9) return 'good';
            if (c.actualAmount <= c.budgetedAmount) return 'warning';
            return 'critical';
        }

        function categoryStatusLabel(tone) {
            if (tone === 'good') return 'Within budget';
            if (tone === 'warning') return 'Near limit';
            if (tone === 'critical') return 'Over budget';
            return 'No budget set';
        }

        function balanceText(start, end) {
            if (!hasValue(start) || !hasValue(end)) return '';
            const change = end - start;
            return 'Balance ' + formatMoney(start) + ' &rarr; ' + formatMoney(end) +
                ' (' + (change >= 0 ? '+' : '') + formatMoney(change) + ')';
        }

        function planDeltaText(actual, planned, aboveWord, belowWord) {
            if (!(planned > 0)) return '';
            const diff = actual - planned;
            if (Math.abs(diff) < 0.005) return 'Exactly on plan (' + formatMoney(planned) + ')';
            return formatMoney(Math.abs(diff)) + ' ' + (diff > 0 ? aboveWord : belowWord) + ' plan of ' + formatMoney(planned);
        }

        function aggregateCategories(months) {
            const map = {};
            const order = [];
            months.forEach(function (m) {
                (m.categories || []).forEach(function (c) {
                    if (!map[c.categoryName]) {
                        map[c.categoryName] = { categoryName: c.categoryName, budgetedAmount: 0, actualAmount: 0 };
                        order.push(c.categoryName);
                    }
                    map[c.categoryName].budgetedAmount += c.budgetedAmount || 0;
                    map[c.categoryName].actualAmount += c.actualAmount || 0;
                });
            });
            return order.map(function (k) { return map[k]; });
        }

        function setPeriodChips() {
            let html = '';
            budgetPeriods.forEach(function (p) {
                const isActive = p.month === currentSelectedMonth;
                html += '<button type="button" role="tab" class="bgt-period-btn budget-month-btn' + (p.month === 0 ? ' bgt-period-btn--year' : '') + (isActive ? ' active' : '') + '"' +
                    ' data-month="' + p.month + '" aria-selected="' + isActive + '" title="' + escapeHtml(p.title) + '">' + escapeHtml(p.label) + '</button>';
            });
            $('#budgetMonthButtonGroup').html(html);

            const idx = budgetPeriods.findIndex(function (p) { return p.month === currentSelectedMonth; });
            $('#btnBudgetPrev').prop('disabled', idx <= 0);
            $('#btnBudgetNext').prop('disabled', idx === -1 || idx >= budgetPeriods.length - 1);
            $('#btnBudgetPrev, #btnBudgetNext').toggle(budgetPeriods.length > 1);
            $('#budgetKeyboardHint').toggleClass('d-md-block', budgetPeriods.length > 1);

            const activeEl = $('#budgetMonthButtonGroup .active')[0];
            if (activeEl && activeEl.scrollIntoView) activeEl.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        }

        function setTableVisible(visible) {
            $('#budgetReportTableContainer').toggleClass('d-none', !visible);
            $('#btnToggleBudgetTable').text(visible ? 'Hide table' : 'Show table').attr('aria-expanded', visible);
        }

        function renderInsight(tone, sentences) {
            $('#budgetInsight')
                .attr('data-tone', tone)
                .html(sentences.filter(Boolean).join(' '));
        }

        function renderCategoryList(categories) {
            const items = categories.filter(function (c) { return (c.actualAmount || 0) !== 0 || (c.budgetedAmount || 0) !== 0; });
            if (items.length === 0) {
                $('#budgetCategoryList').html('<div class="text-muted small">No category spending was found for this period.</div>');
                return;
            }
            items.sort(function (a, b) { return b.actualAmount - a.actualAmount; });
            const maxActual = Math.max.apply(null, items.map(function (c) { return c.actualAmount; }).concat([1]));

            let html = '';
            items.forEach(function (c) {
                const tone = categoryTone(c);
                const hasBudget = tone !== 'none';
                const pct = hasBudget ? (c.actualAmount / c.budgetedAmount) * 100 : 0;
                const width = hasBudget ? Math.min(pct, 100) : Math.max(0, (c.actualAmount / maxActual) * 100);
                const remaining = c.budgetedAmount - c.actualAmount;
                const footLeft = hasBudget
                    ? (remaining >= 0 ? formatMoney(remaining) + ' left' : formatMoney(-remaining) + ' over')
                    : 'No budget set';
                const footRight = hasBudget ? Math.round(pct) + '% used' : '';
                const statusHtml = (tone === 'warning' || tone === 'critical') ? ' ' + statusPill(tone, categoryStatusLabel(tone)) : '';

                html += '<div class="bgt-cat-row">' +
                    '<div class="bgt-cat-head">' +
                        '<span class="bgt-cat-name" title="' + escapeHtml(c.categoryName) + '">' + escapeHtml(c.categoryName) + '</span>' +
                        '<span class="bgt-cat-amount">' + formatMoney(c.actualAmount) + (hasBudget ? ' <small>of ' + formatMoney(c.budgetedAmount) + '</small>' : '') + '</span>' +
                    '</div>' +
                    '<div class="bgt-meter' + (hasBudget ? '' : ' bgt-meter--nobudget') + '" role="meter" aria-valuemin="0" aria-valuemax="100" aria-valuenow="' + Math.round(width) + '"' +
                        ' aria-label="' + escapeHtml(c.categoryName) + ': ' + (hasBudget ? Math.round(pct) + '% of budget used' : 'no budget set') + '">' +
                        '<div class="bgt-meter-fill bgt-meter-fill--' + tone + '" style="width: ' + width.toFixed(1) + '%;"></div>' +
                    '</div>' +
                    '<div class="bgt-cat-foot"><span>' + footLeft + statusHtml + '</span><span>' + footRight + '</span></div>' +
                '</div>';
            });
            $('#budgetCategoryList').html(html);
        }

        function destroyYearCharts() {
            if (budgetMainChartInstance) { budgetMainChartInstance.destroy(); budgetMainChartInstance = null; }
            if (budgetNetChartInstance) { budgetNetChartInstance.destroy(); budgetNetChartInstance = null; }
        }

        function baseChartOptions(months) {
            return {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                onClick: function (evt, elements, chart) {
                    const points = chart.getElementsAtEventForMode(evt, 'index', { intersect: false }, false);
                    if (points.length && months[points[0].index]) {
                        renderBudgetMonth(months[points[0].index].monthNumber);
                    }
                },
                onHover: function (evt, elements) {
                    if (evt.native && evt.native.target) evt.native.target.style.cursor = elements.length ? 'pointer' : 'default';
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: '#0b0b0b',
                        padding: 10,
                        cornerRadius: 8,
                        boxPadding: 4,
                        callbacks: {
                            title: function (items) { return items.length ? months[items[0].dataIndex].monthName : ''; },
                            footer: function () { return 'Click to open this month'; }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        border: { color: BUDGET_COLORS.axis },
                        ticks: { color: BUDGET_COLORS.muted }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: BUDGET_COLORS.grid },
                        border: { display: false },
                        ticks: { color: BUDGET_COLORS.muted, maxTicksLimit: 6, callback: function (val) { return formatCompactCurrency(val); } }
                    }
                }
            };
        }

        function renderYearCharts(months) {
            destroyYearCharts();
            const labels = months.map(function (m) { return m.monthName.substring(0, 3); });
            const barStyle = { borderRadius: 4, borderSkipped: 'start', maxBarThickness: 24, categoryPercentage: 0.7, barPercentage: 0.9 };

            const mainOptions = baseChartOptions(months);
            mainOptions.plugins.tooltip.callbacks.label = function (ctx) { return ' ' + ctx.dataset.label + ': ' + formatCurrency(ctx.parsed.y); };
            mainOptions.plugins.tooltip.callbacks.afterBody = function (items) {
                const m = items.length ? months[items[0].dataIndex] : null;
                return m ? ' Net: ' + formatCurrency(m.netSavings) : '';
            };
            budgetMainChartInstance = new Chart(document.getElementById('budgetMainChart').getContext('2d'), {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [
                        $.extend({ label: 'Income', data: months.map(function (m) { return m.actualIncome; }), backgroundColor: BUDGET_COLORS.income }, barStyle),
                        $.extend({ label: 'Spending', data: months.map(function (m) { return m.actualExpenses; }), backgroundColor: BUDGET_COLORS.spend }, barStyle)
                    ]
                },
                options: mainOptions
            });

            const netOptions = baseChartOptions(months);
            netOptions.plugins.tooltip.callbacks.label = function (ctx) {
                const v = ctx.parsed.y;
                return v >= 0 ? ' Saved ' + formatCurrency(v) : ' Overspent ' + formatCurrency(-v);
            };
            netOptions.scales.y.beginAtZero = true;
            netOptions.scales.x.ticks.maxRotation = 0;
            netOptions.scales.x.ticks.autoSkipPadding = 6;
            netOptions.scales.y.grid.color = function (ctx) { return ctx.tick && ctx.tick.value === 0 ? BUDGET_COLORS.axis : BUDGET_COLORS.grid; };
            const netValues = months.map(function (m) { return m.netSavings; });
            budgetNetChartInstance = new Chart(document.getElementById('budgetNetChart').getContext('2d'), {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [$.extend({
                        label: 'Net saved',
                        data: netValues,
                        backgroundColor: netValues.map(function (v) { return v >= 0 ? BUDGET_COLORS.income : BUDGET_COLORS.negative; })
                    }, barStyle, { categoryPercentage: 0.6, barPercentage: 1 })]
                },
                options: netOptions
            });
        }

        function renderYearView(data) {
            const months = data.months;
            const status = yearStatus(data);
            const underCount = months.filter(function (m) { return m.status === 'Under Budget'; }).length;

            $('#budgetPeriodTitle').text(data.year + ' · Full year');
            $('#budgetPeriodSubtitle').html(balanceText(data.startingBalance, data.endingBalance) || (months.length + ' months of data'));

            // KPI tiles
            $('#kpiTotalIncome').text(formatMoney(data.totalAnnualIncome));
            $('#kpiIncomeSubtext').text(planDeltaText(data.totalAnnualIncome, data.totalAnnualBudgetedIncome, 'above', 'below') ||
                ('Avg ' + formatMoney(data.totalAnnualIncome / months.length) + ' / month'));
            $('#kpiTotalExpenses').text(formatMoney(data.totalAnnualExpenses));
            $('#kpiExpensesSubtext').text(planDeltaText(data.totalAnnualExpenses, data.totalAnnualBudgetedExpenses, 'over', 'under') ||
                ('Avg ' + formatMoney(data.totalAnnualExpenses / months.length) + ' / month'));
            $('#kpiNetSavings').text(formatMoney(data.totalAnnualSavings));
            $('#kpiSavingsSubtext').text(data.totalAnnualSavings >= 0 ? 'Income minus spending' : 'Spent more than you earned');
            $('#kpiSavingsRate').text(data.averageSavingsRate.toFixed(1) + '%');
            $('#kpiStatusBadge').html(statusPill(monthStatusTone(status), monthStatusLabel(status)));

            // Plain-language summary
            const best = months.reduce(function (a, b) { return b.netSavings > a.netSavings ? b : a; });
            const worst = months.reduce(function (a, b) { return b.netSavings < a.netSavings ? b : a; });
            const categories = aggregateCategories(months);
            const top = categories.slice().sort(function (a, b) { return b.actualAmount - a.actualAmount; })[0];
            const sentences = [
                data.totalAnnualSavings >= 0
                    ? 'In ' + data.year + ' you saved <strong>' + formatMoney(data.totalAnnualSavings) + '</strong>, <strong>' + data.averageSavingsRate.toFixed(0) + '%</strong> of your income.'
                    : 'In ' + data.year + ' you spent <strong>' + formatMoney(-data.totalAnnualSavings) + ' more</strong> than you earned.',
                '<strong>' + underCount + ' of ' + months.length + '</strong> months came in under budget.',
                months.length > 1 && best.netSavings > 0 ? 'Best month: ' + escapeHtml(best.monthName) + ' (saved ' + formatMoney(best.netSavings) + ').' : '',
                months.length > 1 && worst.netSavings < 0 ? 'Toughest month: ' + escapeHtml(worst.monthName) + ' (overspent ' + formatMoney(-worst.netSavings) + ').' : '',
                top && top.actualAmount > 0 && data.totalAnnualExpenses > 0
                    ? 'Biggest expense: ' + escapeHtml(top.categoryName) + ' (' + formatMoney(top.actualAmount) + ', ' + Math.round(top.actualAmount / data.totalAnnualExpenses * 100) + '% of spending).'
                    : ''
            ];
            renderInsight(monthStatusTone(status), sentences);

            // Charts
            $('#budgetYearCharts').removeClass('d-none');
            renderYearCharts(months);

            // Categories
            $('#budgetCategoryTitle').text('Spending by category in ' + data.year);
            $('#budgetCategorySubtitle').text('Totals across all months, largest first. Bars show how much of each yearly budget is used.');
            renderCategoryList(categories);

            // Month-by-month table (also the keyboard-accessible way to drill into a month)
            $('#tableSectionTitle').text('Month-by-month details');
            let tableHtml = '<table class="table table-hover align-middle mb-0 bgt-table">' +
                '<thead><tr>' +
                '<th scope="col">Month</th>' +
                '<th scope="col" class="text-end">Income</th>' +
                '<th scope="col" class="text-end">Planned spending</th>' +
                '<th scope="col" class="text-end">Spent</th>' +
                '<th scope="col" class="text-end">Net saved</th>' +
                '<th scope="col" class="text-end">Savings rate</th>' +
                '<th scope="col">Status</th>' +
                '<th scope="col" class="text-end"><span class="visually-hidden">Open</span></th>' +
                '</tr></thead><tbody>';
            months.forEach(function (m) {
                tableHtml += '<tr class="bgt-row-link btn-drill-month" data-month="' + m.monthNumber + '">' +
                    '<td class="fw-semibold">' + escapeHtml(m.monthName) + '</td>' +
                    '<td class="text-end">' + formatCurrency(m.actualIncome) + '</td>' +
                    '<td class="text-end text-muted">' + (m.budgetedExpenses > 0 ? formatCurrency(m.budgetedExpenses) : '&mdash;') + '</td>' +
                    '<td class="text-end">' + formatCurrency(m.actualExpenses) + '</td>' +
                    '<td class="text-end fw-semibold">' + formatCurrency(m.netSavings) + '</td>' +
                    '<td class="text-end">' + m.savingsRate.toFixed(1) + '%</td>' +
                    '<td>' + statusPill(monthStatusTone(m.status), monthStatusLabel(m.status)) + '</td>' +
                    '<td class="text-end"><button type="button" class="btn btn-sm btn-outline-secondary rounded-pill px-3 py-0 btn-drill-month" data-month="' + m.monthNumber + '">Open &rarr;</button></td>' +
                    '</tr>';
            });
            tableHtml += '</tbody></table>';
            $('#budgetReportTableContainer').html(tableHtml);
            if (!budgetTableUserToggled) setTableVisible(true);
        }

        function renderMonthView(data, monthData) {
            const tone = monthStatusTone(monthData.status);
            const single = data.months.length === 1;

            $('#budgetPeriodTitle').text(monthData.monthName + ' ' + data.year);
            $('#budgetPeriodSubtitle').html(balanceText(monthData.startingBalance, monthData.endingBalance));

            // KPI tiles
            $('#kpiTotalIncome').text(formatMoney(monthData.actualIncome));
            $('#kpiIncomeSubtext').text(planDeltaText(monthData.actualIncome, monthData.budgetedIncome, 'above', 'below') || 'No income plan set');
            $('#kpiTotalExpenses').text(formatMoney(monthData.actualExpenses));
            $('#kpiExpensesSubtext').text(planDeltaText(monthData.actualExpenses, monthData.budgetedExpenses, 'over', 'under') || 'No spending plan set');
            $('#kpiNetSavings').text(formatMoney(monthData.netSavings));
            $('#kpiSavingsSubtext').text(
                (monthData.budgetedIncome > 0 || monthData.budgetedExpenses > 0)
                    ? 'Planned: ' + formatMoney(monthData.budgetedNetSavings !== undefined ? monthData.budgetedNetSavings : monthData.budgetedIncome - monthData.budgetedExpenses)
                    : (monthData.netSavings >= 0 ? 'Income minus spending' : 'Spent more than you earned'));
            $('#kpiSavingsRate').text(monthData.savingsRate.toFixed(1) + '%');
            $('#kpiStatusBadge').html(statusPill(tone, monthStatusLabel(monthData.status)));

            // Plain-language summary
            const categories = monthData.categories || [];
            const overList = categories
                .filter(function (c) { return categoryTone(c) === 'critical'; })
                .sort(function (a, b) { return (b.actualAmount - b.budgetedAmount) - (a.actualAmount - a.budgetedAmount); });
            const budgetedCount = categories.filter(function (c) { return c.budgetedAmount > 0; }).length;
            let overText = '';
            if (overList.length > 0) {
                overText = '<strong>' + overList.length + (overList.length === 1 ? ' category' : ' categories') + ' went over budget:</strong> ' +
                    overList.slice(0, 3).map(function (c) { return escapeHtml(c.categoryName) + ' (+' + formatMoney(c.actualAmount - c.budgetedAmount) + ')'; }).join(', ') +
                    (overList.length > 3 ? ', and ' + (overList.length - 3) + ' more' : '') + '.';
            } else if (budgetedCount > 0) {
                overText = 'Every budgeted category stayed within its limit.';
            }
            const planDiff = monthData.budgetedExpenses - monthData.actualExpenses;
            renderInsight(tone, [
                monthData.netSavings >= 0
                    ? 'You saved <strong>' + formatMoney(monthData.netSavings) + '</strong> in ' + escapeHtml(monthData.monthName) + ', <strong>' + monthData.savingsRate.toFixed(0) + '%</strong> of your income.'
                    : 'You spent <strong>' + formatMoney(-monthData.netSavings) + ' more</strong> than you earned in ' + escapeHtml(monthData.monthName) + '.',
                monthData.budgetedExpenses > 0
                    ? 'Spending came in <strong>' + formatMoney(Math.abs(planDiff)) + (planDiff >= 0 ? ' under' : ' over') + '</strong> your ' + formatMoney(monthData.budgetedExpenses) + ' plan.'
                    : '',
                overText
            ]);

            // No year charts in month view
            destroyYearCharts();
            $('#budgetYearCharts').addClass('d-none');

            // Categories
            $('#budgetCategoryTitle').text('Spending by category');
            $('#budgetCategorySubtitle').text('Largest first. Bars show how much of each budget is used.');
            renderCategoryList(categories);

            // Category table
            $('#tableSectionTitle').text(monthData.monthName + ' category details');
            let tableHtml = '<table class="table table-hover align-middle mb-0 bgt-table">' +
                '<thead><tr>' +
                '<th scope="col">Category</th>' +
                '<th scope="col" class="text-end">Planned</th>' +
                '<th scope="col" class="text-end">Spent</th>' +
                '<th scope="col" class="text-end">Left / over</th>' +
                '<th scope="col" class="text-end">% used</th>' +
                '<th scope="col">Status</th>' +
                '</tr></thead><tbody>';
            categories.slice().sort(function (a, b) { return b.actualAmount - a.actualAmount; }).forEach(function (c) {
                const cTone = categoryTone(c);
                const hasBudget = cTone !== 'none';
                tableHtml += '<tr>' +
                    '<td class="fw-semibold">' + escapeHtml(c.categoryName) + '</td>' +
                    '<td class="text-end text-muted">' + (hasBudget ? formatCurrency(c.budgetedAmount) : '&mdash;') + '</td>' +
                    '<td class="text-end">' + formatCurrency(c.actualAmount) + '</td>' +
                    '<td class="text-end">' + (hasBudget ? (c.variance >= 0 ? formatCurrency(c.variance) + ' left' : formatCurrency(-c.variance) + ' over') : '&mdash;') + '</td>' +
                    '<td class="text-end">' + (hasBudget ? c.percentageUsed.toFixed(0) + '%' : '&mdash;') + '</td>' +
                    '<td>' + (hasBudget ? statusPill(cTone, categoryStatusLabel(cTone)) : '<span class="text-muted small">No budget set</span>') + '</td>' +
                    '</tr>';
            });
            tableHtml += '</tbody></table>';
            $('#budgetReportTableContainer').html(tableHtml);
            if (!budgetTableUserToggled) setTableVisible(single);
        }

        function renderBudgetMonth(monthNum) {
            if (!currentBudgetData || !currentBudgetData.months || currentBudgetData.months.length === 0) return;
            const isSingleMonth = currentBudgetData.months.length === 1;

            if (monthNum === 0 && !isSingleMonth) {
                currentSelectedMonth = 0;
                setPeriodChips();
                renderYearView(currentBudgetData);
            } else {
                const monthData = currentBudgetData.months.find(function (m) { return m.monthNumber === monthNum; }) || currentBudgetData.months[0];
                currentSelectedMonth = monthData.monthNumber;
                setPeriodChips();
                renderMonthView(currentBudgetData, monthData);
            }
        }

        function stepBudgetPeriod(direction) {
            const idx = budgetPeriods.findIndex(function (p) { return p.month === currentSelectedMonth; });
            const next = budgetPeriods[idx + direction];
            if (next) {
                renderBudgetMonth(next.month);
                $('#budgetMonthButtonGroup .active').trigger('focus');
            }
        }

        function showBudgetState(state) {
            $('#budgetLoadingState').toggleClass('d-none', state !== 'loading');
            $('#budgetErrorState').toggleClass('d-none', state !== 'error');
            $('#budgetReportContent').toggleClass('d-none', state !== 'report');
            $('#btnPrintBudgetReport').prop('disabled', state !== 'report');
        }

        function loadBudgetReport(request) {
            lastBudgetRequest = request;
            const seq = ++budgetRequestSeq;
            currentBudgetData = null;
            budgetTableUserToggled = false;
            destroyYearCharts();

            $('#budgetModalFileName').text(request.fileName).attr('title', request.fileName);
            $('#budgetModalDataSource').empty();
            showBudgetState('loading');

            $.ajax({
                url: $modal.data('report-url'),
                method: 'GET',
                data: request,
                success: function (data) {
                    if (seq !== budgetRequestSeq) return; // a newer request superseded this one

                    if (!data || !data.months || data.months.length === 0) {
                        $('#budgetErrorMessage').text('No monthly budget data was found in this spreadsheet. Make sure it has income, expense, or transaction rows.');
                        showBudgetState('error');
                        return;
                    }

                    currentBudgetData = data;
                    const rowsText = data.parsedRowCount > 0 ? ' · ' + data.parsedRowCount + ' rows read' : '';
                    const sourceClass = data.isLiveSpreadsheetData ? 'bgt-source--live' : 'bgt-source--sample';
                    $('#budgetModalDataSource').html('<span class="bgt-source ' + sourceClass + '">' + escapeHtml(data.dataSource || 'Google Spreadsheet') + escapeHtml(rowsText) + '</span>');

                    budgetPeriods = [];
                    if (data.months.length > 1) {
                        budgetPeriods.push({ month: 0, label: 'Full year', title: data.year + ' overview' });
                    }
                    data.months.forEach(function (m) {
                        budgetPeriods.push({
                            month: m.monthNumber,
                            label: data.months.length > 1 ? m.monthName.substring(0, 3) : m.monthName + ' ' + data.year,
                            title: m.monthName + ' ' + data.year
                        });
                    });

                    showBudgetState('report');
                    renderBudgetMonth(data.months.length > 1 ? 0 : data.months[0].monthNumber);
                },
                error: function () {
                    if (seq !== budgetRequestSeq) return;
                    $('#budgetErrorMessage').text('Check that the file is a monthly budget sheet and that the drive is still shared, then try again.');
                    showBudgetState('error');
                }
            });
        }

        // Click Handler for "Budget Report" button on DataTables & Grid
        $(document).on('click', '.btn-budget-report', function () {
            loadBudgetReport({
                fileName: $(this).data('file-name'),
                fileId: $(this).data('file-id'),
                connectionId: $modal.data('connection-id')
            });
        });

        $('#btnBudgetRetry').on('click', function () {
            if (lastBudgetRequest) loadBudgetReport(lastBudgetRequest);
        });

        // Period selector, prev/next and keyboard navigation
        $(document).on('click', '.budget-month-btn', function () {
            renderBudgetMonth(parseInt($(this).data('month'), 10));
        });
        $('#btnBudgetPrev').on('click', function () { stepBudgetPeriod(-1); });
        $('#btnBudgetNext').on('click', function () { stepBudgetPeriod(1); });
        $('#monthlyBudgetModal').on('keydown', function (e) {
            if (!currentBudgetData || $(e.target).is('input, select, textarea')) return;
            if (e.key === 'ArrowLeft') { e.preventDefault(); stepBudgetPeriod(-1); }
            if (e.key === 'ArrowRight') { e.preventDefault(); stepBudgetPeriod(1); }
        });

        // Table Month Drill-down Click Handler (whole row or its button)
        $(document).on('click', '.btn-drill-month', function (e) {
            e.stopPropagation();
            renderBudgetMonth(parseInt($(this).data('month'), 10));
            $('#monthlyBudgetModal .modal-body').scrollTop(0);
        });

        $('#btnToggleBudgetTable').on('click', function () {
            budgetTableUserToggled = true;
            setTableVisible($('#budgetReportTableContainer').hasClass('d-none'));
        });

        // Charts rendered during the open animation can measure a zero-size canvas; re-measure once visible
        $('#monthlyBudgetModal').on('shown.bs.modal', function () {
            if (budgetMainChartInstance) budgetMainChartInstance.resize();
            if (budgetNetChartInstance) budgetNetChartInstance.resize();
        });

        $('#monthlyBudgetModal').on('hidden.bs.modal', function () {
            budgetRequestSeq++;
            destroyYearCharts();
        });

        // Print Budget Report Handler: print only the report, with the table expanded
        $('#btnPrintBudgetReport').on('click', function () {
            $('body').addClass('bgt-printing');
            window.print();
        });
        window.addEventListener('afterprint', function () {
            $('body').removeClass('bgt-printing');
        });
    });
})(jQuery);

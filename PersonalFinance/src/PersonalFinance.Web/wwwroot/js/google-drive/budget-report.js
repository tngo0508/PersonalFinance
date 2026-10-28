/* global PFI18n */
// Monthly Budget Summary modal on the Google Drive page.
// Server values come from data- attributes on #monthlyBudgetModal (data-report-url, data-connection-id).
(function ($) {
    'use strict';

    $(() => {
        const $modal = $('#monthlyBudgetModal');
        if ($modal.length === 0) { return; }

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
        // The spreadsheet's currency, chosen when its folder was connected (ADR 0016); read from each report.
        let currentCurrency = 'USD';

        // Reference to shared utilities
        const PF = window.PFUtils;

        function formatCurrency (num) {
            return PFI18n.formatMoney(Number(num) || 0, currentCurrency);
        }

        // Whole-unit format for prose and tiles where minor units are noise
        function formatMoney (num) {
            return PFI18n.formatMoney(Number(num) || 0, currentCurrency, { whole: true });
        }

        function formatCompactCurrency (val) {
            return PFI18n.formatCompactMoney(val, currentCurrency);
        }

        function escapeHtml (value) {
            return PF.escapeHtml(value);
        }

        function hasValue (v) {
            return v !== null && v !== undefined;
        }

        function t (name, args) {
            return PFI18n.t(`GoogleDrive.Js.Report.${ name }`, args);
        }

        // Markup for a value inside a translated sentence; the sentence templates are the app's own text
        function strong (text) {
            return `<strong>${ escapeHtml(text) }</strong>`;
        }

        function formatPercent (value, decimals) {
            return PFI18n.t('GoogleDrive.Js.Percent', {
                value: PFI18n.formatNumber(value, { minimumFractionDigits: decimals || 0, maximumFractionDigits: decimals || 0 })
            });
        }

        // Month names come from the month number in the page culture, not the sheet's English name
        function monthName (monthNumber, style) {
            const year = currentBudgetData ? currentBudgetData.year : new Date().getFullYear();
            return PFI18n.formatDate(new Date(year, monthNumber - 1, 1), { month: style || 'long' });
        }

        function monthYear (monthNumber) {
            const year = currentBudgetData ? currentBudgetData.year : new Date().getFullYear();
            return PFI18n.formatDate(new Date(year, monthNumber - 1, 1), { month: 'long', year: 'numeric' });
        }

        function sourceLabel (data) {
            return PFI18n.t(data.isLiveSpreadsheetData ? 'GoogleDrive.Js.Source.Live' : 'GoogleDrive.Js.Source.Sample');
        }

        // Status always ships as icon + label, never color alone
        function statusPill (tone, label) {
            const icon = tone === 'good' ? '&#10003;' : (tone === 'warning' ? '!' : '&#10005;');
            return `<span class="bgt-status bgt-status--${ tone }"><span class="bgt-status-icon" aria-hidden="true">${ icon }</span>${ escapeHtml(label) }</span>`;
        }

        function monthStatusTone (status) {
            if (status === 'Under Budget') { return 'good'; }
            if (status === 'On Track') { return 'warning'; }
            return 'critical';
        }

        function monthStatusLabel (status) {
            if (status === 'Under Budget') { return t('UnderBudget'); }
            if (status === 'On Track') { return t('OnTrack'); }
            return t('OverBudget');
        }

        function yearStatus (data) {
            const planned = data.totalAnnualBudgetedExpenses;
            if (planned > 0) {
                if (data.totalAnnualExpenses <= planned) { return 'Under Budget'; }
                if (data.totalAnnualExpenses <= planned * 1.05) { return 'On Track'; }
                return 'Over Budget';
            }
            return data.totalAnnualSavings >= 0 ? 'Under Budget' : 'Over Budget';
        }

        function categoryTone (c) {
            if (!(c.budgetedAmount > 0)) { return 'none'; }
            if (c.actualAmount <= c.budgetedAmount * 0.9) { return 'good'; }
            if (c.actualAmount <= c.budgetedAmount) { return 'warning'; }
            return 'critical';
        }

        function categoryStatusLabel (tone) {
            if (tone === 'good') { return t('WithinBudget'); }
            if (tone === 'warning') { return t('NearLimit'); }
            if (tone === 'critical') { return t('OverBudget'); }
            return t('NoBudgetSet');
        }

        // HTML: the subtitle is set with .html()
        function balanceText (start, end) {
            if (!hasValue(start) || !hasValue(end)) { return ''; }
            const change = end - start;
            return escapeHtml(t('Balance', {
                start: formatMoney(start),
                end: formatMoney(end),
                change: `${ change >= 0 ? '+' : '' }${ formatMoney(change) }`
            }));
        }

        // aboveKey / belowKey name the sentence for actual above or below the plan, e.g. 'PlanAbove' and 'PlanBelow'
        function planDeltaText (actual, planned, aboveKey, belowKey) {
            if (!(planned > 0)) { return ''; }
            const diff = actual - planned;
            if (Math.abs(diff) < 0.005) { return t('PlanExact', { planned: formatMoney(planned) }); }
            return t(diff > 0 ? aboveKey : belowKey, { amount: formatMoney(Math.abs(diff)), planned: formatMoney(planned) });
        }

        function aggregateCategories (months) {
            const map = {};
            const order = [];
            months.forEach((m) => {
                (m.categories || []).forEach((c) => {
                    if (!map[c.categoryName]) {
                        map[c.categoryName] = { categoryName: c.categoryName, budgetedAmount: 0, actualAmount: 0 };
                        order.push(c.categoryName);
                    }
                    map[c.categoryName].budgetedAmount += c.budgetedAmount || 0;
                    map[c.categoryName].actualAmount += c.actualAmount || 0;
                });
            });
            return order.map((k) => { return map[k]; });
        }

        function setPeriodChips () {
            let html = '';
            budgetPeriods.forEach((p) => {
                const isActive = p.month === currentSelectedMonth;
                html += `<button type="button" role="tab" class="bgt-period-btn budget-month-btn${ p.month === 0 ? ' bgt-period-btn--year' : '' }${isActive ? ' active' : '' }"` +
                    ` data-month="${ p.month }" aria-selected="${ isActive }" title="${ escapeHtml(p.title) }">${ escapeHtml(p.label) }</button>`;
            });
            $('#budgetMonthButtonGroup').html(html);

            const idx = budgetPeriods.findIndex((p) => { return p.month === currentSelectedMonth; });
            $('#btnBudgetPrev').prop('disabled', idx <= 0);
            $('#btnBudgetNext').prop('disabled', idx === -1 || idx >= budgetPeriods.length - 1);
            $('#btnBudgetPrev, #btnBudgetNext').toggle(budgetPeriods.length > 1);
            $('#budgetKeyboardHint').toggleClass('d-md-block', budgetPeriods.length > 1);

            const activeEl = $('#budgetMonthButtonGroup .active')[0];
            if (activeEl && activeEl.scrollIntoView) { activeEl.scrollIntoView({ block: 'nearest', inline: 'nearest' }); }
        }

        function setTableVisible (visible) {
            $('#budgetReportTableContainer').toggleClass('d-none', !visible);
            $('#btnToggleBudgetTable').text(visible ? t('HideTable') : t('ShowTable')).attr('aria-expanded', visible);
        }

        // Summary card: tone colors the accent stripe, status pill sits on the right
        function renderInsight (tone, sentences, statusHtml) {
            $('#budgetHero').attr('data-tone', tone);
            $('#budgetInsight').html(sentences.filter(Boolean).join(' '));
            $('#budgetHeroStatus').html(statusHtml || '');
        }

        // Actual-vs-plan meter for a KPI tile; the tick marks the plan
        function renderTileMeter (selector, actual, planned, kind, label) {
            const $el = $(selector);
            if (!(planned > 0)) { $el.empty(); return; }
            const scale = Math.max(actual, planned) || 1;
            const fillPct = Math.max(0, actual) / scale * 100;
            const planPct = planned / scale * 100;
            $el.html(`<div class="bgt-mini-meter" role="meter" aria-valuemin="0" aria-valuemax="${ Math.round(scale) }" aria-valuenow="${ Math.round(actual) }"` +
                ` aria-label="${ escapeHtml(t('MeterAria', { label: label, actual: formatMoney(actual), planned: formatMoney(planned) })) }">` +
                `<div class="bgt-mini-meter-fill bgt-mini-meter-fill--${ kind }" style="width:${ fillPct.toFixed(1) }%"></div>` +
                `<div class="bgt-mini-meter-mark" style="left:${ planPct.toFixed(1) }%" title="${ escapeHtml(t('PlanTitle', { amount: formatMoney(planned) })) }"></div>` +
                '</div>');
        }

        // Savings rate against the common "save 20% of income" guideline
        const SAVINGS_GOAL_PCT = 20;
        function renderSavingsRate (rate) {
            const scale = Math.max(SAVINGS_GOAL_PCT * 1.5, rate);
            const fill = Math.max(0, rate) / scale * 100;
            $('#kpiSavingsRate').text(formatPercent(rate, 1));
            $('#kpiSavingsMeter').html(`<div class="bgt-mini-meter" role="meter" aria-valuemin="0" aria-valuemax="${ Math.round(scale) }" aria-valuenow="${ Math.round(rate) }"` +
                ` aria-label="${ escapeHtml(t('SavingsRateAria', { rate: formatPercent(rate, 1), goal: formatPercent(SAVINGS_GOAL_PCT) })) }">` +
                `<div class="bgt-mini-meter-fill bgt-mini-meter-fill--income" style="width:${ fill.toFixed(1) }%"></div>` +
                `<div class="bgt-mini-meter-mark" style="left:${ (SAVINGS_GOAL_PCT / scale * 100).toFixed(1) }%" title="${ escapeHtml(t('GoalTitle', { goal: formatPercent(SAVINGS_GOAL_PCT) })) }"></div>` +
                '</div>');
            const gap = rate - SAVINGS_GOAL_PCT;
            $('#kpiSavingsRateSub').text(gap >= 0
                ? t('GoalMet', { goal: formatPercent(SAVINGS_GOAL_PCT) })
                : t('GoalBelow', {
                    points: PFI18n.formatNumber(Math.abs(gap), { minimumFractionDigits: 1, maximumFractionDigits: 1 }),
                    goal: formatPercent(SAVINGS_GOAL_PCT)
                }));
        }

        // Income split into spent vs. saved; spending beyond income shows as a hatched "overspent" segment
        function renderAllocation (income, spending) {
            const $bar = $('#budgetAllocation');
            const $legend = $('#budgetAllocationLegend');
            if (!(income > 0) && !(spending > 0)) {
                $bar.html($('<div class="text-muted small">').text(t('NoIncomeOrSpending')));
                $legend.empty();
                $('#budgetAllocationSub').text('');
                return;
            }

            const scale = Math.max(income, spending);
            const spentWithinIncome = Math.min(spending, income);
            const saved = Math.max(0, income - spending);
            const overspent = Math.max(0, spending - income);
            const pct = function (v) { return `${(v / scale * 100).toFixed(2) }%`; };
            const segment = function (cls, value, label) {
                return value > 0
                    ? `<div class="bgt-alloc-seg bgt-alloc-seg--${ cls }" style="width:${ pct(value) }" title="${ escapeHtml(PFI18n.t('GoogleDrive.Js.LabelValue', { label: label, amount: formatMoney(value) })) }">` +
                      `<span>${ escapeHtml(t('SegmentText', { label: label, amount: formatMoney(value) })) }</span></div>`
                    : '';
            };

            const allocationAria = overspent > 0
                ? t('AllocationAriaOver', { income: formatMoney(income), spent: formatMoney(spending), over: formatMoney(overspent) })
                : t('AllocationAriaSaved', { income: formatMoney(income), spent: formatMoney(spending), saved: formatMoney(saved) });
            $bar.html(`<div class="bgt-alloc" role="img" aria-label="${ escapeHtml(allocationAria) }">${
                segment('spend', spentWithinIncome, t('Spent'))
            }${segment('save', saved, t('Saved'))
            }${segment('over', overspent, t('Overspent'))
            }</div>`);

            let legend = `<span><span class="bgt-swatch bgt-swatch--spend"></span>${ escapeHtml(t('Spent')) }</span>`;
            legend += overspent > 0
                ? `<span><span class="bgt-swatch bgt-swatch--over"></span>${ escapeHtml(t('Overspent')) }</span>`
                : `<span><span class="bgt-swatch bgt-swatch--income"></span>${ escapeHtml(t('Saved')) }</span>`;
            $legend.html(legend);

            if (income > 0) {
                const per100Spent = Math.round(spending / income * 100);
                $('#budgetAllocationSub').text(overspent > 0
                    ? t('Per100Over', { earned: formatMoney(100), spent: formatMoney(per100Spent) })
                    : t('Per100Saved', { earned: formatMoney(100), spent: formatMoney(per100Spent), saved: formatMoney(100 - per100Spent) }));
            } else {
                $('#budgetAllocationSub').text(t('NoIncome'));
            }
        }

        // Category list state, so the sort and "only over budget" controls can re-render without a reload
        let currentCategories = [];

        function sortedVisibleCategories () {
            const sortBy = $('#budgetCategorySort').val();
            const overOnly = $('#budgetOverOnly').is(':checked');
            let items = currentCategories.filter((c) => { return (c.actualAmount || 0) !== 0 || (c.budgetedAmount || 0) !== 0; });
            if (overOnly) { items = items.filter((c) => { return categoryTone(c) === 'critical'; }); }
            items.sort((a, b) => {
                if (sortBy === 'name') { return a.categoryName.localeCompare(b.categoryName); }
                if (sortBy === 'over') { return (b.actualAmount - b.budgetedAmount) - (a.actualAmount - a.budgetedAmount); }
                return b.actualAmount - a.actualAmount;
            });
            return items;
        }

        function renderCategoryList (categories) {
            if (categories) { currentCategories = categories; }
            const items = sortedVisibleCategories();
            if (items.length === 0) {
                $('#budgetCategoryList').html($('<div class="text-muted small">').text(
                    $('#budgetOverOnly').is(':checked') ? t('NoCategoryOver') : t('NoCategorySpending')));
                return;
            }
            const maxActual = Math.max.apply(null, items.map((c) => { return c.actualAmount; }).concat([1]));

            let html = '';
            items.forEach((c) => {
                const tone = categoryTone(c);
                const hasBudget = tone !== 'none';
                const pct = hasBudget ? (c.actualAmount / c.budgetedAmount) * 100 : 0;
                const width = hasBudget ? Math.min(pct, 100) : Math.max(0, (c.actualAmount / maxActual) * 100);
                const remaining = c.budgetedAmount - c.actualAmount;
                const footLeft = escapeHtml(hasBudget
                    ? (remaining >= 0 ? t('Left', { amount: formatMoney(remaining) }) : t('Over', { amount: formatMoney(-remaining) }))
                    : t('NoBudgetSet'));
                const footRight = hasBudget ? escapeHtml(t('Used', { percent: formatPercent(Math.round(pct)) })) : '';
                const statusHtml = (tone === 'warning' || tone === 'critical') ? ` ${ statusPill(tone, categoryStatusLabel(tone))}` : '';

                html += '<div class="bgt-cat-row">' +
                    '<div class="bgt-cat-head">' +
                        `<span class="bgt-cat-name" title="${ escapeHtml(c.categoryName) }">${ escapeHtml(c.categoryName) }</span>` +
                        `<span class="bgt-cat-amount">${ escapeHtml(formatMoney(c.actualAmount)) }${hasBudget ? ` <small>${ escapeHtml(t('OfBudget', { amount: formatMoney(c.budgetedAmount) })) }</small>` : '' }</span>` +
                    '</div>' +
                    `<div class="bgt-meter${ hasBudget ? '' : ' bgt-meter--nobudget' }" role="meter" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${ Math.round(width) }"` +
                        ` aria-label="${ escapeHtml(hasBudget
                            ? t('CategoryMeterAria', { name: c.categoryName, percent: formatPercent(Math.round(pct)) })
                            : t('CategoryMeterAriaNoBudget', { name: c.categoryName })) }">` +
                        `<div class="bgt-meter-fill bgt-meter-fill--${ tone }" style="width: ${ width.toFixed(1) }%;"></div>` +
                    '</div>' +
                    `<div class="bgt-cat-foot"><span>${ footLeft }${statusHtml }</span><span>${ footRight }</span></div>` +
                '</div>';
            });
            $('#budgetCategoryList').html(html);
        }

        function destroyYearCharts () {
            if (budgetMainChartInstance) { budgetMainChartInstance.destroy(); budgetMainChartInstance = null; }
            if (budgetNetChartInstance) { budgetNetChartInstance.destroy(); budgetNetChartInstance = null; }
        }

        function baseChartOptions (months) {
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
                    if (evt.native && evt.native.target) { evt.native.target.style.cursor = elements.length ? 'pointer' : 'default'; }
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: '#0b0b0b',
                        padding: 10,
                        cornerRadius: 8,
                        boxPadding: 4,
                        callbacks: {
                            title: function (items) { return items.length ? monthName(months[items[0].dataIndex].monthNumber) : ''; },
                            footer: function () { return t('ClickToOpenMonth'); }
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

        function renderYearCharts (months) {
            destroyYearCharts();
            const labels = months.map((m) => { return monthName(m.monthNumber, 'short'); });
            const barStyle = { borderRadius: 4, borderSkipped: 'start', maxBarThickness: 24, categoryPercentage: 0.7, barPercentage: 0.9 };

            const mainOptions = baseChartOptions(months);
            mainOptions.plugins.tooltip.callbacks.label = function (ctx) {
                return ` ${ PFI18n.t('GoogleDrive.Js.LabelValue', { label: ctx.dataset.label, amount: formatCurrency(ctx.parsed.y) }) }`;
            };
            mainOptions.plugins.tooltip.callbacks.afterBody = function (items) {
                const m = items.length ? months[items[0].dataIndex] : null;
                return m ? ` ${ t('TooltipNet', { amount: formatCurrency(m.netSavings) }) }` : '';
            };
            budgetMainChartInstance = new Chart(document.getElementById('budgetMainChart').getContext('2d'), {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [
                        $.extend({ label: PFI18n.t('GoogleDrive.Js.Income'), data: months.map((m) => { return m.actualIncome; }), backgroundColor: BUDGET_COLORS.income }, barStyle),
                        $.extend({ label: PFI18n.t('GoogleDrive.Js.Spending'), data: months.map((m) => { return m.actualExpenses; }), backgroundColor: BUDGET_COLORS.spend }, barStyle)
                    ]
                },
                options: mainOptions
            });

            const netOptions = baseChartOptions(months);
            netOptions.plugins.tooltip.callbacks.label = function (ctx) {
                const v = ctx.parsed.y;
                return ` ${ v >= 0 ? t('TooltipSaved', { amount: formatCurrency(v) }) : t('TooltipOverspent', { amount: formatCurrency(-v) }) }`;
            };
            netOptions.scales.y.beginAtZero = true;
            netOptions.scales.x.ticks.maxRotation = 0;
            netOptions.scales.x.ticks.autoSkipPadding = 6;
            netOptions.scales.y.grid.color = function (ctx) { return ctx.tick && ctx.tick.value === 0 ? BUDGET_COLORS.axis : BUDGET_COLORS.grid; };
            const netValues = months.map((m) => { return m.netSavings; });
            budgetNetChartInstance = new Chart(document.getElementById('budgetNetChart').getContext('2d'), {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [$.extend({
                        label: t('NetSaved'),
                        data: netValues,
                        backgroundColor: netValues.map((v) => { return v >= 0 ? BUDGET_COLORS.income : BUDGET_COLORS.negative; })
                    }, barStyle, { categoryPercentage: 0.6, barPercentage: 1 })]
                },
                options: netOptions
            });
        }

        function renderYearView (data) {
            const months = data.months;
            const status = yearStatus(data);
            const underCount = months.filter((m) => { return m.status === 'Under Budget'; }).length;

            $('#budgetPeriodTitle').text(t('YearTitle', { year: data.year }));
            $('#budgetPeriodSubtitle').html(balanceText(data.startingBalance, data.endingBalance) ||
                escapeHtml(PFI18n.plural('GoogleDrive.Js.Report.MonthsOfData', months.length)));

            // KPI tiles
            $('#kpiTotalIncome').text(formatMoney(data.totalAnnualIncome));
            $('#kpiIncomeSubtext').text(planDeltaText(data.totalAnnualIncome, data.totalAnnualBudgetedIncome, 'PlanAbove', 'PlanBelow') ||
                t('AvgPerMonth', { amount: formatMoney(data.totalAnnualIncome / months.length) }));
            $('#kpiTotalExpenses').text(formatMoney(data.totalAnnualExpenses));
            $('#kpiExpensesSubtext').text(planDeltaText(data.totalAnnualExpenses, data.totalAnnualBudgetedExpenses, 'PlanOver', 'PlanUnder') ||
                t('AvgPerMonth', { amount: formatMoney(data.totalAnnualExpenses / months.length) }));
            $('#kpiNetSavings').text(formatMoney(data.totalAnnualSavings));
            $('#kpiSavingsSubtext').text(data.totalAnnualSavings >= 0 ? PFI18n.t('GoogleDrive.Js.IncomeMinusSpending') : t('SpentMoreThanEarned'));
            renderTileMeter('#kpiIncomeMeter', data.totalAnnualIncome, data.totalAnnualBudgetedIncome, 'income', PFI18n.t('GoogleDrive.Js.Income'));
            renderTileMeter('#kpiExpensesMeter', data.totalAnnualExpenses, data.totalAnnualBudgetedExpenses, 'spend', PFI18n.t('GoogleDrive.Js.Spending'));
            renderSavingsRate(data.averageSavingsRate);
            renderAllocation(data.totalAnnualIncome, data.totalAnnualExpenses);

            // Plain-language summary
            const best = months.reduce((a, b) => { return b.netSavings > a.netSavings ? b : a; });
            const worst = months.reduce((a, b) => { return b.netSavings < a.netSavings ? b : a; });
            const categories = aggregateCategories(months);
            const top = categories.slice().sort((a, b) => { return b.actualAmount - a.actualAmount; })[0];
            // Sentences are HTML: the templates are the app's own text, every value is escaped
            const sentences = [
                data.totalAnnualSavings >= 0
                    ? t('YearSaved', { year: data.year, amount: strong(formatMoney(data.totalAnnualSavings)), rate: strong(formatPercent(data.averageSavingsRate)) })
                    : t('YearOverspent', { year: data.year, amount: strong(formatMoney(-data.totalAnnualSavings)) }),
                t('MonthsUnderBudget', { under: strong(PFI18n.formatNumber(underCount)), total: strong(PFI18n.formatNumber(months.length)) }),
                months.length > 1 && best.netSavings > 0
                    ? t('BestMonth', { month: escapeHtml(monthName(best.monthNumber)), amount: escapeHtml(formatMoney(best.netSavings)) })
                    : '',
                months.length > 1 && worst.netSavings < 0
                    ? t('ToughestMonth', { month: escapeHtml(monthName(worst.monthNumber)), amount: escapeHtml(formatMoney(-worst.netSavings)) })
                    : '',
                top && top.actualAmount > 0 && data.totalAnnualExpenses > 0
                    ? t('BiggestExpense', {
                        name: escapeHtml(top.categoryName),
                        amount: escapeHtml(formatMoney(top.actualAmount)),
                        percent: escapeHtml(formatPercent(Math.round(top.actualAmount / data.totalAnnualExpenses * 100)))
                    })
                    : ''
            ];
            renderInsight(monthStatusTone(status), sentences, statusPill(monthStatusTone(status), monthStatusLabel(status)));

            // Charts
            $('#budgetYearCharts').removeClass('d-none');
            renderYearCharts(months);

            // Categories
            $('#budgetCategoryTitle').text(t('CategoryTitleYear', { year: data.year }));
            $('#budgetCategorySubtitle').text(t('CategorySubtitleYear'));
            renderCategoryList(categories);

            // Month-by-month table: content only, the period chips are the way to change period
            $('#tableSectionTitle').text(t('MonthByMonth'));
            let tableHtml = '<table class="table table-hover align-middle mb-0 bgt-table">' +
                '<thead><tr>' +
                `<th scope="col">${ escapeHtml(t('ColMonth')) }</th>` +
                `<th scope="col" class="text-end">${ escapeHtml(PFI18n.t('GoogleDrive.Js.Income')) }</th>` +
                `<th scope="col" class="text-end">${ escapeHtml(t('ColPlannedSpending')) }</th>` +
                `<th scope="col" class="text-end">${ escapeHtml(t('Spent')) }</th>` +
                `<th scope="col" class="text-end">${ escapeHtml(t('NetSaved')) }</th>` +
                `<th scope="col" class="text-end">${ escapeHtml(t('ColSavingsRate')) }</th>` +
                `<th scope="col">${ escapeHtml(t('ColStatus')) }</th>` +
                '</tr></thead><tbody>';
            months.forEach((m) => {
                tableHtml += '<tr>' +
                    `<td class="fw-semibold">${ escapeHtml(monthName(m.monthNumber)) }</td>` +
                    `<td class="text-end">${ formatCurrency(m.actualIncome) }</td>` +
                    `<td class="text-end text-muted">${ m.budgetedExpenses > 0 ? formatCurrency(m.budgetedExpenses) : '&mdash;' }</td>` +
                    `<td class="text-end">${ formatCurrency(m.actualExpenses) }</td>` +
                    `<td class="text-end fw-semibold">${ formatCurrency(m.netSavings) }</td>` +
                    `<td class="text-end">${ escapeHtml(formatPercent(m.savingsRate, 1)) }</td>` +
                    `<td>${ statusPill(monthStatusTone(m.status), monthStatusLabel(m.status)) }</td>` +
                    '</tr>';
            });
            tableHtml += '</tbody></table>';
            $('#budgetReportTableContainer').html(tableHtml);
            if (!budgetTableUserToggled) { setTableVisible(true); }
        }

        function renderMonthView (data, monthData) {
            const tone = monthStatusTone(monthData.status);
            const single = data.months.length === 1;

            $('#budgetPeriodTitle').text(monthYear(monthData.monthNumber));
            $('#budgetPeriodSubtitle').html(balanceText(monthData.startingBalance, monthData.endingBalance));

            // KPI tiles
            $('#kpiTotalIncome').text(formatMoney(monthData.actualIncome));
            $('#kpiIncomeSubtext').text(planDeltaText(monthData.actualIncome, monthData.budgetedIncome, 'PlanAbove', 'PlanBelow') || t('NoIncomePlan'));
            $('#kpiTotalExpenses').text(formatMoney(monthData.actualExpenses));
            $('#kpiExpensesSubtext').text(planDeltaText(monthData.actualExpenses, monthData.budgetedExpenses, 'PlanOver', 'PlanUnder') || t('NoSpendingPlan'));
            $('#kpiNetSavings').text(formatMoney(monthData.netSavings));
            $('#kpiSavingsSubtext').text(
                (monthData.budgetedIncome > 0 || monthData.budgetedExpenses > 0)
                    ? t('Planned', { amount: formatMoney(monthData.budgetedNetSavings !== undefined ? monthData.budgetedNetSavings : monthData.budgetedIncome - monthData.budgetedExpenses) })
                    : (monthData.netSavings >= 0 ? PFI18n.t('GoogleDrive.Js.IncomeMinusSpending') : t('SpentMoreThanEarned')));
            renderTileMeter('#kpiIncomeMeter', monthData.actualIncome, monthData.budgetedIncome, 'income', PFI18n.t('GoogleDrive.Js.Income'));
            renderTileMeter('#kpiExpensesMeter', monthData.actualExpenses, monthData.budgetedExpenses, 'spend', PFI18n.t('GoogleDrive.Js.Spending'));
            renderSavingsRate(monthData.savingsRate);
            renderAllocation(monthData.actualIncome, monthData.actualExpenses);

            // Plain-language summary
            const categories = monthData.categories || [];
            const overList = categories
                .filter((c) => { return categoryTone(c) === 'critical'; })
                .sort((a, b) => { return (b.actualAmount - b.budgetedAmount) - (a.actualAmount - a.budgetedAmount); });
            const budgetedCount = categories.filter((c) => { return c.budgetedAmount > 0; }).length;
            let overText = '';
            if (overList.length > 0) {
                const items = overList.slice(0, 3).map((c) => {
                    return t('OverItem', { name: c.categoryName, amount: formatMoney(c.actualAmount - c.budgetedAmount) });
                }).join(', ');
                const list = overList.length > 3 ? t('AndMore', { list: items, count: PFI18n.formatNumber(overList.length - 3) }) : items;
                overText = t('OverSentence', {
                    summary: strong(PFI18n.plural('GoogleDrive.Js.Report.CategoriesOver', overList.length)),
                    list: escapeHtml(list)
                });
            } else if (budgetedCount > 0) {
                overText = escapeHtml(t('AllWithinLimit'));
            }
            const planDiff = monthData.budgetedExpenses - monthData.actualExpenses;
            const month = escapeHtml(monthName(monthData.monthNumber));
            renderInsight(tone, [
                monthData.netSavings >= 0
                    ? t('MonthSaved', { amount: strong(formatMoney(monthData.netSavings)), month: month, rate: strong(formatPercent(monthData.savingsRate)) })
                    : t('MonthOverspent', { amount: strong(formatMoney(-monthData.netSavings)), month: month }),
                monthData.budgetedExpenses > 0
                    ? t(planDiff >= 0 ? 'SpendingUnderPlan' : 'SpendingOverPlan', {
                        amount: strong(formatMoney(Math.abs(planDiff))),
                        plan: escapeHtml(formatMoney(monthData.budgetedExpenses))
                    })
                    : '',
                overText
            ], statusPill(tone, monthStatusLabel(monthData.status)));

            // No year charts in month view
            destroyYearCharts();
            $('#budgetYearCharts').addClass('d-none');

            // Categories
            $('#budgetCategoryTitle').text(t('CategoryTitle'));
            $('#budgetCategorySubtitle').text(t('CategorySubtitle'));
            renderCategoryList(categories);

            // Category table
            $('#tableSectionTitle').text(t('MonthCategoryDetails', { month: monthName(monthData.monthNumber) }));
            let tableHtml = '<table class="table table-hover align-middle mb-0 bgt-table">' +
                '<thead><tr>' +
                `<th scope="col">${ escapeHtml(t('ColCategory')) }</th>` +
                `<th scope="col" class="text-end">${ escapeHtml(t('ColPlanned')) }</th>` +
                `<th scope="col" class="text-end">${ escapeHtml(t('Spent')) }</th>` +
                `<th scope="col" class="text-end">${ escapeHtml(t('ColLeftOver')) }</th>` +
                `<th scope="col" class="text-end">${ escapeHtml(t('ColPercentUsed')) }</th>` +
                `<th scope="col">${ escapeHtml(t('ColStatus')) }</th>` +
                '</tr></thead><tbody>';
            categories.slice().sort((a, b) => { return b.actualAmount - a.actualAmount; }).forEach((c) => {
                const cTone = categoryTone(c);
                const hasBudget = cTone !== 'none';
                tableHtml += '<tr>' +
                    `<td class="fw-semibold">${ escapeHtml(c.categoryName) }</td>` +
                    `<td class="text-end text-muted">${ hasBudget ? formatCurrency(c.budgetedAmount) : '&mdash;' }</td>` +
                    `<td class="text-end">${ formatCurrency(c.actualAmount) }</td>` +
                    `<td class="text-end">${ hasBudget ? escapeHtml(c.variance >= 0 ? t('Left', { amount: formatCurrency(c.variance) }) : t('Over', { amount: formatCurrency(-c.variance) })) : '&mdash;' }</td>` +
                    `<td class="text-end">${ hasBudget ? escapeHtml(formatPercent(c.percentageUsed)) : '&mdash;' }</td>` +
                    `<td>${ hasBudget ? statusPill(cTone, categoryStatusLabel(cTone)) : `<span class="text-muted small">${ escapeHtml(t('NoBudgetSet')) }</span>` }</td>` +
                    '</tr>';
            });
            tableHtml += '</tbody></table>';
            $('#budgetReportTableContainer').html(tableHtml);
            if (!budgetTableUserToggled) { setTableVisible(single); }
        }

        function renderBudgetMonth (monthNum) {
            if (!currentBudgetData || !currentBudgetData.months || currentBudgetData.months.length === 0) { return; }
            const isSingleMonth = currentBudgetData.months.length === 1;

            if (monthNum === 0 && !isSingleMonth) {
                currentSelectedMonth = 0;
                setPeriodChips();
                renderYearView(currentBudgetData);
            } else {
                const monthData = currentBudgetData.months.find((m) => { return m.monthNumber === monthNum; }) || currentBudgetData.months[0];
                currentSelectedMonth = monthData.monthNumber;
                setPeriodChips();
                renderMonthView(currentBudgetData, monthData);
            }
        }

        function stepBudgetPeriod (direction) {
            const idx = budgetPeriods.findIndex((p) => { return p.month === currentSelectedMonth; });
            const next = budgetPeriods[idx + direction];
            if (next) {
                renderBudgetMonth(next.month);
                $('#budgetMonthButtonGroup .active').trigger('focus');
            }
        }

        function showBudgetState (state) {
            $('#budgetLoadingState').toggleClass('d-none', state !== 'loading');
            $('#budgetErrorState').toggleClass('d-none', state !== 'error');
            $('#budgetReportContent').toggleClass('d-none', state !== 'report');
            $('#btnPrintBudgetReport, #btnExportBudget').prop('disabled', state !== 'report');
            $('#btnBudgetToTransactions').prop('disabled', state === 'loading');
        }

        function loadBudgetReport (request) {
            lastBudgetRequest = request;
            const seq = ++budgetRequestSeq;
            currentBudgetData = null;
            budgetTableUserToggled = false;
            destroyYearCharts();

            $('#budgetModalFileName').text(request.fileName).attr('title', request.fileName);
            // jQuery caches data-* reads, so set both the cache and the attribute the transactions script reads
            $('#btnBudgetToTransactions')
                .attr({ 'data-file-id': request.fileId, 'data-file-name': request.fileName, 'data-is-budget': 'true' })
                .data({ fileId: request.fileId, fileName: request.fileName, isBudget: true });
            $('#budgetModalDataSource').empty();
            showBudgetState('loading');

            $.ajax({
                url: $modal.data('report-url'),
                method: 'GET',
                data: request,
                success: function (data) {
                    if (seq !== budgetRequestSeq) { return; } // a newer request superseded this one

                    if (!data || !data.months || data.months.length === 0) {
                        $('#budgetErrorMessage').text((data && data.errorMessage) || t('NoData'));
                        showBudgetState('error');
                        return;
                    }

                    currentBudgetData = data;
                    currentCurrency = data.currencyCode || 'USD';
                    const rowsText = data.parsedRowCount > 0 ? ` · ${ PFI18n.plural('GoogleDrive.Js.Report.RowsRead', data.parsedRowCount) }` : '';
                    const sourceClass = data.isLiveSpreadsheetData ? 'bgt-source--live' : 'bgt-source--sample';
                    $('#budgetModalDataSource').html(`<span class="bgt-source ${ sourceClass }">${ escapeHtml(sourceLabel(data)) }${escapeHtml(rowsText) }</span>`);

                    budgetPeriods = [];
                    if (data.months.length > 1) {
                        budgetPeriods.push({ month: 0, label: t('FullYear'), title: t('YearOverview', { year: data.year }) });
                    }
                    data.months.forEach((m) => {
                        budgetPeriods.push({
                            month: m.monthNumber,
                            label: data.months.length > 1 ? monthName(m.monthNumber, 'short') : monthYear(m.monthNumber),
                            title: monthYear(m.monthNumber)
                        });
                    });

                    showBudgetState('report');
                    renderBudgetMonth(data.months.length > 1 ? 0 : data.months[0].monthNumber);
                },
                error: function () {
                    if (seq !== budgetRequestSeq) { return; }
                    $('#budgetErrorMessage').text(t('ErrorText'));
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

        $('#btnBudgetRetry').on('click', () => {
            if (lastBudgetRequest) { loadBudgetReport(lastBudgetRequest); }
        });

        // Period selector, prev/next and keyboard navigation
        $(document).on('click', '.budget-month-btn', function () {
            renderBudgetMonth(parseInt($(this).data('month'), 10));
        });
        $('#btnBudgetPrev').on('click', () => { stepBudgetPeriod(-1); });
        $('#btnBudgetNext').on('click', () => { stepBudgetPeriod(1); });
        $('#monthlyBudgetModal').on('keydown', (e) => {
            if (!currentBudgetData || $(e.target).is('input, select, textarea') || $(e.target).closest('.dropdown').length) { return; }
            if (e.key === 'ArrowLeft') { e.preventDefault(); stepBudgetPeriod(-1); }
            if (e.key === 'ArrowRight') { e.preventDefault(); stepBudgetPeriod(1); }
        });

        // Escape in the open ⋯ menu closes only the menu. The modal listens on its own element, before Bootstrap's
        // dropdown handler on the document, so catch the key on the way down.
        const budgetModalEl = document.getElementById('monthlyBudgetModal');
        if (budgetModalEl) {
            budgetModalEl.addEventListener('keydown', (e) => {
                const toggle = document.getElementById('btnBudgetMore');
                if (e.key !== 'Escape' || !toggle || toggle.getAttribute('aria-expanded') !== 'true') { return; }
                e.stopPropagation();
                bootstrap.Dropdown.getOrCreateInstance(toggle).hide();
                toggle.focus();
            }, true);
        }

        $('#budgetCategorySort, #budgetOverOnly').on('change', () => {
            renderCategoryList();
        });

        // Export the table behind the current view: months for the full year, categories for a month
        function csvCell (value) {
            const text = String(value === null || value === undefined ? '' : value);
            return /[",\n]/.test(text) ? `"${ text.replace(/"/g, '""') }"` : text;
        }

        function downloadCsv (fileName, rows) {
            const csv = rows.map((r) => { return r.map(csvCell).join(','); }).join('\r\n');
            // BOM so Excel opens the file as UTF-8
            const blob = new Blob([`\ufeff${ csv}`], { type: 'text/csv;charset=utf-8' });
            const link = document.createElement('a');
            link.href = URL.createObjectURL(blob);
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => { URL.revokeObjectURL(link.href); }, 1000);
        }

        $('#btnExportBudget').on('click', () => {
            if (!currentBudgetData) { return; }
            const baseName = String(lastBudgetRequest && lastBudgetRequest.fileName || 'budget').replace(/\.[^.]+$/, '');
            const isYear = currentSelectedMonth === 0 && currentBudgetData.months.length > 1;
            let rows;
            if (isYear) {
                // Headers and labels in the page language; amounts stay machine-readable (dot decimal)
                rows = [[t('ColMonth'), t('ColPlannedIncome'), PFI18n.t('GoogleDrive.Js.Income'), t('ColPlannedSpending'), t('Spent'),
                    t('NetSaved'), t('ColSavingsRatePercent'), t('ColStatus')]];
                currentBudgetData.months.forEach((m) => {
                    rows.push([monthName(m.monthNumber), m.budgetedIncome, m.actualIncome, m.budgetedExpenses, m.actualExpenses, m.netSavings,
                        m.savingsRate.toFixed(1), monthStatusLabel(m.status)]);
                });
            } else {
                const month = currentBudgetData.months.find((m) => { return m.monthNumber === currentSelectedMonth; }) || currentBudgetData.months[0];
                rows = [[t('ColCategory'), t('ColPlanned'), t('Spent'), t('ColLeftNegativeOver'), t('ColPercentUsed'), t('ColStatus')]];
                (month.categories || []).forEach((c) => {
                    const tone = categoryTone(c);
                    rows.push([c.categoryName, c.budgetedAmount, c.actualAmount, (c.budgetedAmount - c.actualAmount).toFixed(2),
                        c.budgetedAmount > 0 ? c.percentageUsed.toFixed(0) : '', categoryStatusLabel(tone)]);
                });
            }
            const selected = currentBudgetData.months.find((m) => { return m.monthNumber === currentSelectedMonth; });
            const period = isYear ? String(currentBudgetData.year) : (selected ? monthName(selected.monthNumber) : 'month');
            downloadCsv(`${baseName } - ${ period }.csv`, rows);
        });

        $('#btnToggleBudgetTable').on('click', () => {
            budgetTableUserToggled = true;
            setTableVisible($('#budgetReportTableContainer').hasClass('d-none'));
        });

        // Charts rendered during the open animation can measure a zero-size canvas; re-measure once visible
        $('#monthlyBudgetModal').on('shown.bs.modal', () => {
            if (budgetMainChartInstance) { budgetMainChartInstance.resize(); }
            if (budgetNetChartInstance) { budgetNetChartInstance.resize(); }
        });

        $('#monthlyBudgetModal').on('hidden.bs.modal', () => {
            budgetRequestSeq++;
            destroyYearCharts();
        });

        // Print Budget Report Handler: print only the report, with the table expanded
        $('#btnPrintBudgetReport').on('click', () => {
            $('body').addClass('bgt-printing');
            window.print();
        });
        window.addEventListener('afterprint', () => {
            $('body').removeClass('bgt-printing');
        });
    });
})(jQuery);

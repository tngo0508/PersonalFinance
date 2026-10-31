/* global PFI18n */
(function () {
    'use strict';

    /**
     * The currency of every amount on the page: the Account's Reporting Currency (ADR 0016), from the page root.
     * @returns {string} ISO 4217 code.
     */
    function pageCurrency () {
        const page = document.querySelector('.analysis-page');
        return (page && page.dataset.currency) || 'USD';
    }

    /**
     * Read the Money Flow colours of the theme in effect from the card's CSS custom properties (css/analysis.css,
     * tokens in site.css), and the label colour from the chart palette (core/chart-palette.js, #489).
     * @param {Element} card - The Money Flow section element.
     * @returns {{slots: string[], kinds: Object<string, string>, label: string, fallback: string}} Category slot
     *     colours, colours by node kind, the node label colour, and the colour for anything without a token.
     */
    function readColors (card) {
        const style = getComputedStyle(card);
        const palette = window.ChartPalette.read();
        const read = (name) => style.getPropertyValue(name).trim() || palette.axis;
        return {
            label: palette.axis,
            fallback: palette.axis,
            slots: [0, 1, 2, 3, 4].map((slot) => read(`--money-flow-slot-${slot}`)),
            kinds: {
                income: read('--money-flow-income'),
                shortfall: read('--money-flow-shortfall'),
                spending: read('--money-flow-spending'),
                uncategorized: read('--money-flow-uncategorized'),
                other: read('--money-flow-other'),
                saved: read('--money-flow-saved')
            }
        };
    }

    /**
     * Format an amount as whole currency units in the page culture, e.g. "$1,800".
     * @param {number} amount - The amount to format.
     * @returns {string} The formatted amount.
     */
    function formatAmount (amount) {
        return PFI18n.formatMoney(amount, pageCurrency(), { whole: true });
    }

    /**
     * Format a share as a percentage with one decimal in the page culture, e.g. "12.5%".
     * @param {number} value - The percentage, 0-100.
     * @returns {string} The formatted percentage.
     */
    function formatShare (value) {
        return PFI18n.t('Analysis.Js.Percent', {
            value: PFI18n.formatNumber(value, { minimumFractionDigits: 1, maximumFractionDigits: 1 })
        });
    }

    /**
     * Build the label shown on a Sankey node: name, amount and, for right-hand nodes, the share.
     * @param {Object} node - A Money Flow node from the server model.
     * @returns {string} The node label.
     */
    function nodeLabel (node) {
        const parts = [node.label, formatAmount(node.amount)];
        if (node.sharePercent !== null && node.sharePercent !== undefined) {
            parts.push(formatShare(node.sharePercent));
        }
        return parts.join(' · ');
    }

    /**
     * Render the Money Flow Sankey chart from the server-built model.
     * @param {Object} flow - The Money Flow model (nodes, links, shareBase).
     * @returns {Chart|null} The chart, or null when there is nothing to draw.
     */
    function renderMoneyFlow (flow) {
        const canvas = document.getElementById('moneyFlowChart');
        if (!canvas || !flow || !flow.links || flow.links.length === 0 || typeof Chart === 'undefined') {
            return null;
        }

        // On narrow charts the amounts don't fit beside the bands; the table below them carries the figures.
        const compact = canvas.parentElement.clientWidth < 576;
        const colors = readColors(canvas.closest('.money-flow') || canvas);
        const nodesByKey = {};
        const labels = {};
        const columns = {};
        const priority = {};
        flow.nodes.forEach((node, index) => {
            nodesByKey[node.key] = node;
            labels[node.key] = compact ? node.label : nodeLabel(node);
            columns[node.key] = node.column;
            priority[node.key] = index;
        });

        /**
         * Colour for the node at one end of a link.
         * @param {Object} context - The Chart.js scriptable context.
         * @param {string} end - 'from' or 'to'.
         * @returns {string} A hex colour.
         */
        function endColor (context, end) {
            const link = context.dataset.data[context.dataIndex];
            const node = link ? nodesByKey[link[end]] : null;
            if (!node) {
                return colors.fallback;
            }
            if (node.kind === 'category') {
                return colors.slots[node.colorSlot] || colors.fallback;
            }
            return colors.kinds[node.kind] || colors.fallback;
        }

        return new Chart(canvas.getContext('2d'), {
            type: 'sankey',
            data: {
                datasets: [{
                    data: flow.links.map((link) => ({ from: link.from, to: link.to, flow: link.amount })),
                    labels: labels,
                    column: columns,
                    priority: priority,
                    colorFrom: function (context) {
                        return endColor(context, 'from');
                    },
                    colorTo: function (context) {
                        return endColor(context, 'to');
                    },
                    colorMode: 'gradient',
                    // Node labels: the plugin's default black is unreadable on the dark card
                    color: colors.label,
                    size: 'max',
                    nodePadding: 16,
                    font: { size: 12.5 }
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const link = context.dataset.data[context.dataIndex];
                                const args = {
                                    from: nodesByKey[link.from].label,
                                    to: nodesByKey[link.to].label,
                                    amount: formatAmount(link.flow)
                                };
                                if (flow.shareBase > 0) {
                                    args.share = formatShare(link.flow / flow.shareBase * 100);
                                    return PFI18n.t('Analysis.Js.FlowTooltipShare', args);
                                }
                                return PFI18n.t('Analysis.Js.FlowTooltip', args);
                            }
                        }
                    }
                }
            }
        });
    }

    /**
     * Render Money Flow step by step: income at the top, each category taking its share down to what was saved. In a
     * Shortfall month the steps start from total spending, with the Shortfall stacked on top of income.
     * @param {Object} flow - The Money Flow model (income, expenses, shortfall, saved, tableRows).
     * @returns {Chart|null} The chart, or null when there is nothing to draw.
     */
    function renderSteps (flow) {
        const canvas = document.getElementById('moneyFlowStepsChart');
        if (!canvas || !flow || !flow.tableRows || typeof Chart === 'undefined') {
            return null;
        }

        const colors = readColors(canvas.closest('.money-flow') || canvas);
        const palette = window.ChartPalette.read();
        const base = flow.shareBase > 0 ? flow.shareBase : flow.income;
        const rows = [{ label: PFI18n.t('Analysis.Js.Income'), range: [0, flow.income], color: colors.kinds.income }];
        if (flow.shortfall > 0) {
            rows.push({ label: PFI18n.t('Analysis.Js.Shortfall'), range: [flow.income, flow.expenses], color: colors.kinds.shortfall });
        }
        let top = Math.max(flow.income, flow.expenses);
        flow.tableRows.filter((row) => row.kind !== 'saved').forEach((row) => {
            let color = colors.kinds[row.kind] || colors.fallback;
            if (row.inOther) {
                color = colors.kinds.other;
            } else if (row.colorSlot !== null && row.colorSlot !== undefined) {
                color = colors.slots[row.colorSlot] || colors.fallback;
            }
            rows.push({ label: row.name, range: [top - row.amount, top], color: color, share: row.sharePercent });
            top -= row.amount;
        });
        if (flow.saved > 0) {
            rows.push({ label: PFI18n.t('Analysis.Js.Saved'), range: [0, flow.saved], color: colors.kinds.saved, share: flow.saved / base * 100 });
        }

        return new Chart(canvas.getContext('2d'), {
            type: 'bar',
            data: {
                labels: rows.map((row) => row.label),
                datasets: [{
                    data: rows.map((row) => row.range),
                    backgroundColor: rows.map((row) => row.color),
                    borderRadius: 3,
                    borderSkipped: false,
                    barPercentage: 0.8,
                    categoryPercentage: 0.9
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: 'y',
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: palette.tooltip.background,
                        titleColor: palette.tooltip.text,
                        bodyColor: palette.tooltip.text,
                        callbacks: {
                            label: function (context) {
                                const row = rows[context.dataIndex];
                                const amount = formatAmount(row.range[1] - row.range[0]);
                                if (row.share === undefined || row.share === null) {
                                    return amount;
                                }
                                return PFI18n.t('Analysis.Js.ShareTooltip', {
                                    label: row.label,
                                    amount: amount,
                                    percent: PFI18n.formatNumber(row.share, { minimumFractionDigits: 1, maximumFractionDigits: 1 })
                                });
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        grid: { color: palette.grid },
                        border: { color: palette.grid },
                        ticks: { color: palette.axis, maxTicksLimit: 6, callback: (value) => formatAmount(value) }
                    },
                    y: {
                        grid: { display: false },
                        border: { color: palette.grid },
                        ticks: { color: palette.axis, autoSkip: false }
                    }
                }
            }
        });
    }

    /** The viewer's last choice between the flow and the step-down view, kept in this browser only. */
    const VIEW_KEY = 'analysis.moneyFlowView';

    /**
     * Read the remembered Money Flow view.
     * @returns {string} 'flow' or 'steps'.
     */
    function rememberedView () {
        try {
            return window.localStorage.getItem(VIEW_KEY) === 'steps' ? 'steps' : 'flow';
        } catch (e) {
            return 'flow';
        }
    }

    /**
     * Show one Money Flow view and hide the other; the switch's buttons say which is pressed.
     * @param {Element} section - The Money Flow section.
     * @param {string} view - 'flow' or 'steps'.
     * @returns {void}
     */
    function showView (section, view) {
        section.querySelectorAll('[data-flow-panel]').forEach((panel) => {
            panel.hidden = panel.dataset.flowPanel !== view;
        });
        section.querySelectorAll('[data-flow-view]').forEach((button) => {
            button.setAttribute('aria-pressed', button.dataset.flowView === view ? 'true' : 'false');
        });
    }

    /**
     * Initialize both Money Flow views and the switch between them once the page data is available.
     * @returns {void}
     */
    function init () {
        const data = window.__analysisData;
        const section = document.querySelector('.money-flow');
        if (!data || !section) {
            return;
        }

        // Narrow screens draw the flow without amounts, so the table opens to carry them
        const table = section.querySelector('[data-flow-table]');
        if (table && section.clientWidth < 576) {
            table.open = true;
        }

        const toggle = section.querySelector('[data-flow-switch]');
        if (toggle) {
            toggle.hidden = false;
            showView(section, rememberedView());
            toggle.addEventListener('click', (event) => {
                const button = event.target.closest('[data-flow-view]');
                if (!button) {
                    return;
                }
                showView(section, button.dataset.flowView);
                try {
                    window.localStorage.setItem(VIEW_KEY, button.dataset.flowView);
                } catch (e) {
                    // Storage unavailable: the choice lasts until the page reloads
                }
            });
        }

        let charts = [renderMoneyFlow(data.moneyFlow), renderSteps(data.moneyFlow)];
        // A theme switch redraws the charts in the new colours without a reload (#489)
        window.ChartPalette.subscribe(() => {
            charts.forEach((chart) => chart && chart.destroy());
            charts = [renderMoneyFlow(data.moneyFlow), renderSteps(data.moneyFlow)];
        });
    }

    // DOM ready pattern
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

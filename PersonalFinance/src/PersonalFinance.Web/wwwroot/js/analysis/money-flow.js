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

    // Fallbacks if the card's CSS variables are missing; the view defines the real values (see Index.cshtml).
    const FALLBACK_COLOR = '#9CA3AF';

    /**
     * Read the Money Flow colours from the card's CSS custom properties.
     * @param {Element} card - The Money Flow section element.
     * @returns {{slots: string[], kinds: Object<string, string>}} Category slot colours and colours by node kind.
     */
    function readColors (card) {
        const style = getComputedStyle(card);
        const read = (name) => style.getPropertyValue(name).trim() || FALLBACK_COLOR;
        return {
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
     * @returns {void}
     */
    function renderMoneyFlow (flow) {
        const canvas = document.getElementById('moneyFlowChart');
        if (!canvas || !flow || !flow.links || flow.links.length === 0 || typeof Chart === 'undefined') {
            return;
        }

        // On narrow charts the amounts don't fit beside the bands; the table above them carries the figures.
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
                return FALLBACK_COLOR;
            }
            if (node.kind === 'category') {
                return colors.slots[node.colorSlot] || FALLBACK_COLOR;
            }
            return colors.kinds[node.kind] || FALLBACK_COLOR;
        }

        new Chart(canvas.getContext('2d'), {
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
                    size: 'max',
                    nodePadding: 14
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
     * Initialize the Money Flow chart once the page data is available.
     * @returns {void}
     */
    function init () {
        const data = window.__analysisData;
        if (!data) {
            return;
        }

        renderMoneyFlow(data.moneyFlow);
    }

    // DOM ready pattern
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

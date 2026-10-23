(function () {
    'use strict';

    // Colours by node kind, from the site.css tokens (brand indigo, positive, negative, warning, neutral grey).
    const KIND_COLORS = {
        income: '#4F46E5',
        shortfall: '#DC2626',
        spending: '#6B7280',
        category: '#6366F1',
        uncategorized: '#D97706',
        other: '#9CA3AF',
        saved: '#059669'
    };

    /**
     * Format an amount as whole US dollars, e.g. "$1,800".
     * @param {number} amount - The amount to format.
     * @returns {string} The formatted amount.
     */
    function formatAmount (amount) {
        return `$${Math.round(amount).toLocaleString('en-US')}`;
    }

    /**
     * Build the label shown on a Sankey node: name, amount and, for right-hand nodes, the share.
     * @param {Object} node - A Money Flow node from the server model.
     * @returns {string} The node label.
     */
    function nodeLabel (node) {
        const parts = [node.label, formatAmount(node.amount)];
        if (node.sharePercent !== null && node.sharePercent !== undefined) {
            parts.push(`${node.sharePercent.toFixed(1)}%`);
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
            return (node && KIND_COLORS[node.kind]) || KIND_COLORS.category;
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
                                const share = flow.shareBase > 0
                                    ? ` (${(link.flow / flow.shareBase * 100).toFixed(1)}%)`
                                    : '';
                                return `${nodesByKey[link.from].label} → ${nodesByKey[link.to].label}: ${formatAmount(link.flow)}${share}`;
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

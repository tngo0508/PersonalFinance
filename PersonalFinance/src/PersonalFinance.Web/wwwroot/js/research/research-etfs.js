/* global d3 */
/**
 * Research page, ETFs tab: a D3 collapsible tree of what six ETFs hold, one quarter at a time. Levels: the six ETFs,
 * then each fund's N-PORT asset categories, then each category's 25 largest holdings and an "Other" node for the rest.
 * A node's radius is 3 + 1.05 x the square root of its % of net assets. Reads the ETFs snapshot from the JSON data
 * island rendered by Views/Research/_EtfsTab.cshtml, so it makes no request of its own.
 *
 * The tree is a WAI-ARIA tree: one node is in the tab order at a time; Enter or Space opens or closes it, the arrow
 * keys move between the visible nodes (Right opens, Left closes or goes to the parent), Home and End go to the first
 * and last. Which nodes are open is kept when the page's scrubber (js/research/research-page.js) changes quarter. SPY
 * and its common-equity branch start open.
 */
(function () {
    'use strict';

    const DATA_ELEMENT_ID = 'research-etfs-data';
    const HOST_ID = 'research-etfs-tree';
    const TOOLTIP_ID = 'research-etfs-tooltip';
    const SUBTITLE_ID = 'research-etfs-subtitle';
    const TAB_ID = 'etfs';
    const TRANSITION_MS = 600;
    const ROW_HEIGHT = 24;
    const NARROW_WIDTH = 560;
    const OPEN_FIRST = ['root', 'f:SPY', 'c:SPY:EC'];
    const KIND_LABELS = {
        root: 'Six well-known ETFs',
        fund: 'ETF',
        category: 'Asset category',
        holding: 'Holding',
        other: 'Smaller holdings, summed'
    };

    /**
     * Parse the ETFs data island.
     * @returns {Object|null} The camelCase ETFs snapshot, or null when it is missing or malformed.
     */
    function readData () {
        const element = document.getElementById(DATA_ELEMENT_ID);
        if (!element) {
            return null;
        }
        try {
            return JSON.parse(element.textContent || 'null');
        } catch (error) {
            // eslint-disable-next-line no-console
            console.warn('Research ETFs data could not be parsed:', error);
            return null;
        }
    }

    /**
     * Whether the user asked the operating system to minimise motion.
     * @returns {boolean} True when changes should apply without a transition.
     */
    function prefersReducedMotion () {
        return typeof window.matchMedia === 'function' &&
            window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    /**
     * Format a % of net assets: one decimal from 10%, two below, and "<0.01%" for a sliver.
     * @param {number} value - The percentage, e.g. 7.517.
     * @returns {string} The formatted percentage.
     */
    function formatPercent (value) {
        if (value > 0 && value < 0.005) {
            return '<0.01%';
        }
        return `${value.toFixed(Math.abs(value) >= 10 ? 1 : 2)}%`;
    }

    /**
     * A node's radius: 3 plus 1.05 x the square root of its % of net assets (a negative share draws the minimum).
     * @param {number} value - The % of net assets.
     * @returns {number} The radius in pixels.
     */
    function radius (value) {
        return 3 + 1.05 * Math.sqrt(Math.max(0, value));
    }

    /**
     * Build the tree data of one quarter. Keys stay the same across quarters, so open nodes stay open.
     * @param {Object} data - The ETFs snapshot.
     * @param {number} index - The quarter's index in data.values.
     * @returns {Object} The root node data for d3.hierarchy.
     */
    function buildTree (data, index) {
        const period = data.values[index];
        const funds = data.funds.map((fund) => {
            const report = period.funds.find((f) => f.ticker === fund.ticker);
            const node = {
                key: `f:${fund.ticker}`, kind: 'fund', name: fund.ticker, full: fund.name, value: 100,
                count: report ? report.holdingCount : 0, missing: !report
            };
            if (report) {
                node.children = report.categories.map((category) => categoryNode(fund.ticker, category));
            }
            return node;
        });
        return {
            key: 'root', kind: 'root', name: `${data.funds.length} ETFs`, value: 100, children: funds
        };
    }

    /**
     * Build one asset category's node: its largest holdings, then "Other" for the rest.
     * @param {string} ticker - The fund's ticker.
     * @param {Object} category - The category from the snapshot.
     * @returns {Object} The category node data.
     */
    function categoryNode (ticker, category) {
        const seen = new Map();
        const children = category.top.map((holding) => {
            // Two share classes of one issuer can share a name (e.g. Alphabet); keep their keys apart.
            const count = (seen.get(holding.name) || 0) + 1;
            seen.set(holding.name, count);
            return {
                key: `h:${ticker}:${category.code}:${holding.name}#${count}`,
                kind: 'holding',
                name: holding.name,
                value: holding.percent,
                fund: ticker,
                category: category.name
            };
        });
        if (category.otherCount > 0) {
            children.push({
                key: `o:${ticker}:${category.code}`,
                kind: 'other',
                name: `Other (${category.otherCount.toLocaleString('en-US')} holdings)`,
                value: category.otherPercent,
                fund: ticker,
                category: category.name
            });
        }
        return {
            key: `c:${ticker}:${category.code}`,
            kind: 'category',
            name: category.name,
            value: category.percent,
            count: category.holdingCount,
            fund: ticker,
            children
        };
    }

    /**
     * The visible label of a node.
     * @param {Object} node - A d3 hierarchy node.
     * @returns {string} The label.
     */
    function labelOf (node) {
        const d = node.data;
        if (d.kind === 'root') {
            return d.name;
        }
        if (d.kind === 'fund') {
            return d.missing ? `${d.name} · no report this quarter` : `${d.name} · ${d.count.toLocaleString('en-US')} holdings`;
        }
        return `${d.name}  ${formatPercent(d.value)}`;
    }

    /**
     * The accessible name of a node: its label, plus the fund's full name or the holding's fund and category.
     * @param {Object} node - A d3 hierarchy node.
     * @returns {string} The accessible name.
     */
    function accessibleName (node) {
        const d = node.data;
        if (d.kind === 'fund') {
            return `${labelOf(node)}, ${d.full}`;
        }
        if (d.kind === 'holding' || d.kind === 'other') {
            return `${d.name}, ${formatPercent(d.value)} of ${d.fund} net assets, ${d.category}`;
        }
        if (d.kind === 'category') {
            return `${d.name}, ${formatPercent(d.value)} of ${d.fund} net assets, ${d.count.toLocaleString('en-US')} holdings`;
        }
        return `${d.name}: what six well-known ETFs hold`;
    }

    /**
     * Create the tooltip controller for the ETFs tooltip element.
     * @param {HTMLElement|null} tooltip - The tooltip element.
     * @returns {{show: Function, hide: Function}} Show it beside the pointer for a node; hide it.
     */
    function createTooltip (tooltip) {
        const line = (text, className) => {
            const span = document.createElement('span');
            span.className = className;
            span.textContent = text;
            return span;
        };
        return {
            show (event, node) {
                if (!tooltip || node.data.kind === 'root') {
                    return;
                }
                const d = node.data;
                const parts = [line(d.kind === 'fund' ? `${d.name}, ${d.full}` : d.name, 'research-tooltip__name'),
                    line(KIND_LABELS[d.kind], 'research-tooltip__muted')];
                if (d.kind === 'fund') {
                    parts.push(line(d.missing ? 'No report for this quarter' : `${d.count.toLocaleString('en-US')} holdings`,
                        'research-tooltip__value'));
                } else {
                    const value = line(formatPercent(d.value), 'research-tooltip__value');
                    const wrap = document.createElement('span');
                    wrap.className = 'research-tooltip__muted';
                    wrap.append(value, ` of ${d.fund} net assets`);
                    parts.push(wrap);
                }
                tooltip.replaceChildren(...parts);
                tooltip.hidden = false;
                const x = Math.min(event.clientX + 14, window.innerWidth - tooltip.offsetWidth - 8);
                const y = Math.min(event.clientY + 14, window.innerHeight - tooltip.offsetHeight - 8);
                tooltip.style.left = `${Math.max(8, x)}px`;
                tooltip.style.top = `${Math.max(8, y)}px`;
            },
            hide () {
                if (tooltip) {
                    tooltip.hidden = true;
                }
            }
        };
    }

    /**
     * Create the collapsible tree in its host.
     * @param {HTMLElement} host - The element to draw into; it scrolls sideways when the tree is wider.
     * @param {Object} data - The ETFs snapshot.
     * @param {Object} tooltip - The tooltip controller.
     * @returns {Object} The chart registered with ResearchPage.
     */
    function createTree (host, data, tooltip) {
        const narrow = window.innerWidth < NARROW_WIDTH;
        const columnWidth = narrow ? 120 : 190;
        const labelSpace = narrow ? 230 : 300;
        const leftMargin = 70;
        const expanded = new Set(OPEN_FIRST);
        const previous = new Map();
        let index = data.values.length - 1;
        let focusKey = 'root';
        let visible = [];

        const svg = d3.select(host).append('svg')
            .attr('class', 'research-etfs__svg')
            .attr('role', 'tree')
            .attr('aria-label', 'What six ETFs hold, by asset category and holding');
        const linkLayer = svg.append('g').attr('class', 'research-etfs__links').attr('aria-hidden', 'true');
        const nodeLayer = svg.append('g');

        /**
         * Lay out and draw the tree, moving from the old layout when animated.
         * @param {string|null} sourceKey - The node that was toggled: new nodes grow from it, removed ones fold into it.
         * @param {boolean} animate - Whether to transition.
         * @returns {void}
         */
        function render (sourceKey, animate) {
            const root = d3.hierarchy(buildTree(data, index));
            root.each((d) => {
                if (d.children && d.depth > 0 && !expanded.has(d.data.key)) {
                    d.hiddenChildren = d.children;
                    d.children = null;
                }
            });
            d3.tree().nodeSize([ROW_HEIGHT, columnWidth])(root);
            visible = [];
            root.eachBefore((d) => visible.push(d));
            const byKey = new Map(visible.map((d) => [d.data.key, d]));
            if (!byKey.has(focusKey)) {
                focusKey = 'root';
            }

            const x0 = d3.min(visible, (d) => d.x);
            const x1 = d3.max(visible, (d) => d.x);
            const maxDepthY = d3.max(visible, (d) => d.y);
            const width = leftMargin + maxDepthY + labelSpace;
            const height = x1 - x0 + ROW_HEIGHT * 2;
            svg.attr('width', width).attr('height', height)
                .attr('viewBox', [-leftMargin, x0 - ROW_HEIGHT, width, height].join(' '));

            const duration = animate && !prefersReducedMotion() ? TRANSITION_MS : 0;
            const transition = svg.transition().duration(duration);
            const source = byKey.get(sourceKey) || root;
            const from = (key) => previous.get(key) || previous.get(sourceKey) || { x: source.x, y: source.y };
            const at = (p) => `translate(${p.y},${p.x})`;

            const node = nodeLayer.selectAll('g.research-etfs__node').data(visible, (d) => d.data.key);
            const entered = node.enter().append('g')
                .attr('class', 'research-etfs__node')
                .attr('role', 'treeitem')
                .attr('transform', (d) => at(from(d.parent ? d.parent.data.key : 'root')))
                .attr('opacity', 0)
                .on('click', (event, d) => {
                    focusKey = d.data.key;
                    toggle(d);
                })
                .on('keydown', (event, d) => onKey(event, d))
                .on('focus', (event, d) => {
                    focusKey = d.data.key;
                })
                .on('pointermove', (event, d) => tooltip.show(event, d))
                .on('pointerleave', tooltip.hide);
            entered.append('circle').attr('class', 'research-etfs__dot');
            entered.append('text').attr('class', 'research-etfs__label').attr('dy', '0.32em');

            const merged = entered.merge(node);
            merged
                .attr('aria-level', (d) => d.depth + 1)
                .attr('aria-setsize', (d) => (d.parent ? d.parent.children.length : 1))
                .attr('aria-posinset', (d) => (d.parent ? d.parent.children.indexOf(d) + 1 : 1))
                .attr('aria-expanded', (d) => (d.depth > 0 && (d.children || d.hiddenChildren) ? String(Boolean(d.children)) : null))
                .attr('aria-label', accessibleName)
                .attr('tabindex', (d) => (d.data.key === focusKey ? 0 : -1))
                .attr('data-key', (d) => d.data.key)
                .attr('data-kind', (d) => d.data.kind)
                .classed('is-collapsed', (d) => Boolean(d.hiddenChildren))
                .classed('is-missing', (d) => Boolean(d.data.missing));
            merged.select('text')
                .attr('x', (d) => (d.children ? -(radius(d.data.value) + 6) : radius(d.data.value) + 6))
                .attr('text-anchor', (d) => (d.children ? 'end' : 'start'))
                .text(labelOf);
            merged.transition(transition)
                .attr('transform', (d) => at(d))
                .attr('opacity', 1)
                .select('circle')
                .attr('r', (d) => radius(d.data.value));
            node.exit().transition(transition)
                .attr('transform', (d) => at(nearestVisibleAncestor(d, byKey) || source))
                .attr('opacity', 0)
                .remove();

            const diagonal = d3.linkHorizontal().x((p) => p.y).y((p) => p.x);
            const link = linkLayer.selectAll('path').data(root.links(), (d) => d.target.data.key);
            link.enter().append('path')
                .attr('d', (d) => {
                    const o = from(d.source.data.key);
                    return diagonal({ source: o, target: o });
                })
                .merge(link)
                .transition(transition)
                .attr('d', diagonal);
            link.exit().transition(transition)
                .attr('d', () => diagonal({ source, target: source }))
                .remove();

            previous.clear();
            visible.forEach((d) => previous.set(d.data.key, { x: d.x, y: d.y }));
        }

        /**
         * The closest ancestor of a removed node that is still drawn, for it to fold into.
         * @param {Object} d - The removed node.
         * @param {Map} byKey - The drawn nodes by key.
         * @returns {Object|null} The ancestor, or null.
         */
        function nearestVisibleAncestor (d, byKey) {
            for (let p = d.parent; p; p = p.parent) {
                if (byKey.has(p.data.key)) {
                    return byKey.get(p.data.key);
                }
            }
            return null;
        }

        /**
         * Open or close a node with children, then announce it and refresh the table.
         * @param {Object} d - The node.
         * @returns {void}
         */
        function toggle (d) {
            if (d.depth === 0 || (!d.children && !d.hiddenChildren)) {
                return;
            }
            const key = d.data.key;
            if (expanded.has(key)) {
                expanded.delete(key);
            } else {
                expanded.add(key);
            }
            render(key, true);
            focusNode(key);
            announce(`${d.data.name} ${expanded.has(key) ? 'opened' : 'closed'}.`);
            if (window.ResearchPage && window.ResearchPage.refreshTable) {
                window.ResearchPage.refreshTable(TAB_ID);
            }
        }

        /**
         * Move keyboard focus to a drawn node, making it the one node in the tab order.
         * @param {string} key - The node's key.
         * @returns {void}
         */
        function focusNode (key) {
            focusKey = key;
            nodeLayer.selectAll('g.research-etfs__node').attr('tabindex', (d) => (d.data.key === key ? 0 : -1));
            const element = nodeLayer.select(`g[data-key="${CSS.escape(key)}"]`).node();
            if (element) {
                element.focus();
            }
        }

        /**
         * Keyboard handling of a focused node (WAI-ARIA tree pattern).
         * @param {KeyboardEvent} event - The key event.
         * @param {Object} d - The focused node.
         * @returns {void}
         */
        function onKey (event, d) {
            const position = visible.findIndex((v) => v.data.key === d.data.key);
            const target = {
                ArrowDown: visible[position + 1],
                ArrowUp: visible[position - 1],
                Home: visible[0],
                End: visible[visible.length - 1]
            };
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                toggle(d);
            } else if (event.key === 'ArrowRight') {
                event.preventDefault();
                if (d.hiddenChildren) {
                    toggle(d);
                } else if (d.children) {
                    focusNode(d.children[0].data.key);
                }
            } else if (event.key === 'ArrowLeft') {
                event.preventDefault();
                if (d.children && d.depth > 0) {
                    toggle(d);
                } else if (d.parent) {
                    focusNode(d.parent.data.key);
                }
            } else if (event.key in target) {
                event.preventDefault();
                if (target[event.key]) {
                    focusNode(target[event.key].data.key);
                }
            }
        }

        /**
         * Put a sentence in the page's polite live region.
         * @param {string} message - The sentence to announce.
         * @returns {void}
         */
        function announce (message) {
            const live = document.getElementById('research-live');
            if (live) {
                live.textContent = message;
            }
        }

        /**
         * Show a quarter, keeping open nodes open.
         * @param {number} next - The quarter's index.
         * @param {{animate: boolean}} options - Whether to transition.
         * @returns {string} A one-sentence summary of the quarter.
         */
        function update (next, options) {
            index = next;
            tooltip.hide();
            render(null, Boolean(options && options.animate));
            const period = data.values[index];
            const subtitle = document.getElementById(SUBTITLE_ID);
            if (subtitle) {
                subtitle.textContent = `Holdings reported for ${period.period}, by asset category. Choose an ETF or a ` +
                    'category to open or close it; each category lists its 25 largest holdings.';
            }
            const first = period.funds[0];
            const category = first && first.categories[0];
            const top = category && category.top[0];
            if (!top) {
                return `${period.period}: ${period.funds.length} of ${data.funds.length} ETFs reported.`;
            }
            return `${period.period}: ${period.funds.length} of ${data.funds.length} ETFs reported. ${first.ticker}'s ` +
                `largest holding is ${top.name}, ${formatPercent(top.percent)} of net assets.`;
        }

        /**
         * The table view of a quarter: each open ETF's categories and listed holdings.
         * @param {number} at - The quarter's index.
         * @returns {{caption: string, columns: Array, rows: Array}} The table model.
         */
        function table (at) {
            const period = data.values[at];
            const rows = [];
            period.funds.filter((fund) => expanded.has(`f:${fund.ticker}`)).forEach((fund) => {
                fund.categories.forEach((category) => {
                    rows.push([fund.ticker, category.name, `All ${category.holdingCount.toLocaleString('en-US')} holdings`,
                        formatPercent(category.percent)]);
                    category.top.forEach((holding) => {
                        rows.push([fund.ticker, category.name, holding.name, formatPercent(holding.percent)]);
                    });
                    if (category.otherCount > 0) {
                        rows.push([fund.ticker, category.name,
                            `Other (${category.otherCount.toLocaleString('en-US')} holdings)`,
                            formatPercent(category.otherPercent)]);
                    }
                });
            });
            return {
                caption: rows.length > 0
                    ? `Holdings of the open ETFs, ${period.period}, as % of each fund's net assets`
                    : `No ETF is open. Open one in the tree to list its holdings for ${period.period}.`,
                columns: [
                    { label: 'ETF', numeric: false },
                    { label: 'Asset category', numeric: false },
                    { label: 'Holding', numeric: false },
                    { label: '% of net assets', numeric: true }
                ],
                rows
            };
        }

        return {
            periods: data.periods,
            transitionMs: TRANSITION_MS,
            playLabel: 'Play through the quarters',
            update,
            table
        };
    }

    /**
     * Draw the tree and register it with the page's tab shell.
     * @returns {void}
     */
    function init () {
        const host = document.getElementById(HOST_ID);
        const data = readData();
        if (!host || !data || !Array.isArray(data.values) || data.values.length === 0 || typeof d3 === 'undefined') {
            return;
        }
        const tooltip = createTooltip(document.getElementById(TOOLTIP_ID));
        const chart = createTree(host, data, tooltip);
        if (window.ResearchPage) {
            window.ResearchPage.registerTab(TAB_ID, chart);
        }
        host.addEventListener('scroll', tooltip.hide, { passive: true });
        window.addEventListener('scroll', tooltip.hide, { passive: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

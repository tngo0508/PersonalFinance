/* global d3 */
/**
 * Research page, Sectors tab: a D3 sunburst of the largest SEC filers by Public float, one year at a time.
 * Rings, inside out: SEC industry office, SIC industry, company (plus one "Other" slice per industry for the smaller
 * filers). Reads the Sectors snapshot from the JSON data island rendered by Views/Research/Index.cshtml, so it makes
 * no request of its own. Hover or tap a slice for its figures; the centre shows the total. The page's scrubber
 * (js/research/research-page.js) moves it between years through update(), and its table view reads table().
 */
(function () {
    'use strict';

    const DATA_ELEMENT_ID = 'research-sectors-data';
    const HOST_ID = 'research-sunburst';
    const LEGEND_ID = 'research-sectors-legend';
    const TOOLTIP_ID = 'research-tooltip';
    const SUBTITLE_ID = 'research-sectors-subtitle';
    const TAB_ID = 'sectors';
    const TRANSITION_MS = 750;
    const TABLE_ROWS = 25;
    const MAX_SIZE = 640;
    const MIN_SIZE = 240;
    const RINGS = 3;

    // SEC industry offices in SEC office-number order; each takes the categorical slot of the same number.
    const OFFICE_SLOTS = [
        'Energy & Transportation',
        'Finance',
        'Life Sciences',
        'Manufacturing',
        'Real Estate & Construction',
        'Technology',
        'Trade & Services',
        'Industrial Applications and Services'
    ];
    const DARK_LABEL_OFFICES = ['Manufacturing'];
    const RING_OPACITY = [1, 1, 0.88, 0.76];
    const OTHER_OPACITY = 0.45;
    const KIND_LABELS = {
        office: () => 'SEC industry office',
        industry: () => 'SIC industry',
        company: (node) => (node.data.ticker ? `Ticker ${node.data.ticker}` : 'No ticker'),
        other: (node) => `Smaller filers in ${node.parent ? node.parent.data.name : 'this industry'}`
    };

    /**
     * Parse the Sectors data island.
     * @returns {Object|null} The camelCase Sectors snapshot, or null when it is missing or malformed.
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
            console.warn('Research Sectors data could not be parsed:', error);
            return null;
        }
    }

    /**
     * Format a US$ amount compactly, as the server does: $3.02T, $412.5B, $18.0M.
     * @param {number} amount - The amount in US$.
     * @returns {string} The formatted amount.
     */
    function formatMoney (amount) {
        const absolute = Math.abs(amount);
        if (absolute >= 1e12) {
            return `$${(amount / 1e12).toFixed(2)}T`;
        }
        if (absolute >= 1e9) {
            return `$${(amount / 1e9).toFixed(1)}B`;
        }
        if (absolute >= 1e6) {
            return `$${(amount / 1e6).toFixed(1)}M`;
        }
        return `$${Math.round(amount).toLocaleString('en-US')}`;
    }

    /**
     * The fill colour of an office, read from the page's CSS tokens.
     * @param {Element} element - An element inside the Research page.
     * @param {string} office - The office name.
     * @returns {string} A CSS colour.
     */
    function officeColour (element, office) {
        const slot = OFFICE_SLOTS.indexOf(office);
        const token = slot >= 0 ? `--research-office-${slot + 1}` : '--research-office-neutral';
        return getComputedStyle(element).getPropertyValue(token).trim();
    }

    /**
     * Build the hierarchy of one period: office, then industry, then company and "Other".
     * @param {Object} data - The Sectors snapshot.
     * @param {Object} period - One entry of data.values.
     * @returns {Object} The root node data for d3.hierarchy.
     */
    function buildTree (data, period) {
        const offices = new Map();

        const industryNode = (industryIndex) => {
            const industry = data.industries[industryIndex];
            const officeName = data.offices[industry.office];
            if (!offices.has(industry.office)) {
                offices.set(industry.office, {
                    key: `o${industry.office}`, name: officeName, office: officeName, kind: 'office', children: new Map()
                });
            }
            const office = offices.get(industry.office);
            if (!office.children.has(industryIndex)) {
                office.children.set(industryIndex, {
                    key: `i${industryIndex}`, name: industry.name, office: officeName, kind: 'industry', children: []
                });
            }
            return office.children.get(industryIndex);
        };

        period.ranked.forEach((row) => {
            const company = data.companies[row.company];
            const node = industryNode(company.industry);
            node.children.push({
                key: `c${row.company}`,
                name: company.name,
                ticker: company.ticker,
                office: node.office,
                kind: 'company',
                value: row.publicFloat
            });
        });
        (period.other || []).forEach((row) => {
            const node = industryNode(row.industry);
            const filers = `${row.count.toLocaleString('en-US')} ${row.count === 1 ? 'filer' : 'filers'}`;
            node.children.push({
                key: `x${row.industry}`,
                name: `Other (${filers})`,
                office: node.office,
                kind: 'other',
                value: row.publicFloat
            });
        });

        // Offices keep SEC office-number order, so colour and position follow the office, never its size.
        const children = Array.from(offices.keys())
            .sort((a, b) => a - b)
            .map((index) => {
                const office = offices.get(index);
                return { ...office, children: Array.from(office.children.values()) };
            });
        return { key: 'root', name: 'All filers', kind: 'root', children };
    }

    /**
     * Sort siblings largest first, with an industry's "Other" slice last. Offices keep their order.
     * @param {Object} a - A d3 hierarchy node.
     * @param {Object} b - A d3 hierarchy node.
     * @returns {number} The sort order.
     */
    function compareNodes (a, b) {
        if (a.depth === 1 && b.depth === 1) {
            return 0;
        }
        const aOther = a.data.kind === 'other' ? 1 : 0;
        const bOther = b.data.kind === 'other' ? 1 : 0;
        return aOther - bOther || b.value - a.value;
    }

    /**
     * Create the tooltip controller for the Research tooltip element.
     * @param {HTMLElement|null} tooltip - The tooltip element.
     * @returns {{show: function(PointerEvent, Object): void, hide: function(): void}} The controller.
     */
    function createTooltip (tooltip) {
        const line = (text, className) => {
            const span = document.createElement('span');
            span.className = className;
            span.textContent = text;
            return span;
        };

        const show = (event, node) => {
            if (!tooltip) {
                return;
            }
            const valueLine = document.createElement('span');
            valueLine.append(line(formatMoney(node.value), 'research-tooltip__value'), ' Public float');
            tooltip.replaceChildren(
                line(node.data.name, 'research-tooltip__name'),
                line(KIND_LABELS[node.data.kind](node), 'research-tooltip__muted'),
                valueLine
            );
            if (node.parent && node.parent.value > 0) {
                const share = (node.value / node.parent.value * 100).toFixed(1);
                tooltip.append(line(`${share}% of ${node.parent.data.name}`, 'research-tooltip__muted'));
            }
            tooltip.hidden = false;
            const x = Math.min(event.clientX + 14, window.innerWidth - tooltip.offsetWidth - 8);
            const y = Math.min(event.clientY + 14, window.innerHeight - tooltip.offsetHeight - 8);
            tooltip.style.left = `${Math.max(8, x)}px`;
            tooltip.style.top = `${Math.max(8, y)}px`;
        };

        const hide = () => {
            if (tooltip) {
                tooltip.hidden = true;
            }
        };

        return { show, hide };
    }

    /**
     * Draw the office legend into the side panel.
     * @param {HTMLElement|null} legend - The legend list element.
     * @param {Object} root - The d3 hierarchy root.
     * @returns {void}
     */
    function renderLegend (legend, root) {
        if (!legend) {
            return;
        }
        legend.replaceChildren(...(root.children || []).map((office) => {
            const item = document.createElement('li');
            item.className = 'research-legend__item';
            const swatch = document.createElement('span');
            swatch.className = 'research-legend__swatch';
            swatch.setAttribute('aria-hidden', 'true');
            swatch.style.background = officeColour(legend, office.data.office);
            const name = document.createElement('span');
            name.textContent = office.data.name;
            item.append(swatch, name);
            return item;
        }));
    }

    /**
     * The text of an arc's label, shortened to fit the ring.
     * @param {Object} node - A d3 hierarchy node.
     * @param {number} unit - The ring thickness in px.
     * @returns {string} The label.
     */
    function labelText (node, unit) {
        const text = node.data.kind === 'company' && node.data.ticker ? node.data.ticker : node.data.name;
        const max = Math.floor((unit - 6) / 6);
        return text.length > max ? `${text.slice(0, Math.max(1, max - 1))}…` : text;
    }

    /**
     * Place a label at the middle of its arc, reading outwards and never upside down.
     * @param {Object} node - A d3 hierarchy node.
     * @param {number} unit - The ring thickness in px.
     * @returns {string} The SVG transform.
     */
    function labelTransform (node, unit) {
        const angle = (node.x0 + node.x1) / 2 * 180 / Math.PI;
        const radius = (node.y0 + node.y1) / 2 * unit;
        return `rotate(${angle - 90}) translate(${radius},0) rotate(${angle < 180 ? 0 : 180})`;
    }

    /**
     * An arc's geometry: its angles and rings, as d3.partition lays them out.
     * @param {Object} node - A d3 hierarchy node, or a geometry.
     * @returns {{x0: number, x1: number, y0: number, y1: number}} The geometry.
     */
    function geometry (node) {
        return { x0: node.x0, x1: node.x1, y0: node.y0, y1: node.y1 };
    }

    /**
     * The same geometry with zero width at its start angle: where an entering arc grows from and a leaving one
     * shrinks to.
     * @param {{x0: number, x1: number, y0: number, y1: number}} shape - A geometry.
     * @returns {{x0: number, x1: number, y0: number, y1: number}} The collapsed geometry.
     */
    function collapsed (shape) {
        return { ...shape, x1: shape.x0 };
    }

    /**
     * Create the Sectors sunburst in the host. It is drawn once, then moved between periods by update(): arcs and
     * labels are keyed by office, industry and company, so an arc tweens from where it is drawn now; an arc that
     * enters grows from zero width and one that leaves shrinks to zero width. Each element keeps the geometry it is
     * drawn at in `__arc`, so an update that interrupts a running transition carries on from there.
     * @param {HTMLElement} host - The chart host.
     * @param {Object} data - The Sectors snapshot.
     * @param {Object} tooltip - The tooltip controller.
     * @returns {{periods: string[], transitionMs: number, update: function(number, Object): string,
     *     table: function(number): Object, resize: function(): void}} The chart.
     */
    function createSunburst (host, data, tooltip) {
        const subtitle = document.getElementById(SUBTITLE_ID);
        let periodIndex = data.values.length - 1;
        let unit = 0;
        let arc = null;
        let svg = null;
        let arcLayer = null;
        let labelLayer = null;
        let centreYear = null;
        let centreValue = null;

        /**
         * Size the chart to the host's width and draw its empty layers.
         * @returns {void}
         */
        function layout () {
            const size = Math.max(MIN_SIZE, Math.min(host.clientWidth || MAX_SIZE, MAX_SIZE));
            unit = size / 2 / (RINGS + 1);
            arc = d3.arc()
                .startAngle((d) => d.x0)
                .endAngle((d) => d.x1)
                .padAngle((d) => Math.min((d.x1 - d.x0) / 2, 0.003))
                .padRadius(unit * 2)
                .innerRadius((d) => d.y0 * unit)
                .outerRadius((d) => Math.max(d.y0 * unit, d.y1 * unit - 1));

            d3.select(host).selectAll('svg').remove();
            svg = d3.select(host).append('svg')
                .attr('viewBox', [-size / 2, -size / 2, size, size].join(' '))
                .attr('role', 'img');
            arcLayer = svg.append('g');
            labelLayer = svg.append('g').attr('text-anchor', 'middle');

            // The centre: the year, the total and what it measures, scaled to fit the inner circle.
            const valueSize = Math.min(18, unit / 4);
            const titleSize = Math.min(12, unit / 6);
            const centre = svg.append('g').attr('text-anchor', 'middle');
            centreYear = centre.append('text')
                .attr('class', 'research-sunburst__centre-title')
                .style('font-size', `${titleSize}px`)
                .attr('y', -valueSize * 0.9);
            centreValue = centre.append('text')
                .attr('class', 'research-sunburst__centre-value')
                .style('font-size', `${valueSize}px`)
                .attr('dy', '0.35em');
            centre.append('text')
                .attr('class', 'research-sunburst__centre-title')
                .style('font-size', `${titleSize}px`)
                .attr('y', valueSize * 0.9 + titleSize * 0.8)
                .text('Public float');
        }

        /**
         * Draw the arcs of the period, tweening from what is drawn now when animated.
         * @param {Object[]} nodes - The period's visible nodes.
         * @param {Object|null} transition - The shared d3 transition, or null to apply at once.
         * @returns {void}
         */
        function drawArcs (nodes, transition) {
            const paths = arcLayer.selectAll('path').data(nodes, (d) => d.data.key);
            const leaving = paths.exit().attr('pointer-events', 'none');
            if (transition) {
                leaving.transition(transition)
                    .attrTween('d', function () {
                        const shrink = d3.interpolate(this.__arc, collapsed(this.__arc));
                        return (t) => arc(this.__arc = shrink(t));
                    })
                    .remove();
            } else {
                leaving.remove();
            }

            const entering = paths.enter().append('path')
                .attr('class', 'research-sunburst__arc')
                .each(function (d) {
                    this.__arc = collapsed(geometry(d));
                })
                .attr('d', function () {
                    return arc(this.__arc);
                })
                .on('pointermove', (event, d) => tooltip.show(event, d))
                .on('pointerdown', (event, d) => tooltip.show(event, d))
                .on('pointerleave', (event) => {
                    if (event.pointerType === 'mouse') {
                        tooltip.hide();
                    }
                });

            const all = entering.merge(paths)
                .attr('pointer-events', null)
                .attr('fill', (d) => officeColour(host, d.data.office))
                .attr('fill-opacity', (d) => (d.data.kind === 'other' ? OTHER_OPACITY : RING_OPACITY[d.depth]));
            if (transition) {
                all.transition(transition)
                    .attrTween('d', function (d) {
                        const move = d3.interpolate(this.__arc, geometry(d));
                        return (t) => arc(this.__arc = move(t));
                    });
            } else {
                all.each(function (d) {
                    this.__arc = geometry(d);
                }).attr('d', function () {
                    return arc(this.__arc);
                });
            }
        }

        /**
         * Draw the labels of the period: only where the arc is tall enough for a line of text at its middle radius.
         * @param {Object[]} nodes - The period's visible nodes.
         * @param {Object|null} transition - The shared d3 transition, or null to apply at once.
         * @returns {void}
         */
        function drawLabels (nodes, transition) {
            const labelled = nodes.filter((d) => d.data.kind !== 'other' &&
                (d.x1 - d.x0) * (d.y0 + d.y1) / 2 * unit > 13);
            const labels = labelLayer.selectAll('text').data(labelled, (d) => d.data.key);
            if (transition) {
                labels.exit().transition(transition).attr('fill-opacity', 0).remove();
            } else {
                labels.exit().remove();
            }

            const entering = labels.enter().append('text')
                .attr('class', 'research-sunburst__label')
                .attr('dy', '0.35em')
                .each(function (d) {
                    this.__arc = geometry(d);
                })
                .attr('transform', function () {
                    return labelTransform(this.__arc, unit);
                })
                .attr('fill-opacity', transition ? 0 : 1);

            const all = entering.merge(labels)
                .attr('fill', (d) => (DARK_LABEL_OFFICES.includes(d.data.office)
                    ? 'var(--research-label-dark)'
                    : 'var(--research-label-light)'))
                .text((d) => labelText(d, unit));
            if (transition) {
                all.transition(transition)
                    .attr('fill-opacity', 1)
                    .attrTween('transform', function (d) {
                        const move = d3.interpolate(this.__arc, geometry(d));
                        return (t) => labelTransform(this.__arc = move(t), unit);
                    });
            } else {
                all.each(function (d) {
                    this.__arc = geometry(d);
                }).attr('transform', function () {
                    return labelTransform(this.__arc, unit);
                }).attr('fill-opacity', 1);
            }
        }

        /**
         * Show one period.
         * @param {number} index - The period's index in data.values.
         * @param {{animate: boolean}} [options] - Whether to tween from what is drawn now.
         * @returns {string} A one-sentence summary of the period, for the live region.
         */
        function update (index, options) {
            periodIndex = Math.max(0, Math.min(data.values.length - 1, index));
            const period = data.values[periodIndex];
            const root = d3.hierarchy(buildTree(data, period))
                .sum((d) => (d.children ? 0 : d.value || 0))
                .sort(compareNodes);
            d3.partition().size([2 * Math.PI, RINGS + 1])(root);
            const nodes = root.descendants().slice(1).filter((d) => d.x1 > d.x0);

            // A change without animation stops any transition still running, so nothing moves afterwards.
            svg.selectAll('*').interrupt();
            const transition = options && options.animate ? svg.transition().duration(TRANSITION_MS) : null;
            drawArcs(nodes, transition);
            drawLabels(nodes, transition);

            const largest = root.leaves()
                .filter((d) => d.data.kind === 'company')
                .sort((a, b) => b.value - a.value)[0];
            const largestText = largest ? ` Largest company: ${largest.data.name}, ${formatMoney(largest.value)}.` : '';
            const offices = (root.children || []).length;
            const total = `${formatMoney(root.value || 0)} in total across ${offices} SEC industry offices.`;
            svg.attr('aria-label', `Sunburst of SEC filers by Public float in ${period.period}: ${total}${largestText}`);
            centreYear.text(period.period);
            centreValue.text(formatMoney(root.value || 0));
            if (subtitle) {
                const filers = (period.ranked.length + (period.restCount || 0)).toLocaleString('en-US');
                subtitle.textContent = `The ${period.ranked.length} largest of ${filers} filers in ${period.period}, ` +
                    'by SEC industry office and SIC industry. Smaller filers are summed into "Other" per industry.';
            }
            renderLegend(document.getElementById(LEGEND_ID), root);
            return `${period.period}: ${total}${largestText}`;
        }

        /**
         * The period's largest companies, as the table view shows them.
         * @param {number} index - The period's index in data.values.
         * @returns {{caption: string, columns: Object[], rows: string[][]}} The table.
         */
        function table (index) {
            const period = data.values[Math.max(0, Math.min(data.values.length - 1, index))];
            const rows = period.ranked.slice(0, TABLE_ROWS).map((row, rank) => {
                const company = data.companies[row.company];
                const industry = data.industries[company.industry];
                return [
                    String(rank + 1),
                    company.ticker ? `${company.name} (${company.ticker})` : company.name,
                    industry.name,
                    data.offices[industry.office],
                    formatMoney(row.publicFloat)
                ];
            });
            return {
                caption: `The ${rows.length} largest SEC filers by Public float in ${period.period}`,
                columns: [
                    { label: 'Rank', numeric: true },
                    { label: 'Company' },
                    { label: 'SIC industry' },
                    { label: 'SEC industry office' },
                    { label: 'Public float', numeric: true }
                ],
                rows
            };
        }

        /**
         * Redraw at the host's new width, at the current period, without a transition.
         * @returns {void}
         */
        function resize () {
            layout();
            update(periodIndex, { animate: false });
        }

        layout();
        return { periods: data.periods, transitionMs: TRANSITION_MS, update, table, resize };
    }

    /**
     * Draw the sunburst when its host and data are on the page, hand it to the page's scrubber, and redraw it when
     * the card changes width.
     * @returns {void}
     */
    function init () {
        const host = document.getElementById(HOST_ID);
        const data = readData();
        if (!host || !data || !Array.isArray(data.values) || data.values.length === 0 || typeof d3 === 'undefined') {
            return;
        }
        const tooltip = createTooltip(document.getElementById(TOOLTIP_ID));
        const chart = createSunburst(host, data, tooltip);
        if (window.ResearchPage) {
            window.ResearchPage.registerTab(TAB_ID, chart);
        } else {
            chart.update(data.values.length - 1, { animate: false });
        }
        let width = host.clientWidth;

        // A tap outside the chart, or a scroll, puts the tooltip away.
        document.addEventListener('pointerdown', (event) => {
            if (!host.contains(event.target)) {
                tooltip.hide();
            }
        });
        window.addEventListener('scroll', tooltip.hide, { passive: true });

        if (typeof ResizeObserver !== 'undefined') {
            const observer = new ResizeObserver(() => {
                if (host.clientWidth !== width) {
                    width = host.clientWidth;
                    tooltip.hide();
                    chart.resize();
                }
            });
            observer.observe(host);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

/* global d3 */
/**
 * Research page, Sectors tab: a D3 sunburst of the largest SEC filers by Public float for the latest year.
 * Rings, inside out: SEC industry office, SIC industry, company (plus one "Other" slice per industry for the smaller
 * filers). Reads the Sectors snapshot from the JSON data island rendered by Views/Research/Index.cshtml, so it makes
 * no request of its own. Hover or tap a slice for its figures; the centre shows the total.
 */
(function () {
    'use strict';

    const DATA_ELEMENT_ID = 'research-sectors-data';
    const HOST_ID = 'research-sunburst';
    const LEGEND_ID = 'research-sectors-legend';
    const TOOLTIP_ID = 'research-tooltip';
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
     * Draw the sunburst into the host, sized to the host's width.
     * @param {HTMLElement} host - The chart host.
     * @param {Object} data - The Sectors snapshot.
     * @param {Object} tooltip - The tooltip controller.
     * @returns {void}
     */
    function render (host, data, tooltip) {
        const period = data.values[data.values.length - 1];
        const size = Math.max(MIN_SIZE, Math.min(host.clientWidth || MAX_SIZE, MAX_SIZE));
        const unit = size / 2 / (RINGS + 1);

        const root = d3.hierarchy(buildTree(data, period))
            .sum((d) => (d.children ? 0 : d.value || 0))
            .sort(compareNodes);
        d3.partition().size([2 * Math.PI, RINGS + 1])(root);

        const arc = d3.arc()
            .startAngle((d) => d.x0)
            .endAngle((d) => d.x1)
            .padAngle((d) => Math.min((d.x1 - d.x0) / 2, 0.003))
            .padRadius(unit * 2)
            .innerRadius((d) => d.y0 * unit)
            .outerRadius((d) => d.y1 * unit - 1);

        const nodes = root.descendants().slice(1).filter((d) => d.x1 > d.x0);
        const largest = root.leaves()
            .filter((d) => d.data.kind === 'company')
            .sort((a, b) => b.value - a.value)[0];
        const largestText = largest ? `. Largest company: ${largest.data.name}, ${formatMoney(largest.value)}.` : '.';

        d3.select(host).selectAll('svg').remove();
        const svg = d3.select(host).append('svg')
            .attr('viewBox', [-size / 2, -size / 2, size, size].join(' '))
            .attr('role', 'img')
            .attr('aria-label', `Sunburst of SEC filers by Public float in ${period.period}: ` +
                `${formatMoney(root.value)} in total across ${root.children.length} SEC industry offices${largestText}`);

        svg.append('g')
            .selectAll('path')
            .data(nodes, (d) => d.data.key)
            .join('path')
            .attr('class', 'research-sunburst__arc')
            .attr('d', arc)
            .attr('fill', (d) => officeColour(host, d.data.office))
            .attr('fill-opacity', (d) => (d.data.kind === 'other' ? OTHER_OPACITY : RING_OPACITY[d.depth]))
            .on('pointermove', (event, d) => tooltip.show(event, d))
            .on('pointerdown', (event, d) => tooltip.show(event, d))
            .on('pointerleave', (event) => {
                if (event.pointerType === 'mouse') {
                    tooltip.hide();
                }
            });

        // Labels only where the arc is tall enough for a line of text at its middle radius.
        const labelled = nodes.filter((d) => d.data.kind !== 'other' && (d.x1 - d.x0) * (d.y0 + d.y1) / 2 * unit > 13);
        svg.append('g')
            .attr('text-anchor', 'middle')
            .selectAll('text')
            .data(labelled, (d) => d.data.key)
            .join('text')
            .attr('class', 'research-sunburst__label')
            .attr('dy', '0.35em')
            .attr('fill', (d) => (DARK_LABEL_OFFICES.includes(d.data.office)
                ? 'var(--research-label-dark)'
                : 'var(--research-label-light)'))
            .attr('transform', (d) => labelTransform(d, unit))
            .text((d) => labelText(d, unit));

        // The centre: the year, the total and what it measures, scaled to fit the inner circle.
        const valueSize = Math.min(18, unit / 4);
        const titleSize = Math.min(12, unit / 6);
        const centre = svg.append('g').attr('text-anchor', 'middle');
        centre.append('text')
            .attr('class', 'research-sunburst__centre-title')
            .style('font-size', `${titleSize}px`)
            .attr('y', -valueSize * 0.9)
            .text(period.period);
        centre.append('text')
            .attr('class', 'research-sunburst__centre-value')
            .style('font-size', `${valueSize}px`)
            .attr('dy', '0.35em')
            .text(formatMoney(root.value));
        centre.append('text')
            .attr('class', 'research-sunburst__centre-title')
            .style('font-size', `${titleSize}px`)
            .attr('y', valueSize * 0.9 + titleSize * 0.8)
            .text('Public float');

        renderLegend(document.getElementById(LEGEND_ID), root);
    }

    /**
     * Draw the sunburst when its host and data are on the page, and redraw it when the card changes width.
     * @returns {void}
     */
    function init () {
        const host = document.getElementById(HOST_ID);
        const data = readData();
        if (!host || !data || !Array.isArray(data.values) || data.values.length === 0 || typeof d3 === 'undefined') {
            return;
        }
        const tooltip = createTooltip(document.getElementById(TOOLTIP_ID));
        let width = host.clientWidth;
        render(host, data, tooltip);

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
                    render(host, data, tooltip);
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

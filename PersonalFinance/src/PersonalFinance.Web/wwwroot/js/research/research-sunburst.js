/* global d3 */
/**
 * Research page, Sectors tab: a D3 sunburst of the largest SEC filers by Public float for the latest year.
 * Rings, inside out: SEC industry office, SIC industry, company (plus one "Other" slice per industry for the smaller
 * filers). Reads the Sectors snapshot from the JSON data island rendered by Views/Research/Index.cshtml, so it makes
 * no request of its own. Hover or tap a slice for its figures. Click an office or industry (or press an office's legend
 * button) to zoom in, so three rings show below it; the centre and the Zoom out control zoom out. The "Technology (incl.
 * chip makers)" toggle dims every slice outside the industries the server flags as Technology.
 */
(function () {
    'use strict';

    const DATA_ELEMENT_ID = 'research-sectors-data';
    const HOST_ID = 'research-sunburst';
    const LEGEND_ID = 'research-sectors-legend';
    const TOOLTIP_ID = 'research-tooltip';
    const ZOOM_OUT_ID = 'research-zoom-out';
    const FOCUS_LABEL_ID = 'research-sunburst-focus';
    const TECH_TOGGLE_ID = 'research-tech-toggle';
    const TECH_SHARE_ID = 'research-tech-share';
    const ROOT_KEY = 'root';
    const MAX_SIZE = 640;
    const MIN_SIZE = 240;
    const RINGS = 3;
    const TRANSITION_MS = 750;

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
    // Dimmed by the Technology highlight: the fill keeps its office colour, only fainter.
    const DIMMED_OPACITY = 0.15;
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
                    key: `i${industryIndex}`,
                    name: industry.name,
                    office: officeName,
                    kind: 'industry',
                    technology: industry.technology === true,
                    children: []
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
                technology: node.technology,
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
                technology: node.technology,
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
        return { key: ROOT_KEY, name: 'All filers', kind: 'root', children };
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
     * The duration of a zoom transition: none when the user asks for reduced motion.
     * @returns {number} The duration in ms.
     */
    function transitionDuration () {
        const reduced = typeof window.matchMedia === 'function' &&
            window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        return reduced ? 0 : TRANSITION_MS;
    }

    /**
     * Mark each node lit by the Technology highlight: a company or "Other" slice by its industry's server-side flag,
     * an industry by its flag, and an office only when everything in it is lit.
     * @param {Object} root - The d3 hierarchy root.
     * @returns {void}
     */
    function markTechnology (root) {
        root.eachAfter((node) => {
            node.technology = node.children
                ? node.children.every((child) => child.technology)
                : node.data.technology === true;
        });
    }

    /**
     * A node's geometry relative to the focused node: the focus fills the full circle and sits at the centre.
     * @param {Object} node - A d3 hierarchy node.
     * @param {Object} focus - The focused d3 hierarchy node.
     * @returns {{x0: number, x1: number, y0: number, y1: number}} The geometry in radians and rings.
     */
    function targetOf (node, focus) {
        const span = focus.x1 - focus.x0;
        const angle = (x) => Math.max(0, Math.min(1, (x - focus.x0) / span)) * 2 * Math.PI;
        return {
            x0: angle(node.x0),
            x1: angle(node.x1),
            y0: Math.max(0, node.y0 - focus.depth),
            y1: Math.max(0, node.y1 - focus.depth)
        };
    }

    /**
     * Whether an arc with this geometry is in one of the three visible rings.
     * @param {Object} geometry - The arc geometry.
     * @returns {boolean} True when the arc is drawn.
     */
    function isVisible (geometry) {
        return geometry.y0 >= 1 && geometry.y1 <= RINGS + 1 && geometry.x1 > geometry.x0;
    }

    /**
     * Shorten a name to a number of characters.
     * @param {string} text - The name.
     * @param {number} max - The most characters to keep.
     * @returns {string} The name, ending in an ellipsis when cut.
     */
    function shorten (text, max) {
        return text.length > max ? `${text.slice(0, Math.max(1, max - 1))}…` : text;
    }

    /**
     * Draw the office legend into the side panel: one button per office, which zooms to that office.
     * @param {HTMLElement|null} legend - The legend list element.
     * @param {Object} root - The d3 hierarchy root.
     * @param {function(string): void} onPick - Called with the office's node key when its button is pressed.
     * @returns {void}
     */
    function renderLegend (legend, root, onPick) {
        if (!legend) {
            return;
        }
        legend.replaceChildren(...(root.children || []).map((office) => {
            const item = document.createElement('li');
            item.className = 'research-legend__item';
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'research-legend__button';
            button.dataset.key = office.data.key;
            button.setAttribute('aria-pressed', 'false');
            const swatch = document.createElement('span');
            swatch.className = 'research-legend__swatch';
            swatch.setAttribute('aria-hidden', 'true');
            swatch.style.background = officeColour(legend, office.data.office);
            const name = document.createElement('span');
            name.textContent = office.data.name;
            button.append(swatch, name);
            button.addEventListener('click', () => onPick(office.data.key));
            item.append(button);
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
        return shorten(text, Math.floor((unit - 6) / 6));
    }

    /**
     * Place a label at the middle of its arc, reading outwards and never upside down.
     * @param {Object} geometry - The arc geometry.
     * @param {number} unit - The ring thickness in px.
     * @returns {string} The SVG transform.
     */
    function labelTransform (geometry, unit) {
        const angle = (geometry.x0 + geometry.x1) / 2 * 180 / Math.PI;
        const radius = (geometry.y0 + geometry.y1) / 2 * unit;
        return `rotate(${angle - 90}) translate(${radius},0) rotate(${angle < 180 ? 0 : 180})`;
    }

    /**
     * Create the sunburst controller. The view (period, focus and Technology highlight) lives here, outside any one
     * drawing, so a redraw at a new width, or of another period, keeps where the user has zoomed and what is lit.
     * @param {HTMLElement} host - The chart host.
     * @param {Object} data - The Sectors snapshot.
     * @param {Object} tooltip - The tooltip controller.
     * @returns {{draw: function(): void, zoomTo: function(string): void, zoomOut: function(): void,
     *     setTechnology: function(boolean): void}} The controller.
     */
    function createSunburst (host, data, tooltip) {
        const view = { periodIndex: data.values.length - 1, focusKey: ROOT_KEY, technology: false };
        const zoomOutButton = document.getElementById(ZOOM_OUT_ID);
        const focusLabel = document.getElementById(FOCUS_LABEL_ID);
        const techToggle = document.getElementById(TECH_TOGGLE_ID);
        const techShare = document.getElementById(TECH_SHARE_ID);
        const legend = document.getElementById(LEGEND_ID);
        let chart = null;

        // The focused node of the current drawing; a focus the period does not have falls back to every filer.
        const focusNode = () => chart.root.descendants().find((d) => d.data.key === view.focusKey) || chart.root;
        const isLit = (node) => !view.technology || node.technology;
        const opacityOf = (node) => {
            if (!isVisible(node.target)) {
                return 0;
            }
            const base = node.data.kind === 'other' ? OTHER_OPACITY : RING_OPACITY[node.depth - chart.focus.depth];
            return isLit(node) ? base : DIMMED_OPACITY;
        };
        const hasLabel = (node) => node.data.kind !== 'other' && isVisible(node.target) && isLit(node) &&
            (node.target.x1 - node.target.x0) * (node.target.y0 + node.target.y1) / 2 * chart.unit > 13;

        /**
         * Show the focus in the centre, the Zoom out control, the legend, the Technology share and the chart's label.
         * @param {Object} focus - The focused d3 hierarchy node.
         * @returns {void}
         */
        function describeView (focus) {
            const period = data.values[view.periodIndex].period;
            const zoomed = focus.depth > 0;
            chart.centreTitle.text(zoomed ? shorten(focus.data.name, Math.floor(chart.unit / 3)) : period);
            chart.centreValue.text(formatMoney(focus.value));
            chart.centreHint.text(zoomed ? 'Zoom out' : 'Public float');
            chart.centre.classed('research-sunburst__centre--zoomed', zoomed);

            const trail = focus.ancestors().reverse().map((d) => d.data.name).join(' › ');
            if (focusLabel) {
                focusLabel.textContent = `${trail}: ${formatMoney(focus.value)}`;
            }
            if (zoomOutButton) {
                zoomOutButton.setAttribute('aria-disabled', String(!zoomed));
            }
            if (legend) {
                const office = focus.ancestors().find((d) => d.depth === 1);
                legend.querySelectorAll('.research-legend__button').forEach((button) => {
                    button.setAttribute('aria-pressed', String(Boolean(office) && button.dataset.key === office.data.key));
                });
            }
            if (techToggle) {
                techToggle.setAttribute('aria-pressed', String(view.technology));
            }
            if (techShare) {
                const lit = d3.sum(focus.leaves().filter((d) => d.technology), (d) => d.value);
                const share = focus.value > 0 ? (lit / focus.value * 100).toFixed(1) : '0.0';
                techShare.textContent = `${formatMoney(lit)}, ${share}% of ${focus.data.name}.`;
                techShare.hidden = !view.technology;
            }

            const largest = focus.leaves()
                .filter((d) => d.data.kind === 'company')
                .sort((a, b) => b.value - a.value)[0];
            const largestText = largest ? ` Largest company: ${largest.data.name}, ${formatMoney(largest.value)}.` : '';
            chart.svg.attr('aria-label', `Sunburst of SEC filers by Public float in ${period}, showing ${trail}: ` +
                `${formatMoney(focus.value)}.${largestText}`);
        }

        /**
         * Move the arcs and labels to the current view, tweening from where they are.
         * @param {number} duration - The transition length in ms; 0 applies the view at once.
         * @returns {void}
         */
        function applyView (duration) {
            const focus = focusNode();
            view.focusKey = focus.data.key;
            chart.focus = focus;
            chart.nodes.forEach((node) => { node.target = targetOf(node, focus); });
            const { unit, arc } = chart;

            const paths = chart.arcs.selectAll('path')
                .attr('pointer-events', (d) => (isVisible(d.target) ? 'auto' : 'none'))
                .classed('research-sunburst__arc--zoomable', (d) => Boolean(d.children) && isVisible(d.target));
            const labels = chart.labels.selectAll('text')
                .data(chart.nodes.filter(hasLabel), (d) => d.data.key)
                .join('text')
                .attr('class', 'research-sunburst__label')
                .attr('dy', '0.35em')
                .attr('fill', (d) => (DARK_LABEL_OFFICES.includes(d.data.office)
                    ? 'var(--research-label-dark)'
                    : 'var(--research-label-light)'))
                .text((d) => labelText(d, unit));

            paths.interrupt();
            labels.interrupt();
            if (duration === 0) {
                paths.each((d) => { d.current = d.target; })
                    .attr('d', (d) => arc(d.target))
                    .attr('fill-opacity', opacityOf);
                labels.attr('transform', (d) => labelTransform(d.target, unit));
            } else {
                const t = chart.svg.transition().duration(duration).ease(d3.easeCubicInOut);
                labels.attr('transform', (d) => labelTransform(d.current || d.target, unit))
                    .transition(t)
                    .attrTween('transform', (d) => {
                        const between = d3.interpolate(d.current || d.target, d.target);
                        return (step) => labelTransform(between(step), unit);
                    });
                paths.transition(t)
                    .attr('fill-opacity', opacityOf)
                    .attrTween('d', (d) => {
                        const between = d3.interpolate(d.current || d.target, d.target);
                        return (step) => {
                            d.current = between(step);
                            return arc(d.current);
                        };
                    });
            }
            describeView(focus);
        }

        /**
         * Zoom to a group, by its node key (ROOT_KEY for every filer).
         * @param {string} key - The node key.
         * @returns {void}
         */
        function zoomTo (key) {
            tooltip.hide();
            view.focusKey = key;
            applyView(transitionDuration());
        }

        /**
         * Zoom out one level, to the focused group's parent.
         * @returns {void}
         */
        function zoomOut () {
            const focus = chart ? focusNode() : null;
            if (focus && focus.parent) {
                zoomTo(focus.parent.data.key);
            }
        }

        /**
         * Turn the Technology highlight on or off. Fills keep their office colour; only the opacity changes.
         * @param {boolean} on - Whether to light only the Technology industries.
         * @returns {void}
         */
        function setTechnology (on) {
            view.technology = on;
            applyView(transitionDuration());
        }

        /**
         * Draw the sunburst of the current period at the host's width, in the current view (focus and highlight).
         * @returns {void}
         */
        function draw () {
            const period = data.values[view.periodIndex];
            const size = Math.max(MIN_SIZE, Math.min(host.clientWidth || MAX_SIZE, MAX_SIZE));
            const unit = size / 2 / (RINGS + 1);

            const root = d3.hierarchy(buildTree(data, period))
                .sum((d) => (d.children ? 0 : d.value || 0))
                .sort(compareNodes);
            d3.partition().size([2 * Math.PI, RINGS + 1])(root);
            markTechnology(root);

            const arc = d3.arc()
                .startAngle((d) => d.x0)
                .endAngle((d) => d.x1)
                .padAngle((d) => Math.min((d.x1 - d.x0) / 2, 0.003))
                .padRadius(unit * 2)
                .innerRadius((d) => d.y0 * unit)
                .outerRadius((d) => Math.max(d.y0 * unit, d.y1 * unit - 1));

            d3.select(host).selectAll('svg').remove();
            const svg = d3.select(host).append('svg')
                .attr('viewBox', [-size / 2, -size / 2, size, size].join(' '))
                .attr('role', 'img');

            const nodes = root.descendants().slice(1).filter((d) => d.x1 > d.x0);
            const arcs = svg.append('g');
            arcs.selectAll('path')
                .data(nodes, (d) => d.data.key)
                .join('path')
                .attr('class', 'research-sunburst__arc')
                .attr('data-key', (d) => d.data.key)
                .attr('fill', (d) => officeColour(host, d.data.office))
                .on('click', (event, d) => {
                    if (d.children) {
                        zoomTo(d.data.key);
                    }
                })
                .on('pointermove', (event, d) => tooltip.show(event, d))
                .on('pointerdown', (event, d) => tooltip.show(event, d))
                .on('pointerleave', (event) => {
                    if (event.pointerType === 'mouse') {
                        tooltip.hide();
                    }
                });
            const labels = svg.append('g').attr('text-anchor', 'middle');

            // The centre: the period (or the focused group's name), its total and a hint. A click zooms out.
            const valueSize = Math.min(18, unit / 4);
            const titleSize = Math.min(12, unit / 6);
            const centre = svg.append('g')
                .attr('class', 'research-sunburst__centre')
                .attr('text-anchor', 'middle')
                .on('click', zoomOut);
            centre.append('circle').attr('r', Math.max(0, unit - 1)).attr('fill', 'transparent');
            const centreTitle = centre.append('text')
                .attr('class', 'research-sunburst__centre-title')
                .style('font-size', `${titleSize}px`)
                .attr('y', -valueSize * 0.9);
            const centreValue = centre.append('text')
                .attr('class', 'research-sunburst__centre-value')
                .style('font-size', `${valueSize}px`)
                .attr('dy', '0.35em');
            const centreHint = centre.append('text')
                .attr('class', 'research-sunburst__centre-title')
                .style('font-size', `${titleSize}px`)
                .attr('y', valueSize * 0.9 + titleSize * 0.8);

            chart = { root, nodes, unit, arc, svg, arcs, labels, centre, centreTitle, centreValue, centreHint, focus: root };
            // Pressing the focused office's button again zooms back out to every filer.
            renderLegend(legend, root, (key) => zoomTo(view.focusKey === key ? ROOT_KEY : key));
            applyView(0);
        }

        if (zoomOutButton) {
            zoomOutButton.addEventListener('click', zoomOut);
        }
        if (techToggle) {
            techToggle.addEventListener('click', () => setTechnology(!view.technology));
        }

        return { draw, zoomTo, zoomOut, setTechnology };
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
        const sunburst = createSunburst(host, data, tooltip);
        let width = host.clientWidth;
        sunburst.draw();

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
                    sunburst.draw();
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

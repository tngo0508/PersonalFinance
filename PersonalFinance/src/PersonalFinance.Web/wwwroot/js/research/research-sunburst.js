/* global d3 */
/**
 * Research page, Sectors tab: a D3 sunburst of the largest SEC filers by Public float, one year at a time.
 * Rings, inside out: SEC industry office, SIC industry, company (plus one "Other" slice per industry for the smaller
 * filers). Reads the Sectors snapshot from the JSON data island rendered by Views/Research/Index.cshtml, so it makes
 * no request of its own. Hover or tap a slice for its figures; the centre shows the total. The page's scrubber
 * (js/research/research-page.js) moves it between years through update(), and its table view reads table().
 * Click an office or industry (or press an office's legend button) to zoom in, so three rings show below it; the
 * centre and the Zoom out control zoom out. The "Technology (incl. chip makers)" toggle dims every slice outside the
 * industries the server flags as Technology. Both survive a change of year.
 */
(function () {
    'use strict';

    // research-page.js loads first and holds the helpers every Research chart shares.
    const page = window.ResearchPage;
    if (!page) {
        return;
    }
    const { prefersReducedMotion, formatMoney } = page;

    const DATA_ELEMENT_ID = 'research-sectors-data';
    const HOST_ID = 'research-sunburst';
    const LEGEND_ID = 'research-sectors-legend';
    const TOOLTIP_ID = 'research-tooltip';
    const SUBTITLE_ID = 'research-sectors-subtitle';
    const ZOOM_OUT_ID = 'research-zoom-out';
    const FOCUS_LABEL_ID = 'research-sunburst-focus';
    const TECH_TOGGLE_ID = 'research-tech-toggle';
    const TECH_SHARE_ID = 'research-tech-share';
    const LIVE_ID = 'research-live';
    const ROOT_KEY = 'root';
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
    // Dimmed by the Technology highlight: the fill keeps its office colour, only fainter.
    const DIMMED_OPACITY = 0.15;
    const KIND_LABELS = {
        office: () => 'SEC industry office',
        industry: () => 'SIC industry',
        company: (node) => (node.data.ticker ? `Ticker ${node.data.ticker}` : 'No ticker'),
        other: (node) => `Smaller filers in ${node.parent ? node.parent.data.name : 'this industry'}`
    };

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
        return page.createTooltip(tooltip, (line, node) => {
            const valueLine = document.createElement('span');
            valueLine.append(line(formatMoney(node.value), 'research-tooltip__value'), ' Public float');
            const parts = [
                line(node.data.name, 'research-tooltip__name'),
                line(KIND_LABELS[node.data.kind](node), 'research-tooltip__muted'),
                valueLine
            ];
            if (node.parent && node.parent.value > 0) {
                const share = (node.value / node.parent.value * 100).toFixed(1);
                parts.push(line(`${share}% of ${node.parent.data.name}`, 'research-tooltip__muted'));
            }
            return parts;
        });
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
     * A node's geometry relative to the focused node: the focus fills the full circle and sits at the centre, so the
     * three rings below it are the ones drawn.
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
     * @param {{x0: number, x1: number, y0: number, y1: number}} shape - The arc geometry.
     * @returns {boolean} True when the arc is drawn.
     */
    function isVisible (shape) {
        return shape.y0 >= 1 && shape.y1 <= RINGS + 1 && shape.x1 > shape.x0;
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
     * Draw the office legend into the side panel: one button per office, which zooms to that office. The buttons
     * are kept while the period has the same offices, so keyboard focus stays on the one pressed; when they are
     * redrawn, focus moves to the new button of the same office.
     * @param {HTMLElement|null} legend - The legend list element.
     * @param {Object} root - The d3 hierarchy root.
     * @param {function(string): void} onPick - Called with the office's node key when its button is pressed.
     * @returns {void}
     */
    function renderLegend (legend, root, onPick) {
        if (!legend) {
            return;
        }
        const keys = (root.children || []).map((office) => office.data.key).join(',');
        if (legend.dataset.keys === keys) {
            return;
        }
        legend.dataset.keys = keys;
        const focused = legend.contains(document.activeElement) ? document.activeElement.dataset.key : null;
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
        const refocus = focused ? legend.querySelector(`[data-key="${focused}"]`) : null;
        if (refocus) {
            refocus.focus();
        }
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
     *
     * The view (the zoom focus and the Technology highlight) is kept here, outside any one period, and every update()
     * draws in it: a scrubber step or a redraw at a new width keeps where the user has zoomed and what is lit. A zoom
     * or a highlight change is an update() of the current period.
     * @param {HTMLElement} host - The chart host.
     * @param {Object} data - The Sectors snapshot.
     * @param {Object} tooltip - The tooltip controller.
     * @returns {{periods: string[], transitionMs: number, update: function(number, Object): string,
     *     table: function(number): Object, resize: function(): void, zoomTo: function(string): void,
     *     zoomOut: function(): void, setTechnology: function(boolean): void}} The chart.
     */
    function createSunburst (host, data, tooltip) {
        const subtitle = document.getElementById(SUBTITLE_ID);
        const legend = document.getElementById(LEGEND_ID);
        const zoomOutButton = document.getElementById(ZOOM_OUT_ID);
        const focusLabel = document.getElementById(FOCUS_LABEL_ID);
        const techToggle = document.getElementById(TECH_TOGGLE_ID);
        const techShare = document.getElementById(TECH_SHARE_ID);
        const live = document.getElementById(LIVE_ID);
        const view = { focusKey: ROOT_KEY, technology: false };
        let periodIndex = data.values.length - 1;
        let focus = null;
        let unit = 0;
        let arc = null;
        let svg = null;
        let arcLayer = null;
        let labelLayer = null;
        let centre = null;
        let centreTitle = null;
        let centreValue = null;
        let centreHint = null;

        const isLit = (node) => !view.technology || node.technology;
        const opacityOf = (node) => {
            if (!isVisible(node.target)) {
                return 0;
            }
            const base = node.data.kind === 'other' ? OTHER_OPACITY : RING_OPACITY[node.depth - focus.depth];
            return isLit(node) ? base : DIMMED_OPACITY;
        };

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

            // The centre: the year (or the focused group's name), its total and a hint, scaled to fit the inner
            // circle. A click on it zooms out.
            const valueSize = Math.min(18, unit / 4);
            const titleSize = Math.min(12, unit / 6);
            centre = svg.append('g')
                .attr('class', 'research-sunburst__centre')
                .attr('text-anchor', 'middle')
                .on('click', zoomOut);
            centre.append('circle').attr('r', Math.max(0, unit - 1)).attr('fill', 'transparent');
            centreTitle = centre.append('text')
                .attr('class', 'research-sunburst__centre-title')
                .style('font-size', `${titleSize}px`)
                .attr('y', -valueSize * 0.9);
            centreValue = centre.append('text')
                .attr('class', 'research-sunburst__centre-value')
                .style('font-size', `${valueSize}px`)
                .attr('dy', '0.35em');
            centreHint = centre.append('text')
                .attr('class', 'research-sunburst__centre-title')
                .style('font-size', `${titleSize}px`)
                .attr('y', valueSize * 0.9 + titleSize * 0.8);
        }

        /**
         * Draw the arcs of the period in the view, tweening from what is drawn now when animated.
         * @param {Object[]} nodes - The period's nodes, each with its `target` geometry.
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
                .attr('data-key', (d) => d.data.key)
                .each(function (d) {
                    this.__arc = collapsed(d.target);
                })
                .attr('d', function () {
                    return arc(this.__arc);
                })
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

            // The fill follows the office and never changes with the view; the highlight only changes the opacity.
            const all = entering.merge(paths)
                .attr('pointer-events', (d) => (isVisible(d.target) ? null : 'none'))
                .classed('research-sunburst__arc--zoomable', (d) => Boolean(d.children) && isVisible(d.target))
                .attr('fill', (d) => officeColour(host, d.data.office));
            if (transition) {
                all.transition(transition)
                    .attr('fill-opacity', opacityOf)
                    .attrTween('d', function (d) {
                        const move = d3.interpolate(this.__arc, d.target);
                        return (t) => arc(this.__arc = move(t));
                    });
            } else {
                all.each(function (d) {
                    this.__arc = d.target;
                }).attr('d', function () {
                    return arc(this.__arc);
                }).attr('fill-opacity', opacityOf);
            }
        }

        /**
         * Draw the labels of the period: only on lit arcs in the visible rings that are tall enough for a line of
         * text at their middle radius.
         * @param {Object[]} nodes - The period's nodes, each with its `target` geometry.
         * @param {Object|null} transition - The shared d3 transition, or null to apply at once.
         * @returns {void}
         */
        function drawLabels (nodes, transition) {
            const labelled = nodes.filter((d) => d.data.kind !== 'other' && isVisible(d.target) && isLit(d) &&
                (d.target.x1 - d.target.x0) * (d.target.y0 + d.target.y1) / 2 * unit > 13);
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
                    // Enter where the arc is drawn now, so a label rides in with its arc when zooming.
                    const drawn = arcLayer.select(`path[data-key="${d.data.key}"]`).node();
                    this.__arc = drawn && drawn.__arc ? drawn.__arc : d.target;
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
                        const move = d3.interpolate(this.__arc, d.target);
                        return (t) => labelTransform(this.__arc = move(t), unit);
                    });
            } else {
                all.each(function (d) {
                    this.__arc = d.target;
                }).attr('transform', function () {
                    return labelTransform(this.__arc, unit);
                }).attr('fill-opacity', 1);
            }
        }

        /**
         * Show the focus in the centre and above the chart, and the state of the Zoom out control, the legend
         * buttons, the Technology toggle and its share.
         * @param {string} periodLabel - The period shown.
         * @returns {{trail: string, techText: string}} Where the chart is (e.g. "All filers › Technology") and the
         *     Technology share of the focus.
         */
        function describeView (periodLabel) {
            const zoomed = focus.depth > 0;
            const trail = focus.ancestors().reverse().map((d) => d.data.name).join(' › ');
            centreTitle.text(zoomed ? shorten(focus.data.name, Math.floor(unit / 3)) : periodLabel);
            centreValue.text(formatMoney(focus.value || 0));
            centreHint.text(zoomed ? 'Zoom out' : 'Public float');
            centre.classed('research-sunburst__centre--zoomed', zoomed);
            if (focusLabel) {
                focusLabel.textContent = `${trail}: ${formatMoney(focus.value || 0)}`;
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
            const lit = d3.sum(focus.leaves().filter((d) => d.technology), (d) => d.value);
            const share = focus.value > 0 ? (lit / focus.value * 100).toFixed(1) : '0.0';
            const techText = `${formatMoney(lit)}, ${share}% of ${focus.data.name} in ${periodLabel}.`;
            if (techShare) {
                techShare.textContent = techText;
                techShare.hidden = !view.technology;
            }
            return { trail, techText };
        }

        /**
         * Show one period, in the current view (zoom focus and Technology highlight).
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
            markTechnology(root);

            // Keep the focus by its key; a group the period does not have (or has no Public float in) falls back
            // to every filer.
            focus = root.descendants().find((d) => d.data.key === view.focusKey && d.x1 > d.x0) || root;
            view.focusKey = focus.data.key;
            const nodes = root.descendants().slice(1).filter((d) => d.x1 > d.x0);
            nodes.forEach((d) => { d.target = targetOf(d, focus); });

            // A change without animation stops any transition still running, so nothing moves afterwards.
            svg.selectAll('*').interrupt();
            const transition = options && options.animate
                ? svg.transition().duration(TRANSITION_MS).ease(d3.easeCubicInOut)
                : null;
            drawArcs(nodes, transition);
            drawLabels(nodes, transition);
            renderLegend(legend, root, (key) => zoomTo(view.focusKey === key ? ROOT_KEY : key));
            const { trail, techText } = describeView(period.period);

            const largest = focus.leaves()
                .filter((d) => d.data.kind === 'company')
                .sort((a, b) => b.value - a.value)[0];
            const largestText = largest ? ` Largest company: ${largest.data.name}, ${formatMoney(largest.value)}.` : '';
            const offices = (root.children || []).length;
            const total = `${formatMoney(root.value || 0)} in total across ${offices} SEC industry offices.`;
            const where = focus.depth > 0 ? ` Showing ${trail}: ${formatMoney(focus.value || 0)}.` : '';
            svg.attr('aria-label',
                `Sunburst of SEC filers by Public float in ${period.period}: ${total}${where}${largestText}`);
            if (subtitle) {
                const filers = (period.ranked.length + (period.restCount || 0)).toLocaleString('en-US');
                subtitle.textContent = `The ${period.ranked.length} largest of ${filers} filers in ${period.period}, ` +
                    'by SEC industry office and SIC industry. Smaller filers are summed into "Other" per industry.';
            }
            const tech = view.technology ? ` Technology (incl. chip makers): ${techText}` : '';
            return `${period.period}: ${total}${where}${largestText}${tech}`;
        }

        /**
         * Redraw the current period in a changed view, animated unless the user asks for reduced motion, and
         * announce it in the page's live region.
         * @returns {void}
         */
        function applyView () {
            tooltip.hide();
            const summary = update(periodIndex, { animate: !prefersReducedMotion() });
            if (live) {
                live.textContent = summary;
            }
        }

        /**
         * Zoom to a group, by its node key (ROOT_KEY for every filer).
         * @param {string} key - The node key.
         * @returns {void}
         */
        function zoomTo (key) {
            view.focusKey = key;
            applyView();
        }

        /**
         * Zoom out one level, to the focused group's parent.
         * @returns {void}
         */
        function zoomOut () {
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
            applyView();
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

        if (zoomOutButton) {
            zoomOutButton.addEventListener('click', zoomOut);
        }
        if (techToggle) {
            techToggle.addEventListener('click', () => setTechnology(!view.technology));
        }

        layout();
        return { periods: data.periods, transitionMs: TRANSITION_MS, update, table, resize, zoomTo, zoomOut, setTechnology };
    }

    /**
     * Draw the sunburst when its host and data are on the page, hand it to the page's scrubber, and redraw it when
     * the card changes width.
     * @returns {void}
     */
    function init () {
        const host = document.getElementById(HOST_ID);
        const data = page.readData(DATA_ELEMENT_ID, 'Research Sectors data');
        if (!host || !data || !Array.isArray(data.values) || data.values.length === 0 || typeof d3 === 'undefined') {
            return;
        }
        const tooltip = createTooltip(document.getElementById(TOOLTIP_ID));
        const chart = createSunburst(host, data, tooltip);
        page.registerTab(TAB_ID, chart);
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

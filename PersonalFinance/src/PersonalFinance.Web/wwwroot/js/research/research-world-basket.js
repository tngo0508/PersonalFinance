/* global d3, topojson, PFI18n, ChartPalette */
/**
 * Research page, World tab: the "World market | Your basket" switch and the basket view.
 *
 * The basket view is a choropleth of the Fund Basket's weight by issuer country: each fund's country shares (from the
 * fund exposure snapshot, every holding of its latest N-PORT report) weighted by the fund's share of the basket. It
 * zooms and pans. Places the vendored 110m map has no shape for (Hong Kong, Singapore, Bermuda, …) are dots sized by
 * weight from a small coordinate table; dots keep their size under zoom. The single-hue scale tops out at the largest
 * country outside the US; the US always takes the darkest step, with its true share in the tooltip. Countries and dots
 * with weight are focusable. Callouts give the share outside the US (with the largest foreign country), the share not
 * priced in US dollars (with the largest currency) and the largest region. Shares under 0.05% read "<0.1%". Two
 * meters show US issuers against the rest of the world and US dollars against other currencies; region bars beside the
 * map light their countries while pointed at or focused (choosing one keeps them lit), and currency bars list what the
 * holdings are priced in.
 *
 * The switch opens on Your basket only when a basket is set at load; the market view (research-world.js) is left
 * exactly as it was. Reads the `research-fund-exposure-data` island and the basket from `window.ResearchBasket`.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const DATA_ID = 'research-fund-exposure-data';
    const VIEWS_ID = 'research-world-views';
    const PANEL_ID = 'research-panel-world';
    const MAP_ID = 'research-world-basket-map';
    const PROMPT_ID = 'research-world-basket-prompt';
    const VIEW_ID = 'research-world-basket-view';
    const CALLOUTS_ID = 'research-world-basket-callouts';
    const LEGEND_ID = 'research-world-basket-legend';
    const NOTE_ID = 'research-world-basket-note';
    const RESET_ID = 'research-world-basket-reset';
    const TOOLTIP_ID = 'research-world-basket-tooltip';
    const METERS_ID = 'research-world-basket-meters';
    const REGIONS_ID = 'research-world-basket-regions';
    const CURRENCIES_ID = 'research-world-basket-currencies';
    const REGION_ORDER = ['North America', 'Europe', 'Asia Pacific', 'Latin America', 'Middle East & Africa'];
    const TOP_CURRENCIES = 5;
    const ANTARCTICA_ID = '010';
    const US = 'US';
    const ASPECT = 0.5;
    const MIN_WIDTH = 280;
    const STEPS = 8;
    // Places the 110m map has no shape for, drawn as dots: [longitude, latitude].
    const SMALL_PLACES = {
        HK: [114.17, 22.32],
        SG: [103.82, 1.35],
        BM: [-64.75, 32.3],
        KY: [-81.25, 19.3],
        JE: [-2.13, 49.21],
        GG: [-2.58, 49.45],
        IM: [-4.55, 54.24],
        VG: [-64.62, 18.42],
        MH: [171.18, 7.13],
        GI: [-5.35, 36.14],
        LI: [9.55, 47.16],
        MO: [113.54, 22.2],
        MC: [7.42, 43.74],
        LU: [6.13, 49.61],
        MT: [14.38, 35.94],
        CW: [-68.99, 12.17]
    };
    const regionNames = typeof Intl !== 'undefined' && Intl.DisplayNames
        ? new Intl.DisplayNames([PFI18n.lang], { type: 'region' })
        : null;

    /**
     * A share for display: "<0.1%" under 0.05%, else one decimal.
     * @param {number} value - The share in percent.
     * @returns {string} E.g. "12.3%" or "<0.1%", in the page culture.
     */
    function formatShare (value) {
        return value > 0 && value < 0.05 ? `<${page.formatPercent(0.1, 1)}` : page.formatPercent(value, 1);
    }

    /**
     * A country's name from its two-letter code.
     * @param {string} code - The ISO alpha-2 code.
     * @returns {string} The name in the page language, or the code.
     */
    function countryName (code) {
        try {
            return (regionNames && regionNames.of(code)) || code;
        } catch (error) {
            return code;
        }
    }

    /**
     * Create an element with a class and text.
     * @param {string} tag - The tag name.
     * @param {string} [className] - The class.
     * @param {string} [text] - The text content.
     * @returns {HTMLElement} The element.
     */
    function element (tag, className, text) {
        const node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        if (text !== undefined) {
            node.textContent = text;
        }
        return node;
    }

    /**
     * The basket's exposure: each fund's country and currency shares weighted by its share of the basket. A basket fund
     * with no exposure data is left out and the others' shares are scaled up to cover it.
     * @param {Object} data - The fund exposure snapshot.
     * @param {Object[]} basket - The basket's funds (ticker, share).
     * @returns {{countries: Map<string, Object>, currencies: Map<string, number>, regions: Map<string, number>,
     *     missing: string[], count: number}} The exposure; count is how many basket funds have data.
     */
    function basketExposure (data, basket) {
        const byTicker = new Map(data.funds.map((f) => [f.ticker, f]));
        const funds = basket.filter((f) => byTicker.has(f.ticker));
        const missing = basket.filter((f) => !byTicker.has(f.ticker)).map((f) => f.ticker);
        const total = funds.reduce((sum, f) => sum + f.share, 0) || 1;
        const countries = new Map();
        const currencies = new Map();
        const regions = new Map();
        funds.forEach((fund) => {
            const weight = fund.share / total;
            const exposure = byTicker.get(fund.ticker);
            exposure.countries.forEach((c) => {
                const entry = countries.get(c.code) || { code: c.code, mapId: c.mapId, region: c.region, percent: 0 };
                entry.percent += weight * c.percent;
                countries.set(c.code, entry);
                regions.set(c.region, (regions.get(c.region) || 0) + weight * c.percent);
            });
            exposure.currencies.forEach((c) => currencies.set(c.code, (currencies.get(c.code) || 0) + weight * c.percent));
        });
        return { countries, currencies, regions, missing, count: funds.length };
    }

    /**
     * The step colours and the empty colour, from the World tab's CSS tokens.
     * @param {Element} host - An element inside the Research page.
     * @returns {{steps: string[], empty: string}} The colours.
     */
    function readColours (host) {
        const style = getComputedStyle(host);
        const token = (name) => style.getPropertyValue(name).trim();
        return {
            steps: Array.from({ length: STEPS }, (_, i) => token(`--research-world-step-${i + 1}`)),
            empty: token('--research-world-nodata')
        };
    }

    /**
     * Wire the switch and the basket view.
     * @param {Object} data - The fund exposure snapshot.
     * @param {Object[]} features - The map's country shapes.
     * @returns {void}
     */
    function createView (data, features) {
        const panel = document.getElementById(PANEL_ID);
        const views = document.getElementById(VIEWS_ID);
        const host = document.getElementById(MAP_ID);
        const prompt = document.getElementById(PROMPT_ID);
        const view = document.getElementById(VIEW_ID);
        const calloutList = document.getElementById(CALLOUTS_ID);
        const legend = document.getElementById(LEGEND_ID);
        const note = document.getElementById(NOTE_ID);
        const reset = document.getElementById(RESET_ID);
        const buttons = Array.from(views.querySelectorAll('[data-world-view]'));
        const meters = document.getElementById(METERS_ID);
        const regionList = document.getElementById(REGIONS_ID);
        const currencyList = document.getElementById(CURRENCIES_ID);
        let pinnedRegion = null;
        let colours = readColours(host);
        const tooltip = page.createTooltip(document.getElementById(TOOLTIP_ID), (line, entry) => [
            line(countryName(entry.code), 'research-tooltip__name'),
            line(PFI18n.t('Research.Js.WorldBasket.OfBasket', { value: formatShare(entry.percent) }), 'research-tooltip__value'),
            line(entry.region, 'research-tooltip__muted')
        ]);
        let mode = 'market';
        let exposure = null;
        let scaleMax = 1;
        let zoom = null;
        let lastWidth = 0;

        const svg = d3.select(host).append('svg').attr('class', 'research-world-basket__svg');
        const layer = svg.append('g');
        const shapes = layer.append('g').attr('class', 'research-world__countries');
        const dots = layer.append('g').attr('class', 'research-world-basket__dots');
        const paths = shapes.selectAll('path').data(features, (f) => f.id || f.properties.name).join('path')
            .attr('class', 'research-world__country');

        /**
         * The step (0 to 7) of a share: the US always the darkest, every other country on the scale up to the largest
         * country outside the US.
         * @param {Object} entry - The country's exposure entry.
         * @returns {number} The step.
         */
        function stepOf (entry) {
            if (entry.code === US) {
                return STEPS - 1;
            }
            return Math.min(STEPS - 1, Math.floor(entry.percent / scaleMax * STEPS));
        }

        /**
         * Show the card for a country or dot beside its element.
         * @param {Object} entry - The exposure entry.
         * @param {Element} node - Its element.
         * @param {Event} [event] - The pointer event, when there is one.
         * @returns {void}
         */
        function show (entry, node, event) {
            if (event && event.clientX !== undefined) {
                tooltip.show(event, entry);
                return;
            }
            const box = node.getBoundingClientRect();
            tooltip.show({ clientX: box.left + box.width / 2, clientY: box.top + box.height / 2 }, entry);
        }

        /**
         * Fit the projection to the host's width and draw the shapes and dots.
         * @returns {void}
         */
        function layout () {
            const width = Math.max(MIN_WIDTH, host.clientWidth || MIN_WIDTH);
            const height = Math.round(width * ASPECT);
            lastWidth = width;
            const projection = d3.geoEqualEarth().fitExtent([[2, 2], [width - 2, height - 2]], { type: 'FeatureCollection', features });
            svg.attr('viewBox', [0, 0, width, height]);
            paths.attr('d', d3.geoPath(projection));
            const byMapId = new Map(Array.from(exposure.countries.values()).filter((c) => c.mapId).map((c) => [c.mapId, c]));
            const shaped = new Set(features.map((f) => f.id));
            paths
                .attr('fill', (f) => {
                    const entry = byMapId.get(f.id);
                    return entry && entry.percent > 0 ? colours.steps[stepOf(entry)] : colours.empty;
                })
                .attr('tabindex', (f) => (byMapId.has(f.id) ? 0 : null))
                .attr('data-code', (f) => (byMapId.has(f.id) ? byMapId.get(f.id).code : null))
                .attr('data-region', (f) => (byMapId.has(f.id) ? byMapId.get(f.id).region : null))
                .attr('aria-label', (f) => {
                    const entry = byMapId.get(f.id);
                    return entry
                        ? PFI18n.t('Research.Js.WorldBasket.CountryLabel',
                            { name: countryName(entry.code), value: formatShare(entry.percent) })
                        : null;
                })
                .on('pointermove', (event, f) => {
                    const entry = byMapId.get(f.id);
                    if (entry) {
                        show(entry, event.currentTarget, event);
                    }
                })
                .on('pointerleave', tooltip.hide)
                .on('focus', (event, f) => show(byMapId.get(f.id), event.currentTarget))
                .on('blur', tooltip.hide);
            const small = Array.from(exposure.countries.values())
                .filter((c) => c.percent > 0 && SMALL_PLACES[c.code] && !(c.mapId && shaped.has(c.mapId)));
            const radius = d3.scaleSqrt().domain([0, scaleMax || 1]).range([2.5, 10]);
            const transform = d3.zoomTransform(svg.node());
            dots.selectAll('circle').data(small, (c) => c.code).join('circle')
                .attr('class', 'research-world-basket__dot')
                .attr('data-code', (c) => c.code)
                .attr('data-region', (c) => c.region)
                .attr('tabindex', 0)
                .attr('aria-label', (c) => PFI18n.t('Research.Js.WorldBasket.CountryLabel',
                    { name: countryName(c.code), value: formatShare(c.percent) }))
                .attr('cx', (c) => projection(SMALL_PLACES[c.code])[0])
                .attr('cy', (c) => projection(SMALL_PLACES[c.code])[1])
                .attr('data-r', (c) => Math.min(10, radius(Math.min(c.percent, scaleMax))))
                .attr('r', (c) => Math.min(10, radius(Math.min(c.percent, scaleMax))) / transform.k)
                .attr('fill', (c) => colours.steps[stepOf(c)])
                .on('pointermove', (event, c) => show(c, event.currentTarget, event))
                .on('pointerleave', tooltip.hide)
                .on('focus', (event, c) => show(c, event.currentTarget))
                .on('blur', tooltip.hide);
            zoom = d3.zoom().scaleExtent([1, 8]).translateExtent([[0, 0], [width, height]])
                .on('zoom', (event) => {
                    layer.attr('transform', event.transform);
                    // Dots keep their size, and borders their width, under zoom.
                    dots.selectAll('circle').attr('r', function () {
                        return Number(this.getAttribute('data-r')) / event.transform.k;
                    }).attr('stroke-width', 1 / event.transform.k);
                    shapes.attr('stroke-width', 0.5 / event.transform.k);
                    tooltip.hide();
                });
            svg.call(zoom);
            svg.attr('aria-label', PFI18n.t('Research.Js.WorldBasket.MapLabel', { callouts: calloutText() }));
            highlight(pinnedRegion);
        }

        /**
         * Light a region's countries and dots on the map and dim the rest, or clear it.
         * @param {string|null} region - The region, or null.
         * @returns {void}
         */
        function highlight (region) {
            svg.classed('research-world-basket__svg--highlighting', Boolean(region));
            [paths, dots.selectAll('circle')].forEach((selection) => selection
                .classed('is-lit', function () {
                    return Boolean(region) && this.getAttribute('data-region') === region;
                }));
        }

        /**
         * A bar row: the label, a bar as wide as its share of the largest, and the share written out.
         * @param {string} label - The row's label.
         * @param {number} value - Its share of the basket, in percent.
         * @param {number} max - The largest share in the list.
         * @param {string} tag - The row's element, "li" or "button".
         * @returns {HTMLElement} The row's content element.
         */
        function barRow (label, value, max, tag) {
            const row = element(tag, 'research-world-basket__bar-row');
            const track = element('span', 'research-world-basket__bar-track');
            const bar = element('span', 'research-world-basket__bar');
            // A share that rounds to nothing draws no bar; any other at least a sliver.
            bar.style.width = `${max > 0 && value >= 0.05 ? Math.max(1, value / max * 100) : 0}%`;
            track.append(bar);
            track.setAttribute('aria-hidden', 'true');
            row.append(element('span', 'research-world-basket__bar-label', label), track,
                element('span', 'research-world-basket__bar-value', formatShare(value)));
            return row;
        }

        /**
         * Draw the region bars, in the fixed region order: each a button that lights its countries.
         * @returns {void}
         */
        function drawRegions () {
            const shares = REGION_ORDER.map((region) => [region, exposure.regions.get(region) || 0]);
            const max = Math.max(...shares.map(([, v]) => v));
            if (pinnedRegion && !shares.some(([r, v]) => r === pinnedRegion && v > 0)) {
                pinnedRegion = null;
            }
            regionList.replaceChildren(...shares.map(([region, value]) => {
                const li = element('li');
                const button = barRow(region, value, max, 'button');
                button.type = 'button';
                button.dataset.region = region;
                button.setAttribute('aria-pressed', String(pinnedRegion === region));
                button.setAttribute('aria-label', PFI18n.t('Research.Js.WorldBasket.RegionLabel', { region, value: formatShare(value) }));
                button.addEventListener('pointerenter', () => highlight(region));
                button.addEventListener('pointerleave', () => highlight(pinnedRegion));
                button.addEventListener('focus', () => highlight(region));
                button.addEventListener('blur', () => highlight(pinnedRegion));
                button.addEventListener('click', () => {
                    pinnedRegion = pinnedRegion === region ? null : region;
                    regionList.querySelectorAll('button').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.region === pinnedRegion)));
                    highlight(pinnedRegion);
                });
                li.append(button);
                return li;
            }));
        }

        /**
         * Draw the currency bars: the largest few, then the rest together.
         * @returns {void}
         */
        function drawCurrencies () {
            const sorted = Array.from(exposure.currencies.entries()).filter(([, v]) => v > 0).sort((a, b) => b[1] - a[1]);
            const top = sorted.slice(0, TOP_CURRENCIES);
            const rest = sorted.slice(TOP_CURRENCIES).reduce((sum, [, v]) => sum + v, 0);
            const rows = rest > 0
                ? [...top, [PFI18n.plural('Research.Js.WorldBasket.OtherCurrencies', sorted.length - TOP_CURRENCIES), rest]]
                : top;
            const max = Math.max(...rows.map(([, v]) => v), 0);
            currencyList.replaceChildren(...rows.map(([code, value]) => barRow(code, value, max, 'li')));
        }

        /**
         * Draw the two home-bias meters: US issuers against the rest of the world, US dollars against other currencies.
         * @returns {void}
         */
        function drawMeters () {
            const us = (exposure.countries.get(US) || { percent: 0 }).percent;
            const rest = Array.from(exposure.countries.values()).filter((c) => c.code !== US).reduce((sum, c) => sum + c.percent, 0);
            const usd = exposure.currencies.get('USD') || 0;
            const other = Array.from(exposure.currencies.entries()).filter(([code]) => code !== 'USD').reduce((sum, [, v]) => sum + v, 0);
            const meter = (key, title, homeLabel, home, awayLabel, away) => {
                const unallocated = Math.max(0, 100 - home - away);
                const wrap = element('div', 'research-world-basket__meter');
                const id = `research-world-basket-meter-${key}`;
                const heading = element('p', 'research-world-basket__meter-title', title);
                heading.id = id;
                const bar = element('div', 'research-world-basket__meter-bar');
                bar.setAttribute('role', 'meter');
                bar.setAttribute('aria-labelledby', id);
                bar.setAttribute('aria-valuemin', '0');
                bar.setAttribute('aria-valuemax', '100');
                bar.setAttribute('aria-valuenow', (Math.round(home * 10) / 10).toString());
                const parts = { home: homeLabel, homeValue: formatShare(home), away: awayLabel, awayValue: formatShare(away) };
                bar.setAttribute('aria-valuetext', unallocated >= 0.05
                    ? PFI18n.t('Research.Js.WorldBasket.MeterTextUnallocated',
                        Object.assign(parts, { unallocated: formatShare(unallocated) }))
                    : PFI18n.t('Research.Js.WorldBasket.MeterText', parts));
                const homePart = element('span', 'research-world-basket__meter-home');
                homePart.style.width = `${home}%`;
                const awayPart = element('span', 'research-world-basket__meter-away');
                awayPart.style.width = `${away}%`;
                bar.append(homePart, awayPart);
                const legend = element('p', 'research-world-basket__meter-legend');
                legend.setAttribute('aria-hidden', 'true');
                const keyText = (label, value) => PFI18n.t('Research.Js.WorldBasket.MeterKey', { label, value: formatShare(value) });
                legend.append(element('span', 'research-world-basket__meter-key research-world-basket__meter-key--home', keyText(homeLabel, home)),
                    element('span', 'research-world-basket__meter-key research-world-basket__meter-key--away', keyText(awayLabel, away)));
                wrap.append(heading, bar, legend);
                return wrap;
            };
            meters.replaceChildren(
                meter('us', PFI18n.t('Research.Js.WorldBasket.MeterCountryTitle'), PFI18n.t('Research.Js.WorldBasket.MeterUs'), us,
                    PFI18n.t('Research.Js.WorldBasket.MeterRest'), rest),
                meter('us-dollar', PFI18n.t('Research.Js.WorldBasket.MeterCurrencyTitle'), PFI18n.t('Research.Js.WorldBasket.MeterUsd'), usd,
                    PFI18n.t('Research.Js.WorldBasket.MeterOtherCurrencies'), other));
        }

        /**
         * The callouts' figures as a sentence, for the map's label.
         * @returns {string} The sentence.
         */
        function calloutText () {
            return Array.from(calloutList.querySelectorAll('li')).map((li) => li.textContent.replace(/\s+/g, ' ').trim()).join('. ');
        }

        /**
         * Draw the callouts: outside the US, not priced in US dollars, and the largest region.
         * @returns {void}
         */
        function drawCallouts () {
            const countries = Array.from(exposure.countries.values()).sort((a, b) => b.percent - a.percent);
            const foreign = countries.filter((c) => c.code !== US);
            const outside = foreign.reduce((sum, c) => sum + c.percent, 0);
            const currencies = Array.from(exposure.currencies.entries()).sort((a, b) => b[1] - a[1]);
            const others = currencies.filter(([code]) => code !== 'USD');
            const notDollars = others.reduce((sum, [, v]) => sum + v, 0);
            const regions = Array.from(exposure.regions.entries()).filter(([r]) => r !== 'Other').sort((a, b) => b[1] - a[1]);
            const items = [
                {
                    label: PFI18n.t('Research.Js.WorldBasket.Outside'),
                    value: formatShare(outside),
                    note: foreign.length
                        ? PFI18n.t('Research.Js.WorldBasket.Largest',
                            { name: countryName(foreign[0].code), value: formatShare(foreign[0].percent) })
                        : PFI18n.t('Research.Js.WorldBasket.OutsideNone')
                },
                {
                    label: PFI18n.t('Research.Js.WorldBasket.NotDollars'),
                    value: formatShare(notDollars),
                    note: others.length
                        ? PFI18n.plural('Research.Js.WorldBasket.LargestCurrency', others.length,
                            { name: others[0][0], value: formatShare(others[0][1]) })
                        : PFI18n.t('Research.Js.WorldBasket.NotDollarsNone')
                }
            ];
            if (regions.length) {
                items.push({
                    label: PFI18n.t('Research.Js.WorldBasket.LargestRegion'),
                    value: formatShare(regions[0][1]),
                    note: regions[0][0]
                });
            }
            calloutList.replaceChildren(...items.map((c) => {
                const li = element('li', 'research-overlap__callout');
                li.append(element('span', 'research-overlap__callout-label', c.label),
                    element('span', 'research-overlap__callout-value', c.value),
                    element('span', 'research-overlap__callout-note', c.note));
                return li;
            }));
        }

        /**
         * Draw the colour scale's legend, from just above zero to the largest country outside the US.
         * @returns {void}
         */
        function drawLegend () {
            const items = colours.steps.map((colour, i) => {
                const li = element('li', 'research-legend__item');
                const swatch = element('span', 'research-legend__swatch research-world__swatch');
                swatch.style.background = colour;
                swatch.setAttribute('aria-hidden', 'true');
                const from = i / STEPS * scaleMax;
                const label = i === STEPS - 1
                    ? PFI18n.t('Research.Js.WorldBasket.LegendTop', { value: formatShare(from) })
                    : PFI18n.t('Research.Js.Common.FromTo', { from: formatShare(from), to: formatShare((i + 1) / STEPS * scaleMax) });
                li.append(swatch, element('span', null, label));
                return li;
            });
            legend.replaceChildren(...items);
        }

        /**
         * Redraw the basket view for the basket.
         * @returns {void}
         */
        function drawBasket () {
            const basket = window.ResearchBasket ? window.ResearchBasket.get() : [];
            exposure = basketExposure(data, basket);
            const has = exposure.count > 0;
            prompt.hidden = has;
            view.hidden = !has;
            buttons.find((b) => b.dataset.worldView === 'basket').disabled = basket.length === 0;
            if (!has) {
                return;
            }
            const foreign = Array.from(exposure.countries.values()).filter((c) => c.code !== US);
            scaleMax = Math.max(0.1, ...foreign.map((c) => c.percent));
            note.textContent = exposure.missing.length
                ? PFI18n.plural('Research.Js.Common.MissingReport', exposure.missing.length, { funds: exposure.missing.join(', ') })
                : '';
            drawCallouts();
            drawMeters();
            drawRegions();
            drawCurrencies();
            drawLegend();
            if (host.clientWidth > 0) {
                layout();
            }
        }

        /**
         * Show one view and hide the other's parts.
         * @param {string} next - "market" or "basket".
         * @returns {void}
         */
        function setMode (next) {
            mode = next;
            buttons.forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.worldView === mode)));
            panel.querySelectorAll('[data-world-view-part]').forEach((part) => {
                part.hidden = part.dataset.worldViewPart !== mode;
            });
            tooltip.hide();
            if (mode === 'basket') {
                drawBasket();
            }
        }

        buttons.forEach((button) => button.addEventListener('click', () => {
            if (button.disabled) {
                return;
            }
            setMode(button.dataset.worldView);
            if (page.announce) {
                page.announce(PFI18n.t(mode === 'basket'
                    ? 'Research.Js.WorldBasket.ShowingBasket'
                    : 'Research.Js.WorldBasket.ShowingMarket'));
            }
        }));
        reset.addEventListener('click', () => {
            if (zoom) {
                svg.call(zoom.transform, d3.zoomIdentity);
            }
        });
        document.addEventListener('research:basketchange', () => {
            const basket = window.ResearchBasket ? window.ResearchBasket.get() : [];
            if (basket.length === 0 && mode === 'basket') {
                setMode('market');
            }
            drawBasket();
        });
        document.addEventListener('research:tabchange', () => {
            if (mode === 'basket' && exposure && exposure.count > 0 && host.clientWidth > 0) {
                layout();
            }
        });
        window.addEventListener('scroll', () => tooltip.hide(), { passive: true });
        if (typeof ResizeObserver === 'function') {
            new ResizeObserver(() => {
                if (mode === 'basket' && exposure && exposure.count > 0 && host.clientWidth > 0
                    && Math.abs(host.clientWidth - lastWidth) > 4) {
                    layout();
                }
            }).observe(host);
        }
        // A theme switch re-reads the step colours and redraws the basket view (#486).
        ChartPalette.subscribe(() => {
            colours = readColours(host);
            drawBasket();
        });
        const basket = window.ResearchBasket ? window.ResearchBasket.get() : [];
        drawBasket();
        setMode(basket.length > 0 && exposure.count > 0 ? 'basket' : 'market');
    }

    /**
     * Wire the view when its data, the map and the page helpers are present.
     * @returns {Promise<void>}
     */
    async function init () {
        const host = document.getElementById(MAP_ID);
        if (!page || !host || !document.getElementById(VIEWS_ID) || typeof d3 === 'undefined' || typeof topojson === 'undefined') {
            return;
        }
        const data = page.readData(DATA_ID, 'Research fund exposure data');
        if (!data || !Array.isArray(data.funds)) {
            return;
        }
        try {
            const response = await fetch(host.dataset.atlasUrl, { credentials: 'same-origin' });
            if (!response.ok) {
                return;
            }
            const atlas = await response.json();
            const features = topojson.feature(atlas, atlas.objects.countries).features.filter((f) => f.id !== ANTARCTICA_ID);
            createView(data, features);
        } catch (error) {
            // eslint-disable-next-line no-console
            console.warn('The basket map could not be drawn:', error);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

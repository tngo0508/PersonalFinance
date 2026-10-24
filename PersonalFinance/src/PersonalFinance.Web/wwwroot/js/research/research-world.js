/* global d3, topojson */
/**
 * Research page, World tab: a D3 choropleth of stock-market capitalisation by country, one year at a time, in an
 * Equal Earth projection without Antarctica. Reads the World snapshot from the JSON data island rendered by
 * Views/Research/_WorldTab.cshtml and the vendored world-atlas countries-110m borders (keyed by ISO numeric code).
 * An 8-step single-hue scale colours each country by market cap or by market cap as % of GDP (the metric switch in
 * the side panel); countries with no figure for the year are hatched grey ("No data"). Hover or tap a country for
 * its figures. The page's scrubber (js/research/research-page.js) moves it between years through update(), and its
 * table view reads table().
 */
(function () {
    'use strict';

    // research-page.js loads first and holds the helpers every Research chart shares.
    const page = window.ResearchPage;
    if (!page) {
        return;
    }
    const { prefersReducedMotion, formatMoney } = page;

    const DATA_ELEMENT_ID = 'research-world-data';
    const HOST_ID = 'research-world-map';
    const LEGEND_ID = 'research-world-legend';
    const TOOLTIP_ID = 'research-world-tooltip';
    const SUBTITLE_ID = 'research-world-subtitle';
    const LIVE_ID = 'research-live';
    const TAB_ID = 'world';
    const ANTARCTICA_ID = '010';
    const NO_DATA_PATTERN_ID = 'research-world-nodata';
    const TRANSITION_MS = 600;
    const MIN_WIDTH = 280;
    const ASPECT = 0.5;
    const STEPS = 8;

    /**
     * Format a percentage of GDP to one decimal place.
     * @param {number} value - The percentage.
     * @returns {string} The formatted percentage, e.g. 172.4%.
     */
    function formatPercent (value) {
        return `${value.toFixed(1)}%`;
    }

    // The two metrics of the switch: which field they read, their seven thresholds (eight steps), and their labels.
    const METRICS = {
        marketCap: {
            label: 'Market cap',
            legendTitle: 'Market cap (US$)',
            field: 'marketCap',
            thresholds: [1e9, 1e10, 5e10, 1e11, 5e11, 1e12, 5e12],
            stepLabels: ['$1B', '$10B', '$50B', '$100B', '$500B', '$1T', '$5T'],
            format: formatMoney
        },
        percentOfGdp: {
            label: 'Market cap as % of GDP',
            legendTitle: 'Market cap as % of GDP',
            field: 'percentOfGdp',
            thresholds: [10, 25, 50, 75, 100, 150, 200],
            stepLabels: ['10%', '25%', '50%', '75%', '100%', '150%', '200%'],
            format: formatPercent
        }
    };

    /**
     * Read the vendored world-atlas borders.
     * @param {string} url - The countries-110m.json URL.
     * @returns {Promise<Object[]|null>} The country features without Antarctica, or null when they cannot be read.
     */
    async function readCountries (url) {
        try {
            const response = await fetch(url, { credentials: 'same-origin' });
            if (!response.ok) {
                return null;
            }
            const atlas = await response.json();
            return topojson.feature(atlas, atlas.objects.countries).features
                .filter((feature) => feature.id !== ANTARCTICA_ID);
        } catch (error) {
            // eslint-disable-next-line no-console
            console.warn('World map borders could not be read:', error);
            return null;
        }
    }

    /**
     * The step (0 to 7) of a value on a metric's scale.
     * @param {Object} metric - One of METRICS.
     * @param {number} value - The value.
     * @returns {number} The step index.
     */
    function stepOf (metric, value) {
        const index = metric.thresholds.findIndex((threshold) => value < threshold);
        return index < 0 ? metric.thresholds.length : index;
    }

    /**
     * The eight step colours and the "No data" colours, read from the page's CSS tokens.
     * @param {Element} element - An element inside the Research page.
     * @returns {{steps: string[], noData: string, noDataInk: string}} CSS colours.
     */
    function readColours (element) {
        const style = getComputedStyle(element);
        const token = (name) => style.getPropertyValue(name).trim();
        return {
            steps: Array.from({ length: STEPS }, (_, i) => token(`--research-world-step-${i + 1}`)),
            noData: token('--research-world-nodata'),
            noDataInk: token('--research-world-nodata-ink')
        };
    }

    /**
     * Index the snapshot for drawing: each year's figures by ISO numeric id, and each country's name.
     * @param {Object} data - The World snapshot.
     * @returns {{years: Map<string, Object>[], names: Map<string, string>}} The lookups, one map per period.
     */
    function indexData (data) {
        const names = new Map(data.countries.map((country) => [country.id, country.name]));
        const years = data.values.map((period) => new Map(period.countries.map((value) => {
            const country = data.countries[value.country];
            return [country.id, { name: country.name, marketCap: value.marketCap, percentOfGdp: value.percentOfGdp }];
        })));
        return { years, names };
    }

    /**
     * Create the tooltip controller for the World tooltip element.
     * @param {HTMLElement|null} tooltip - The tooltip element.
     * @returns {{show: function(PointerEvent, string, Object|null, string): void, hide: function(): void}} The
     *     controller: show(event, country name, figures or null, year).
     */
    function createTooltip (tooltip) {
        return page.createTooltip(tooltip, (line, name, figures, year) => {
            const lines = [line(name, 'research-tooltip__name')];
            if (!figures) {
                lines.push(line(`No data for ${year}`, 'research-tooltip__muted'));
            } else {
                const cap = document.createElement('span');
                cap.className = 'research-tooltip__line';
                cap.append(line(formatMoney(figures.marketCap), 'research-tooltip__value'), ' market cap');
                const share = document.createElement('span');
                share.className = 'research-tooltip__line';
                share.append(figures.percentOfGdp === null || figures.percentOfGdp === undefined
                    ? line('No GDP figure', 'research-tooltip__muted')
                    : line(formatPercent(figures.percentOfGdp), 'research-tooltip__value'),
                figures.percentOfGdp === null || figures.percentOfGdp === undefined ? '' : ' of GDP');
                lines.push(cap, share, line(year, 'research-tooltip__muted'));
            }
            return lines;
        });
    }

    /**
     * Draw the stepped legend for a metric into the side panel: eight steps, then "No data".
     * @param {HTMLElement|null} legend - The legend list element.
     * @param {Object} metric - One of METRICS.
     * @param {Object} colours - The colours from readColours().
     * @returns {void}
     */
    function renderLegend (legend, metric, colours) {
        if (!legend) {
            return;
        }
        const labels = colours.steps.map((_, i) => {
            if (i === 0) {
                return `Under ${metric.stepLabels[0]}`;
            }
            if (i === STEPS - 1) {
                return `${metric.stepLabels[i - 1]} or more`;
            }
            return `${metric.stepLabels[i - 1]} to ${metric.stepLabels[i]}`;
        });
        const item = (label, className, background) => {
            const li = document.createElement('li');
            li.className = 'research-legend__item';
            const swatch = document.createElement('span');
            swatch.className = `research-legend__swatch ${className}`;
            swatch.setAttribute('aria-hidden', 'true');
            if (background) {
                swatch.style.background = background;
            }
            const text = document.createElement('span');
            text.textContent = label;
            li.append(swatch, text);
            return li;
        };
        legend.setAttribute('aria-label', metric.legendTitle);
        legend.replaceChildren(
            ...labels.map((label, i) => item(label, 'research-world__swatch', colours.steps[i])),
            item('No data', 'research-world__swatch research-world__swatch--nodata', null)
        );
    }

    /**
     * Create the World choropleth in the host. It is drawn once, then moved between years by update() and between
     * metrics by setMetric().
     * @param {HTMLElement} host - The map host element.
     * @param {Object} data - The World snapshot.
     * @param {Object[]} features - The world-atlas country features, without Antarctica.
     * @param {Object} tooltip - The tooltip controller.
     * @returns {{periods: string[], transitionMs: number, update: function(number, Object): string,
     *     table: function(number): Object, setMetric: function(string): string, resize: function(): void}} The chart.
     */
    function createMap (host, data, features, tooltip) {
        const { years, names } = indexData(data);
        const legend = document.getElementById(LEGEND_ID);
        const subtitle = document.getElementById(SUBTITLE_ID);
        const colours = readColours(host);
        let metricKey = 'marketCap';
        let periodIndex = data.periods.length - 1;

        const svg = d3.select(host).append('svg').attr('role', 'img');
        const defs = svg.append('defs');
        const pattern = defs.append('pattern')
            .attr('id', NO_DATA_PATTERN_ID)
            .attr('patternUnits', 'userSpaceOnUse')
            .attr('width', 6)
            .attr('height', 6)
            .attr('patternTransform', 'rotate(45)');
        pattern.append('rect').attr('width', 6).attr('height', 6).attr('fill', colours.noData);
        pattern.append('line').attr('x1', 0).attr('y1', 0).attr('x2', 0).attr('y2', 6)
            .attr('stroke', colours.noDataInk).attr('stroke-width', 1.5);
        const countries = svg.append('g').attr('class', 'research-world__countries');
        const paths = countries.selectAll('path')
            .data(features, (feature) => feature.id || feature.properties.name)
            .join('path')
            .attr('class', 'research-world__country');

        const figuresOf = (feature) => (feature.id ? years[periodIndex].get(feature.id) : undefined);
        const nameOf = (feature) => (feature.id && names.get(feature.id)) || feature.properties.name;
        const valueOf = (feature) => {
            const figures = figuresOf(feature);
            const value = figures ? figures[METRICS[metricKey].field] : null;
            return value === null || value === undefined ? null : value;
        };

        paths
            .on('pointermove', (event, feature) => {
                tooltip.show(event, nameOf(feature), figuresOf(feature) || null, data.periods[periodIndex]);
            })
            .on('pointerdown', (event, feature) => {
                tooltip.show(event, nameOf(feature), figuresOf(feature) || null, data.periods[periodIndex]);
            })
            .on('pointerleave', tooltip.hide);

        /**
         * Fit the projection to the host's width and redraw the borders.
         * @returns {void}
         */
        function layout () {
            const width = Math.max(MIN_WIDTH, host.clientWidth || MIN_WIDTH);
            const height = Math.round(width * ASPECT);
            const projection = d3.geoEqualEarth()
                .fitExtent([[2, 2], [width - 2, height - 2]], { type: 'FeatureCollection', features });
            svg.attr('viewBox', [0, 0, width, height]).attr('width', width).attr('height', height);
            paths.attr('d', d3.geoPath(projection));
        }

        /**
         * Colour every country for the current year and metric.
         * @param {boolean} animate - Whether to tween the fills.
         * @returns {void}
         */
        function paint (animate) {
            const metric = METRICS[metricKey];
            const fillOf = (feature) => {
                const value = valueOf(feature);
                return value === null ? colours.noData : colours.steps[stepOf(metric, value)];
            };
            // "No data" takes the hatch through a class at once; a fill tween runs only between scale steps.
            paths.classed('research-world__country--nodata', (feature) => valueOf(feature) === null);
            paths.interrupt();
            if (animate) {
                paths.transition().duration(TRANSITION_MS).attr('fill', fillOf);
            } else {
                paths.attr('fill', fillOf);
            }
        }

        /**
         * The one-sentence summary of the current year and metric, for the subtitle, the map's label and the live
         * region.
         * @returns {string} The summary.
         */
        function summary () {
            const metric = METRICS[metricKey];
            const year = data.periods[periodIndex];
            const reporting = Array.from(years[periodIndex].values())
                .filter((figures) => figures[metric.field] !== null && figures[metric.field] !== undefined);
            if (reporting.length === 0) {
                return `${year}: no country has a ${metric.label.toLowerCase()} figure.`;
            }
            const largest = reporting.reduce((best, figures) =>
                (figures[metric.field] > best[metric.field] ? figures : best));
            return `${metric.label} by country, ${year}: ${reporting.length} countries have data. ` +
                `Highest: ${largest.name}, ${metric.format(largest[metric.field])}.`;
        }

        /**
         * Show the summary on the page and on the map.
         * @returns {string} The summary.
         */
        function describe () {
            const text = summary();
            svg.attr('aria-label', `World map. ${text}`);
            if (subtitle) {
                subtitle.textContent = text;
            }
            return text;
        }

        /**
         * Show one year.
         * @param {number} index - The period index.
         * @param {{animate: boolean}} options - Whether to tween the fills.
         * @returns {string} A one-sentence summary of the year, for the live region.
         */
        function update (index, options) {
            periodIndex = Math.max(0, Math.min(data.periods.length - 1, index));
            paint(Boolean(options && options.animate));
            return describe();
        }

        /**
         * Switch the metric the map is coloured by.
         * @param {string} key - A key of METRICS.
         * @returns {string} A one-sentence summary, for the live region.
         */
        function setMetric (key) {
            if (!METRICS[key]) {
                return '';
            }
            metricKey = key;
            renderLegend(legend, METRICS[key], colours);
            paint(!prefersReducedMotion());
            return describe();
        }

        /**
         * The year's countries as a table, largest market cap first.
         * @param {number} index - The period index.
         * @returns {{caption: string, columns: Object[], rows: string[][]}} The table.
         */
        function table (index) {
            const period = data.values[index];
            const rows = period.countries.map((value) => [
                data.countries[value.country].name,
                formatMoney(value.marketCap),
                value.percentOfGdp === null || value.percentOfGdp === undefined ? 'No data' : formatPercent(value.percentOfGdp)
            ]);
            return {
                caption: `Market capitalisation of listed domestic companies by country, ${period.period}`,
                columns: [
                    { label: 'Country', numeric: false },
                    { label: 'Market cap', numeric: true },
                    { label: '% of GDP', numeric: true }
                ],
                rows
            };
        }

        /**
         * Redraw at the host's new width, without a transition.
         * @returns {void}
         */
        function resize () {
            layout();
            paint(false);
        }

        layout();
        renderLegend(legend, METRICS[metricKey], colours);
        return { periods: data.periods, transitionMs: TRANSITION_MS, update, table, setMetric, resize };
    }

    /**
     * Wire the metric switch in the side panel.
     * @param {Object} chart - The chart from createMap().
     * @returns {void}
     */
    function initMetricSwitch (chart) {
        const buttons = Array.from(document.querySelectorAll('[data-research-world-metric]'));
        buttons.forEach((button) => {
            button.addEventListener('click', () => {
                buttons.forEach((other) => other.setAttribute('aria-pressed', String(other === button)));
                const text = chart.setMetric(button.dataset.researchWorldMetric);
                const live = document.getElementById(LIVE_ID);
                if (live && text) {
                    live.textContent = text;
                }
            });
        });
    }

    /**
     * Draw the map when its host, data and borders are available, hand it to the page's scrubber, and redraw it when
     * the card changes width.
     * @returns {Promise<void>}
     */
    async function init () {
        const host = document.getElementById(HOST_ID);
        const data = page.readData(DATA_ELEMENT_ID, 'Research World data');
        if (!host || !data || !Array.isArray(data.values) || data.values.length === 0 ||
            typeof d3 === 'undefined' || typeof topojson === 'undefined') {
            return;
        }
        const features = await readCountries(host.dataset.atlasUrl);
        if (!features) {
            const message = document.createElement('p');
            message.className = 'research-card__subtitle';
            message.textContent = 'The map could not be drawn. The table view still lists every country.';
            host.replaceChildren(message);
            return;
        }
        const tooltip = createTooltip(document.getElementById(TOOLTIP_ID));
        const chart = createMap(host, data, features, tooltip);
        initMetricSwitch(chart);
        page.registerTab(TAB_ID, chart);

        // A tap outside the map, or a scroll, puts the tooltip away.
        document.addEventListener('pointerdown', (event) => {
            if (!host.contains(event.target)) {
                tooltip.hide();
            }
        });
        window.addEventListener('scroll', tooltip.hide, { passive: true });

        let width = host.clientWidth;
        if (typeof ResizeObserver !== 'undefined') {
            const observer = new ResizeObserver(() => {
                if (host.clientWidth !== width && host.clientWidth > 0) {
                    width = host.clientWidth;
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

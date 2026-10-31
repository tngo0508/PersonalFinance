/* global d3, topojson, PFI18n, ChartPalette */
/**
 * Research World page and World tab: a D3 choropleth of stock-market capitalisation by country, one year at a time, in
 * an Equal Earth projection without Antarctica. Reads the World snapshot from the JSON data island rendered by
 * Views/Research/_WorldPanel.cshtml and the vendored world-atlas countries-110m borders (keyed by ISO numeric code).
 * An 8-step single-hue scale colours each country by market cap or by market cap as % of GDP (the metric switch in
 * the side panel); countries with no figure for the year are hatched grey ("No data"). Hover or tap a country for
 * its figures. The page's scrubber (js/research/research-page.js) moves it between years through update(), and its
 * table view reads table().
 *
 * A country is selected by clicking it on the map or choosing it in the side panel's list: it is outlined, and the
 * country detail shows its figures for the scrubber's year and its rank by market cap. The panel's
 * data-research-country opens the map with that country selected, matched by name whatever its case (an unknown name
 * selects nothing); window.ResearchWorld.selectCountry(name) does the same later. On the World page (not the tab) the
 * address's ?country= follows the selection, replacing the history entry.
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
    const PANEL_ID = 'research-panel-world';
    const COUNTRY_SELECT_ID = 'research-world-country';
    const DETAIL_ID = 'research-world-detail';
    const DETAIL_HINT_ID = 'research-world-detail-hint';
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
     * @returns {string} The formatted percentage, e.g. 172.4%, in the page culture.
     */
    function formatPercent (value) {
        return page.formatPercent(value, 1);
    }

    // The two metrics of the switch: which field they read, their seven thresholds (eight steps), and the keys of
    // their labels. The step labels follow the page culture ($1B, 10%).
    const MONEY_THRESHOLDS = [1e9, 1e10, 5e10, 1e11, 5e11, 1e12, 5e12];
    const PERCENT_THRESHOLDS = [10, 25, 50, 75, 100, 150, 200];
    const METRICS = {
        marketCap: {
            labelKey: 'Research.Js.World.MarketCap',
            legendTitleKey: 'Research.Js.World.LegendMarketCap',
            noneKey: 'Research.Js.World.NoneMarketCap',
            field: 'marketCap',
            thresholds: MONEY_THRESHOLDS,
            stepLabels: MONEY_THRESHOLDS.map((t) => PFI18n.formatCompactMoney(t, 'USD')),
            format: formatMoney
        },
        percentOfGdp: {
            labelKey: 'Research.Js.World.PercentOfGdpLabel',
            legendTitleKey: 'Research.Js.World.PercentOfGdpLabel',
            noneKey: 'Research.Js.World.NonePercentOfGdp',
            field: 'percentOfGdp',
            thresholds: PERCENT_THRESHOLDS,
            stepLabels: PERCENT_THRESHOLDS.map((t) => page.formatPercent(t, 0)),
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
     * Find a country of the snapshot by name, whatever its case and surrounding spaces.
     * @param {Object} data - The World snapshot.
     * @param {string} name - The name, e.g. "japan".
     * @returns {Object|null} The snapshot's country ({id, iso3, name}), or null when it has none of that name.
     */
    function findCountry (data, name) {
        const wanted = String(name || '').trim().toLocaleLowerCase();
        if (!wanted) {
            return null;
        }
        return data.countries.find((country) => country.name.toLocaleLowerCase() === wanted) || null;
    }

    /**
     * One country's figures for a year, with its rank by market cap among the countries that report one.
     * @param {Object} data - The World snapshot.
     * @param {number} periodIndex - The period index.
     * @param {string} countryId - The country's ISO numeric id.
     * @returns {{figures: Object|null, rank: number, count: number}} Its figures (null when it has none that year),
     *     its rank (0 when it has no market cap) and how many countries have one.
     */
    function countryFigures (data, periodIndex, countryId) {
        const period = data.values[periodIndex];
        const ranked = period.countries
            .filter((value) => value.marketCap !== null && value.marketCap !== undefined)
            .sort((a, b) => b.marketCap - a.marketCap);
        const value = period.countries.find((v) => data.countries[v.country].id === countryId);
        const rank = value ? ranked.indexOf(value) + 1 : 0;
        return { figures: value || null, rank, count: ranked.length };
    }

    /**
     * Fill the country detail for the selected country, or hide it (and show the hint) when none is selected.
     * @param {HTMLElement|null} detail - The detail element.
     * @param {HTMLElement|null} hint - The hint shown while nothing is selected.
     * @param {Object|null} country - The selected country, or null.
     * @param {{figures: Object|null, rank: number, count: number}|null} figures - From countryFigures().
     * @param {string} year - The scrubber's year.
     * @returns {string} The detail as one sentence, for the live region; '' when nothing is selected.
     */
    function renderDetail (detail, hint, country, figures, year) {
        if (hint) {
            hint.hidden = Boolean(country);
        }
        if (!detail) {
            return '';
        }
        detail.hidden = !country;
        if (!country) {
            detail.replaceChildren();
            return '';
        }
        const paragraph = (className, text) => {
            const p = document.createElement('p');
            p.className = className;
            p.textContent = text;
            return p;
        };
        const parts = [
            paragraph('research-world__detail-name', country.name),
            paragraph('research-world__detail-year', PFI18n.t('Research.Js.World.DetailYear', { year }))
        ];
        const sentence = [];
        if (!figures.figures) {
            const none = PFI18n.t('Research.Js.World.NoDataFor', { year });
            parts.push(paragraph('research-world__detail-none', none));
            sentence.push(none);
        } else {
            const list = document.createElement('dl');
            list.className = 'research-world__detail-figures';
            const noGdp = figures.figures.percentOfGdp === null || figures.figures.percentOfGdp === undefined;
            const rows = [
                [PFI18n.t('Research.Js.World.MarketCap'), formatMoney(figures.figures.marketCap)],
                [PFI18n.t('Research.Js.World.PercentOfGdp'),
                    noGdp ? PFI18n.t('Research.Js.World.NoGdp') : formatPercent(figures.figures.percentOfGdp)]
            ];
            rows.forEach(([label, value]) => {
                const term = document.createElement('dt');
                term.textContent = label;
                const definition = document.createElement('dd');
                definition.textContent = value;
                list.append(term, definition);
                sentence.push(`${label}: ${value}`);
            });
            parts.push(list);
            if (figures.rank > 0) {
                const rank = PFI18n.t('Research.Js.World.Rank', { rank: figures.rank, count: figures.count });
                parts.push(paragraph('research-world__detail-rank', rank));
                sentence.push(rank);
            }
        }
        detail.replaceChildren(...parts);
        return `${year}. ${sentence.join('. ')}`;
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
                lines.push(line(PFI18n.t('Research.Js.World.NoDataFor', { year }), 'research-tooltip__muted'));
            } else {
                const cap = document.createElement('span');
                cap.className = 'research-tooltip__line';
                cap.append(line(formatMoney(figures.marketCap), 'research-tooltip__value'), ' ',
                    PFI18n.t('Research.Js.World.TooltipMarketCap'));
                const share = document.createElement('span');
                share.className = 'research-tooltip__line';
                const noGdp = figures.percentOfGdp === null || figures.percentOfGdp === undefined;
                share.append(noGdp
                    ? line(PFI18n.t('Research.Js.World.NoGdp'), 'research-tooltip__muted')
                    : line(formatPercent(figures.percentOfGdp), 'research-tooltip__value'),
                noGdp ? '' : ` ${PFI18n.t('Research.Js.World.TooltipOfGdp')}`);
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
                return PFI18n.t('Research.Js.World.LegendUnder', { value: metric.stepLabels[0] });
            }
            if (i === STEPS - 1) {
                return PFI18n.t('Research.Js.World.LegendOrMore', { value: metric.stepLabels[i - 1] });
            }
            return PFI18n.t('Research.Js.Common.FromTo', { from: metric.stepLabels[i - 1], to: metric.stepLabels[i] });
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
        legend.setAttribute('aria-label', PFI18n.t(metric.legendTitleKey));
        legend.replaceChildren(
            ...labels.map((label, i) => item(label, 'research-world__swatch', colours.steps[i])),
            item(PFI18n.t('Research.Js.World.NoData'), 'research-world__swatch research-world__swatch--nodata', null)
        );
    }

    /**
     * Create the World choropleth in the host. It is drawn once, then moved between years by update() and between
     * metrics by setMetric().
     * @param {HTMLElement} host - The map host element.
     * @param {Object} data - The World snapshot.
     * @param {Object[]} features - The world-atlas country features, without Antarctica.
     * @param {Object} tooltip - The tooltip controller.
     * @param {function(string): void} onPick - Called with the country's name when the user selects one on the map.
     * @returns {{periods: string[], transitionMs: number, update: function(number, Object): string,
     *     table: function(number): Object, setMetric: function(string): string, resize: function(): void,
     *     selectCountry: function(string): boolean, clearCountry: function(): void,
     *     selectionText: function(): string}} The chart.
     */
    function createMap (host, data, features, tooltip, onPick) {
        const { years, names } = indexData(data);
        const legend = document.getElementById(LEGEND_ID);
        const subtitle = document.getElementById(SUBTITLE_ID);
        const detail = document.getElementById(DETAIL_ID);
        const detailHint = document.getElementById(DETAIL_HINT_ID);
        const countrySelect = document.getElementById(COUNTRY_SELECT_ID);
        let colours = readColours(host);
        let metricKey = 'marketCap';
        let periodIndex = data.periods.length - 1;
        // The selected country of the snapshot ({id, iso3, name}), or null, and its detail as a sentence.
        let selected = null;
        let selectedText = '';

        const svg = d3.select(host).append('svg').attr('role', 'img');
        const defs = svg.append('defs');
        const pattern = defs.append('pattern')
            .attr('id', NO_DATA_PATTERN_ID)
            .attr('patternUnits', 'userSpaceOnUse')
            .attr('width', 6)
            .attr('height', 6)
            .attr('patternTransform', 'rotate(45)');
        const patternGround = pattern.append('rect').attr('width', 6).attr('height', 6).attr('fill', colours.noData);
        const patternLine = pattern.append('line').attr('x1', 0).attr('y1', 0).attr('x2', 0).attr('y2', 6)
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
            .on('pointerleave', tooltip.hide)
            .on('click', (event, feature) => {
                const name = feature.id && names.get(feature.id);
                if (name && selectCountry(name)) {
                    onPick(name);
                }
            });

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
                return PFI18n.t(metric.noneKey, { year });
            }
            const largest = reporting.reduce((best, figures) =>
                (figures[metric.field] > best[metric.field] ? figures : best));
            return PFI18n.plural('Research.Js.World.Summary', reporting.length, {
                metric: PFI18n.t(metric.labelKey),
                year,
                name: largest.name,
                value: metric.format(largest[metric.field])
            });
        }

        /**
         * Show the summary on the page and on the map.
         * @returns {string} The summary.
         */
        function describe () {
            const text = summary();
            svg.attr('aria-label', PFI18n.t('Research.Js.World.MapLabel', { summary: text }));
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
            showDetail();
            return describe();
        }

        /**
         * Fill the country detail for the selected country and the year shown.
         * @returns {void}
         */
        function showDetail () {
            const figures = selected ? countryFigures(data, periodIndex, selected.id) : null;
            const text = renderDetail(detail, detailHint, selected, figures, data.periods[periodIndex]);
            selectedText = selected ? PFI18n.t('Research.Js.World.Selected', { name: selected.name, detail: text }) : '';
        }

        /**
         * Outline the selected country on the map and keep the side panel's list in step.
         * @returns {void}
         */
        function markSelected () {
            const id = selected ? selected.id : null;
            paths.classed('research-world__country--selected', (feature) => Boolean(id) && feature.id === id);
            // The outline is drawn last, so its neighbours do not cover it.
            paths.filter((feature) => Boolean(id) && feature.id === id).raise();
            if (countrySelect) {
                countrySelect.value = selected ? selected.name : '';
            }
        }

        /**
         * Select a country by name, whatever its case, and show its detail.
         * @param {string} name - The country's name, e.g. "Japan".
         * @returns {boolean} True when the snapshot has the country; otherwise nothing changes.
         */
        function selectCountry (name) {
            const country = findCountry(data, name);
            if (!country) {
                return false;
            }
            selected = country;
            markSelected();
            showDetail();
            return true;
        }

        /**
         * Clear the selection and hide the country detail.
         * @returns {void}
         */
        function clearCountry () {
            selected = null;
            markSelected();
            showDetail();
        }

        /**
         * The selected country and its detail as one sentence, for the live region.
         * @returns {string} The sentence, or '' when nothing is selected.
         */
        function selectionText () {
            return selectedText;
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
                value.percentOfGdp === null || value.percentOfGdp === undefined
                    ? PFI18n.t('Research.Js.World.NoData')
                    : formatPercent(value.percentOfGdp)
            ]);
            return {
                caption: PFI18n.t('Research.Js.World.TableCaption', { period: period.period }),
                columns: [
                    { label: PFI18n.t('Research.Js.World.Country'), numeric: false },
                    { label: PFI18n.t('Research.Js.World.MarketCap'), numeric: true },
                    { label: PFI18n.t('Research.Js.World.PercentOfGdp'), numeric: true }
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

        /**
         * Re-read the colours for the theme in effect and repaint the map, the hatch and the legend (#486).
         * @returns {void}
         */
        function restyle () {
            colours = readColours(host);
            patternGround.attr('fill', colours.noData);
            patternLine.attr('stroke', colours.noDataInk);
            paint(false);
            renderLegend(legend, METRICS[metricKey], colours);
        }

        layout();
        renderLegend(legend, METRICS[metricKey], colours);
        ChartPalette.subscribe(restyle);
        return {
            periods: data.periods,
            transitionMs: TRANSITION_MS,
            update,
            table,
            setMetric,
            resize,
            selectCountry,
            clearCountry,
            selectionText
        };
    }

    /**
     * Say a sentence in the page's live region.
     * @param {string} text - The sentence; nothing is said when empty.
     * @returns {void}
     */
    function announce (text) {
        const live = document.getElementById(LIVE_ID);
        if (live && text) {
            live.textContent = text;
        }
    }

    /**
     * On the World page (not the tabbed page's World tab), keep the address's ?country= in step with the selection,
     * replacing the current history entry, so a reload or a shared link opens the same country.
     * @param {HTMLElement|null} panel - The World panel.
     * @param {string|null} name - The selected country, or null for none.
     * @returns {void}
     */
    function rememberCountry (panel, name) {
        if (!panel || panel.getAttribute('role') === 'tabpanel' || !window.history ||
            typeof window.history.replaceState !== 'function') {
            return;
        }
        const url = new URL(window.location.href);
        if (name) {
            url.searchParams.set('country', name);
        } else {
            url.searchParams.delete('country');
        }
        window.history.replaceState(window.history.state, '', url);
    }

    /**
     * Wire the side panel's country list (the keyboard path; a click on the map is the other), and select the country
     * the panel asks for on load. A name the data does not have selects nothing and is taken out of the address.
     * @param {Object} chart - The chart from createMap().
     * @param {HTMLElement|null} panel - The World panel.
     * @param {function(string): void} onPick - Says the selection and puts it in the address.
     * @returns {void}
     */
    function initCountry (chart, panel, onPick) {
        const select = document.getElementById(COUNTRY_SELECT_ID);
        if (select) {
            select.addEventListener('change', () => {
                if (select.value && chart.selectCountry(select.value)) {
                    onPick(select.value);
                } else {
                    chart.clearCountry();
                    rememberCountry(panel, null);
                }
            });
        }
        const requested = panel ? panel.dataset.researchCountry : '';
        if (requested && !chart.selectCountry(requested)) {
            rememberCountry(panel, null);
        }
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
            message.textContent = PFI18n.t('Research.Js.World.MapFailed');
            host.replaceChildren(message);
            return;
        }
        const tooltip = createTooltip(document.getElementById(TOOLTIP_ID));
        const panel = document.getElementById(PANEL_ID);
        const chart = createMap(host, data, features, tooltip, onPick);
        /**
         * Say the user's selection and put it in the address.
         * @param {string} name - The selected country's name.
         * @returns {void}
         */
        function onPick (name) {
            announce(chart.selectionText());
            rememberCountry(panel, name);
        }
        initMetricSwitch(chart);
        page.registerTab(TAB_ID, chart);
        initCountry(chart, panel, onPick);
        window.ResearchWorld = { selectCountry: chart.selectCountry };

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

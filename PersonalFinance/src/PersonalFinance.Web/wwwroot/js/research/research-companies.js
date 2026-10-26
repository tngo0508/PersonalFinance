/* global PFI18n */
/**
 * Research Companies tab: look up a company registered with the SEC by ticker or name and show its Company Profile
 * (identity, annual figures as four D3 bar charts and a table, press releases, recent filings), for information only.
 * The press releases load from `data-releases-url` after the profile shows, so a slow or failed read never delays it.
 *
 * The search box is a WAI-ARIA combobox: suggestions come from the panel's `data-suggest-url` as the user types, and
 * choosing one (or pressing Enter) fetches the profile from `data-company-url`. The ticker is kept in the address
 * (`?tab=companies&ticker=MSFT`): each lookup adds a history entry, so Back returns to the previous company or to the
 * empty search. The charts repeat the table, which is their accessible equivalent; under prefers-reduced-motion they
 * change without a transition.
 *
 * Needs research-page.js (window.ResearchPage) and D3 loaded first.
 */
(function () {
    'use strict';

    const TRANSITION_MS = 750;
    const SUGGEST_DELAY_MS = 200;
    const TICKER_PATTERN = /^[A-Z0-9][A-Z0-9-]{0,9}$/;
    const MINUS = '−';
    /** What separates an 8-K's events in words, as ApiService joins them in a fallback headline. */
    const EVENT_SEPARATOR = ' · ';

    /** The four charts, drawn left to right, top to bottom; titleKey is the key of the chart's title. */
    const CHARTS = [
        { key: 'revenue', titleKey: 'Research.Js.Companies.Chart.Revenue', format: 'money' },
        { key: 'netIncome', titleKey: 'Research.Js.Companies.Chart.NetIncome', format: 'money' },
        { key: 'dilutedEps', titleKey: 'Research.Js.Companies.Chart.DilutedEps', format: 'eps' },
        { key: 'operatingCashFlow', titleKey: 'Research.Js.Companies.Chart.OperatingCashFlow', format: 'money' }
    ];

    /**
     * The table's rows: the reported figures, then the derived ones with their formula; labelKey and formulaKey are
     * the keys of the row's label and formula.
     */
    const ROWS = [
        { key: 'revenue', labelKey: 'Research.Js.Companies.Row.Revenue', format: 'millions' },
        { key: 'grossProfit', labelKey: 'Research.Js.Companies.Row.GrossProfit', format: 'millions' },
        { key: 'netIncome', labelKey: 'Research.Js.Companies.Row.NetIncome', format: 'millions' },
        { key: 'dilutedEps', labelKey: 'Research.Js.Companies.Row.DilutedEps', format: 'eps' },
        { key: 'operatingCashFlow', labelKey: 'Research.Js.Companies.Row.OperatingCashFlow', format: 'millions' },
        { key: 'totalAssets', labelKey: 'Research.Js.Companies.Row.TotalAssets', format: 'millions' },
        { key: 'totalLiabilities', labelKey: 'Research.Js.Companies.Row.TotalLiabilities', format: 'millions' },
        { key: 'equity', labelKey: 'Research.Js.Companies.Row.Equity', format: 'millions' },
        { key: 'sharesOutstanding', labelKey: 'Research.Js.Companies.Row.SharesOutstanding', format: 'millions' },
        { key: 'publicFloat', labelKey: 'Research.Js.Companies.Row.PublicFloat', format: 'millions' },
        {
            key: 'grossMargin',
            labelKey: 'Research.Js.Companies.Row.GrossMargin',
            format: 'percent',
            formulaKey: 'Research.Js.Companies.Formula.GrossMargin'
        },
        {
            key: 'netMargin',
            labelKey: 'Research.Js.Companies.Row.NetMargin',
            format: 'percent',
            formulaKey: 'Research.Js.Companies.Formula.NetMargin'
        },
        {
            key: 'revenueGrowth',
            labelKey: 'Research.Js.Companies.Row.RevenueGrowth',
            format: 'percent',
            formulaKey: 'Research.Js.Companies.Formula.RevenueGrowth'
        },
        {
            key: 'epsGrowth',
            labelKey: 'Research.Js.Companies.Row.EpsGrowth',
            format: 'percent',
            formulaKey: 'Research.Js.Companies.Formula.EpsGrowth'
        },
        {
            key: 'liabilitiesToEquity',
            labelKey: 'Research.Js.Companies.Row.LiabilitiesToEquity',
            format: 'ratio',
            formulaKey: 'Research.Js.Companies.Formula.LiabilitiesToEquity'
        }
    ];

    /**
     * Split a template on one placeholder, so an element can go where the translation puts it.
     * @param {string} key - The template's key.
     * @param {string} name - The placeholder's name, e.g. "date".
     * @param {Node} node - What goes in the placeholder's place.
     * @returns {Array<Node|string>} The parts, in order.
     */
    function around (key, name, node) {
        const [before, after] = PFI18n.t(key).split(`{${name}}`);
        return [before, node, after || ''].filter((part) => part !== '');
    }

    /**
     * Write a number with a true minus sign.
     * @param {string} text - The number as text, perhaps with a leading hyphen.
     * @returns {string} The text with the hyphen replaced by a minus sign.
     */
    function withMinus (text) {
        return text.replace(/^-/, MINUS);
    }

    /**
     * Format a figure for the table or a tooltip.
     * @param {number|null|undefined} value - The figure, or null when not reported.
     * @param {string} format - 'millions', 'eps', 'percent', 'ratio' or 'money'.
     * @returns {string} The formatted figure, or "Not reported".
     */
    function formatValue (value, format) {
        if (value === null || value === undefined) {
            return PFI18n.t('Research.Js.Companies.NotReported');
        }
        switch (format) {
        case 'millions': {
            const millions = value / 1e6;
            const digits = Math.abs(millions) >= 100 ? 0 : 1;
            return withMinus(PFI18n.formatNumber(millions, { minimumFractionDigits: digits, maximumFractionDigits: digits }));
        }
        case 'eps':
            return withMinus(PFI18n.formatNumber(value, { minimumFractionDigits: 2, maximumFractionDigits: 2 }));
        case 'percent':
            return `${withMinus(PFI18n.formatNumber(value * 100, { minimumFractionDigits: 1, maximumFractionDigits: 1 }))}%`;
        case 'ratio':
            return `${withMinus(PFI18n.formatNumber(value, { minimumFractionDigits: 2, maximumFractionDigits: 2 }))}×`;
        default:
            return withMinus(window.ResearchPage.formatMoney(value));
        }
    }

    /**
     * Format a chart value for its tooltip and axis: compact money, or EPS in dollars.
     * @param {number} value - The value.
     * @param {string} format - 'money' or 'eps'.
     * @returns {string} The formatted value.
     */
    function formatChartValue (value, format) {
        return format === 'eps' ? withMinus(PFI18n.formatMoney(value, 'USD')) : formatValue(value, 'money');
    }

    /**
     * Format an ISO date (yyyy-MM-dd) as "20 October 2026", in the page culture.
     * @param {string|null|undefined} iso - The date.
     * @returns {string} The date in words, or an empty string.
     */
    function formatDate (iso) {
        return /^\d{4}-\d{2}-\d{2}$/.test(iso || '') ? window.ResearchPage.formatDay(iso) : '';
    }

    /**
     * Normalise typed text as SEC lists tickers: upper case, a dot read as a dash.
     * @param {string} text - The typed text.
     * @returns {string|null} The ticker, or null when the text cannot be a ticker.
     */
    function toTicker (text) {
        const ticker = (text || '').trim().toUpperCase().replace(/\./g, '-');
        return TICKER_PATTERN.test(ticker) ? ticker : null;
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
     * Draw or update one bar chart of a figure by fiscal year. Bars are keyed by year, so moving between companies
     * animates the years they share.
     * @param {HTMLElement} figure - The chart's figure element, holding its svg.
     * @param {Array<Object>} years - The profile's fiscal years, oldest first.
     * @param {Object} chart - One of CHARTS.
     * @param {boolean} animate - Whether to transition.
     * @param {{show: Function, hide: Function}} tooltip - The tooltip controller.
     * @returns {void}
     */
    function drawChart (figure, years, chart, animate, tooltip) {
        const d3 = window.d3;
        const width = 320;
        const height = 170;
        const margin = { top: 10, right: 6, bottom: 22, left: 52 };
        const duration = animate ? TRANSITION_MS : 0;
        const points = years.map((y) => ({ year: String(y.fiscalYear), value: y[chart.key] }));
        const values = points.filter((p) => p.value !== null && p.value !== undefined).map((p) => p.value);

        const svg = d3.select(figure).select('svg')
            .attr('viewBox', `0 0 ${width} ${height}`)
            .attr('role', 'presentation');
        const x = d3.scaleBand()
            .domain(points.map((p) => p.year))
            .range([margin.left, width - margin.right])
            .padding(0.2);
        const y = d3.scaleLinear()
            .domain([Math.min(0, d3.min(values) ?? 0), Math.max(0, d3.max(values) ?? 0) || 1])
            .nice(3)
            .range([height - margin.bottom, margin.top]);

        svg.select('.research-companies__axis--y')
            .attr('transform', `translate(${margin.left},0)`)
            .transition().duration(duration)
            .call(d3.axisLeft(y).ticks(3).tickSizeOuter(0).tickFormat((v) => formatChartValue(v, chart.format)));
        svg.select('.research-companies__axis--x')
            .attr('transform', `translate(0,${height - margin.bottom})`)
            .transition().duration(duration)
            .call(d3.axisBottom(x).tickSizeOuter(0)
                .tickFormat((year) => (points.length > 6 ? `'${year.slice(2)}` : year)));
        svg.select('.research-companies__zero')
            .transition().duration(duration)
            .attr('x1', margin.left).attr('x2', width - margin.right)
            .attr('y1', y(0)).attr('y2', y(0));

        const reported = points.filter((p) => p.value !== null && p.value !== undefined);
        svg.select('.research-companies__bars')
            .selectAll('rect')
            .data(reported, (p) => p.year)
            .join(
                (enter) => enter.append('rect')
                    .attr('class', 'research-companies__bar')
                    .attr('x', (p) => x(p.year))
                    .attr('width', x.bandwidth())
                    .attr('y', y(0))
                    .attr('height', 0),
                (update) => update,
                (exit) => exit.transition().duration(duration).attr('y', y(0)).attr('height', 0).remove()
            )
            .classed('research-companies__bar--negative', (p) => p.value < 0)
            .on('pointerenter pointermove', (event, p) => tooltip.show(event, PFI18n.t(chart.titleKey), p, chart.format))
            .on('pointerleave', () => tooltip.hide())
            .transition().duration(duration)
            .attr('x', (p) => x(p.year))
            .attr('width', x.bandwidth())
            .attr('y', (p) => Math.min(y(p.value), y(0)))
            .attr('height', (p) => Math.abs(y(p.value) - y(0)));
    }

    /**
     * Build the annual figures table: one row per figure, one column per fiscal year, oldest on the left.
     * @param {Array<Object>} years - The fiscal years, oldest first.
     * @returns {HTMLTableElement} The table.
     */
    function buildTable (years) {
        const table = element('table', 'research-table research-companies__figures');
        table.append(element('caption', '', PFI18n.t('Research.Js.Companies.TableCaption')));
        const headRow = element('tr');
        const corner = element('th', '', PFI18n.t('Research.Js.Companies.Figure'));
        corner.scope = 'col';
        headRow.append(corner);
        years.forEach((year) => {
            const th = element('th', 'research-table__num', PFI18n.t('Research.Js.Companies.FiscalYear', { year: year.fiscalYear }));
            th.scope = 'col';
            headRow.append(th);
        });
        const head = element('thead');
        head.append(headRow);
        const body = element('tbody');
        ROWS.forEach((row) => {
            const tr = element('tr');
            const formula = row.formulaKey ? PFI18n.t(row.formulaKey) : null;
            const th = element('th', formula ? 'research-companies__derived' : '', PFI18n.t(row.labelKey));
            th.scope = 'row';
            if (formula) {
                th.title = formula;
                th.tabIndex = 0;
                th.append(element('span', 'research-companies__formula', formula));
            }
            tr.append(th);
            years.forEach((year) => {
                const value = year[row.key];
                const missing = value === null || value === undefined;
                // A reported figure that is missing was not reported; a derived one could not be worked out.
                tr.append(element('td', missing ? 'research-table__num research-companies__missing' : 'research-table__num',
                    missing && formula ? PFI18n.t('Research.Js.Companies.NotShown') : formatValue(value, row.format)));
            });
            body.append(tr);
        });
        table.append(head, body);
        return table;
    }

    /**
     * Build one filing's list item: form, dates, 8-K events and a link to its EDGAR index page.
     * @param {Object} filing - The filing.
     * @returns {HTMLLIElement} The list item.
     */
    function buildFiling (filing) {
        const item = element('li', 'research-companies__filing');
        const heading = element('p', 'research-companies__filing-head');
        heading.append(element('strong', 'research-companies__form', filing.form), ' ',
            PFI18n.t('Research.Js.Companies.FiledOn', { date: formatDate(filing.filedOn) }));
        if (filing.reportedOn) {
            heading.append(', ', PFI18n.t('Research.Js.Companies.ForDate', { date: formatDate(filing.reportedOn) }));
        }
        item.append(heading);
        if (filing.events && filing.events.length > 0) {
            item.append(element('p', 'research-companies__events', filing.events.join(EVENT_SEPARATOR)));
        }
        const link = element('a', 'research-companies__filing-link', PFI18n.t('Research.Js.Companies.ViewOnEdgar'));
        link.href = filing.url;
        link.target = '_blank';
        link.rel = 'noopener noreferrer';
        link.append(element('span', 'visually-hidden',
            ` ${PFI18n.t('Research.Js.Companies.FilingLinkHint', { form: filing.form, date: formatDate(filing.filedOn) })}`));
        item.append(link);
        return item;
    }

    /**
     * Build one press release's list item: its headline as a link to the release on sec.gov, the filing date in
     * words and the 8-K's events. A headline that fell back to the events shows in the same place, unmarked.
     * @param {Object} release - The press release.
     * @returns {HTMLLIElement} The list item.
     */
    function buildRelease (release) {
        const item = element('li', 'research-companies__release');
        const heading = element('p', 'research-companies__release-head');
        const link = element('a', 'research-companies__release-link', release.headline);
        link.href = release.url;
        link.target = '_blank';
        link.rel = 'noopener noreferrer';
        link.append(element('span', 'visually-hidden', ` ${PFI18n.t('Research.Js.Companies.ReleaseLinkHint')}`));
        heading.append(link);
        item.append(heading);
        const filed = element('p', 'research-companies__release-date');
        const time = element('time', '', formatDate(release.filedOn));
        time.dateTime = release.filedOn;
        filed.append(...around('Research.Js.Companies.FiledDate', 'date', time));
        item.append(filed);
        if (release.events && release.events.length > 0) {
            item.append(element('p', 'research-companies__events', release.events.join(EVENT_SEPARATOR)));
        }
        return item;
    }

    /**
     * Wire the Companies tab panel.
     * @param {HTMLElement} panel - The tab panel.
     * @returns {void}
     */
    function initCompanies (panel) {
        const page = window.ResearchPage;
        const form = panel.querySelector('[data-research-company-form]');
        const input = panel.querySelector('#research-company-input');
        const listbox = panel.querySelector('#research-company-suggestions');
        const message = panel.querySelector('[data-research-company-message]');
        const loading = panel.querySelector('[data-research-company-loading]');
        const profileHost = panel.querySelector('[data-research-company-profile]');
        const chartsHost = panel.querySelector('[data-research-company-charts]');
        const tableHost = panel.querySelector('[data-research-company-table]');
        const filingsHost = panel.querySelector('[data-research-company-filings]');
        if (!page || !form || !input || !listbox || !message || !loading || !profileHost || !chartsHost ||
            !tableHost || !filingsHost) {
            return;
        }

        const suggestUrl = panel.dataset.suggestUrl;
        const companyUrl = panel.dataset.companyUrl;
        const releasesUrl = panel.dataset.releasesUrl;
        const releases = {
            section: panel.querySelector('[data-research-company-releases]'),
            loading: panel.querySelector('[data-research-releases-loading]'),
            list: panel.querySelector('[data-research-releases-list]'),
            empty: panel.querySelector('[data-research-releases-empty]'),
            unavailable: panel.querySelector('[data-research-releases-unavailable]'),
            retry: panel.querySelector('[data-research-releases-retry]')
        };
        const hasReleasesSection = Boolean(releasesUrl) && Object.values(releases).every(Boolean);
        const field = (name) => panel.querySelector(`[data-field="${name}"]`);
        const tooltip = page.createTooltip(document.getElementById('research-company-tooltip'),
            (line, title, point, format) => [
                line(PFI18n.t('Research.Js.Companies.TooltipTitle', { title, year: point.year }), 'research-tooltip__name'),
                line(formatChartValue(point.value, format), 'research-tooltip__value')
            ]);

        let suggestions = [];
        let activeIndex = -1;
        let suggestTimer = null;
        let suggestRequest = null;
        let lookupRequest = null;
        let releasesRequest = null;
        // The ticker whose press releases were announced since its lookup, so a Retry does not announce them again.
        let releasesAnnouncedFor = null;
        let currentTicker = null;

        // ----- Suggestions (WAI-ARIA combobox with a listbox popup) -----

        /**
         * Close the suggestions.
         * @returns {void}
         */
        function closeSuggestions () {
            listbox.hidden = true;
            listbox.replaceChildren();
            input.setAttribute('aria-expanded', 'false');
            input.removeAttribute('aria-activedescendant');
            suggestions = [];
            activeIndex = -1;
        }

        /**
         * Mark one suggestion active, for arrow-key navigation.
         * @param {number} index - The suggestion's index, or -1 for none.
         * @returns {void}
         */
        function setActive (index) {
            activeIndex = index;
            Array.from(listbox.children).forEach((option, i) => {
                option.setAttribute('aria-selected', String(i === index));
            });
            if (index >= 0 && listbox.children[index]) {
                input.setAttribute('aria-activedescendant', listbox.children[index].id);
                listbox.children[index].scrollIntoView({ block: 'nearest' });
            } else {
                input.removeAttribute('aria-activedescendant');
            }
        }

        /**
         * Show suggestions under the search box.
         * @param {Array<Object>} items - The suggestions.
         * @returns {void}
         */
        function showSuggestions (items) {
            suggestions = items;
            activeIndex = -1;
            if (items.length === 0) {
                closeSuggestions();
                return;
            }
            listbox.replaceChildren(...items.map((item, index) => {
                const option = element('li', 'research-companies__option');
                option.id = `research-company-option-${index}`;
                option.setAttribute('role', 'option');
                option.setAttribute('aria-selected', 'false');
                option.append(element('span', 'research-companies__option-ticker', item.ticker),
                    element('span', 'research-companies__option-name', item.name));
                const meta = item.isFund ? PFI18n.t('Research.Js.Common.Fund') : (item.exchange || '');
                if (meta) {
                    option.append(element('span', 'research-companies__option-meta', meta));
                }
                // Keep focus in the box: choose on pointerdown's click without blurring the input first.
                option.addEventListener('mousedown', (event) => event.preventDefault());
                option.addEventListener('click', () => choose(item));
                return option;
            }));
            listbox.hidden = false;
            input.setAttribute('aria-expanded', 'true');
            input.removeAttribute('aria-activedescendant');
        }

        /**
         * Fetch suggestions for the box's text, cancelling any earlier request.
         * @returns {Promise<void>} Resolves when shown.
         */
        async function suggest () {
            const query = input.value.trim();
            if (suggestRequest) {
                suggestRequest.abort();
            }
            if (!query) {
                closeSuggestions();
                return;
            }
            suggestRequest = new AbortController();
            try {
                const response = await fetch(`${suggestUrl}?q=${encodeURIComponent(query)}`, {
                    headers: { Accept: 'application/json' },
                    signal: suggestRequest.signal
                });
                if (!response.ok) {
                    closeSuggestions();
                    return;
                }
                const items = await response.json();
                if (input.value.trim() === query && document.activeElement === input) {
                    showSuggestions(Array.isArray(items) ? items : []);
                }
            } catch (error) {
                if (error.name !== 'AbortError') {
                    closeSuggestions();
                }
            }
        }

        /**
         * Choose a suggestion: put its ticker in the box and look it up.
         * @param {Object} item - The suggestion.
         * @returns {void}
         */
        function choose (item) {
            input.value = item.ticker;
            closeSuggestions();
            lookUp(item.ticker, { push: true });
        }

        // ----- Messages in place of a profile -----

        /**
         * Show a message in place of the profile.
         * @param {Array<Node|string>} parts - The message's content.
         * @param {string} [modifier] - A class modifier, e.g. 'error'.
         * @returns {void}
         */
        function showMessage (parts, modifier) {
            message.className = `research-companies__message${modifier ? ` research-companies__message--${modifier}` : ''}`;
            message.replaceChildren(...parts);
            message.hidden = false;
        }

        /**
         * Show the Unavailable State with a Retry that repeats the lookup.
         * @param {string} ticker - The ticker that could not be loaded.
         * @returns {void}
         */
        function showUnavailable (ticker) {
            const state = element('section', 'unavailable-state');
            state.setAttribute('role', 'status');
            const body = element('div', 'unavailable-state__body');
            body.append(element('h4', 'unavailable-state__title', PFI18n.t('Research.Js.Companies.Unavailable', { ticker })),
                element('p', 'unavailable-state__text', PFI18n.t('Research.Js.Companies.UnavailableText')));
            const retry = element('button', 'btn btn-primary btn-sm', PFI18n.t('Research.Js.Common.Retry'));
            retry.type = 'button';
            retry.addEventListener('click', () => lookUp(ticker, { push: false }));
            state.append(body, retry);
            showMessage([state]);
        }

        // ----- The profile -----

        /**
         * Fill the profile region from a Company Profile.
         * @param {Object} profile - The profile.
         * @returns {void}
         */
        function renderProfile (profile) {
            const set = (name, text) => {
                const node = field(name);
                if (node) {
                    node.textContent = text;
                }
            };
            set('name', profile.name);
            set('ticker', profile.ticker);
            const notListed = PFI18n.t('Research.Js.Companies.NotListed');
            set('exchange', profile.exchange || notListed);
            set('cik', String(profile.cik));
            set('industry', profile.sicDescription
                ? `${profile.sicDescription}${profile.sicCode
                    ? ` ${PFI18n.t('Research.Js.Companies.SicCode', { code: profile.sicCode })}`
                    : ''}`
                : notListed);
            set('office', profile.industryOffice || notListed);
            set('incorporated', profile.stateOfIncorporation || notListed);
            set('fiscal-year-end', profile.fiscalYearEnd || notListed);
            set('as-of', profile.dataAsOf ? formatDate(profile.dataAsOf) : PFI18n.t('Research.Js.Companies.NotAvailable'));
            const ifrsNote = field('ifrs-note');
            if (ifrsNote) {
                ifrsNote.hidden = !profile.reportsUnderIfrs;
            }
            const allFilings = field('all-filings');
            if (allFilings && profile.allFilingsUrl) {
                allFilings.href = profile.allFilingsUrl;
            }

            const years = profile.fiscalYears || [];
            set('years-note', years.length > 0
                ? PFI18n.plural('Research.Js.Companies.YearsNote', years.length)
                : PFI18n.t('Research.Js.Companies.NoYears'));
            chartsHost.hidden = years.length === 0;
            tableHost.hidden = years.length === 0;
            if (years.length > 0) {
                // Bars grow in, and move between companies, unless the user asked for less motion.
                const animate = !page.prefersReducedMotion();
                if (!chartsHost.children.length) {
                    CHARTS.forEach((chart) => {
                        const figure = element('figure', 'research-companies__chart');
                        figure.dataset.chart = chart.key;
                        figure.append(element('figcaption', 'research-companies__chart-title', PFI18n.t(chart.titleKey)));
                        const svg = window.d3.select(figure).append('svg');
                        svg.append('g').attr('class', 'research-companies__bars');
                        svg.append('line').attr('class', 'research-companies__zero');
                        svg.append('g').attr('class', 'research-companies__axis research-companies__axis--y');
                        svg.append('g').attr('class', 'research-companies__axis research-companies__axis--x');
                        chartsHost.append(figure);
                    });
                }
                CHARTS.forEach((chart) => {
                    drawChart(chartsHost.querySelector(`[data-chart="${chart.key}"]`), years, chart, animate, tooltip);
                });
                tableHost.replaceChildren(buildTable(years));
            } else {
                tableHost.replaceChildren();
            }

            filingsHost.replaceChildren(...(profile.filings || []).map(buildFiling));
            if (!profile.filings || profile.filings.length === 0) {
                filingsHost.append(element('li', 'research-companies__filing', PFI18n.t('Research.Js.Companies.NoFilings')));
            }
            profileHost.hidden = false;
        }

        // ----- Press releases, loaded after the profile -----

        /**
         * Cancel a press releases request still on its way, so an older company's list never lands on a newer one.
         * @returns {void}
         */
        function abortReleases () {
            if (releasesRequest) {
                releasesRequest.abort();
                releasesRequest = null;
            }
        }

        /**
         * Leave the press releases section off the profile: ApiService has them switched off.
         * @returns {void}
         */
        function hideReleases () {
            if (releases.section) {
                releases.section.hidden = true;
            }
        }

        /**
         * Show one state of the press releases section and hide the others.
         * @param {'loading'|'list'|'empty'|'unavailable'} state - The state to show.
         * @returns {void}
         */
        function showReleasesState (state) {
            releases.loading.hidden = state !== 'loading';
            releases.list.hidden = state !== 'list';
            releases.empty.hidden = state !== 'empty';
            releases.unavailable.hidden = state !== 'unavailable';
        }

        /**
         * Fetch and show a company's press releases. A failure shows the section's Unavailable State with a Retry,
         * never an error page; a 404 means ApiService switched them off since the lookup, so the section is hidden.
         * The live region announces the count once per lookup, not again after a Retry.
         * @param {string} ticker - The ticker of the profile shown.
         * @param {string} name - The company's name, for the announcement.
         * @returns {Promise<void>} Resolves when shown.
         */
        async function loadReleases (ticker, name) {
            if (!hasReleasesSection) {
                return;
            }
            abortReleases();
            const request = new AbortController();
            releasesRequest = request;
            releases.section.hidden = false;
            releases.list.replaceChildren();
            showReleasesState('loading');
            releases.retry.onclick = () => loadReleases(ticker, name);
            try {
                const response = await fetch(`${releasesUrl}?ticker=${encodeURIComponent(ticker)}`, {
                    headers: { Accept: 'application/json' },
                    signal: request.signal
                });
                if (response.status === 404) {
                    hideReleases();
                    return;
                }
                if (!response.ok) {
                    showReleasesState('unavailable');
                    return;
                }
                const result = await response.json();
                if (request.signal.aborted) {
                    return;
                }
                const items = Array.isArray(result.releases) ? result.releases : [];
                releases.list.replaceChildren(...items.map(buildRelease));
                showReleasesState(items.length > 0 ? 'list' : 'empty');
                if (releasesAnnouncedFor !== ticker) {
                    releasesAnnouncedFor = ticker;
                    page.announce(PFI18n.plural('Research.Js.Companies.ReleasesLoaded', items.length, { name }));
                }
            } catch (error) {
                if (error.name !== 'AbortError') {
                    showReleasesState('unavailable');
                }
            } finally {
                if (releasesRequest === request) {
                    releasesRequest = null;
                }
            }
        }

        /**
         * Show the empty search: no profile, no message.
         * @returns {void}
         */
        function clearProfile () {
            if (lookupRequest) {
                lookupRequest.abort();
            }
            abortReleases();
            currentTicker = null;
            profileHost.hidden = true;
            message.hidden = true;
            loading.hidden = true;
            input.value = '';
            const asOf = field('as-of');
            if (asOf) {
                asOf.textContent = PFI18n.t('Research.Js.Companies.AsOfPrompt');
            }
        }

        /**
         * Keep the ticker in the address: a new history entry for a new lookup, so Back returns to the previous one.
         * @param {string} ticker - The ticker.
         * @returns {void}
         */
        function rememberTicker (ticker) {
            const url = new URL(window.location.href);
            if (url.searchParams.get('ticker') === ticker && url.searchParams.get('tab') === 'companies') {
                return;
            }
            url.searchParams.set('tab', 'companies');
            url.searchParams.set('ticker', ticker);
            window.history.pushState({ researchTicker: ticker }, '', url);
        }

        /**
         * Look up a ticker and show its profile, or a message saying why there is none.
         * @param {string} ticker - The ticker, as typed or chosen.
         * @param {{push: boolean}} options - Whether to add a history entry.
         * @returns {Promise<void>} Resolves when shown.
         */
        async function lookUp (ticker, options) {
            const normalized = toTicker(ticker);
            if (!normalized) {
                showMessage([PFI18n.t('Research.Js.Companies.InvalidTicker')], 'error');
                return;
            }
            if (lookupRequest) {
                lookupRequest.abort();
            }
            abortReleases();
            releasesAnnouncedFor = null;
            lookupRequest = new AbortController();
            if (options.push) {
                rememberTicker(normalized);
            }
            currentTicker = normalized;
            message.hidden = true;
            loading.hidden = false;
            try {
                const response = await fetch(`${companyUrl}?ticker=${encodeURIComponent(normalized)}`, {
                    headers: { Accept: 'application/json' },
                    signal: lookupRequest.signal
                });
                loading.hidden = true;
                if (response.status === 429) {
                    showMessage([PFI18n.t('Research.Js.Companies.TooMany')], 'error');
                    page.announce(PFI18n.t('Research.Js.Companies.TooMany'));
                    return;
                }
                if (response.status === 400) {
                    showMessage([PFI18n.t('Research.Js.Companies.InvalidTicker')], 'error');
                    return;
                }
                if (!response.ok) {
                    showUnavailable(normalized);
                    page.announce(PFI18n.t('Research.Js.Companies.UnavailableAnnounce', { ticker: normalized }));
                    return;
                }
                const result = await response.json();
                if (result.kind === 'profile' && result.profile) {
                    renderProfile(result.profile);
                    page.announce(result.profile.dataAsOf
                        ? PFI18n.t('Research.Js.Companies.LoadedAsOf',
                            { name: result.profile.name, date: formatDate(result.profile.dataAsOf) })
                        : PFI18n.t('Research.Js.Companies.Loaded', { name: result.profile.name }));
                    if (result.pressReleasesAvailable) {
                        loadReleases(normalized, result.profile.name);
                    } else {
                        hideReleases();
                    }
                    return;
                }
                profileHost.hidden = true;
                if (result.kind === 'fund') {
                    const link = element('a', '', PFI18n.t('Research.Js.Companies.SeeFunds'));
                    link.href = '?tab=funds';
                    link.addEventListener('click', (event) => {
                        event.preventDefault();
                        page.selectTabById('funds');
                    });
                    const [before, after] = PFI18n.t('Research.Js.Companies.IsFund', { ticker: normalized }).split('{link}');
                    showMessage([before, link, after || ''].filter((part) => part !== ''));
                    page.announce(PFI18n.t('Research.Js.Companies.IsFundAnnounce', { ticker: normalized }));
                } else {
                    showMessage([PFI18n.t('Research.Js.Companies.NotSec', { ticker: normalized })]);
                    page.announce(PFI18n.t('Research.Js.Companies.NotSecAnnounce', { ticker: normalized }));
                }
            } catch (error) {
                if (error.name === 'AbortError') {
                    return;
                }
                loading.hidden = true;
                showUnavailable(normalized);
            }
        }

        // ----- Events -----

        input.addEventListener('input', () => {
            clearTimeout(suggestTimer);
            suggestTimer = setTimeout(suggest, SUGGEST_DELAY_MS);
        });

        input.addEventListener('keydown', (event) => {
            const open = !listbox.hidden && suggestions.length > 0;
            if (event.key === 'ArrowDown' && open) {
                event.preventDefault();
                setActive((activeIndex + 1) % suggestions.length);
            } else if (event.key === 'ArrowUp' && open) {
                event.preventDefault();
                setActive(activeIndex <= 0 ? suggestions.length - 1 : activeIndex - 1);
            } else if (event.key === 'Escape' && open) {
                event.preventDefault();
                closeSuggestions();
            } else if (event.key === 'Enter' && open && activeIndex >= 0) {
                event.preventDefault();
                choose(suggestions[activeIndex]);
            }
        });

        input.addEventListener('blur', () => {
            // Let a click on a suggestion land first.
            setTimeout(closeSuggestions, 150);
        });

        form.addEventListener('submit', (event) => {
            event.preventDefault();
            clearTimeout(suggestTimer);
            const text = input.value.trim();
            if (activeIndex >= 0 && suggestions[activeIndex]) {
                choose(suggestions[activeIndex]);
                return;
            }
            // A name that is not a ticker takes the first suggestion, which is the best match.
            if (!toTicker(text) && suggestions.length > 0) {
                choose(suggestions[0]);
                return;
            }
            closeSuggestions();
            lookUp(text, { push: true });
        });

        window.addEventListener('popstate', () => {
            const url = new URL(window.location.href);
            const tab = url.searchParams.get('tab');
            if (tab) {
                page.selectTabById(tab);
            }
            const ticker = toTicker(url.searchParams.get('ticker') || '');
            if (tab === 'companies' && ticker) {
                input.value = ticker;
                lookUp(ticker, { push: false });
            } else if (tab === 'companies' || !tab) {
                clearProfile();
            }
        });

        document.addEventListener('research:tabchange', (event) => {
            if (event.detail && event.detail.tabId === 'companies' && currentTicker &&
                typeof window.history.replaceState === 'function') {
                const url = new URL(window.location.href);
                url.searchParams.set('ticker', currentTicker);
                window.history.replaceState(window.history.state, '', url);
            }
        });

        const initial = toTicker(panel.dataset.initialTicker || '');
        if (initial) {
            lookUp(initial, { push: false });
        }
    }

    /**
     * Wire the Companies tab, if the page has one.
     * @returns {void}
     */
    function init () {
        const panel = document.querySelector('[data-research-companies]');
        if (panel && window.d3) {
            initCompanies(panel);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

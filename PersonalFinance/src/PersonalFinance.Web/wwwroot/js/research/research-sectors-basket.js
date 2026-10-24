/**
 * Research page, Sectors tab: the "Market | Your basket" switch and the basket view's data.
 *
 * The basket view draws the Fund Basket in the Sectors sunburst and treemap (js/research/research-sunburst.js), with
 * the same breadcrumb, keyboard paths and cards. Each fund's shares (from the fund industries snapshot, every holding
 * of its latest Form N-PORT report) are weighted by the fund's share of the basket. The hierarchy is SEC industry
 * office, SIC industry, then the ten largest companies of each industry and "Other"; then "Not classified by the SEC"
 * (foreign companies that file no reports with the SEC, hatched grey), issuer country and issuer; then Bonds by issuer
 * type; then Cash and other. Every figure is a share of the basket, counting stocks, bonds and cash.
 *
 * The switch opens on Your basket only when a basket is set at load, and the market view is left as it was. Reads the
 * `research-fund-industries-data` island and the basket from `window.ResearchBasket`.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const DATA_ID = 'research-fund-industries-data';
    const PANEL_ID = 'research-panel-sectors';
    const VIEWS_ID = 'research-sectors-views';
    const NOTE_ID = 'research-sectors-basket-note';
    const TITLE_ID = 'research-sectors-title';
    const TAB_ID = 'sectors';
    const TOP_COMPANIES = 10;
    const TOP_ISSUERS = 10;
    const TOP_COUNTRIES = 3;
    const TABLE_ROWS = 25;
    const ROOT_NAME = 'Your basket';
    const UNCLASSIFIED = 'Not classified by the SEC';
    const BONDS = 'Bonds';
    const CASH = 'Cash and other';
    const TITLES = { market: 'Largest SEC filers by Public float', basket: 'Your basket by SEC industry' };
    const regionNames = typeof Intl !== 'undefined' && Intl.DisplayNames
        ? new Intl.DisplayNames(['en'], { type: 'region' })
        : null;

    /**
     * A share of the basket for display: "<0.1%" under 0.05%, else one decimal.
     * @param {number} value - The share in percent.
     * @returns {string} E.g. "12.3%" or "<0.1%".
     */
    function formatShare (value) {
        return value > 0 && value < 0.05 ? '<0.1%' : `${value.toFixed(1)}%`;
    }

    // How the basket's figures read: every one a share of the basket.
    const MEASURE = {
        format: formatShare,
        unit: 'of your basket',
        describe: (value) => `${formatShare(value)} of your basket`,
        relative: false
    };

    /**
     * A country's name from its two-letter code.
     * @param {string|null} code - The ISO alpha-2 code, or null when the fund gives none.
     * @returns {string} The English name, or the code.
     */
    function countryName (code) {
        if (!code) {
            return 'Country not reported';
        }
        try {
            return (regionNames && regionNames.of(code)) || code;
        } catch (error) {
            return code;
        }
    }

    /**
     * What one issuer's name matches across funds: letters and digits only, lower case.
     * @param {string} name - The issuer's name.
     * @returns {string} The key.
     */
    function issuerKey (name) {
        return name.toLowerCase().replace(/[^a-z0-9]/g, '');
    }

    /**
     * Add a value to a map entry, creating it from make() when missing.
     * @param {Map} map - The map.
     * @param {*} key - The key.
     * @param {function(): Object} make - Makes a new entry.
     * @returns {Object} The entry.
     */
    function entryOf (map, key, make) {
        if (!map.has(key)) {
            map.set(key, make());
        }
        return map.get(key);
    }

    /**
     * The top of a group's members as leaves, with the rest summed into one "Other" leaf. Members' values are shares of
     * the basket.
     * @param {{value: number, count: number, members: Map}} group - The group: its total, how many members the funds
     *     hold at most, and the members listed ({ name, ticker?, value } by key).
     * @param {number} top - How many members to keep.
     * @param {function(Object, *): Object} leaf - Makes a member's node data.
     * @param {Object} other - The "Other" node's key, note, noun and flags.
     * @returns {Object[]} The children.
     */
    function leavesOf (group, top, leaf, other) {
        const members = Array.from(group.members.entries())
            .filter(([, m]) => m.value > 0)
            .sort((a, b) => b[1].value - a[1].value);
        const kept = members.slice(0, top);
        const children = kept.map(([key, m]) => leaf(m, key));
        const rest = group.value - kept.reduce((sum, [, m]) => sum + m.value, 0);
        // How many more the funds hold: a fund lists only its own largest, so this counts at least that many.
        const more = Math.max(group.count, members.length) - kept.length;
        if (rest > 0.00005) {
            const noun = more === 1 ? other.noun : `${other.noun.replace(/y$/, 'ie')}s`;
            children.push({
                key: other.key,
                name: more > 0 ? `Other (${more.toLocaleString('en-US')} ${noun})` : `Other ${other.noun.replace(/y$/, 'ie')}s`,
                note: other.note,
                office: other.office,
                technology: other.technology,
                hatched: other.hatched,
                kind: 'other',
                value: rest
            });
        }
        return children;
    }

    /**
     * The basket's totals: each fund's shares weighted by its share of the basket. A basket fund with no industries
     * data is left out and the others' shares are scaled up to cover it.
     * @param {Object} data - The fund industries snapshot.
     * @param {Object[]} basket - The basket's funds (ticker, share).
     * @returns {Object} Industries, unclassified countries, bonds and cash, the funds left out, and how many have data.
     */
    function basketTotals (data, basket) {
        const byTicker = new Map(data.funds.map((f) => [f.ticker, f]));
        const funds = basket.filter((f) => byTicker.has(f.ticker));
        const missing = basket.filter((f) => !byTicker.has(f.ticker)).map((f) => f.ticker);
        const total = funds.reduce((sum, f) => sum + f.share, 0) || 1;
        const industries = new Map();
        const countries = new Map();
        const bonds = new Map();
        let cash = 0;
        funds.forEach((fund) => {
            const weight = fund.share / total;
            const holdings = byTicker.get(fund.ticker);
            holdings.industries.forEach((i) => {
                const industry = entryOf(industries, i.industry, () => ({ value: 0, count: 0, members: new Map() }));
                industry.value += weight * i.percent;
                industry.count = Math.max(industry.count, i.companyCount);
            });
            holdings.companies.forEach((c) => {
                const company = data.companies[c.company];
                const industry = entryOf(industries, company.industry, () => ({ value: 0, count: 0, members: new Map() }));
                const member = entryOf(industry.members, c.company, () => ({ name: company.name, ticker: company.ticker, value: 0 }));
                member.value += weight * c.percent;
            });
            (holdings.unclassified || []).forEach((u) => {
                const country = entryOf(countries, u.country || '', () => ({ code: u.country, value: 0, count: 0, members: new Map() }));
                country.value += weight * u.percent;
                country.count = Math.max(country.count, u.issuerCount);
                u.issuers.forEach((issuer) => {
                    const member = entryOf(country.members, issuerKey(issuer.name), () => ({ name: issuer.name, value: 0 }));
                    member.value += weight * issuer.percent;
                });
            });
            (holdings.bonds || []).forEach((b) => bonds.set(b.name, (bonds.get(b.name) || 0) + weight * b.percent));
            cash += weight * (holdings.cashAndOther || 0);
        });
        return { industries, countries, bonds, cash, missing, count: funds.length };
    }

    /**
     * The basket's tree for the sunburst: offices in SEC office order, then the unclassified stocks, bonds and cash.
     * Keys never collide with the market's, so switching views starts from the top.
     * @param {Object} data - The fund industries snapshot.
     * @param {Object} totals - The basket's totals.
     * @returns {Object} The root node data.
     */
    function buildTree (data, totals) {
        const offices = new Map();
        totals.industries.forEach((group, index) => {
            const industry = data.industries[index];
            const officeName = data.offices[industry.office];
            const technology = industry.technology === true;
            const office = entryOf(offices, industry.office, () => ({
                key: `bo${industry.office}`, name: officeName, office: officeName, kind: 'office', children: []
            }));
            office.children.push({
                key: `bi${index}`,
                name: industry.name,
                office: officeName,
                kind: 'industry',
                technology,
                children: leavesOf(group, TOP_COMPANIES, (company, key) => ({
                    key: `bc${key}`,
                    name: company.name,
                    ticker: company.ticker,
                    office: officeName,
                    kind: 'company',
                    technology,
                    value: company.value
                }), {
                    key: `bx${index}`, noun: 'company', note: `Smaller holdings in ${industry.name}`, office: officeName, technology
                })
            });
        });
        const children = Array.from(offices.keys()).sort((a, b) => a - b).map((index) => offices.get(index));

        if (totals.countries.size > 0) {
            children.push({
                key: 'bu',
                name: UNCLASSIFIED,
                office: UNCLASSIFIED,
                kind: 'unclassified',
                hatched: true,
                children: Array.from(totals.countries.values()).map((country) => {
                    const code = country.code || 'none';
                    return {
                        key: `bu-${code}`,
                        name: countryName(country.code),
                        office: UNCLASSIFIED,
                        kind: 'country',
                        hatched: true,
                        children: leavesOf(country, TOP_ISSUERS, (issuer, key) => ({
                            key: `bu-${code}-${key}`, name: issuer.name, office: UNCLASSIFIED, kind: 'issuer', hatched: true, value: issuer.value
                        }), {
                            key: `bu-${code}-x`, noun: 'company', note: `Smaller holdings in ${countryName(country.code)}`, office: UNCLASSIFIED, hatched: true
                        })
                    };
                })
            });
        }
        if (totals.bonds.size > 0) {
            children.push({
                key: 'bb',
                name: BONDS,
                office: BONDS,
                kind: 'bonds',
                children: Array.from(totals.bonds.entries()).map(([name, value], index) => ({
                    key: `bb${index}`, name, office: BONDS, kind: 'bondType', value
                }))
            });
        }
        if (totals.cash > 0) {
            children.push({ key: 'bcash', name: CASH, office: CASH, kind: 'cash', value: totals.cash });
        }
        return { key: 'broot', name: ROOT_NAME, kind: 'root', children };
    }

    /**
     * The share in stocks SEC does not classify and its largest countries, as one sentence.
     * @param {Object|undefined} node - The "Not classified by the SEC" node, if the basket has one.
     * @returns {string} E.g. "Largest countries: Netherlands 0.3%, United Kingdom 0.2%."
     */
    function topCountriesText (node) {
        const countries = (node && node.children ? node.children : []).slice().sort((a, b) => b.value - a.value)
            .slice(0, TOP_COUNTRIES);
        return countries.length
            ? `Largest countries: ${countries.map((c) => `${c.data.name} ${formatShare(c.value)}`).join(', ')}.`
            : '';
    }

    /**
     * The basket view's takeaway cards. At the top: the share classified by the SEC, the unclassified share with its
     * largest countries, the largest office and the largest company. Zoomed in: the group's share, its largest part,
     * and its largest company; on or inside the unclassified stocks, the card explaining them (on them, in place of the
     * share).
     * @param {Object} root - The d3 hierarchy root of the basket.
     * @param {Object} focus - The focused d3 hierarchy node.
     * @returns {{label: string, value: string, note: string}[]} The cards.
     */
    function takeaways (root, focus) {
        const unclassified = (root.children || []).find((d) => d.data.kind === 'unclassified');
        const unclassifiedCard = unclassified
            ? {
                label: UNCLASSIFIED,
                value: formatShare(unclassified.value),
                note: `of your basket. Foreign companies that file no reports with the SEC have no SEC industry. ${topCountriesText(unclassified)}`
            }
            : null;
        const largestCompany = (node) => node.leaves().filter((d) => d.data.kind === 'company')
            .sort((a, b) => b.value - a.value)[0];

        if (focus.depth === 0) {
            const classified = (root.children || []).filter((d) => d.data.kind === 'office').reduce((s, d) => s + d.value, 0);
            const cards = [{
                label: 'Classified by the SEC',
                value: formatShare(classified),
                note: 'of your basket is in companies with an SEC industry office and SIC industry.'
            }];
            if (unclassifiedCard) {
                cards.push(unclassifiedCard);
            }
            const office = (root.children || []).filter((d) => d.data.kind === 'office').sort((a, b) => b.value - a.value)[0];
            if (office) {
                cards.push({ label: 'Largest SEC industry office', value: formatShare(office.value), note: `of your basket: ${office.data.name}` });
            }
            const company = largestCompany(root);
            if (company) {
                const ticker = company.data.ticker ? ` (${company.data.ticker})` : '';
                cards.push({ label: 'Largest company', value: formatShare(company.value), note: `of your basket: ${company.data.name}${ticker}` });
            }
            return cards;
        }

        // Zoomed into the unclassified stocks themselves, their card gives the share, so it leads in place of the share.
        const cards = focus === unclassified
            ? [unclassifiedCard]
            : [{ label: 'Share of your basket', value: formatShare(focus.value || 0), note: focus.data.name }];
        const part = (focus.children || []).filter((d) => d.data.kind !== 'other').sort((a, b) => b.value - a.value)[0];
        if (part) {
            cards.push({ label: 'Largest part', value: formatShare(part.value), note: `of your basket: ${part.data.name}` });
        }
        const company = largestCompany(focus);
        if (company && company !== part) {
            cards.push({ label: 'Largest company', value: formatShare(company.value), note: `of your basket: ${company.data.name}` });
        }
        if (unclassifiedCard && focus !== unclassified && focus.ancestors().includes(unclassified)) {
            cards.push(unclassifiedCard);
        }
        return cards;
    }

    /**
     * The basket's largest companies and issuers, as the table view shows them.
     * @param {Object} root - The d3 hierarchy root of the basket.
     * @returns {{caption: string, columns: Object[], rows: string[][]}} The table.
     */
    function table (root) {
        const rows = root.leaves()
            .filter((d) => d.data.kind === 'company' || d.data.kind === 'issuer')
            .sort((a, b) => b.value - a.value)
            .slice(0, TABLE_ROWS)
            .map((d, rank) => {
                const issuer = d.data.kind === 'issuer';
                return [
                    String(rank + 1),
                    d.data.ticker ? `${d.data.name} (${d.data.ticker})` : d.data.name,
                    issuer ? UNCLASSIFIED : d.parent.data.name,
                    issuer ? d.parent.data.name : d.data.office,
                    formatShare(d.value)
                ];
            });
        return {
            caption: `The ${rows.length} largest companies in your basket`,
            columns: [
                { label: 'Rank', numeric: true },
                { label: 'Company' },
                { label: 'SIC industry' },
                { label: 'SEC industry office or country' },
                { label: 'Share of your basket', numeric: true }
            ],
            rows
        };
    }

    /**
     * The chart's label, the card's subtitle and the live summary for the basket.
     * @param {Object} data - The fund industries snapshot.
     * @param {Object} root - The d3 hierarchy root of the basket.
     * @param {Object} focus - The focused d3 hierarchy node.
     * @param {string} trail - Where the chart is, e.g. "Your basket › Technology".
     * @returns {{aria: string, subtitle: string, summary: string}} The wording.
     */
    function describe (data, root, focus, trail) {
        const offices = (root.children || []).filter((d) => d.data.kind === 'office');
        const classified = offices.reduce((sum, d) => sum + d.value, 0);
        const unclassified = (root.children || []).find((d) => d.data.kind === 'unclassified');
        const rest = unclassified ? `; ${formatShare(unclassified.value)} is not classified by the SEC` : '';
        const where = focus.depth > 0 ? ` Showing ${trail}: ${formatShare(focus.value || 0)} of your basket.` : '';
        const summary = `Your basket: ${formatShare(classified)} in ${offices.length} SEC industry offices${rest}.${where}`;
        const asOf = new Date(`${data.asOf}T00:00:00`).toLocaleDateString('en-US', { day: 'numeric', month: 'long', year: 'numeric' });
        return {
            aria: `Sunburst of your basket by SEC industry office and SIC industry. ${summary}`,
            subtitle: `Your basket by SEC industry office, SIC industry and company, from each fund's holdings as of ${asOf} ` +
                'or earlier. Every percent is of your basket. Foreign companies the SEC does not classify are grouped by country.',
            summary
        };
    }

    /**
     * Wire the switch and the basket view to the drawn sunburst.
     * @param {Object} data - The fund industries snapshot.
     * @param {Object} chart - The sunburst (window.ResearchSunburst).
     * @returns {void}
     */
    function createView (data, chart) {
        const panel = document.getElementById(PANEL_ID);
        const views = document.getElementById(VIEWS_ID);
        const note = document.getElementById(NOTE_ID);
        const title = document.getElementById(TITLE_ID);
        const buttons = Array.from(views.querySelectorAll('[data-sectors-view]'));
        let mode = 'market';
        let totals = basketTotals(data, []);

        const currentBasket = () => (window.ResearchBasket ? window.ResearchBasket.get() : []);
        const hierarchy = () => window.d3.hierarchy(buildTree(data, totals)).sum((d) => (d.children ? 0 : d.value || 0));
        const provider = {
            measure: MEASURE,
            tree: () => buildTree(data, totals),
            takeaways,
            table: () => table(hierarchy()),
            describe: (root, focus, trail) => describe(data, root, focus, trail)
        };

        /**
         * Recompute the basket's totals, enable the switch's basket button and say which funds are left out.
         * @returns {void}
         */
        function readBasket () {
            const basket = currentBasket();
            totals = basketTotals(data, basket);
            buttons.find((b) => b.dataset.sectorsView === 'basket').disabled = totals.count === 0;
            note.textContent = totals.missing.length && totals.count > 0
                ? `${totals.missing.join(', ')} ${totals.missing.length === 1 ? 'has' : 'have'} no holdings report yet, so the others' shares are scaled up to cover ${totals.missing.length === 1 ? 'it' : 'them'}.`
                : '';
            note.hidden = note.textContent === '';
        }

        /**
         * Show one view: the chart's source, the card title, and the parts that belong to one view only.
         * @param {string} next - "market" or "basket".
         * @returns {void}
         */
        function setMode (next) {
            mode = next === 'basket' && totals.count > 0 ? 'basket' : 'market';
            buttons.forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.sectorsView === mode)));
            panel.querySelectorAll('[data-sectors-view-part]').forEach((part) => {
                part.hidden = part.dataset.sectorsViewPart !== mode;
            });
            if (title) {
                title.textContent = TITLES[mode];
            }
            chart.setSource(mode, provider);
            if (page.refreshTable) {
                page.refreshTable(TAB_ID);
            }
        }

        buttons.forEach((button) => button.addEventListener('click', () => {
            if (button.disabled || button.dataset.sectorsView === mode) {
                return;
            }
            setMode(button.dataset.sectorsView);
            if (page.announce) {
                page.announce(mode === 'basket'
                    ? 'Showing your basket by SEC industry. Every percent is of your basket.'
                    : 'Showing the largest SEC filers by Public float.');
            }
        }));
        document.addEventListener('research:basketchange', () => {
            readBasket();
            if (mode === 'basket' && totals.count === 0) {
                setMode('market');
            } else if (mode === 'basket') {
                chart.refresh();
                if (page.refreshTable) {
                    page.refreshTable(TAB_ID);
                }
            }
        });

        readBasket();
        setMode(currentBasket().length > 0 && totals.count > 0 ? 'basket' : 'market');
    }

    /**
     * Wire the view when its data, the sunburst and the page helpers are present.
     * @returns {void}
     */
    function init () {
        const chart = window.ResearchSunburst;
        if (!page || !chart || !document.getElementById(VIEWS_ID) || typeof window.d3 === 'undefined') {
            return;
        }
        const data = page.readData(DATA_ID, 'Research fund industries data');
        if (!data || !Array.isArray(data.funds) || data.funds.length === 0) {
            return;
        }
        createView(data, chart);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

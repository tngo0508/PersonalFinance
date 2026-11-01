/* global PFI18n */
/**
 * Research Sectors (the Sectors page, and the tabbed Research page's Sectors tab): the "Market | Your basket" switch
 * and the basket view's data.
 *
 * The basket view draws the Fund Basket in the Sectors treemap (js/research/research-sectors.js), with the same
 * breadcrumb, keyboard paths and cards. Each fund's shares (from the fund industries snapshot, every holding
 * of its latest Form N-PORT report) are weighted by the fund's share of the basket. The hierarchy is SEC industry
 * office, SIC industry, then the ten largest companies of each industry and "Other"; then "Not classified by the SEC"
 * (companies with no SEC industry, mostly foreign ones that file no reports with the SEC, hatched grey), issuer country
 * and issuer; then Bonds by issuer
 * type; then Cash and other. Every figure is a share of the basket, counting stocks, bonds and cash.
 *
 * Under the chart, "Lean vs the market" compares each SEC office's share of the SEC-classified part of the basket with
 * its share of every SEC filer's Public float in the year the scrubber shows (the one place a share is not of the
 * whole basket, and it says so), with the difference in points: blue and "+" where the basket leans more, orange and
 * "−" where it leans less. It describes the basket; it is not a target. A row whose office the basket holds zooms the
 * chart into it.
 *
 * On the Sectors page the panel's data-research-sectors-view says which view it opens on: Your basket only when it is
 * "basket" (the address's ?view=basket) and a basket is set; a press on the switch keeps the address's view in step.
 * On the tabbed page, which has no such attribute, the switch opens on Your basket whenever a basket is set at load.
 * The market view is left as it was. Reads the
 * `research-sectors-data` island, the basket from `window.ResearchBasket`, and fetches the fund industries snapshot from
 * the URL on the switch (`data-industries-url`) the first time the basket view is needed.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const AS_OF_ID = 'research-sectors-holdings-as-of';
    const SECTORS_DATA_ID = 'research-sectors-data';
    const LEAN_TITLE_ID = 'research-lean-title';
    const LEAN_NOTE_ID = 'research-lean-note';
    const LEAN_ROWS_ID = 'research-lean-rows';
    // A difference smaller than this (in points) reads as level, with no arrow or colour.
    const LEVEL_POINTS = 0.05;
    const INTERNATIONAL_OFFICE = 'International Corp Fin';
    const PANEL_ID = 'research-panel-sectors';
    const VIEWS_ID = 'research-sectors-views';
    const NOTE_ID = 'research-sectors-basket-note';
    const TITLE_ID = 'research-sectors-title';
    const TAB_ID = 'sectors';
    // On the Basket page the switch does not write ?view= to the address, which is the Basket page's.
    const FIXED_ADDRESS_ATTRIBUTE = 'data-research-fixed-address';
    const TOP_COMPANIES = 10;
    const TOP_ISSUERS = 10;
    const TOP_COUNTRIES = 3;
    const TABLE_ROWS = 25;
    const TITLE_KEYS = { market: 'Research.Js.Sectors.Title', basket: 'Research.Js.SectorsBasket.Title' };
    const regionNames = typeof Intl !== 'undefined' && Intl.DisplayNames
        ? new Intl.DisplayNames([PFI18n.lang], { type: 'region' })
        : null;

    /**
     * The basket tree's own group names, in the page language: the root, the stocks SEC does not classify, bonds, and
     * cash.
     * @returns {{root: string, unclassified: string, bonds: string, cash: string}} The names.
     */
    function names () {
        return {
            root: PFI18n.t('Research.Js.Common.YourBasket'),
            unclassified: PFI18n.t('Research.Js.SectorsBasket.Unclassified'),
            bonds: PFI18n.t('Research.Js.SectorsBasket.Bonds'),
            cash: PFI18n.t('Research.Js.SectorsBasket.Cash')
        };
    }

    /**
     * A share of the basket for display: "<0.1%" under 0.05%, else one decimal.
     * @param {number} value - The share in percent.
     * @returns {string} E.g. "12.3%" or "<0.1%", in the page culture.
     */
    function formatShare (value) {
        return value > 0 && value < 0.05 ? `<${page.formatPercent(0.1, 1)}` : page.formatPercent(value, 1);
    }

    /**
     * A share followed by "of your basket".
     * @param {number} value - The share in percent.
     * @returns {string} E.g. "12.3% of your basket".
     */
    function ofBasket (value) {
        return PFI18n.t('Research.Js.WorldBasket.OfBasket', { value: formatShare(value) });
    }

    // How the basket's figures read: every one a share of the basket.
    const MEASURE = {
        format: formatShare,
        get unit () {
            return PFI18n.t('Research.Js.SectorsBasket.Unit');
        },
        describe: ofBasket,
        relative: false
    };

    /**
     * A country's name from its two-letter code.
     * @param {string|null} code - The ISO alpha-2 code, or null when the fund gives none.
     * @returns {string} The name in the page language, or the code.
     */
    function countryName (code) {
        if (!code) {
            return PFI18n.t('Research.Js.SectorsBasket.CountryNotReported');
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
     * @param {Object} other - The "Other" node's key, note and flags.
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
            children.push({
                key: other.key,
                name: more > 0
                    ? PFI18n.plural('Research.Js.SectorsBasket.OtherCompanies', more)
                    : PFI18n.t('Research.Js.SectorsBasket.OtherCompaniesNone'),
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
     * The basket's tree for the treemap: offices in SEC office order, then the unclassified stocks, bonds and cash.
     * Keys never collide with the market's, so switching views starts from the top.
     * @param {Object} data - The fund industries snapshot.
     * @param {Object} totals - The basket's totals.
     * @returns {Object} The root node data.
     */
    function buildTree (data, totals) {
        const groups = names();
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
                    key: `bx${index}`,
                    note: PFI18n.t('Research.Js.SectorsBasket.SmallerIn', { name: industry.name }),
                    office: officeName,
                    technology
                })
            });
        });
        const children = Array.from(offices.keys()).sort((a, b) => a - b).map((index) => offices.get(index));

        if (totals.countries.size > 0) {
            children.push({
                key: 'bu',
                name: groups.unclassified,
                office: groups.unclassified,
                kind: 'unclassified',
                hatched: true,
                children: Array.from(totals.countries.values()).map((country) => {
                    const code = country.code || 'none';
                    return {
                        key: `bu-${code}`,
                        name: countryName(country.code),
                        office: groups.unclassified,
                        kind: 'country',
                        hatched: true,
                        children: leavesOf(country, TOP_ISSUERS, (issuer, key) => ({
                            key: `bu-${code}-${key}`, name: issuer.name, office: groups.unclassified, kind: 'issuer', hatched: true, value: issuer.value
                        }), {
                            key: `bu-${code}-x`,
                            note: PFI18n.t('Research.Js.SectorsBasket.SmallerIn', { name: countryName(country.code) }),
                            office: groups.unclassified,
                            hatched: true
                        })
                    };
                })
            });
        }
        if (totals.bonds.size > 0) {
            children.push({
                key: 'bb',
                name: groups.bonds,
                office: groups.bonds,
                kind: 'bonds',
                children: Array.from(totals.bonds.entries()).map(([name, value], index) => ({
                    key: `bb${index}`, name, office: groups.bonds, kind: 'bondType', value
                }))
            });
        }
        if (totals.cash > 0) {
            children.push({ key: 'bcash', name: groups.cash, office: groups.cash, kind: 'cash', value: totals.cash });
        }
        return { key: 'broot', name: groups.root, kind: 'root', children };
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
            ? PFI18n.t('Research.Js.SectorsBasket.TopCountries',
                { countries: countries.map((c) => `${c.data.name} ${formatShare(c.value)}`).join(', ') })
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
                label: names().unclassified,
                value: formatShare(unclassified.value),
                note: `${PFI18n.t('Research.Js.SectorsBasket.UnclassifiedNote')} ${topCountriesText(unclassified)}`
            }
            : null;
        const ofBasketNote = (name) => PFI18n.t('Research.Js.SectorsBasket.OfBasketNote', { name });
        const largestCompany = (node) => node.leaves().filter((d) => d.data.kind === 'company')
            .sort((a, b) => b.value - a.value)[0];

        if (focus.depth === 0) {
            const classified = (root.children || []).filter((d) => d.data.kind === 'office').reduce((s, d) => s + d.value, 0);
            const cards = [{
                label: PFI18n.t('Research.Js.SectorsBasket.Classified'),
                value: formatShare(classified),
                note: PFI18n.t('Research.Js.SectorsBasket.ClassifiedNote')
            }];
            if (unclassifiedCard) {
                cards.push(unclassifiedCard);
            }
            const office = (root.children || []).filter((d) => d.data.kind === 'office').sort((a, b) => b.value - a.value)[0];
            if (office) {
                cards.push({
                    label: PFI18n.t('Research.Js.SectorsBasket.LargestOffice'),
                    value: formatShare(office.value),
                    note: ofBasketNote(office.data.name)
                });
            }
            const company = largestCompany(root);
            if (company) {
                const ticker = company.data.ticker ? ` (${company.data.ticker})` : '';
                cards.push({
                    label: PFI18n.t('Research.Js.SectorsBasket.LargestCompany'),
                    value: formatShare(company.value),
                    note: ofBasketNote(`${company.data.name}${ticker}`)
                });
            }
            return cards;
        }

        // Zoomed into the unclassified stocks themselves, their card gives the share, so it leads in place of the share.
        const cards = focus === unclassified
            ? [unclassifiedCard]
            : [{ label: PFI18n.t('Research.Js.Common.ShareOfBasket'), value: formatShare(focus.value || 0), note: focus.data.name }];
        const part = (focus.children || []).filter((d) => d.data.kind !== 'other').sort((a, b) => b.value - a.value)[0];
        if (part) {
            cards.push({
                label: PFI18n.t('Research.Js.Sectors.LargestPart'),
                value: formatShare(part.value),
                note: ofBasketNote(part.data.name)
            });
        }
        const company = largestCompany(focus);
        if (company && company !== part) {
            cards.push({
                label: PFI18n.t('Research.Js.SectorsBasket.LargestCompany'),
                value: formatShare(company.value),
                note: ofBasketNote(company.data.name)
            });
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
                    PFI18n.formatNumber(rank + 1),
                    d.data.ticker ? `${d.data.name} (${d.data.ticker})` : d.data.name,
                    issuer ? names().unclassified : d.parent.data.name,
                    issuer ? d.parent.data.name : d.data.office,
                    formatShare(d.value)
                ];
            });
        return {
            caption: PFI18n.plural('Research.Js.SectorsBasket.TableCaption', rows.length),
            columns: [
                { label: PFI18n.t('Research.Js.Sectors.Rank'), numeric: true },
                { label: PFI18n.t('Research.Js.Sectors.Company') },
                { label: PFI18n.t('Research.Js.Sectors.Industry') },
                { label: PFI18n.t('Research.Js.SectorsBasket.OfficeOrCountry') },
                { label: PFI18n.t('Research.Js.Common.ShareOfBasket'), numeric: true }
            ],
            rows
        };
    }

    /**
     * The card's subtitle and the live summary for the basket.
     * @param {Object} data - The fund industries snapshot.
     * @param {Object} root - The d3 hierarchy root of the basket.
     * @param {Object} focus - The focused d3 hierarchy node.
     * @param {string} trail - Where the chart is, e.g. "Your basket › Technology".
     * @returns {{subtitle: string, summary: string}} The wording.
     */
    function describe (data, root, focus, trail) {
        const offices = (root.children || []).filter((d) => d.data.kind === 'office');
        const classified = offices.reduce((sum, d) => sum + d.value, 0);
        const unclassified = (root.children || []).find((d) => d.data.kind === 'unclassified');
        const figures = {
            value: formatShare(classified),
            offices: PFI18n.plural('Research.Js.SectorsBasket.Offices', offices.length),
            rest: unclassified ? formatShare(unclassified.value) : ''
        };
        const sentences = [PFI18n.t(unclassified ? 'Research.Js.SectorsBasket.SummaryWithRest' : 'Research.Js.SectorsBasket.Summary',
            figures)];
        if (focus.depth > 0) {
            sentences.push(PFI18n.t('Research.Js.SectorsBasket.SummaryWhere', { trail, value: formatShare(focus.value || 0) }));
        }
        const summary = sentences.join(' ');
        return {
            subtitle: PFI18n.t('Research.Js.SectorsBasket.Subtitle', { date: page.formatDay(data.asOf) }),
            summary
        };
    }

    /**
     * A difference in points for display, always signed: "+3.2 pts", "−1.0 pts", or "0.0 pts" when level.
     * @param {number} points - The difference in percentage points.
     * @returns {string} The difference.
     */
    function formatPoints (points) {
        const value = Math.abs(points) < LEVEL_POINTS
            ? page.formatFixed(0, 1)
            : `${points > 0 ? '+' : '−'}${page.formatFixed(Math.abs(points), 1)}`;
        return PFI18n.t('Research.Js.SectorsBasket.Points', { value });
    }

    /**
     * Each SEC office's share of every filer's Public float in one year of the Sectors snapshot: its ranked filers plus
     * its industries' "Other" slices, over the year's total.
     * @param {Object} sectors - The Sectors snapshot.
     * @param {Object} period - One entry of sectors.values.
     * @returns {Map<string, number>} The share in percent by office name.
     */
    function marketShares (sectors, period) {
        const byOffice = new Map();
        const add = (industryIndex, value) => {
            const office = sectors.offices[sectors.industries[industryIndex].office];
            byOffice.set(office, (byOffice.get(office) || 0) + value);
        };
        period.ranked.forEach((row) => add(sectors.companies[row.company].industry, row.publicFloat));
        (period.other || []).forEach((row) => add(row.industry, row.publicFloat));
        const total = Array.from(byOffice.values()).reduce((sum, v) => sum + v, 0) || 1;
        return new Map(Array.from(byOffice.entries()).map(([office, value]) => [office, value / total * 100]));
    }

    /**
     * The lean rows: each office the basket or the market has, in SEC office order (offices only one side numbers
     * last), with the basket's share of its SEC-classified part, the market's share, and the difference.
     * @param {Object} data - The fund industries snapshot.
     * @param {Object} totals - The basket's totals.
     * @param {Map<string, number>} market - The market's shares by office.
     * @param {string[]} officeOrder - The Sectors snapshot's offices, in SEC office order.
     * @returns {{rows: Object[], classified: number}} The rows, and the SEC-classified share of the basket.
     */
    function leanRows (data, totals, market, officeOrder) {
        const basket = new Map();
        totals.industries.forEach((group, index) => {
            const office = data.offices[data.industries[index].office];
            basket.set(office, (basket.get(office) || 0) + group.value);
        });
        const classified = Array.from(basket.values()).reduce((sum, v) => sum + v, 0);
        const order = (office) => {
            const position = officeOrder.indexOf(office);
            return position >= 0 ? position : officeOrder.length + data.offices.indexOf(office);
        };
        const offices = Array.from(new Set([...basket.keys(), ...market.keys()])).sort((a, b) => order(a) - order(b));
        const rows = offices.map((office) => {
            const own = classified > 0 ? (basket.get(office) || 0) / classified * 100 : 0;
            const theirs = market.get(office) || 0;
            return { office, basket: own, market: theirs, difference: own - theirs, officeIndex: data.offices.indexOf(office) };
        });
        return { rows, classified };
    }

    /**
     * Draw the lean section for one year and return a sentence for the live region.
     * @param {Object} elements - The section's title, note and list elements.
     * @param {{rows: Object[], classified: number}} lean - The rows and the SEC-classified share of the basket.
     * @param {string} year - The year the market shares are for.
     * @param {function(string): void} onPick - Called with an office's node key to zoom the chart into it.
     * @returns {string} The largest leans, e.g. "Lean vs the market in 2025: Technology +12.0 pts, Finance −8.1 pts."
     */
    function renderLean (elements, lean, year, onPick) {
        const { title, note, list } = elements;
        title.textContent = PFI18n.t('Research.Js.SectorsBasket.LeanTitle', { year });
        if (lean.classified <= 0) {
            note.textContent = PFI18n.t('Research.Js.SectorsBasket.LeanNone');
            list.replaceChildren();
            return '';
        }
        const sentences = [PFI18n.t('Research.Js.SectorsBasket.LeanNote', { value: formatShare(lean.classified), year })];
        if (lean.rows.some((r) => r.office === INTERNATIONAL_OFFICE && r.basket > 0)) {
            sentences.push(PFI18n.t('Research.Js.SectorsBasket.LeanForeign', { office: INTERNATIONAL_OFFICE }));
        }
        note.textContent = sentences.join(' ');
        const widest = Math.max(...lean.rows.map((r) => Math.abs(r.difference)), LEVEL_POINTS);
        list.replaceChildren(...lean.rows.map((row) => {
            const item = document.createElement('li');
            item.className = 'research-lean__item';
            const zoomable = row.basket > 0 && row.officeIndex >= 0;
            const line = document.createElement(zoomable ? 'button' : 'div');
            line.className = 'research-lean__row';
            const direction = Math.abs(row.difference) < LEVEL_POINTS ? 'level' : (row.difference > 0 ? 'more' : 'less');
            const words = PFI18n.t({
                more: 'Research.Js.SectorsBasket.LeanMore',
                less: 'Research.Js.SectorsBasket.LeanLess',
                level: 'Research.Js.SectorsBasket.LeanLevel'
            }[direction]);
            const label = PFI18n.t('Research.Js.SectorsBasket.LeanRowLabel', {
                office: row.office,
                basket: formatShare(row.basket),
                market: formatShare(row.market),
                year,
                points: formatPoints(row.difference),
                words
            });
            if (zoomable) {
                line.type = 'button';
                line.setAttribute('aria-label', PFI18n.t('Research.Js.SectorsBasket.LeanZoom', { label, office: row.office }));
                line.addEventListener('click', () => onPick(`bo${row.officeIndex}`));
            } else {
                // Not a button, but still reachable by keyboard so its figures can be read.
                line.tabIndex = 0;
                line.setAttribute('role', 'group');
                line.setAttribute('aria-label', label);
            }
            const name = document.createElement('span');
            name.className = 'research-lean__office';
            name.textContent = row.office;
            const own = document.createElement('span');
            own.className = 'research-lean__value';
            own.textContent = formatShare(row.basket);
            const theirs = document.createElement('span');
            theirs.className = 'research-lean__value';
            theirs.textContent = formatShare(row.market);
            const difference = document.createElement('span');
            difference.className = `research-lean__difference research-lean__difference--${direction}`;
            const arrow = document.createElement('span');
            arrow.setAttribute('aria-hidden', 'true');
            arrow.textContent = { more: '▲ ', less: '▼ ', level: '' }[direction];
            const text = document.createElement('span');
            text.append(arrow, formatPoints(row.difference));
            // One axis for every row, inside the Difference column: zero in the middle, more right, less left.
            const bar = document.createElement('span');
            bar.className = 'research-lean__bar';
            bar.setAttribute('aria-hidden', 'true');
            const fill = document.createElement('span');
            fill.className = `research-lean__fill research-lean__fill--${direction}`;
            fill.style.width = `${Math.abs(row.difference) / widest * 50}%`;
            bar.append(fill);
            difference.append(text, bar);
            line.append(name, own, theirs, difference);
            item.append(line);
            return item;
        }));
        const sorted = lean.rows.slice().sort((a, b) => b.difference - a.difference);
        const most = sorted[0];
        const least = sorted[sorted.length - 1];
        const parts = [most, least].filter((r, i, all) => r && Math.abs(r.difference) >= LEVEL_POINTS && all.indexOf(r) === i)
            .map((r) => `${r.office} ${formatPoints(r.difference)}`);
        return parts.length
            ? PFI18n.t('Research.Js.SectorsBasket.LeanSummary', { year, parts: parts.join(', ') })
            : PFI18n.t('Research.Js.SectorsBasket.LeanLevelSummary', { year });
    }

    /**
     * Wire the switch and the basket view to the drawn treemap. The fund industries snapshot is not in the page: it is
     * fetched the first time the basket view is needed (a basket set at load, or the "Your basket" button), so a visit
     * that never opens it never downloads it.
     * @param {string} url - Where to fetch the fund industries snapshot (the Web app's JSON action).
     * @param {Object} sectors - The Sectors snapshot, for the market's shares in the lean section.
     * @param {Object} chart - The treemap (window.ResearchSectors).
     * @returns {void}
     */
    function createView (url, sectors, chart) {
        const panel = document.getElementById(PANEL_ID);
        const views = document.getElementById(VIEWS_ID);
        const note = document.getElementById(NOTE_ID);
        const title = document.getElementById(TITLE_ID);
        const asOfElement = document.getElementById(AS_OF_ID);
        const leanElements = {
            title: document.getElementById(LEAN_TITLE_ID),
            note: document.getElementById(LEAN_NOTE_ID),
            list: document.getElementById(LEAN_ROWS_ID)
        };
        const buttons = Array.from(views.querySelectorAll('[data-sectors-view]'));
        const basketButton = buttons.find((b) => b.dataset.sectorsView === 'basket');
        let mode = 'market';
        // The view the Sectors page asked for ('market' or 'basket'); undefined on the tabbed page.
        const requested = panel.dataset.researchSectorsView;
        // The snapshot once fetched, and the fetch while it runs (so it is made once).
        let data = null;
        let loading = null;
        let totals = { count: 0, missing: [] };

        const currentBasket = () => (window.ResearchBasket ? window.ResearchBasket.get() : []);
        const hierarchy = () => window.d3.hierarchy(buildTree(data, totals)).sum((d) => (d.children ? 0 : d.value || 0));
        const provider = {
            measure: MEASURE,
            tree: () => buildTree(data, totals),
            takeaways,
            table: () => table(hierarchy()),
            describe: (root, focus, trail) => describe(data, root, focus, trail),
            // The scrubber's year reaches the basket here: the chart stays, the lean section follows.
            period: (index) => {
                const period = sectors.values[Math.max(0, Math.min(sectors.values.length - 1, index))];
                if (!leanElements.list) {
                    return '';
                }
                const lean = leanRows(data, totals, marketShares(sectors, period), sectors.offices);
                return renderLean(leanElements, lean, period.period, (key) => chart.zoomTo(key));
            }
        };

        /**
         * Show a note under the card's subtitle, or hide it when empty.
         * @param {string} text - The note.
         * @returns {void}
         */
        function showNote (text) {
            note.textContent = text;
            note.hidden = text === '';
        }

        /**
         * Fetch the fund industries snapshot once. Resolves to whether it is there to draw; a failure says so in the
         * note and leaves the market view.
         * @returns {Promise<boolean>} True when the snapshot is loaded.
         */
        function ensureLoaded () {
            if (data) {
                return Promise.resolve(true);
            }
            if (!loading) {
                loading = fetchSnapshot();
            }
            return loading;
        }

        /**
         * Fetch the fund industries snapshot and keep it; a failure says so in the note.
         * @returns {Promise<boolean>} True when the snapshot is loaded.
         */
        async function fetchSnapshot () {
            basketButton.setAttribute('aria-busy', 'true');
            let snapshot = null;
            try {
                const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
                snapshot = response.ok ? await response.json() : null;
            } catch (error) {
                snapshot = null;
            }
            basketButton.removeAttribute('aria-busy');
            loading = null;
            if (!snapshot || !Array.isArray(snapshot.funds) || snapshot.funds.length === 0) {
                showNote(PFI18n.t('Research.Js.SectorsBasket.LoadFailed'));
                return false;
            }
            data = snapshot;
            if (asOfElement) {
                asOfElement.textContent = PFI18n.t('Research.Js.SectorsBasket.HoldingsAsOf', { dates: holdingsDates(data) });
            }
            readBasket();
            return true;
        }

        /**
         * Recompute the basket's totals (once the snapshot is here), enable the switch's basket button and say which
         * funds are left out. Before the snapshot is fetched, the button is enabled whenever a basket is set.
         * @returns {void}
         */
        function readBasket () {
            const basket = currentBasket();
            if (!data) {
                basketButton.disabled = basket.length === 0;
                return;
            }
            totals = basketTotals(data, basket);
            basketButton.disabled = totals.count === 0;
            showNote(totals.missing.length && totals.count > 0
                ? PFI18n.plural('Research.Js.Common.MissingReport', totals.missing.length, { funds: totals.missing.join(', ') })
                : '');
        }

        /**
         * Show one view: the chart's source, the card title, and the parts that belong to one view only.
         * @param {string} next - "market" or "basket".
         * @returns {void}
         */
        function setMode (next) {
            mode = next === 'basket' && data && totals.count > 0 ? 'basket' : 'market';
            buttons.forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.sectorsView === mode)));
            panel.querySelectorAll('[data-sectors-view-part]').forEach((part) => {
                part.hidden = part.dataset.sectorsViewPart !== mode;
            });
            if (title) {
                title.textContent = PFI18n.t(TITLE_KEYS[mode]);
            }
            chart.setSource(mode, provider);
            if (page.refreshTable) {
                page.refreshTable(TAB_ID);
            }
        }

        /**
         * Open the basket view, fetching the snapshot first if need be.
         * @param {boolean} announce - Whether to tell screen readers.
         * @returns {Promise<void>}
         */
        async function openBasket (announce) {
            if (!await ensureLoaded()) {
                return;
            }
            setMode('basket');
            if (announce && page.announce) {
                page.announce(PFI18n.t(mode === 'basket'
                    ? 'Research.Js.SectorsBasket.ShowingBasket'
                    : 'Research.Js.SectorsBasket.NoReports'));
            }
        }

        /**
         * On the Sectors page, keep the address's view in step with the switch (?view=basket, or none for the
         * market), replacing the current history entry, so a reload or a shared link opens the same view.
         * @returns {void}
         */
        function rememberView () {
            if (requested === undefined || panel.hasAttribute(FIXED_ADDRESS_ATTRIBUTE) || !window.history || typeof window.history.replaceState !== 'function') {
                return;
            }
            const url = new URL(window.location.href);
            if (mode === 'basket') {
                url.searchParams.set('view', 'basket');
            } else {
                url.searchParams.delete('view');
            }
            window.history.replaceState(window.history.state, '', url);
        }

        buttons.forEach((button) => button.addEventListener('click', async () => {
            if (button.disabled || button.dataset.sectorsView === mode) {
                return;
            }
            if (button.dataset.sectorsView === 'basket') {
                await openBasket(true);
                rememberView();
                return;
            }
            setMode('market');
            rememberView();
            if (page.announce) {
                page.announce(PFI18n.t('Research.Js.SectorsBasket.ShowingMarket'));
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
        if (currentBasket().length > 0 && (requested === undefined || requested === 'basket')) {
            openBasket(false);
        }
    }

    /**
     * The fund holdings' report dates for display: one date, or the earliest to the latest.
     * @param {Object} data - The fund industries snapshot.
     * @returns {string} E.g. "30 June 2026" or "31 May 2026 to 31 August 2026", in the page culture.
     */
    function holdingsDates (data) {
        const format = page.formatDay;
        const earliest = data.funds.map((f) => f.reportDate).sort()[0];
        return earliest === data.asOf
            ? format(data.asOf)
            : PFI18n.t('Research.Js.Common.FromTo', { from: format(earliest), to: format(data.asOf) });
    }

    /**
     * Wire the view when the switch, the treemap, the Sectors data and the page helpers are present.
     * @returns {void}
     */
    function init () {
        const chart = window.ResearchSectors;
        const views = document.getElementById(VIEWS_ID);
        if (!page || !chart || !views || !views.dataset.industriesUrl || typeof window.d3 === 'undefined') {
            return;
        }
        const sectors = page.readData(SECTORS_DATA_ID, 'Research Sectors data');
        if (!sectors || !Array.isArray(sectors.values)) {
            return;
        }
        createView(views.dataset.industriesUrl, sectors, chart);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

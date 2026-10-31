/* global d3, PFI18n, ChartPalette */
/**
 * Research Sectors: a D3 treemap of the largest SEC filers by Public float, one year at a time, on the Sectors page and
 * the tabbed Research page's Sectors tab. The hierarchy is SEC industry office, SIC industry, then company (plus one
 * "Other" tile per industry for the smaller filers); the treemap draws the focused group's parts as tiles, in office
 * colours (siblings of one office stepped in lightness). Reads the Sectors snapshot from the JSON data island the
 * Sectors panel renders (Views/Research/_SectorsPanel.cshtml), so it makes no request of its own. Hover or tap a tile
 * for its figures. The page's scrubber (js/research/research-page.js) moves it between years through update(), and its
 * table view reads table().
 *
 * Click a group's tile (or press an office's legend button) to zoom in; the breadcrumb above the chart jumps to any
 * level on the way back. The side panel's takeaway cards describe the focused group. Two keyboard paths reach every
 * level: the tiles (one tab stop; arrow keys move between them, Enter zooms in, Escape zooms out and returns focus to
 * the group left), and the ranked list under the chart (the focused group's parts as buttons; hovering or focusing a
 * row fades the other tiles). The "Technology (incl. chip makers)" toggle dims every tile outside the industries the
 * server flags as Technology. The zoom and the highlight survive a change of year.
 *
 * The panel's data-research-focus ("Office" or "Office/Industry") opens the chart zoomed into that group, matched by
 * name; window.ResearchSectors.focusPath(parts) does the same later.
 *
 * The chart draws one of two sources. The market (above) is the default; js/research/research-sectors-basket.js can
 * switch it to the Fund Basket through setSource('basket', provider), where the provider gives the basket's tree, its
 * takeaway cards, table and wording. In the basket every figure is a share of the basket (never of a group), the
 * year does not change the chart (the provider's period() hears it, for its lean section), and stocks SEC does not
 * classify are hatched grey (data.hatched), never in an office colour. window.ResearchSectors holds the chart once it
 * is drawn.
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
    const HOST_ID = 'research-treemap';
    const PANEL_ID = 'research-panel-sectors';
    const LEGEND_ID = 'research-sectors-legend';
    const TOOLTIP_ID = 'research-tooltip';
    const SUBTITLE_ID = 'research-sectors-subtitle';
    const BREADCRUMB_ID = 'research-sectors-breadcrumb';
    const TAKEAWAYS_ID = 'research-sectors-takeaways';
    const TECH_TOGGLE_ID = 'research-tech-toggle';
    const TECH_SHARE_ID = 'research-tech-share';
    const LIVE_ID = 'research-live';
    const ROOT_KEY = 'root';
    const TAB_ID = 'sectors';
    const TRANSITION_MS = 750;
    const TABLE_ROWS = 25;
    const MAX_SIZE = 640;
    const MIN_SIZE = 240;
    // The "Top 10 companies" takeaway shows only when the focus has more companies than this.
    const TOP_COMPANIES = 10;
    // How many of the focused group's parts the ranked list under the chart shows.
    const RANKED_ROWS = 15;
    const RANKED_ID = 'research-sectors-ranked';
    const RANKED_TITLE_ID = 'research-sectors-ranked-title';
    // The treemap's height as a share of its width, within limits.
    const TREEMAP_RATIO = 0.62;
    const TREEMAP_MIN_HEIGHT = 260;
    const TREEMAP_MAX_HEIGHT = 520;
    // Sibling tiles of one office step their lightness by this much (HSL lightness, 0 to 1) so they stay apart.
    const TILE_LIGHTNESS_STEP = 0.12;
    // The hatch pattern of stocks SEC does not classify; each chart's SVG defines its own copy.
    const HATCH_ID = 'research-hatch';
    const HATCH_SIZE = 6;
    // How the market's figures read: US$ of Public float, with shares of the group and of every filer.
    const MARKET_MEASURE = {
        format: formatMoney,
        get unit () {
            return PFI18n.t('Research.Js.Sectors.PublicFloat');
        },
        describe: (value) => PFI18n.t('Research.Js.Sectors.PublicFloatValue', { value: formatMoney(value) }),
        relative: true
    };

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
    const OTHER_OPACITY = 0.45;
    // Dimmed by the Technology highlight: the fill keeps its office colour, only fainter.
    const DIMMED_OPACITY = 0.15;
    const KIND_LABELS = {
        office: () => PFI18n.t('Research.Js.Sectors.Office'),
        industry: () => PFI18n.t('Research.Js.Sectors.Industry'),
        company: (node) => (node.data.ticker
            ? PFI18n.t('Research.Js.Sectors.Ticker', { ticker: node.data.ticker })
            : PFI18n.t('Research.Js.Sectors.NoTicker')),
        other: (node) => node.data.note || PFI18n.t('Research.Js.Sectors.SmallerFilers',
            { name: node.parent ? node.parent.data.name : PFI18n.t('Research.Js.Sectors.ThisIndustry') }),
        unclassified: () => PFI18n.t('Research.Js.Sectors.Kind.Unclassified'),
        country: () => PFI18n.t('Research.Js.Sectors.Kind.Country'),
        issuer: () => PFI18n.t('Research.Js.Sectors.Kind.Issuer'),
        bonds: () => PFI18n.t('Research.Js.Sectors.Kind.Bonds'),
        bondType: () => PFI18n.t('Research.Js.Sectors.Kind.BondType'),
        cash: () => PFI18n.t('Research.Js.Sectors.Kind.Cash')
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
     * Add the hatch pattern for stocks SEC does not classify to a chart's SVG: grey lines at 45 degrees on a light grey
     * ground, from the page's CSS tokens.
     * @param {Object} svg - The d3 selection of the SVG.
     * @param {string} id - The pattern's element id.
     * @returns {void}
     */
    function appendHatch (svg, id) {
        const pattern = svg.append('defs').append('pattern')
            .attr('id', id)
            .attr('patternUnits', 'userSpaceOnUse')
            .attr('width', HATCH_SIZE)
            .attr('height', HATCH_SIZE)
            .attr('patternTransform', 'rotate(45)');
        pattern.append('rect')
            .attr('width', HATCH_SIZE)
            .attr('height', HATCH_SIZE)
            .style('fill', 'var(--research-hatch-ground)');
        pattern.append('line')
            .attr('x1', 0).attr('y1', 0).attr('x2', 0).attr('y2', HATCH_SIZE)
            .style('stroke', 'var(--research-hatch-line)')
            .style('stroke-width', 2.5);
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
            node.children.push({
                key: `x${row.industry}`,
                name: PFI18n.plural('Research.Js.Sectors.OtherFilers', row.count),
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
        return { key: ROOT_KEY, name: PFI18n.t('Research.Js.Sectors.AllSectors'), kind: 'root', children };
    }

    /**
     * Sort siblings largest first, with an industry's "Other" tile last. Offices keep their order.
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
     * A share as a percentage with one decimal, e.g. "12.3%".
     * @param {number} part - The part.
     * @param {number} whole - The whole.
     * @returns {string} The percentage in the page culture, or "0.0%" when the whole is empty.
     */
    function percentOf (part, whole) {
        return page.formatPercent(whole > 0 ? part / whole * 100 : 0, 1);
    }

    /**
     * A node's rank by Public float among its siblings, leaving out "Other" tiles (they sum many filers).
     * @param {Object} node - A d3 hierarchy node.
     * @returns {{rank: number, of: number}|null} The rank (1 is largest) and how many siblings are ranked, or null
     *     for an "Other" tile or a node without a parent.
     */
    function rankAmongSiblings (node) {
        if (!node.parent || node.data.kind === 'other') {
            return null;
        }
        const ranked = node.parent.children
            .filter((d) => d.data.kind !== 'other')
            .sort((a, b) => b.value - a.value);
        return { rank: ranked.indexOf(node) + 1, of: ranked.length };
    }

    /**
     * Create the tooltip controller for the Research tooltip element: a tile's name, kind and figure, in the market
     * its share of its parent group (below the top ring) and of all filers, and its rank among its siblings.
     * @param {HTMLElement|null} tooltip - The tooltip element.
     * @param {function(): Object} measureOf - The measure of the source shown (MARKET_MEASURE or the basket's).
     * @returns {{show: function(PointerEvent, Object): void, hide: function(): void}} The controller.
     */
    function createTooltip (tooltip, measureOf) {
        return page.createTooltip(tooltip, (line, node) => {
            const measure = measureOf();
            const root = node.ancestors().pop();
            const valueLine = document.createElement('span');
            valueLine.append(line(measure.format(node.value), 'research-tooltip__value'), ` ${measure.unit}`);
            const parts = [
                line(node.data.name, 'research-tooltip__name'),
                line(KIND_LABELS[node.data.kind](node), 'research-tooltip__muted'),
                valueLine
            ];
            if (measure.relative && node.parent && node.parent !== root) {
                parts.push(line(PFI18n.t('Research.Js.Sectors.OfGroup',
                    { value: percentOf(node.value, node.parent.value), name: node.parent.data.name }), 'research-tooltip__muted'));
            }
            if (measure.relative) {
                parts.push(line(PFI18n.t('Research.Js.Sectors.OfAllFilers', { value: percentOf(node.value, root.value) }),
                    'research-tooltip__muted'));
            }
            const rank = rankAmongSiblings(node);
            if (rank) {
                parts.push(line(PFI18n.t('Research.Js.Sectors.RankOf', {
                    rank: PFI18n.formatNumber(rank.rank), of: PFI18n.formatNumber(rank.of), name: node.parent.data.name
                }), 'research-tooltip__muted'));
            }
            return parts;
        });
    }

    /**
     * Mark each node lit by the Technology highlight: a company or "Other" tile by its industry's server-side flag,
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
            if (office.data.hatched) {
                swatch.classList.add('research-legend__swatch--hatched');
            } else {
                swatch.style.background = officeColour(legend, office.data.office);
            }
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
     * Draw the breadcrumb above the chart: every group from all filers down to the focus, each a button that zooms to
     * it, and the focus itself (with its Public float) as the current location. When the breadcrumb held keyboard
     * focus, focus moves to the new current location, so it is not lost when the pressed button is redrawn.
     * @param {HTMLElement|null} nav - The breadcrumb nav element.
     * @param {Object} focus - The focused d3 hierarchy node.
     * @param {function(string): void} onPick - Called with a group's node key when its button is pressed.
     * @param {Object} measure - How figures read (MARKET_MEASURE or the basket's).
     * @returns {void}
     */
    function renderBreadcrumb (nav, focus, onPick, measure) {
        if (!nav) {
            return;
        }
        const hadFocus = nav.contains(document.activeElement);
        const list = document.createElement('ol');
        list.className = 'research-breadcrumb__list';
        const trail = focus.ancestors().reverse();
        trail.forEach((node, index) => {
            const item = document.createElement('li');
            item.className = 'research-breadcrumb__item';
            if (index === trail.length - 1) {
                const current = document.createElement('span');
                current.className = 'research-breadcrumb__current';
                current.setAttribute('aria-current', 'location');
                current.tabIndex = -1;
                current.textContent = `${node.data.name} · ${measure.format(node.value || 0)}`;
                item.append(current);
            } else {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = 'research-breadcrumb__link';
                button.dataset.key = node.data.key;
                button.textContent = node.data.name;
                button.addEventListener('click', () => onPick(node.data.key));
                item.append(button);
            }
            list.append(item);
        });
        nav.replaceChildren(list);
        if (hadFocus) {
            nav.querySelector('.research-breadcrumb__current').focus();
        }
    }

    /**
     * The takeaway cards for the focused group. A card that would say nothing is left out: the largest part needs
     * named parts, the top-10 share needs more than ten companies, the Technology share is left out when the group is
     * all or none Technology, and the change needs the same group in the year before.
     * @param {Object} root - The d3 hierarchy root of the period shown.
     * @param {Object} focus - The focused d3 hierarchy node.
     * @param {Object|null} previous - The d3 hierarchy root of the year before, or null for the first year.
     * @param {string} periodLabel - The period shown.
     * @returns {{label: string, value: string, note: string}[]} The cards, in display order.
     */
    function takeaways (root, focus, previous, periodLabel) {
        const cards = [{
            label: PFI18n.t('Research.Js.Sectors.Total'),
            value: formatMoney(focus.value || 0),
            note: focus.depth > 0
                ? PFI18n.t('Research.Js.Sectors.TotalNote',
                    { name: focus.data.name, period: periodLabel, value: percentOf(focus.value, root.value) })
                : PFI18n.t('Research.Js.Sectors.TotalNoteAll', { period: periodLabel })
        }];

        const parts = (focus.children || []).filter((d) => d.data.kind !== 'other').sort((a, b) => b.value - a.value);
        if (parts.length > 0) {
            cards.push({
                label: PFI18n.t('Research.Js.Sectors.LargestPart'),
                value: percentOf(parts[0].value, focus.value),
                note: parts[0].data.name
            });
        }

        const companies = focus.leaves().filter((d) => d.data.kind === 'company').sort((a, b) => b.value - a.value);
        if (companies.length > TOP_COMPANIES) {
            const top = d3.sum(companies.slice(0, TOP_COMPANIES), (d) => d.value);
            cards.push({
                label: PFI18n.t('Research.Js.Sectors.TopCompanies', { count: PFI18n.formatNumber(TOP_COMPANIES) }),
                value: percentOf(top, focus.value),
                note: PFI18n.t('Research.Js.Sectors.TopCompaniesNote', { count: PFI18n.formatNumber(companies.length) })
            });
        }

        const lit = d3.sum(focus.leaves().filter((d) => d.technology), (d) => d.value);
        if (lit > 0 && lit < focus.value) {
            cards.push({
                label: PFI18n.t('Research.Js.Sectors.Technology'),
                value: percentOf(lit, focus.value),
                note: formatMoney(lit)
            });
        }

        const before = previous ? previous.descendants().find((d) => d.data.key === focus.data.key) : null;
        if (before && before.value > 0) {
            const change = (focus.value / before.value - 1) * 100;
            cards.push({
                label: PFI18n.t('Research.Js.Sectors.Change'),
                value: `${change >= 0 ? '+' : ''}${page.formatPercent(change, 1)}`,
                note: PFI18n.t('Research.Js.Sectors.ChangeNote', { value: formatMoney(before.value) })
            });
        }
        return cards;
    }

    /**
     * Draw the takeaway cards into the side panel as a list, one card per item.
     * @param {HTMLElement|null} host - The takeaways list element.
     * @param {{label: string, value: string, note: string}[]} cards - The cards.
     * @returns {void}
     */
    function renderTakeaways (host, cards) {
        if (!host) {
            return;
        }
        host.replaceChildren(...cards.map((card) => {
            const item = document.createElement('li');
            item.className = 'research-takeaway';
            const label = document.createElement('span');
            label.className = 'research-takeaway__label';
            label.textContent = card.label;
            const value = document.createElement('span');
            value.className = 'research-takeaway__value';
            value.textContent = card.value;
            const note = document.createElement('span');
            note.className = 'research-takeaway__note';
            note.textContent = card.note;
            item.append(label, value, note);
            return item;
        }));
    }

    /**
     * Draw the ranked list under the chart: the focused group's parts, largest first (an industry's "Other" tile
     * last), each with its share of the group, its Public float and a bar sized against the largest. A part that
     * has parts of its own is a button that zooms into it; a company or "Other" tile is plain text. Pointer or
     * keyboard on a row fades the other tiles through onHighlight.
     * @param {HTMLElement|null} list - The ranked list element.
     * @param {Object} focus - The focused d3 hierarchy node.
     * @param {function(Element, string): string} colourOf - The fill colour of an office.
     * @param {{onPick: function(string): void, onHighlight: function(string|null): void}} handlers - Called with a
     *     row's node key when its button is pressed, and with the key (or null) to fade the others or clear the fade.
     * @param {Object} measure - How figures read: in the market the share column is of the group and the value is
     *     Public float; in the basket the share column is of the basket and there is no value column.
     * @returns {void}
     */
    function renderRanked (list, focus, colourOf, handlers, measure) {
        if (!list) {
            return;
        }
        const isOther = (d) => (d.data.kind === 'other' ? 1 : 0);
        const parts = (focus.children || []).filter((d) => d.value > 0)
            .sort((a, b) => isOther(a) - isOther(b) || b.value - a.value);
        const shown = parts.slice(0, RANKED_ROWS);
        const largest = d3.max(shown, (d) => d.value) || 1;
        const rows = shown.map((node, index) => {
            const item = document.createElement('li');
            item.className = 'research-ranked__item';
            const row = document.createElement(node.children ? 'button' : 'div');
            row.className = 'research-ranked__row';
            if (node.children) {
                row.type = 'button';
                row.dataset.key = node.data.key;
                row.setAttribute('aria-label', measure.relative
                    ? PFI18n.t('Research.Js.Sectors.RankedLabelShare', {
                        name: node.data.name,
                        value: measure.describe(node.value),
                        share: percentOf(node.value, focus.value),
                        group: focus.data.name
                    })
                    : PFI18n.t('Research.Js.Sectors.RankedLabel', { name: node.data.name, value: measure.describe(node.value) }));
                row.addEventListener('click', () => handlers.onPick(node.data.key));
                row.addEventListener('focus', () => handlers.onHighlight(node.data.key));
                row.addEventListener('blur', () => handlers.onHighlight(null));
            }
            item.addEventListener('pointerenter', () => handlers.onHighlight(node.data.key));
            item.addEventListener('pointerleave', () => handlers.onHighlight(null));

            const rank = document.createElement('span');
            rank.className = 'research-ranked__rank';
            rank.textContent = node.data.kind === 'other' ? '' : PFI18n.formatNumber(index + 1);
            const name = document.createElement('span');
            name.className = 'research-ranked__name';
            name.textContent = node.data.kind === 'company' && node.data.ticker
                ? `${node.data.name} (${node.data.ticker})`
                : node.data.name;
            const share = document.createElement('span');
            share.className = 'research-ranked__share';
            share.textContent = measure.relative ? percentOf(node.value, focus.value) : measure.format(node.value);
            const value = document.createElement('span');
            value.className = 'research-ranked__value';
            value.textContent = measure.relative ? formatMoney(node.value) : '';
            const bar = document.createElement('span');
            bar.className = 'research-ranked__bar';
            bar.setAttribute('aria-hidden', 'true');
            bar.style.width = `${node.value / largest * 100}%`;
            if (node.data.hatched) {
                bar.classList.add('research-ranked__bar--hatched');
            } else {
                bar.style.background = colourOf(list, node.data.office);
            }
            row.append(rank, name, share, value, bar);
            item.append(row);
            return item;
        });
        if (parts.length > shown.length) {
            const more = document.createElement('li');
            more.className = 'research-ranked__more';
            more.textContent = PFI18n.t('Research.Js.Sectors.RankedMore', { count: PFI18n.formatNumber(parts.length - shown.length) });
            rows.push(more);
        }
        list.replaceChildren(...rows);
    }

    /**
     * A tile's fill: its office colour, with siblings that share one office stepped in lightness (alternately
     * lighter and darker, growing with the index) so neighbouring industries or companies stay distinguishable.
     * @param {string} base - The office colour.
     * @param {number} index - The tile's index among its siblings.
     * @param {boolean} step - Whether to step (false when the siblings already differ by office).
     * @returns {string} The fill colour.
     */
    function tileColour (base, index, step) {
        if (!step || index === 0) {
            return base;
        }
        const colour = d3.hsl(base);
        const amount = Math.ceil(index / 2) * TILE_LIGHTNESS_STEP * (index % 2 === 1 ? 1 : -1);
        colour.l = Math.max(0.18, Math.min(0.82, colour.l + amount));
        return colour.formatHex();
    }

    /**
     * The label ink that reads on a fill: dark text on a light fill, light text on a dark one (by relative
     * luminance).
     * @param {string} fill - The fill colour.
     * @returns {string} A CSS colour.
     */
    function inkFor (fill) {
        const rgb = d3.rgb(fill);
        const channel = (c) => {
            const v = c / 255;
            return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
        };
        const luminance = 0.2126 * channel(rgb.r) + 0.7152 * channel(rgb.g) + 0.0722 * channel(rgb.b);
        return luminance > 0.35 ? 'var(--research-label-dark)' : 'var(--research-label-light)';
    }

    /**
     * The d3 hierarchy of one period, summed by Public float, sorted, and with the Technology flag marked.
     * @param {Object} data - The Sectors snapshot.
     * @param {Object} period - One entry of data.values.
     * @returns {Object} The d3 hierarchy root.
     */
    function rootOf (data, period) {
        return hierarchyOf(buildTree(data, period));
    }

    /**
     * The d3 hierarchy of a tree (the market's or the basket's), summed, sorted, and with the Technology flag marked.
     * @param {Object} tree - The root node data.
     * @returns {Object} The d3 hierarchy root.
     */
    function hierarchyOf (tree) {
        const root = d3.hierarchy(tree)
            .sum((d) => (d.children ? 0 : d.value || 0))
            .sort(compareNodes);
        markTechnology(root);
        return root;
    }

    /**
     * The accessible name of a tile: its name and figure, its share of its group when given, and the zoom
     * hint when it has parts.
     * @param {{name: string, value: string, share?: string, group?: string}} values - The placeholders.
     * @param {boolean} withShare - Whether values carries the share and group.
     * @param {boolean} zoomable - Whether the part has parts of its own.
     * @returns {string} The label.
     */
    function partLabel (values, withShare, zoomable) {
        const key = withShare ? 'Research.Js.Sectors.PartLabelShare' : 'Research.Js.Sectors.PartLabel';
        const label = PFI18n.t(key, values);
        return zoomable ? `${label} ${PFI18n.t('Research.Js.Sectors.ZoomHint')}` : label;
    }

    /**
     * Create the Sectors treemap in the host. It draws the focused group's parts as tiles, in the same office colours
     * (stepped in lightness when the parts share one office), and is moved between periods by update(): tiles are
     * keyed by office, industry and company, so a zoom or a change of year moves them when animated.
     *
     * The view (the zoom focus and the Technology highlight) is kept here, outside any one period, and every update()
     * draws in it: a scrubber step or a redraw at a new width keeps where the user has zoomed and what is lit. A zoom
     * or a highlight change is an update() of the current period.
     * @param {HTMLElement} host - The chart host.
     * @param {Object} data - The Sectors snapshot.
     * @param {Object} tooltip - The tooltip controller.
     * @returns {{periods: string[], transitionMs: number, update: function(number, Object): string,
     *     table: function(number): Object, resize: function(): void, zoomTo: function(string): void,
     *     zoomOut: function(): void, focusPath: function(string[]): boolean, setTechnology: function(boolean): void,
     *     setSource: function(string, Object=): void, refresh: function(): void, measure: function(): Object}} The
     *     chart.
     */
    function createTreemap (host, data, tooltip) {
        const subtitle = document.getElementById(SUBTITLE_ID);
        const legend = document.getElementById(LEGEND_ID);
        const breadcrumb = document.getElementById(BREADCRUMB_ID);
        const takeawaysList = document.getElementById(TAKEAWAYS_ID);
        const techToggle = document.getElementById(TECH_TOGGLE_ID);
        const techShare = document.getElementById(TECH_SHARE_ID);
        const live = document.getElementById(LIVE_ID);
        const rankedList = document.getElementById(RANKED_ID);
        const rankedTitle = document.getElementById(RANKED_TITLE_ID);
        // source: 'market' or 'basket'.
        const view = { focusKey: ROOT_KEY, technology: false, source: 'market' };
        // The basket's provider, once research-sectors-basket.js has set one.
        let basket = null;
        let svg = null;
        let width = 0;
        let height = 0;
        // Keyboard state: the tile that holds the one tab stop (by node key), and where focus goes after the zoom a
        // key press asked for ({ tile: key }, { firstTile: true } or { list: true }), or null.
        let activeKey = null;
        let pendingFocus = null;
        let periodIndex = data.values.length - 1;
        let root = null;
        let focus = null;
        // The tiles drawn, in drawing order (largest first): the arrow keys move along it.
        let tiles = [];

        const isLit = (node) => !view.technology || node.technology;
        const showsBasket = () => view.source === 'basket' && basket !== null;
        const measure = () => (showsBasket() ? basket.measure : MARKET_MEASURE);

        /**
         * Size the treemap to the host's width and draw its empty layer.
         * @returns {void}
         */
        function layout () {
            width = Math.max(MIN_SIZE, host.clientWidth || MAX_SIZE);
            height = Math.round(Math.max(TREEMAP_MIN_HEIGHT, Math.min(TREEMAP_MAX_HEIGHT, width * TREEMAP_RATIO)));
            d3.select(host).selectAll('svg').remove();
            // A group, not an image: the tiles inside are focusable and announce themselves.
            svg = d3.select(host).append('svg')
                .attr('viewBox', [0, 0, width, height].join(' '))
                .attr('role', 'group');
            appendHatch(svg, `${HATCH_ID}-treemap`);
        }

        /**
         * A tile's label, shortened to its width.
         * @param {Object} node - The tile's d3 hierarchy node.
         * @param {number} tileWidth - The tile's width in px.
         * @returns {string} The label, or empty when the tile is too small.
         */
        function tileLabel (node, tileWidth) {
            const text = node.data.kind === 'company' && node.data.ticker ? node.data.ticker : node.data.name;
            return tileWidth > 48 ? shorten(text, Math.floor((tileWidth - 12) / 6.5)) : '';
        }

        /**
         * The drawn tile element of a node key, or null.
         * @param {string} key - The node key.
         * @returns {SVGGElement|null} The tile.
         */
        function tileElement (key) {
            return svg.selectAll('g.research-treemap__tile').nodes()
                .find((element) => d3.select(element).datum().key === key) || null;
        }

        /**
         * Move the tab stop to a tile and focus it.
         * @param {string} key - The tile's node key.
         * @returns {void}
         */
        function focusTile (key) {
            const element = tileElement(key);
            if (!element) {
                return;
            }
            svg.selectAll('g.research-treemap__tile[tabindex="0"]').attr('tabindex', '-1');
            activeKey = key;
            element.setAttribute('tabindex', '0');
            element.focus();
        }

        /**
         * Arrow keys move between the tiles (wrapping, largest first), Enter or Space zooms into a group, and Escape
         * zooms out with focus returned to the group the user came from.
         * @param {KeyboardEvent} event - The key press on a tile.
         * @param {Object} tile - The tile's datum.
         * @returns {void}
         */
        function onTileKey (event, tile) {
            const index = tiles.findIndex((t) => t.key === tile.key);
            if (['ArrowRight', 'ArrowDown', 'ArrowLeft', 'ArrowUp'].includes(event.key) && tiles.length > 0) {
                event.preventDefault();
                const step = event.key === 'ArrowRight' || event.key === 'ArrowDown' ? 1 : -1;
                focusTile(tiles[(index + step + tiles.length) % tiles.length].key);
            } else if ((event.key === 'Enter' || event.key === ' ') && tile.node.children) {
                event.preventDefault();
                pendingFocus = { firstTile: true };
                zoomTo(tile.key);
            } else if (event.key === 'Escape' && focus.parent) {
                event.preventDefault();
                pendingFocus = { tile: focus.data.key };
                zoomOut();
            }
        }

        /**
         * Draw the focus's parts as tiles. The layout runs on a copy of the focus, so the real hierarchy keeps no
         * geometry; each tile carries the original node, so the tooltip, the zoom and the keyboard work on it.
         * @param {boolean} animate - Whether to tween from what is drawn now.
         * @returns {void}
         */
        function drawTiles (animate) {
            const parts = new Map((focus.children || []).map((d) => [d.data.key, d]));
            const isOther = (d) => (d.data.kind === 'other' ? 1 : 0);
            const copy = d3.hierarchy(focus.data, (d) => d.children)
                .sum((d) => (d.children ? 0 : d.value || 0))
                .sort((a, b) => isOther(a) - isOther(b) || b.value - a.value);
            d3.treemap().size([width, height]).paddingInner(2).round(true)(copy);
            const step = focus.depth > 0;
            tiles = (copy.children || []).filter((d) => d.value > 0 && parts.has(d.data.key)).map((d, index) => ({
                key: d.data.key,
                node: parts.get(d.data.key),
                x: d.x0,
                y: d.y0,
                w: Math.max(0, d.x1 - d.x0),
                h: Math.max(0, d.y1 - d.y0),
                hatched: d.data.hatched === true,
                fill: d.data.hatched
                    ? `url(#${HATCH_ID}-treemap)`
                    : tileColour(officeColour(host, d.data.office), index, step)
            }));
            const figures = measure();
            if (!tiles.some((t) => t.key === activeKey)) {
                activeKey = tiles.length > 0 ? tiles[0].key : null;
            }

            // A change without animation stops any transition still running, so nothing moves afterwards.
            svg.selectAll('*').interrupt();
            const joined = svg.selectAll('g.research-treemap__tile').data(tiles, (t) => t.key);
            joined.exit().remove();
            const entering = joined.enter().append('g')
                .attr('class', 'research-treemap__tile')
                .attr('transform', (t) => `translate(${t.x},${t.y})`)
                .on('click', (event, t) => {
                    if (t.node.children) {
                        zoomTo(t.key);
                    }
                })
                .on('pointermove', (event, t) => tooltip.show(event, t.node))
                .on('pointerdown', (event, t) => tooltip.show(event, t.node))
                .on('pointerleave', (event) => {
                    if (event.pointerType === 'mouse') {
                        tooltip.hide();
                    }
                })
                .on('focus', (event, t) => {
                    activeKey = t.key;
                    const box = event.currentTarget.getBoundingClientRect();
                    tooltip.show({ clientX: box.right, clientY: box.top }, t.node);
                })
                .on('blur', () => tooltip.hide())
                .on('keydown', (event, t) => onTileKey(event, t));
            entering.append('rect').attr('class', 'research-treemap__rect');
            entering.append('text').attr('class', 'research-treemap__name').attr('x', 6).attr('y', 15);
            entering.append('text').attr('class', 'research-treemap__value').attr('x', 6).attr('y', 30);

            // DOM order follows the tiles (largest first), so a screen reader meets them in the arrow keys' order.
            const all = entering.merge(joined).order()
                .attr('tabindex', (t) => (t.key === activeKey ? '0' : '-1'))
                .attr('data-key', (t) => t.key)
                .attr('role', (t) => (t.node.children ? 'button' : 'img'))
                .attr('aria-label', (t) => {
                    const values = { name: t.node.data.name, value: figures.describe(t.node.value) };
                    if (figures.relative) {
                        Object.assign(values, { share: percentOf(t.node.value, focus.value), group: focus.data.name });
                    }
                    return partLabel(values, figures.relative, Boolean(t.node.children));
                })
                .classed('research-treemap__tile--zoomable', (t) => Boolean(t.node.children));
            // Animated: tween from where the tiles are; otherwise apply at once (also under reduced motion).
            const move = (selection) => (animate
                ? selection.transition().duration(TRANSITION_MS).ease(d3.easeCubicInOut)
                : selection);
            // A tile that enters takes its size at once; only tiles already drawn move and resize. The fill is set,
            // never tweened, so it always holds the token colour exactly (a tween would leave an interpolated rgb()).
            entering.select('rect')
                .attr('width', (t) => t.w)
                .attr('height', (t) => t.h);
            all.select('rect').attr('fill', (t) => t.fill);
            move(all).attr('transform', (t) => `translate(${t.x},${t.y})`);
            move(all.select('rect'))
                .attr('width', (t) => t.w)
                .attr('height', (t) => t.h)
                .attr('fill-opacity', (t) => (t.node.data.kind === 'other' ? OTHER_OPACITY : (isLit(t.node) ? 1 : DIMMED_OPACITY)));
            const inkOf = (t) => (t.hatched ? 'var(--research-label-dark)' : inkFor(t.fill));
            all.select('.research-treemap__name')
                .attr('fill', inkOf)
                .text((t) => (t.h > 18 ? tileLabel(t.node, t.w) : ''));
            all.select('.research-treemap__value')
                .attr('fill', inkOf)
                .text((t) => {
                    if (t.h <= 36 || t.w <= 64) {
                        return '';
                    }
                    return figures.relative
                        ? `${formatMoney(t.node.value)} · ${percentOf(t.node.value, focus.value)}`
                        : figures.format(t.node.value);
                });
            svg.attr('aria-label', PFI18n.plural('Research.Js.Sectors.TreemapLabel', tiles.length,
                { name: focus.data.name, value: figures.describe(focus.value || 0) }));
        }

        /**
         * After a zoom asked for by a key press or the ranked list, put keyboard focus where the user expects it: on
         * the group they came from, on the new focus's first tile, or on the ranked list's first button (its title
         * when the parts have no parts of their own).
         * @returns {void}
         */
        function applyPendingFocus () {
            const request = pendingFocus;
            pendingFocus = null;
            if (!request) {
                return;
            }
            if (request.list) {
                const first = rankedList ? rankedList.querySelector('button') : null;
                if (first) {
                    first.focus();
                } else if (rankedTitle) {
                    rankedTitle.focus();
                }
            } else if (request.tile && tileElement(request.tile)) {
                focusTile(request.tile);
            } else if (tiles.length > 0) {
                focusTile(tiles[0].key);
            }
        }

        /**
         * Fade every tile but one group's, or clear the fade.
         * @param {string|null} key - The group's node key, or null to clear.
         * @returns {void}
         */
        function setHighlight (key) {
            svg.selectAll('g.research-treemap__tile')
                .classed('research-treemap__tile--faded', (t) => Boolean(key) && t.key !== key);
        }

        /**
         * The Technology sentence of a summary, when the Technology highlight is on.
         * @param {string} techText - The Technology share of the focus.
         * @returns {string} The sentence, or empty when the highlight is off.
         */
        function technologySentence (techText) {
            return view.technology ? PFI18n.t('Research.Js.Sectors.TechSentence', { text: techText }) : '';
        }

        /**
         * Show the focus in the breadcrumb, the takeaway cards and the ranked list, and the state of the legend
         * buttons, the Technology toggle and its share.
         * @param {Object|null} previous - The d3 hierarchy root of the year before, or null for the first year.
         * @param {string} periodLabel - The period shown.
         * @returns {{trail: string, techText: string}} Where the chart is (e.g. "All sectors › Technology") and the
         *     Technology share of the focus.
         */
        function describeView (previous, periodLabel) {
            const figures = measure();
            const trail = focus.ancestors().reverse().map((d) => d.data.name).join(' › ');
            renderBreadcrumb(breadcrumb, focus, zoomTo, figures);
            renderTakeaways(takeawaysList, showsBasket()
                ? basket.takeaways(root, focus)
                : takeaways(root, focus, previous, periodLabel));
            if (rankedTitle) {
                rankedTitle.textContent = showsBasket()
                    ? PFI18n.t('Research.Js.Sectors.RankedTitle', { name: focus.data.name })
                    : PFI18n.t('Research.Js.Sectors.RankedTitlePeriod', { name: focus.data.name, period: periodLabel });
            }
            renderRanked(rankedList, focus, officeColour, {
                onPick: (key) => {
                    pendingFocus = { list: true };
                    zoomTo(key);
                },
                onHighlight: setHighlight
            }, figures);
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
            const techText = showsBasket()
                ? `${figures.describe(lit)}.`
                : PFI18n.t('Research.Js.Sectors.TechShare', {
                    value: formatMoney(lit),
                    share: percentOf(lit, focus.value),
                    name: focus.data.name,
                    period: periodLabel
                });
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
            // The basket has no years: the scrubber's period leaves it as it is.
            root = showsBasket() ? hierarchyOf(basket.tree()) : rootOf(data, period);
            const previous = !showsBasket() && periodIndex > 0 ? rootOf(data, data.values[periodIndex - 1]) : null;

            // Keep the focus by its key; a group the period does not have (or has no Public float in) falls back
            // to the top.
            focus = root.descendants().find((d) => d.data.key === view.focusKey && d.value > 0) || root;
            view.focusKey = focus.data.key;

            drawTiles(Boolean(options && options.animate));
            renderLegend(legend, root, (key) => zoomTo(view.focusKey === key ? ROOT_KEY : key));
            const { trail, techText } = describeView(previous, period.period);
            setHighlight(null);
            applyPendingFocus();

            if (showsBasket()) {
                const described = basket.describe(root, focus, trail);
                if (subtitle) {
                    subtitle.textContent = described.subtitle;
                }
                const lean = basket.period ? basket.period(periodIndex) : '';
                return [described.summary, technologySentence(techText), lean].filter(Boolean).join(' ');
            }

            const largest = focus.leaves()
                .filter((d) => d.data.kind === 'company')
                .sort((a, b) => b.value - a.value)[0];
            const details = [PFI18n.plural('Research.Js.Sectors.TotalAcross', (root.children || []).length,
                { value: formatMoney(root.value || 0) })];
            if (focus.depth > 0) {
                details.push(PFI18n.t('Research.Js.Sectors.Showing', { trail, value: formatMoney(focus.value || 0) }));
            }
            if (largest) {
                details.push(PFI18n.t('Research.Js.Sectors.LargestCompany',
                    { name: largest.data.name, value: formatMoney(largest.value) }));
            }
            if (subtitle) {
                subtitle.textContent = PFI18n.t('Research.Js.Sectors.Subtitle', {
                    count: PFI18n.formatNumber(period.ranked.length),
                    total: PFI18n.formatNumber(period.ranked.length + (period.restCount || 0)),
                    period: period.period
                });
            }
            const summary = PFI18n.t('Research.Js.Sectors.Summary', { period: period.period, details: details.join(' ') });
            return [summary, technologySentence(techText)].filter(Boolean).join(' ');
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
         * Zoom to a group, by its node key (ROOT_KEY for the top).
         * @param {string} key - The node key.
         * @returns {void}
         */
        function zoomTo (key) {
            view.focusKey = key;
            // A zoom moves the tab stop to the new focus's first tile (Escape then restores the group left).
            activeKey = null;
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
         * Zoom to a group by name: an SEC office, or an office and one of its SIC industries, each matched without
         * regard to case. Drawn at once and not announced, as when the page opens on it.
         * @param {string[]} parts - The office name, then optionally the industry name.
         * @returns {boolean} True when the group was found; false leaves the chart where it is.
         */
        function focusPath (parts) {
            if (!Array.isArray(parts) || parts.length === 0 || parts.length > 2 || !root) {
                return false;
            }
            const same = (name, part) => typeof part === 'string' &&
                String(name).trim().toLowerCase() === part.trim().toLowerCase();
            let node = root;
            for (const part of parts) {
                node = (node.children || []).find((child) => child.children && child.value > 0 && same(child.data.name, part));
                if (!node) {
                    return false;
                }
            }
            view.focusKey = node.data.key;
            activeKey = null;
            tooltip.hide();
            update(periodIndex, { animate: false });
            return true;
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
            if (showsBasket()) {
                return basket.table();
            }
            const period = data.values[Math.max(0, Math.min(data.values.length - 1, index))];
            const rows = period.ranked.slice(0, TABLE_ROWS).map((row, rank) => {
                const company = data.companies[row.company];
                const industry = data.industries[company.industry];
                return [
                    PFI18n.formatNumber(rank + 1),
                    company.ticker ? `${company.name} (${company.ticker})` : company.name,
                    industry.name,
                    data.offices[industry.office],
                    formatMoney(row.publicFloat)
                ];
            });
            return {
                caption: PFI18n.plural('Research.Js.Sectors.TableCaption', rows.length, { period: period.period }),
                columns: [
                    { label: PFI18n.t('Research.Js.Sectors.Rank'), numeric: true },
                    { label: PFI18n.t('Research.Js.Sectors.Company') },
                    { label: PFI18n.t('Research.Js.Sectors.Industry') },
                    { label: PFI18n.t('Research.Js.Sectors.Office') },
                    { label: PFI18n.t('Research.Js.Sectors.PublicFloat'), numeric: true }
                ],
                rows
            };
        }

        /**
         * Show the market or the basket. The zoom goes back to the top, since the two have different groups; the
         * change is animated unless the user asks for reduced motion.
         * @param {string} source - 'market' or 'basket'.
         * @param {Object} [provider] - The basket's provider: tree(), takeaways(root, focus), table(),
         *     describe(root, focus, trail), measure, and optionally period(index), told each period shown, which returns
         *     a sentence to announce. Kept for later switches.
         * @returns {void}
         */
        function setSource (source, provider) {
            if (provider) {
                basket = provider;
            }
            const next = source === 'basket' && basket ? 'basket' : 'market';
            if (next === view.source) {
                return;
            }
            view.source = next;
            view.focusKey = ROOT_KEY;
            activeKey = null;
            applyView();
        }

        /**
         * Redraw the source shown without a transition, e.g. after the basket changed; the zoom is kept where the
         * group still exists.
         * @returns {void}
         */
        function refresh () {
            tooltip.hide();
            update(periodIndex, { animate: false });
        }

        /**
         * Redraw at the host's new width, at the current period, without a transition.
         * @returns {void}
         */
        function resize () {
            layout();
            update(periodIndex, { animate: false });
        }

        if (techToggle) {
            techToggle.addEventListener('click', () => setTechnology(!view.technology));
        }

        layout();
        return {
            periods: data.periods,
            transitionMs: TRANSITION_MS,
            update,
            table,
            resize,
            zoomTo,
            zoomOut,
            focusPath,
            setTechnology,
            setSource,
            refresh,
            measure
        };
    }

    /**
     * Draw the treemap when its host and data are on the page, hand it to the page's scrubber, open it on the panel's
     * focus, and redraw it when the card changes width.
     * @returns {void}
     */
    function init () {
        const host = document.getElementById(HOST_ID);
        const data = page.readData(DATA_ELEMENT_ID, 'Research Sectors data');
        if (!host || !data || !Array.isArray(data.values) || data.values.length === 0 || typeof d3 === 'undefined') {
            return;
        }
        let chart = null;
        const tooltip = createTooltip(document.getElementById(TOOLTIP_ID), () => (chart ? chart.measure() : MARKET_MEASURE));
        chart = createTreemap(host, data, tooltip);
        page.registerTab(TAB_ID, chart);
        window.ResearchSectors = chart;
        const panel = document.getElementById(PANEL_ID);
        const focus = panel ? panel.dataset.researchFocus : '';
        if (focus) {
            chart.focusPath(focus.split('/'));
        }
        // A theme switch redraws the tiles, legend and ranked bars in the new office colours (#486).
        ChartPalette.subscribe(() => {
            tooltip.hide();
            chart.refresh();
        });
        const card = host.parentElement || host;
        let width = card.clientWidth;

        // A tap outside the chart, or a scroll, puts the tooltip away.
        document.addEventListener('pointerdown', (event) => {
            if (!host.contains(event.target)) {
                tooltip.hide();
            }
        });
        window.addEventListener('scroll', tooltip.hide, { passive: true });

        if (typeof ResizeObserver !== 'undefined') {
            const observer = new ResizeObserver(() => {
                if (card.clientWidth !== width) {
                    width = card.clientWidth;
                    tooltip.hide();
                    chart.resize();
                }
            });
            observer.observe(card);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

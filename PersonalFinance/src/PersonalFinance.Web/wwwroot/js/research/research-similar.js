/* global PFI18n */
/**
 * Research Fund page, "Similar funds": the Tracked Funds closest to this one, by holdings overlap (the share of money
 * in the same securities, from the fund overlap snapshot) and by correlation of monthly returns (from the fund returns
 * snapshot's window figures). Both read the pair figures the refresh already computed; nothing is recomputed here.
 *
 * `window.ResearchSimilar.byOverlap(overlap, ticker)` and `byCorrelation(returns, ticker, years)` are pure, so
 * `node --test` loads them (tools/research-similar.test.js). On the page, the lists in `#research-similar` are drawn
 * from the `research-fund-overlap-data` and `research-fund-returns-data` islands; each fund links to its own Fund page
 * with `data-research-link`, so the basket goes along.
 */
(function () {
    'use strict';

    const CARD_ID = 'research-similar';
    const OVERLAP_LIST_ID = 'research-similar-overlap';
    const CORRELATION_LIST_ID = 'research-similar-correlation';
    const LIMIT = 5;

    /**
     * The other funds of a pair list, with their figure, highest first.
     * @param {string[]} tickers - The snapshot's funds, which the pairs index.
     * @param {{a: number, b: number}[]} pairs - The pairs.
     * @param {string} ticker - This fund.
     * @param {function(Object): (number|null|undefined)} valueOf - A pair's figure.
     * @returns {{ticker: string, value: number}[]} The other funds, highest first, ties by ticker.
     */
    function rank (tickers, pairs, ticker, valueOf) {
        const self = tickers.findIndex((t) => t.toUpperCase() === ticker.toUpperCase());
        if (self < 0) {
            return [];
        }
        const best = new Map();
        pairs.forEach((pair) => {
            const value = valueOf(pair);
            if ((pair.a !== self && pair.b !== self) || pair.a === pair.b || typeof value !== 'number' || Number.isNaN(value)) {
                return;
            }
            const other = tickers[pair.a === self ? pair.b : pair.a];
            if (other !== undefined && (!best.has(other) || best.get(other) < value)) {
                best.set(other, value);
            }
        });
        return Array.from(best, ([other, value]) => ({ ticker: other, value }))
            .sort((x, y) => y.value - x.value || (x.ticker < y.ticker ? -1 : x.ticker > y.ticker ? 1 : 0));
    }

    /**
     * The funds that hold most of the same securities as this one, by the overlap snapshot's pair figures.
     * @param {Object|null} overlap - The fund overlap snapshot.
     * @param {string} ticker - This fund.
     * @returns {{ticker: string, percent: number}[]} The other funds with an overlap, highest first.
     */
    function byOverlap (overlap, ticker) {
        if (!overlap || !Array.isArray(overlap.funds) || !Array.isArray(overlap.pairs)) {
            return [];
        }
        return rank(overlap.funds.map((f) => f.ticker), overlap.pairs, ticker, (p) => p.overlap)
            .map((r) => ({ ticker: r.ticker, percent: r.value }));
    }

    /**
     * The funds whose monthly returns moved most closely with this one's over a window, by the returns snapshot's
     * precomputed correlations.
     * @param {Object|null} returns - The fund returns snapshot.
     * @param {string} ticker - This fund.
     * @param {number} years - The window's length in years.
     * @returns {{ticker: string, value: number}[]} The other funds with a correlation, highest first.
     */
    function byCorrelation (returns, ticker, years) {
        const window = returns && Array.isArray(returns.windows) ? returns.windows.find((w) => w.years === years) : null;
        if (!window || !Array.isArray(returns.funds) || !Array.isArray(window.correlations)) {
            return [];
        }
        return rank(returns.funds.map((f) => f.ticker), window.correlations, ticker, (p) => p.correlation);
    }

    /**
     * Draw one ranked list, or its empty line.
     * @param {HTMLElement} list - The list element; its `data-research-fund-href` is the Fund page link with `{ticker}`.
     * @param {{ticker: string, text: string}[]} rows - The funds and their figure, in order.
     * @param {string} emptyText - The line when there is no other fund.
     * @returns {void}
     */
    function drawList (list, rows, emptyText) {
        const template = list.dataset.researchFundHref || '/Research/Fund/{ticker}';
        if (rows.length === 0) {
            const item = document.createElement('li');
            item.className = 'research-similar__empty';
            item.textContent = emptyText;
            list.replaceChildren(item);
            return;
        }
        list.replaceChildren(...rows.slice(0, LIMIT).map((row) => {
            const item = document.createElement('li');
            item.className = 'research-similar__item';
            const link = document.createElement('a');
            link.href = template.replace('{ticker}', encodeURIComponent(row.ticker));
            link.setAttribute('data-research-link', '');
            link.textContent = row.ticker;
            const value = document.createElement('span');
            value.className = 'research-similar__value';
            value.textContent = row.text;
            item.append(link, value);
            return item;
        }));
    }

    /**
     * Draw both lists for this fund.
     * @returns {void}
     */
    function init () {
        const card = document.getElementById(CARD_ID);
        const page = window.ResearchPage;
        if (!card || !page) {
            return;
        }
        const ticker = card.dataset.researchFund;
        const overlapList = document.getElementById(OVERLAP_LIST_ID);
        const correlationList = document.getElementById(CORRELATION_LIST_ID);
        if (overlapList) {
            const overlap = page.readData('research-fund-overlap-data', 'Research fund overlap data');
            drawList(overlapList, byOverlap(overlap, ticker).map((r) => ({ ticker: r.ticker, text: page.formatPercent(r.percent, 1) })),
                PFI18n.t('Research.Js.Similar.None'));
        }
        if (correlationList) {
            const returns = page.readData('research-fund-returns-data', 'Research fund returns data');
            const years = Number(correlationList.dataset.researchYears) || 3;
            drawList(correlationList, byCorrelation(returns, ticker, years).map((r) => ({ ticker: r.ticker, text: page.formatFixed(r.value, 2) })),
                PFI18n.t('Research.Js.Similar.None'));
        }
        if (window.ResearchBasket && typeof window.ResearchBasket.linkify === 'function') {
            window.ResearchBasket.linkify(card);
        }
    }

    window.ResearchSimilar = { byOverlap, byCorrelation };

    if (typeof document !== 'undefined' && document.readyState) {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', init);
        } else {
            init();
        }
    }
})();

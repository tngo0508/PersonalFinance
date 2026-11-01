/* global PFI18n */
/**
 * Research Basket page, the overview: the Fund Basket's annualized return, volatility and Sharpe ratio over a 1-, 3- or
 * 5-year window (3 years by default), its average expense ratio and its share in US issuers.
 *
 * Every figure comes from `ResearchBasketMath.summary` (research-basket-math.js), the module the risk chart's basket
 * diamond uses, so the two always agree for the same basket and window; the figures are written the way the risk
 * chart's card writes them. Funds without a filed expense ratio are left out of the average and named in the note.
 * Reads the `research-fund-returns-data`, `research-macro-data`, `research-fund-facts-data` and
 * `research-fund-exposure-data` islands (each may be missing) and the basket from `window.ResearchBasket`, redrawing
 * on `research:basketchange`.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const CARD_ID = 'research-basket-overview';
    const NOTE_ID = 'research-basket-overview-note';
    const FIGURE_IDS = {
        annualReturn: 'research-basket-overview-return',
        volatility: 'research-basket-overview-volatility',
        sharpe: 'research-basket-overview-sharpe',
        expenseRatio: 'research-basket-overview-fee',
        usShare: 'research-basket-overview-us'
    };
    const DEFAULT_YEARS = 3;
    const NONE = '—';

    /**
     * A number with a true minus sign and fixed decimals, as the risk chart writes it.
     * @param {number} value - The value.
     * @param {number} digits - The decimals.
     * @returns {string} E.g. "−0.42", in the page culture.
     */
    function signed (value, digits) {
        const rounded = Number(Math.abs(value).toFixed(digits));
        const text = page.formatFixed(rounded, digits);
        return value < 0 && rounded !== 0 ? `−${text}` : text;
    }

    /**
     * Each figure as the page shows it.
     * @param {Object} summary - The basket's summary.
     * @returns {Object<string, string>} The text of each figure, by its summary name.
     */
    function texts (summary) {
        const has = (value) => value !== null && value !== undefined;
        return {
            annualReturn: has(summary.annualReturn) ? `${signed(summary.annualReturn, 1)}%` : NONE,
            volatility: has(summary.volatility) ? page.formatPercent(summary.volatility, 1) : NONE,
            sharpe: has(summary.sharpe) ? signed(summary.sharpe, 2) : NONE,
            expenseRatio: has(summary.expenseRatio) ? page.formatPercent(summary.expenseRatio, 2) : NONE,
            usShare: has(summary.usShare) ? page.formatPercent(summary.usShare, 1) : NONE
        };
    }

    /**
     * Wire the overview.
     * @param {HTMLElement} card - The overview card.
     * @param {Object|null} returns - The fund returns snapshot.
     * @param {Object|null} macro - The macro snapshot.
     * @param {Object|null} facts - The fund facts snapshot.
     * @param {Object|null} exposure - The fund exposure snapshot.
     * @returns {void}
     */
    function createView (card, returns, macro, facts, exposure) {
        const note = document.getElementById(NOTE_ID);
        const yearButtons = Array.from(card.querySelectorAll('[data-basket-overview-years]'));
        const pressed = yearButtons.find((button) => button.getAttribute('aria-pressed') === 'true');
        let years = pressed ? Number(pressed.dataset.basketOverviewYears) : DEFAULT_YEARS;

        /**
         * Write the figures for the basket and the window.
         * @returns {void}
         */
        function draw () {
            yearButtons.forEach((button) => button.setAttribute('aria-pressed', String(Number(button.dataset.basketOverviewYears) === years)));
            const basket = window.ResearchBasket ? window.ResearchBasket.get() : [];
            const summary = window.ResearchBasketMath.summary(returns, macro, facts, exposure, basket, years);
            const values = texts(summary);
            Object.keys(FIGURE_IDS).forEach((name) => {
                const node = document.getElementById(FIGURE_IDS[name]);
                if (node) {
                    node.textContent = basket.length === 0 ? NONE : values[name];
                }
            });
            const notes = [];
            if (basket.length > 0 && returns && summary.annualReturn === null) {
                notes.push(PFI18n.t('Research.Js.Risk.NoteShort', { span: page.formatYears(years) }));
            }
            if (basket.length > 0 && facts && summary.missingFees.length > 0) {
                notes.push(PFI18n.t('Research.Js.BasketOverview.MissingFees', { tickers: summary.missingFees.join(', ') }));
            }
            note.textContent = notes.join(' ');
            note.hidden = notes.length === 0;
        }

        yearButtons.forEach((button) => button.addEventListener('click', () => {
            years = Number(button.dataset.basketOverviewYears);
            draw();
            if (page.announce) {
                page.announce(PFI18n.t('Research.Js.BasketOverview.Announce', { span: page.formatYears(years) }));
            }
        }));
        document.addEventListener('research:basketchange', draw);
        draw();
    }

    /**
     * Wire the overview when its card, the basket figures and the page helpers are present.
     * @returns {void}
     */
    function init () {
        const card = document.getElementById(CARD_ID);
        if (!page || !card || !window.ResearchBasketMath) {
            return;
        }
        createView(card,
            page.readData('research-fund-returns-data', 'Research fund returns data'),
            page.readData('research-macro-data', 'Research macro data'),
            page.readData('research-fund-facts-data', 'Research fund facts data'),
            page.readData('research-fund-exposure-data', 'Research fund exposure data'));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

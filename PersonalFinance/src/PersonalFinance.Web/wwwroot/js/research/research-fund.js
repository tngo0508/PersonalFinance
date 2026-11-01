/* global PFI18n */
/**
 * Research Fund page, "Where the money goes": the fund's largest issuer countries, named in the page language, and a
 * toggle to the share of the fund in each SEC office. The countries come with the page; the office shares are summed
 * here from the fund industries snapshot, fetched once the first time the offices are shown: each industry's share goes
 * to its office, then the stocks SEC does not classify, bonds, and cash and other.
 *
 * Reads the card `#research-fund-money` (its `data-industries-url`) and the page's `data-research-fund` ticker.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const CARD_ID = 'research-fund-money';
    const OFFICES_ID = 'research-fund-offices';
    const STATUS_ID = 'research-fund-offices-status';
    const VIEW_ATTRIBUTE = 'data-fund-money-view';
    const PART_ATTRIBUTE = 'data-fund-money-part';

    /**
     * The share of one fund in each SEC office, then the parts SEC does not classify by office.
     * @param {Object} data - The fund industries snapshot.
     * @param {string} ticker - The fund's ticker.
     * @param {{unclassified: string, bonds: string, cash: string}} names - The names of the parts outside the offices.
     * @returns {{name: string, percent: number}[]|null} The offices largest first, then the other parts; null when the
     *     snapshot does not hold the fund.
     */
    function officeShares (data, ticker, names) {
        const wanted = ticker.toUpperCase();
        const fund = (data && Array.isArray(data.funds) ? data.funds : []).find((f) => f.ticker.toUpperCase() === wanted);
        if (!fund) {
            return null;
        }
        const offices = new Map();
        (fund.industries || []).forEach((i) => {
            const industry = data.industries[i.industry];
            if (industry) {
                offices.set(industry.office, (offices.get(industry.office) || 0) + i.percent);
            }
        });
        const rows = Array.from(offices.entries())
            .map(([office, percent]) => ({ name: data.offices[office] || '', percent }))
            .sort((a, b) => b.percent - a.percent || a.name.localeCompare(b.name));
        const sum = (items) => (items || []).reduce((total, item) => total + item.percent, 0);
        [
            { name: names.unclassified, percent: sum(fund.unclassified) },
            { name: names.bonds, percent: sum(fund.bonds) },
            { name: names.cash, percent: fund.cashAndOther || 0 }
        ].forEach((row) => {
            if (row.percent > 0) {
                rows.push(row);
            }
        });
        return rows;
    }

    /**
     * Name each issuer country in the page language, keeping the code where the browser has no name for it.
     * @param {HTMLElement} card - The "Where the money goes" card.
     * @returns {void}
     */
    function nameCountries (card) {
        if (typeof Intl === 'undefined' || typeof Intl.DisplayNames !== 'function') {
            return;
        }
        let names;
        try {
            names = new Intl.DisplayNames([document.documentElement.lang || 'en'], { type: 'region' });
        } catch (error) {
            return;
        }
        card.querySelectorAll('[data-country-code]').forEach((cell) => {
            const code = cell.getAttribute('data-country-code');
            let name = null;
            try {
                name = names.of(code);
            } catch (error) {
                name = null;
            }
            if (name && name !== code) {
                cell.textContent = name;
                cell.title = code;
            }
        });
    }

    /**
     * Draw the office rows: each name, a bar and its share.
     * @param {HTMLElement} list - The list to fill.
     * @param {{name: string, percent: number}[]} rows - The rows.
     * @returns {void}
     */
    function drawOffices (list, rows) {
        list.replaceChildren(...rows.map((row) => {
            const item = document.createElement('li');
            item.className = 'research-fund-money__item';
            const name = document.createElement('span');
            name.className = 'research-fund-money__name';
            name.textContent = row.name;
            const bar = document.createElement('span');
            bar.className = 'research-fund-money__bar';
            bar.setAttribute('aria-hidden', 'true');
            bar.style.setProperty('--research-share', String(Math.min(Math.max(row.percent, 0), 100) / 100));
            const value = document.createElement('span');
            value.className = 'research-fund-money__percent';
            value.textContent = page.formatPercent(row.percent, 1);
            item.append(name, bar, value);
            return item;
        }));
    }

    /**
     * Wire the countries/offices toggle.
     * @param {HTMLElement} card - The "Where the money goes" card.
     * @param {string} ticker - The fund's ticker.
     * @returns {void}
     */
    function wireMoney (card, ticker) {
        const buttons = Array.from(card.querySelectorAll(`[${VIEW_ATTRIBUTE}]`));
        const parts = Array.from(card.querySelectorAll(`[${PART_ATTRIBUTE}]`));
        const list = document.getElementById(OFFICES_ID);
        const status = document.getElementById(STATUS_ID);
        const url = card.getAttribute('data-industries-url');
        let loading = null;

        /**
         * Fetch the fund industries snapshot and draw the offices, once.
         * @returns {Promise<void>}
         */
        async function loadOffices () {
            if (!list || !status || !url) {
                return;
            }
            say(PFI18n.t('Research.Js.Fund.OfficesLoading'));
            let rows = null;
            try {
                const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
                rows = response.ok
                    ? officeShares(await response.json(), ticker, {
                        unclassified: PFI18n.t('Research.Js.SectorsBasket.Unclassified'),
                        bonds: PFI18n.t('Research.Js.SectorsBasket.Bonds'),
                        cash: PFI18n.t('Research.Js.SectorsBasket.Cash')
                    })
                    : null;
            } catch (error) {
                rows = null;
            }
            if (!rows) {
                say(PFI18n.t('Research.Js.Fund.OfficesFailed'));
                loading = null;
                return;
            }
            say(rows.length === 0 ? PFI18n.t('Research.Js.Fund.OfficesNone') : '');
            drawOffices(list, rows);
        }

        /**
         * Show a status line under the offices, or hide it when empty.
         * @param {string} text - The status.
         * @returns {void}
         */
        function say (text) {
            status.textContent = text;
            status.hidden = text === '';
        }

        /**
         * Show one view.
         * @param {string} view - "countries" or "offices".
         * @returns {void}
         */
        function show (view) {
            buttons.forEach((b) => b.setAttribute('aria-pressed', String(b.getAttribute(VIEW_ATTRIBUTE) === view)));
            parts.forEach((p) => {
                p.hidden = p.getAttribute(PART_ATTRIBUTE) !== view;
            });
            if (view === 'offices' && !loading) {
                loading = loadOffices();
            }
        }

        buttons.forEach((button) => button.addEventListener('click', () => show(button.getAttribute(VIEW_ATTRIBUTE))));
    }

    /**
     * Wire the page when its card and the page helpers are present.
     * @returns {void}
     */
    function init () {
        const card = document.getElementById(CARD_ID);
        const root = document.querySelector('[data-research-fund]');
        if (!page || !card || !root) {
            return;
        }
        nameCountries(card);
        wireMoney(card, root.getAttribute('data-research-fund'));
    }

    window.ResearchFund = { officeShares };

    if (typeof document !== 'undefined' && document.readyState) {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', init);
        } else {
            init();
        }
    }
})();

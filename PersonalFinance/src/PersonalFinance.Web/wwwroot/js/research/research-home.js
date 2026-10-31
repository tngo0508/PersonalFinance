/* global PFI18n */
/**
 * Research Home: the first-visit strip, the basket card and the starting mixes.
 *
 * - The strip ("New here?") shows until "Got it" is pressed; that is remembered in `localStorage`
 *   (`pf.research.homeSteps`). When the storage cannot be read or written the strip simply shows.
 * - The basket card is drawn from the Fund Basket in the address (`ResearchBasket.get()`): a chip per fund with its
 *   share and a remove button, and "Open basket report"; it redraws on every `research:basketchange`.
 * - A starting mix sets the whole basket. The mixes start hidden and show once checked against the search index the
 *   search box loads (`ResearchSearch.searchIndex`, so the page reads it only once): a mix whose funds are not all
 *   Tracked Funds stays hidden. When the index cannot be read every mix shows.
 * - A starter link with `data-research-home-industry` opens the plain Sectors page until the index names that
 *   industry's SEC office; then it opens the industry itself.
 */
(function () {
    'use strict';

    const STEPS_KEY = 'pf.research.homeSteps';
    const DISMISSED = 'dismissed';

    /**
     * Whether the strip was dismissed before; false when the storage cannot be read.
     * @returns {boolean} True when it was dismissed.
     */
    function stepsDismissed () {
        try {
            return window.localStorage.getItem(STEPS_KEY) === DISMISSED;
        } catch (error) {
            return false;
        }
    }

    /**
     * Remember that the strip was dismissed; a storage failure only means it shows again next time.
     * @returns {void}
     */
    function rememberDismissed () {
        try {
            window.localStorage.setItem(STEPS_KEY, DISMISSED);
        } catch (error) {
            // Nothing to keep it in: the strip shows on the next visit.
        }
    }

    /**
     * Show the strip unless it was dismissed, and hide it for good on "Got it".
     * @returns {void}
     */
    function wireSteps () {
        const steps = document.querySelector('[data-research-home-steps]');
        if (!steps) {
            return;
        }
        steps.hidden = stepsDismissed();
        const close = steps.querySelector('[data-research-home-steps-close]');
        if (!close) {
            return;
        }
        close.addEventListener('click', () => {
            rememberDismissed();
            steps.hidden = true;
            const search = document.querySelector('[data-research-search] [role="combobox"]');
            if (search) {
                search.focus();
            }
        });
    }

    /**
     * A fund's share for display: a whole percent when it is one, else one decimal.
     * @param {number} share - The share in percent.
     * @returns {string} E.g. "60%" or "33.3%".
     */
    function shareText (share) {
        const rounded = Math.round(share * 10) / 10;
        const digits = Number.isInteger(rounded) ? 0 : 1;
        return PFI18n.formatNumber(rounded / 100,
            { style: 'percent', minimumFractionDigits: digits, maximumFractionDigits: digits });
    }

    /**
     * Draw one fund's chip: its ticker linking to its Fund page, its share and a remove button.
     * @param {string} fundHref - The Fund pages' address with the app's base path, e.g. `/Research/Fund/`.
     * @param {{ticker: string, share: number}} fund - The fund.
     * @returns {HTMLLIElement} The chip.
     */
    function chip (fundHref, fund) {
        const item = document.createElement('li');
        item.className = 'research-home__chip';
        const link = document.createElement('a');
        link.className = 'research-home__chip-link';
        link.href = `${fundHref}${encodeURIComponent(fund.ticker)}`;
        link.setAttribute('data-research-link', '');
        link.textContent = fund.ticker;
        const share = document.createElement('span');
        share.className = 'research-home__chip-share';
        share.textContent = shareText(fund.share);
        const remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'research-home__chip-remove';
        remove.dataset.ticker = fund.ticker;
        remove.setAttribute('aria-label', PFI18n.t('Research.Js.Basket.RemoveLabel', { ticker: fund.ticker }));
        remove.textContent = '×';
        item.append(link, share, remove);
        return item;
    }

    /**
     * Wire the basket card: draw it now and on every change, remove a fund from its chip, and apply a starting mix.
     * @returns {void}
     */
    function wireBasket () {
        const card = document.querySelector('[data-research-home-basket]');
        const basket = window.ResearchBasket;
        if (!card || !basket) {
            return;
        }
        const empty = card.querySelector('[data-research-home-basket-empty]');
        const filled = card.querySelector('[data-research-home-basket-filled]');
        const chips = card.querySelector('[data-research-home-basket-chips]');
        const title = card.querySelector('h2');
        const fundHref = card.dataset.fundHref || '/Research/Fund/';
        if (!empty || !filled || !chips) {
            return;
        }

        /**
         * Draw the card from the basket.
         * @returns {void}
         */
        function draw () {
            const funds = basket.get();
            chips.replaceChildren(...funds.map((fund) => chip(fundHref, fund)));
            empty.hidden = funds.length > 0;
            filled.hidden = funds.length === 0;
            // The shell's own listener ran before the chips existed, so their links get the basket here.
            basket.linkify(card);
        }

        chips.addEventListener('click', (event) => {
            const button = event.target instanceof Element ? event.target.closest('.research-home__chip-remove') : null;
            if (!button || typeof basket.remove !== 'function') {
                return;
            }
            const buttons = Array.from(chips.querySelectorAll('.research-home__chip-remove'));
            const at = buttons.indexOf(button);
            basket.remove(button.dataset.ticker);
            // Keep focus in the card: on the chip that took this one's place, else the one before, else the title.
            const left = Array.from(chips.querySelectorAll('.research-home__chip-remove'));
            const next = left[Math.min(at, left.length - 1)];
            if (next) {
                next.focus();
            } else if (title) {
                title.focus();
            }
        });

        card.addEventListener('click', (event) => {
            const button = event.target instanceof Element ? event.target.closest('[data-research-preset]') : null;
            if (!button || typeof basket.set !== 'function') {
                return;
            }
            basket.set(button.dataset.researchPreset);
            const page = window.ResearchPage;
            if (page) {
                page.announce(PFI18n.t('Research.Js.Home.MixApplied', { name: button.dataset.researchPresetName }));
            }
        });

        document.addEventListener('research:basketchange', draw);
        draw();
    }

    /**
     * Show the starting mixes whose funds are all Tracked Funds (all of them when the index is missing), and the mixes'
     * part when any is shown.
     * @param {?Object} index - The search index, or null when it could not be read.
     * @returns {void}
     */
    function showMixes (index) {
        const mixes = document.querySelector('[data-research-home-mixes]');
        if (!mixes) {
            return;
        }
        const tracked = index ? new Set((index.funds || []).map((f) => String(f.ticker).toUpperCase())) : null;
        let shown = 0;
        mixes.querySelectorAll('[data-research-home-mix]').forEach((item) => {
            const button = item.querySelector('[data-research-preset]');
            const tickers = button
                ? button.dataset.researchPreset.split(',').map((part) => part.split(':')[0].toUpperCase())
                : [];
            item.hidden = tickers.length === 0 || (tracked !== null && !tickers.every((ticker) => tracked.has(ticker)));
            shown += item.hidden ? 0 : 1;
        });
        mixes.hidden = shown === 0;
    }

    /**
     * Point each industry starter at its industry, under the SEC office the index gives it. A starter whose industry
     * the index does not list keeps the plain Sectors page.
     * @param {Object} index - The search index.
     * @returns {void}
     */
    function linkIndustries (index) {
        const industries = index.industries || [];
        document.querySelectorAll('a[data-research-home-industry]').forEach((link) => {
            const wanted = link.dataset.researchHomeIndustry.toLowerCase();
            const found = industries.find((i) => typeof i.name === 'string' && i.name.toLowerCase() === wanted);
            if (!found) {
                return;
            }
            // The link already carries the basket; keep its path (with the app's base) and set the focus.
            const href = link.getAttribute('href');
            const path = href.split(/[?#]/)[0];
            const focus = found.office ? `${found.office}/${found.name}` : found.name;
            link.setAttribute('href', `${path}?focus=${encodeURIComponent(focus)}`);
            if (window.ResearchBasket) {
                window.ResearchBasket.linkify(link.parentNode);
            }
        });
    }

    /**
     * Read the search index once (shared with the search box), then show the mixes and link the industry starters.
     * @returns {Promise<void>} Resolves once done.
     */
    async function applyIndex () {
        const search = window.ResearchSearch;
        const box = document.querySelector('[data-research-search]');
        let index = null;
        try {
            if (search && typeof search.searchIndex === 'function') {
                index = await search.searchIndex(box ? box.dataset.indexUrl : undefined);
            }
        } catch (error) {
            index = null;
        }
        showMixes(index);
        if (index) {
            linkIndustries(index);
        }
    }

    /**
     * Wire the page.
     * @returns {void}
     */
    function init () {
        wireSteps();
        wireBasket();
        applyIndex();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

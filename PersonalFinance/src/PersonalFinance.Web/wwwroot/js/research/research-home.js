/* global PFI18n */
/**
 * Research Home: the first-visit strip, the basket card and the starting mixes.
 *
 * - The strip ("New here?") shows until "Got it" is pressed; that is remembered in `localStorage`
 *   (`pf.research.homeSteps`). When the storage cannot be read or written the strip simply shows.
 * - The basket card is drawn from the Fund Basket in the address (`ResearchBasket.get()`): a chip per fund with its
 *   share and a remove button, and "Open basket report"; it redraws on every `research:basketchange`.
 * - A starting mix sets the whole basket. A mix whose funds are not all Tracked Funds is hidden, judged from the search
 *   index the search box loads (`ResearchSearch.searchIndex`), so the page reads it only once; when the index cannot be
 *   read the mixes stay as they are.
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
     * @param {{ticker: string, share: number}} fund - The fund.
     * @returns {HTMLLIElement} The chip.
     */
    function chip (fund) {
        const item = document.createElement('li');
        item.className = 'research-home__chip';
        const link = document.createElement('a');
        link.className = 'research-home__chip-link';
        link.href = `/Research/Fund/${encodeURIComponent(fund.ticker)}`;
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
        if (!empty || !filled || !chips) {
            return;
        }

        /**
         * Draw the card from the basket.
         * @returns {void}
         */
        function draw () {
            const funds = basket.get();
            chips.replaceChildren(...funds.map(chip));
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
     * Hide each starting mix whose funds are not all in the search index's Tracked Funds, and the mixes' part when
     * none is left. When the index cannot be read the mixes stay.
     * @returns {Promise<void>} Resolves once the mixes are checked.
     */
    async function checkMixes () {
        const mixes = document.querySelector('[data-research-home-mixes]');
        const search = window.ResearchSearch;
        if (!mixes || !search || typeof search.searchIndex !== 'function') {
            return;
        }
        const box = document.querySelector('[data-research-search]');
        let index;
        try {
            index = await search.searchIndex(box ? box.dataset.indexUrl : undefined);
        } catch (error) {
            return;
        }
        const tracked = new Set(((index && index.funds) || []).map((f) => String(f.ticker).toUpperCase()));
        let shown = 0;
        mixes.querySelectorAll('[data-research-home-mix]').forEach((item) => {
            const button = item.querySelector('[data-research-preset]');
            const tickers = button
                ? button.dataset.researchPreset.split(',').map((part) => part.split(':')[0].toUpperCase())
                : [];
            item.hidden = tickers.length === 0 || !tickers.every((ticker) => tracked.has(ticker));
            shown += item.hidden ? 0 : 1;
        });
        mixes.hidden = shown === 0;
    }

    /**
     * Wire the page.
     * @returns {void}
     */
    function init () {
        wireSteps();
        wireBasket();
        checkMixes();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

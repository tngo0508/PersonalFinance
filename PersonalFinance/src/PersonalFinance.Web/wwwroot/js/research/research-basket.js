/**
 * Research page, the Fund Basket bar: one to five Tracked Funds the user picks on screen, each with a weight, viewed
 * together by the tabs that use a basket. It is never saved: it lives only in the page's address as
 * `?basket=VTI:60,BND:40` (or `?basket=VTI,BND` for equal weights), replaced on every edit without adding history.
 *
 * The bar sits between the tab bar and the panels and shows only on a tab whose panel carries `data-research-basket`.
 * Funds are picked from a searchable combobox (ticker or name) grouped by role; each fund is a chip with an optional
 * whole-percent weight. Weights that do not add up to 100% are scaled to 100%, with a note saying so; a fund set to
 * 0% is removed; "Use equal weights" goes back to equal shares. A link with untracked funds, repeats, bad weights or
 * more than five funds keeps what it can and says what it left out. The basket starts empty with the picker shown.
 *
 * Reads the Tracked Funds (ticker, name, role) from the Funds tab's JSON data island. Other scripts read the basket
 * through `window.ResearchBasket.get()` and hear changes on the `research:basketchange` document event; the link
 * parser and formatter are exposed as `parse` and `format`.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const DATA_ELEMENT_ID = 'research-funds-data';
    const BAR_ID = 'research-basket';
    const CHIPS_ID = 'research-basket-chips';
    const SEARCH_ID = 'research-basket-search';
    const OPTIONS_ID = 'research-basket-options';
    const NOTE_ID = 'research-basket-note';
    const EQUAL_ID = 'research-basket-equal';
    const NOTICE_ID = 'research-basket-notice';
    const PANEL_ATTRIBUTE = 'data-research-basket';
    const PARAM = 'basket';
    const MAX_FUNDS = 5;
    const NOTICE_INVALID = 'Some funds in this link aren\'t tracked and were left out.';
    const NOTICE_TOO_MANY = 'A Fund Basket holds up to five funds, so only the first five were kept.';
    const ENTRY = /^([A-Za-z0-9.-]{1,10})(?::(\d{1,3}))?$/;

    /**
     * Read a basket from the address value. Each entry is a ticker, optionally followed by `:` and a whole-percent
     * weight from 1 to 100. An untracked ticker, a repeat or a bad weight is left out; past five funds the rest is left
     * out. Weights count only when every kept fund has one; otherwise the funds share equally.
     * @param {string|null} value - The `basket` parameter, or null.
     * @param {string[]} trackedTickers - The Tracked Funds' tickers.
     * @returns {{funds: {ticker: string, weight: number|null}[], invalid: boolean, tooMany: boolean}} The basket and
     *     what was left out.
     */
    function parse (value, trackedTickers) {
        const tracked = new Map(trackedTickers.map((t) => [t.toUpperCase(), t]));
        const funds = [];
        let invalid = false;
        let tooMany = false;
        (value || '').split(',').map((part) => part.trim()).filter(Boolean).forEach((part) => {
            const match = ENTRY.exec(part);
            const ticker = match ? tracked.get(match[1].toUpperCase()) : undefined;
            const weight = match && match[2] !== undefined ? Number(match[2]) : null;
            if (!ticker || funds.some((f) => f.ticker === ticker) || (weight !== null && (weight < 1 || weight > 100))) {
                invalid = true;
                return;
            }
            if (funds.length === MAX_FUNDS) {
                tooMany = true;
                return;
            }
            funds.push({ ticker, weight });
        });
        if (funds.some((f) => f.weight === null)) {
            funds.forEach((f) => { f.weight = null; });
        }
        return { funds, invalid, tooMany };
    }

    /**
     * Write a basket as the address value: `VTI:60,BND:40` with weights, `VTI,BND` for equal shares, or null when
     * empty.
     * @param {{ticker: string, weight: number|null}[]} funds - The basket.
     * @returns {string|null} The parameter value.
     */
    function format (funds) {
        if (funds.length === 0) {
            return null;
        }
        const weighted = funds.every((f) => f.weight !== null);
        return funds.map((f) => (weighted ? `${f.ticker}:${f.weight}` : f.ticker)).join(',');
    }

    /**
     * Each fund's share of the basket in percent, adding up to 100: equal shares, or the weights scaled to 100.
     * @param {{ticker: string, weight: number|null}[]} funds - The basket.
     * @returns {number[]} The shares, in basket order.
     */
    function sharesOf (funds) {
        if (funds.length === 0) {
            return [];
        }
        if (funds.some((f) => f.weight === null)) {
            return funds.map(() => 100 / funds.length);
        }
        const total = funds.reduce((sum, f) => sum + f.weight, 0);
        return funds.map((f) => (total > 0 ? f.weight / total * 100 : 100 / funds.length));
    }

    /**
     * A share for display: a whole number when it is one, else one decimal.
     * @param {number} value - The share in percent.
     * @returns {string} E.g. "60%" or "33.3%".
     */
    function percent (value) {
        return Number.isInteger(Math.round(value * 10) / 10) ? `${Math.round(value)}%` : `${value.toFixed(1)}%`;
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
     * Wire the Fund Basket bar over the Tracked Funds.
     * @param {HTMLElement} bar - The bar element.
     * @param {{ticker: string, name: string, role: (string|null)}[]} tracked - The Tracked Funds, in list order.
     * @returns {{get: function(): Object[]}} The basket reader.
     */
    function createBasket (bar, tracked) {
        const chips = document.getElementById(CHIPS_ID);
        const search = document.getElementById(SEARCH_ID);
        const options = document.getElementById(OPTIONS_ID);
        const note = document.getElementById(NOTE_ID);
        const equal = document.getElementById(EQUAL_ID);
        const notice = document.getElementById(NOTICE_ID);
        const byTicker = new Map(tracked.map((f) => [f.ticker, f]));
        let funds = [];
        let activeIndex = -1;
        let visibleOptions = [];

        /**
         * The basket with each fund's details and share, for other scripts.
         * @returns {{ticker: string, name: string, role: (string|null), weight: (number|null), share: number}[]} The
         *     funds, in basket order.
         */
        function get () {
            const shares = sharesOf(funds);
            return funds.map((f, i) => ({ ...byTicker.get(f.ticker), weight: f.weight, share: shares[i] }));
        }

        /**
         * Keep the basket in the address (replacing the history entry) and tell the tabs.
         * @returns {void}
         */
        function publish () {
            if (window.history && typeof window.history.replaceState === 'function') {
                const url = new URL(window.location.href);
                const value = format(funds);
                if (value) {
                    url.searchParams.set(PARAM, value);
                } else {
                    url.searchParams.delete(PARAM);
                }
                window.history.replaceState(window.history.state, '', url);
            }
            document.dispatchEvent(new CustomEvent('research:basketchange', { detail: { funds: get() } }));
        }

        /**
         * Draw the chips, the weight note, the equal-weights control and the picker's state.
         * @returns {void}
         */
        function render () {
            const shares = sharesOf(funds);
            const weighted = funds.length > 0 && funds.every((f) => f.weight !== null);
            const total = weighted ? funds.reduce((sum, f) => sum + f.weight, 0) : 100;
            chips.replaceChildren(...funds.map((f, i) => {
                const fund = byTicker.get(f.ticker);
                const chip = element('li', 'research-basket__chip');
                chip.dataset.ticker = f.ticker;
                const label = element('span', 'research-basket__chip-label');
                label.append(element('strong', null, f.ticker), ` ${fund.name}`);
                const input = element('input', 'research-basket__weight');
                input.type = 'number';
                input.min = '0';
                input.max = '100';
                input.step = '1';
                input.inputMode = 'numeric';
                input.value = String(weighted ? f.weight : Math.round(shares[i]));
                input.setAttribute('aria-label', `Weight of ${f.ticker}, percent`);
                input.addEventListener('change', () => setWeight(f.ticker, input.value));
                const unit = element('span', 'research-basket__unit', '%');
                unit.setAttribute('aria-hidden', 'true');
                const remove = element('button', 'research-basket__remove', '×');
                remove.type = 'button';
                remove.setAttribute('aria-label', `Remove ${f.ticker} from the basket`);
                remove.addEventListener('click', () => removeFund(f.ticker, true));
                chip.append(label, input, unit);
                if (weighted && total !== 100) {
                    chip.append(element('span', 'research-basket__share', `= ${percent(shares[i])}`));
                }
                chip.append(remove);
                return chip;
            }));
            note.hidden = !(weighted && total !== 100);
            note.textContent = weighted && total !== 100
                ? `Weights add up to ${total}%, so each is scaled to make 100%.`
                : '';
            equal.hidden = !weighted;
            const full = funds.length >= MAX_FUNDS;
            search.disabled = full;
            search.placeholder = full
                ? 'The basket is full: remove a fund to add another'
                : (funds.length === 0 ? 'Add a fund: type a ticker or name' : 'Add another fund');
            bar.classList.toggle('research-basket--empty', funds.length === 0);
        }

        /**
         * Show a notice about what a link or an edit left out, or clear it.
         * @param {string} text - The notice, or empty to clear.
         * @returns {void}
         */
        function showNotice (text) {
            notice.textContent = text;
            notice.hidden = !text;
        }

        /**
         * Add a fund at an equal share (or at the average weight when weights are set).
         * @param {string} ticker - The fund's ticker.
         * @returns {void}
         */
        function addFund (ticker) {
            if (funds.length >= MAX_FUNDS || funds.some((f) => f.ticker === ticker) || !byTicker.has(ticker)) {
                return;
            }
            const weighted = funds.length > 0 && funds.every((f) => f.weight !== null);
            const weight = weighted ? Math.max(1, Math.round(100 / (funds.length + 1))) : null;
            funds.push({ ticker, weight });
            showNotice('');
            render();
            publish();
            if (page) {
                page.announce(`${ticker} added to the Fund Basket: ${funds.length} of ${MAX_FUNDS} funds.`);
            }
        }

        /**
         * Remove a fund.
         * @param {string} ticker - The fund's ticker.
         * @param {boolean} refocus - Whether to put focus back on the picker (after a remove button).
         * @returns {void}
         */
        function removeFund (ticker, refocus) {
            funds = funds.filter((f) => f.ticker !== ticker);
            render();
            publish();
            if (page) {
                page.announce(`${ticker} removed from the Fund Basket.`);
            }
            if (refocus) {
                search.focus();
            }
        }

        /**
         * Set one fund's weight from its input. Setting a weight turns equal shares into explicit weights (the others
         * keep the share they showed); 0 removes the fund; anything not a whole number from 0 to 100 is put back.
         * @param {string} ticker - The fund's ticker.
         * @param {string} raw - The input's value.
         * @returns {void}
         */
        function setWeight (ticker, raw) {
            const value = Number(raw);
            if (raw.trim() === '' || !Number.isInteger(value) || value < 0 || value > 100) {
                render();
                return;
            }
            if (value === 0) {
                removeFund(ticker, true);
                return;
            }
            const shares = sharesOf(funds);
            funds = funds.map((f, i) => ({
                ticker: f.ticker,
                weight: f.ticker === ticker ? value : (f.weight !== null ? f.weight : Math.round(shares[i]))
            }));
            render();
            publish();
        }

        /**
         * The Tracked Funds not in the basket that match the search, in list order.
         * @param {string} query - The search text.
         * @returns {Object[]} The matching funds.
         */
        function matches (query) {
            const q = query.trim().toLowerCase();
            return tracked.filter((f) => !funds.some((b) => b.ticker === f.ticker) &&
                (!q || f.ticker.toLowerCase().startsWith(q) || f.name.toLowerCase().includes(q)));
        }

        /**
         * Open the options for the current search, grouped by role.
         * @returns {void}
         */
        function openOptions () {
            visibleOptions = matches(search.value);
            activeIndex = visibleOptions.length > 0 ? 0 : -1;
            const groups = [];
            visibleOptions.forEach((fund, index) => {
                const role = fund.role || 'Other funds';
                let group = groups.find((g) => g.role === role);
                if (!group) {
                    group = { role, items: [] };
                    groups.push(group);
                }
                group.items.push({ fund, index });
            });
            options.replaceChildren(...groups.map((group, g) => {
                const block = element('div', 'research-basket__group');
                block.setAttribute('role', 'group');
                const heading = element('div', 'research-basket__group-label', group.role);
                heading.id = `${OPTIONS_ID}-group-${g}`;
                heading.setAttribute('role', 'presentation');
                block.setAttribute('aria-labelledby', heading.id);
                block.append(heading, ...group.items.map(({ fund, index }) => {
                    const option = element('div', 'research-basket__option');
                    option.id = `${OPTIONS_ID}-${fund.ticker}`;
                    option.setAttribute('role', 'option');
                    option.dataset.index = String(index);
                    option.append(element('strong', null, fund.ticker), ` ${fund.name}`);
                    option.addEventListener('mousedown', (event) => {
                        event.preventDefault();
                        choose(index);
                    });
                    return option;
                }));
                return block;
            }));
            if (visibleOptions.length === 0) {
                options.append(element('div', 'research-basket__empty', search.value.trim()
                    ? 'No tracked fund matches.'
                    : 'Every tracked fund is already in the basket.'));
            }
            options.hidden = false;
            search.setAttribute('aria-expanded', 'true');
            markActive();
        }

        /**
         * Close the options.
         * @returns {void}
         */
        function closeOptions () {
            options.hidden = true;
            search.setAttribute('aria-expanded', 'false');
            search.removeAttribute('aria-activedescendant');
            activeIndex = -1;
        }

        /**
         * Mark the active option for assistive technology and keep it in view.
         * @returns {void}
         */
        function markActive () {
            options.querySelectorAll('[role="option"]').forEach((option) => {
                const active = Number(option.dataset.index) === activeIndex;
                option.setAttribute('aria-selected', String(active));
                if (active) {
                    search.setAttribute('aria-activedescendant', option.id);
                    option.scrollIntoView({ block: 'nearest' });
                }
            });
            if (activeIndex < 0) {
                search.removeAttribute('aria-activedescendant');
            }
        }

        /**
         * Add the option at an index, then clear the search and close the options.
         * @param {number} index - The option's index in the visible options.
         * @returns {void}
         */
        function choose (index) {
            const fund = visibleOptions[index];
            if (!fund) {
                return;
            }
            addFund(fund.ticker);
            search.value = '';
            closeOptions();
        }

        search.addEventListener('input', openOptions);
        search.addEventListener('focus', () => {
            if (search.value) {
                openOptions();
            }
        });
        search.addEventListener('blur', closeOptions);
        search.addEventListener('keydown', (event) => {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault();
                if (options.hidden) {
                    openOptions();
                    return;
                }
                const count = visibleOptions.length;
                if (count > 0) {
                    activeIndex = (activeIndex + (event.key === 'ArrowDown' ? 1 : -1) + count) % count;
                    markActive();
                }
            } else if (event.key === 'Enter' && !options.hidden && activeIndex >= 0) {
                event.preventDefault();
                choose(activeIndex);
            } else if (event.key === 'Escape' && !options.hidden) {
                event.preventDefault();
                closeOptions();
            }
        });
        equal.addEventListener('click', () => {
            funds = funds.map((f) => ({ ticker: f.ticker, weight: null }));
            render();
            publish();
        });

        // Start from the address: keep what is valid and say what was left out.
        const fromLink = parse(new URL(window.location.href).searchParams.get(PARAM), tracked.map((f) => f.ticker));
        funds = fromLink.funds;
        showNotice([fromLink.invalid ? NOTICE_INVALID : '', fromLink.tooMany ? NOTICE_TOO_MANY : ''].filter(Boolean).join(' '));
        render();
        if (fromLink.invalid || fromLink.tooMany) {
            publish();
        }
        return { get };
    }

    /**
     * Show the bar only on a tab whose panel uses the basket.
     * @param {HTMLElement} bar - The bar element.
     * @param {string|null} tabId - The selected tab's id.
     * @returns {void}
     */
    function showFor (bar, tabId) {
        const panel = tabId ? document.getElementById(`research-panel-${tabId}`) : null;
        bar.hidden = !(panel && panel.hasAttribute(PANEL_ATTRIBUTE));
    }

    /**
     * Wire the bar when it and the Tracked Funds are on the page.
     * @returns {void}
     */
    function init () {
        const bar = document.getElementById(BAR_ID);
        const data = page ? page.readData(DATA_ELEMENT_ID, 'Research funds data') : null;
        if (!bar || !data || !Array.isArray(data.funds) || data.funds.length === 0) {
            return;
        }
        const tracked = data.funds.map((f) => ({ ticker: f.ticker, name: f.name, role: f.role || null }));
        const basket = createBasket(bar, tracked);
        window.ResearchBasket.get = basket.get;
        const selected = document.querySelector('.research [role="tab"][aria-selected="true"]');
        showFor(bar, selected ? selected.dataset.researchTab : null);
        document.addEventListener('research:tabchange', (event) => showFor(bar, event.detail.tabId));
    }

    // The parser and formatter are available at once, and get() once the bar is wired.
    window.ResearchBasket = { parse, format, sharesOf, get: () => [] };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

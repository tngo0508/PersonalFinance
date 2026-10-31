/* global PFI18n */
/**
 * Research search box: one WAI-ARIA combobox for the Tracked Funds, the SEC offices and industries, the countries and
 * any SEC filer. Each `[data-research-search]` holds an `input[role="combobox"]` and its `[role="listbox"]`.
 *
 * The search index (`data-index-url`, `{ funds, offices, industries, countries }`) loads once, on the first focus of
 * any box, and is matched as the user types. Companies come from the company suggestions (`data-suggest-url`), asked
 * 200 ms after typing stops for two characters or more; only the latest answer is shown, so a slow one cannot replace
 * the results for newer text. Results are grouped Funds, Companies, Sectors, Industries, Countries. Arrow keys move
 * through them, Enter opens one (or the Fund page of a typed Tracked Fund ticker), Escape closes the list, and `/`
 * focuses the first box unless the user is typing in a field. Every navigation carries the Fund Basket.
 *
 * `match` and `latestOnly` need no page, so they are exposed on `window.ResearchSearch` for the tests.
 */
(function () {
    'use strict';

    const MAX_RESULTS = 8;
    const SUGGEST_DELAY_MS = 200;
    const SUGGEST_MIN_LENGTH = 2;
    const KIND_ORDER = ['fund', 'company', 'office', 'industry', 'country'];
    const GROUP_KEYS = {
        fund: 'Research.Js.Search.Funds',
        company: 'Research.Js.Search.Companies',
        office: 'Research.Js.Search.Sectors',
        industry: 'Research.Js.Search.Industries',
        country: 'Research.Js.Search.Countries'
    };

    /**
     * Find what the search index holds for a query: a case-insensitive part of a fund's ticker or name, an office, an
     * industry or a country. Funds come first (an exact ticker before the rest), then offices, industries and
     * countries, at most eight in all.
     * @param {{funds: ?Object[], offices: ?string[], industries: ?Object[], countries: ?string[]}} index - The search
     *     index; a missing part is skipped.
     * @param {string} query - The typed text.
     * @returns {{kind: string, label: string, href: string}[]} The results, each with its Research page.
     */
    function match (index, query) {
        const q = (query || '').trim().toLowerCase();
        if (!q || !index) {
            return [];
        }
        const has = (text) => typeof text === 'string' && text.toLowerCase().includes(q);
        const funds = (index.funds || []).filter((f) => has(f.ticker) || has(f.name));
        funds.sort((a, b) => Number(b.ticker.toLowerCase() === q) - Number(a.ticker.toLowerCase() === q));
        const results = [
            ...funds.map((f) => ({
                kind: 'fund', label: `${f.ticker} · ${f.name}`, href: `/Research/Fund/${encodeURIComponent(f.ticker)}`
            })),
            ...(index.offices || []).filter(has).map((office) => ({
                kind: 'office', label: office, href: `/Research/Sectors?focus=${encodeURIComponent(office)}`
            })),
            ...(index.industries || []).filter((i) => has(i.name)).map((i) => ({
                kind: 'industry',
                label: i.name,
                href: `/Research/Sectors?focus=${encodeURIComponent(`${i.office}/${i.name}`)}`
            })),
            ...(index.countries || []).filter(has).map((country) => ({
                kind: 'country', label: country, href: `/Research/World?country=${encodeURIComponent(country)}`
            }))
        ];
        return results.slice(0, MAX_RESULTS);
    }

    /**
     * Wrap an async function so only its latest call's answer counts: an earlier call resolves to null once a newer
     * one has started, whichever answers first.
     * @param {function(...*): Promise<*>} fn - The async function.
     * @returns {function(...*): Promise<*>} The wrapped function.
     */
    function latestOnly (fn) {
        let latest = 0;
        return async function (...args) {
            latest += 1;
            const call = latest;
            try {
                const value = await fn.apply(this, args);
                return call === latest ? value : null;
            } catch (error) {
                if (call === latest) {
                    throw error;
                }
                return null;
            }
        };
    }

    let indexRequest = null;

    /**
     * Read the search index.
     * @param {string} url - The index's address.
     * @returns {Promise<Object>} The index; rejects when it cannot be read.
     */
    async function readIndex (url) {
        const response = await fetch(url, { headers: { Accept: 'application/json' } });
        if (!response.ok) {
            throw new Error(`Search index answered ${response.status}`);
        }
        return response.json();
    }

    /**
     * Load the search index once for every box on the page; a failed read is tried again on the next focus.
     * @param {string} url - The index's address.
     * @returns {Promise<Object>} The index; rejects when it cannot be read.
     */
    async function loadIndex (url) {
        if (!indexRequest) {
            indexRequest = readIndex(url);
        }
        try {
            return await indexRequest;
        } catch (error) {
            indexRequest = null;
            throw error;
        }
    }

    /**
     * Go to a Research page, carrying the Fund Basket.
     * @param {string} href - The page.
     * @returns {void}
     */
    function go (href) {
        const basket = window.ResearchBasket;
        window.location.assign(basket && typeof basket.withBasket === 'function'
            ? basket.withBasket(href, basket.get())
            : href);
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
     * Wire one search box.
     * @param {HTMLElement} box - The `[data-research-search]` element.
     * @returns {void}
     */
    function wire (box) {
        const input = box.querySelector('[role="combobox"]');
        const listbox = box.querySelector('[role="listbox"]');
        if (!input || !listbox) {
            return;
        }
        const indexUrl = box.dataset.indexUrl || '/Research/SearchIndex';
        const suggestUrl = box.dataset.suggestUrl || '/Research/Suggest';
        let index = null;
        let indexState = 'idle';
        let indexLoad = null;
        let companies = { query: '', items: [] };
        let results = [];
        let activeIndex = -1;
        let suggestTimer = null;

        const suggest = latestOnly(async (query) => {
            const response = await fetch(`${suggestUrl}?q=${encodeURIComponent(query)}`,
                { headers: { Accept: 'application/json' } });
            if (!response.ok) {
                return [];
            }
            const items = await response.json();
            return Array.isArray(items) ? items : [];
        });

        /**
         * Load the index on first focus (again after a failure); the list redraws when it arrives or fails.
         * @returns {void}
         */
        function ensureIndex () {
            if (!indexLoad) {
                indexState = 'loading';
                indexLoad = applyIndex();
            }
        }

        /**
         * Take the index in, or note it failed so the next focus tries again, and redraw an open list.
         * @returns {Promise<void>} Resolves once the index is in or has failed.
         */
        async function applyIndex () {
            let loaded = null;
            try {
                loaded = await loadIndex(indexUrl);
            } catch (error) {
                loaded = null;
            }
            index = loaded;
            indexState = loaded ? 'ready' : 'failed';
            if (!loaded) {
                indexLoad = null;
            }
            if (!listbox.hidden) {
                draw();
            }
        }

        /**
         * The company suggestions for the current text, without the Tracked Funds the index already lists.
         * @param {string} query - The trimmed text.
         * @returns {{kind: string, label: string, href: string}[]} The company results.
         */
        function companyResults (query) {
            if (companies.query !== query) {
                return [];
            }
            const tracked = new Set(((index && index.funds) || []).map((f) => f.ticker.toUpperCase()));
            return companies.items
                .filter((item) => item && item.ticker && !tracked.has(item.ticker.toUpperCase()))
                .map((item) => ({
                    kind: 'company',
                    label: item.name ? `${item.ticker} · ${item.name}` : item.ticker,
                    href: `/Research/Company/${encodeURIComponent(item.ticker)}`
                }));
        }

        /**
         * Draw the results for the box's text, grouped by kind, or the loading, failed or no-match line.
         * @returns {void}
         */
        function draw () {
            const query = input.value.trim();
            if (!query) {
                close();
                return;
            }
            const found = [...match(index, query), ...companyResults(query)];
            results = KIND_ORDER.flatMap((kind) => found.filter((r) => r.kind === kind));
            activeIndex = Math.min(activeIndex, results.length - 1);
            const groups = KIND_ORDER.map((kind) => ({ kind, items: results.filter((r) => r.kind === kind) }))
                .filter((group) => group.items.length > 0);
            listbox.replaceChildren(...groups.map((group) => {
                const block = element('div', 'research-search__group');
                block.setAttribute('role', 'group');
                const heading = element('div', 'research-search__group-label', PFI18n.t(GROUP_KEYS[group.kind]));
                heading.id = `${listbox.id}-${group.kind}`;
                heading.setAttribute('role', 'presentation');
                block.setAttribute('aria-labelledby', heading.id);
                block.append(heading, ...group.items.map((result) => {
                    const position = results.indexOf(result);
                    const option = element('div', 'research-search__option', result.label);
                    option.id = `${listbox.id}-option-${position}`;
                    option.setAttribute('role', 'option');
                    option.dataset.index = String(position);
                    // Keep focus in the box: a mouse press must not blur the input before the click.
                    option.addEventListener('mousedown', (event) => event.preventDefault());
                    option.addEventListener('click', () => go(result.href));
                    return option;
                }));
                return block;
            }));
            if (results.length === 0) {
                const text = indexState === 'loading' || indexState === 'idle'
                    ? PFI18n.t('Research.Js.Search.Loading')
                    : PFI18n.t('Research.Js.Search.NoMatch', { query });
                listbox.append(element('div', 'research-search__empty', text));
            }
            if (indexState === 'failed') {
                listbox.append(element('div', 'research-search__empty', PFI18n.t('Research.Js.Search.Failed')));
            }
            listbox.hidden = false;
            input.setAttribute('aria-expanded', 'true');
            markActive();
        }

        /**
         * Close the list.
         * @returns {void}
         */
        function close () {
            listbox.hidden = true;
            listbox.replaceChildren();
            input.setAttribute('aria-expanded', 'false');
            input.removeAttribute('aria-activedescendant');
            results = [];
            activeIndex = -1;
        }

        /**
         * Mark the active option for assistive technology and keep it in view.
         * @returns {void}
         */
        function markActive () {
            listbox.querySelectorAll('[role="option"]').forEach((option) => {
                const active = Number(option.dataset.index) === activeIndex;
                option.setAttribute('aria-selected', String(active));
                if (active) {
                    input.setAttribute('aria-activedescendant', option.id);
                    option.scrollIntoView({ block: 'nearest' });
                }
            });
            if (activeIndex < 0) {
                input.removeAttribute('aria-activedescendant');
            }
        }

        /**
         * Ask for company suggestions once typing pauses; only the latest answer is kept.
         * @returns {void}
         */
        function scheduleSuggest () {
            window.clearTimeout(suggestTimer);
            const query = input.value.trim();
            if (query.length < SUGGEST_MIN_LENGTH) {
                return;
            }
            suggestTimer = window.setTimeout(async () => {
                let items;
                try {
                    items = await suggest(query);
                } catch (error) {
                    // Company search being down leaves the funds, sectors and countries.
                    items = [];
                }
                if (items === null) {
                    return;
                }
                companies = { query, items };
                if (input.value.trim() === query && document.activeElement === input) {
                    draw();
                }
            }, SUGGEST_DELAY_MS);
        }

        /**
         * Open what Enter means: the active result, else the Fund page of a typed Tracked Fund ticker, else the first
         * result.
         * @returns {boolean} Whether there was somewhere to go.
         */
        function submit () {
            const query = input.value.trim().toUpperCase();
            const fund = ((index && index.funds) || []).find((f) => f.ticker.toUpperCase() === query);
            const target = activeIndex >= 0 ? results[activeIndex]
                : (fund ? { href: `/Research/Fund/${encodeURIComponent(fund.ticker)}` } : results[0]);
            if (!target) {
                return false;
            }
            go(target.href);
            return true;
        }

        input.addEventListener('focus', () => {
            ensureIndex();
            if (input.value.trim()) {
                draw();
            }
        });
        input.addEventListener('input', () => {
            activeIndex = -1;
            draw();
            scheduleSuggest();
        });
        input.addEventListener('blur', close);
        input.addEventListener('keydown', (event) => {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault();
                if (listbox.hidden) {
                    draw();
                    return;
                }
                const count = results.length;
                if (count > 0) {
                    activeIndex = (activeIndex + (event.key === 'ArrowDown' ? 1 : -1) + count) % count;
                    markActive();
                }
            } else if (event.key === 'Enter') {
                if (submit()) {
                    event.preventDefault();
                }
            } else if (event.key === 'Escape' && !listbox.hidden) {
                event.preventDefault();
                close();
            }
        });
    }

    /**
     * Wire every search box, and `/` to focus the first one.
     * @returns {void}
     */
    function init () {
        const boxes = Array.from(document.querySelectorAll('[data-research-search]'));
        boxes.forEach(wire);
        if (boxes.length === 0) {
            return;
        }
        document.addEventListener('keydown', (event) => {
            if (event.key !== '/' || event.ctrlKey || event.metaKey || event.altKey) {
                return;
            }
            const target = event.target;
            if (target instanceof Element &&
                (target.closest('input, textarea, select, [contenteditable=""], [contenteditable="true"]'))) {
                return;
            }
            const input = boxes.map((box) => box.querySelector('[role="combobox"]')).find(Boolean);
            if (input) {
                event.preventDefault();
                input.focus();
            }
        });
    }

    window.ResearchSearch = { match, latestOnly };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

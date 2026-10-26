/* global PFI18n */
/**
 * Research page shell: the WAI-ARIA tabs, and in each tab panel a time scrubber (play/pause, a range slider and the
 * period label), a polite live region and the "Show as table" view. Each tab keeps its own scrubber position.
 *
 * A tab's chart registers itself once it is drawn:
 *
 *     window.ResearchPage.registerTab('sectors', {
 *         periods: ['2011', ..., '2025'],   // period labels, oldest first
 *         transitionMs: 750,                // how long one animated update takes
 *         playLabel: 'Play through the years', // optional: the play button's label while paused
 *         update (index, { animate }) {},   // show period `index`; returns a one-sentence summary to announce
 *         table (index) {}                  // { caption, columns: [{ label, numeric }], rows: [[cell, ...]] }
 *     });
 *
 * The tab panel `research-panel-<tabId>` holds the markup this module wires: `[data-research-scrubber]` (with
 * `[data-research-play]`, `[data-research-range]` and `[data-research-period]`), `[data-research-table-toggle]` and
 * `[data-research-table]`. The page opens each tab on its latest period. Under prefers-reduced-motion every change
 * applies at once.
 *
 * It also holds the helpers every chart shares (prefersReducedMotion, formatMoney, formatPercent, formatFixed,
 * readData, createTooltip, announce), so it must load before the chart scripts. Numbers follow the page culture
 * (PFI18n); the text is the page's Research.Js. strings.
 *
 * The selected tab is kept in the address as `?tab=<tabId>` (replacing the history entry, so Back does not step
 * through tabs), and each selection fires a `research:tabchange` event on the document with `detail.tabId`.
 */
(function () {
    'use strict';

    const MIN_STEP_MS = 450;
    const STEP_GAP_MS = 150;
    const LIVE_ID = 'research-live';

    const tabs = new Map();
    let playTimer = null;
    let playingTab = null;

    /**
     * Whether the user asked the operating system to minimise motion.
     * @returns {boolean} True when changes should apply without a transition.
     */
    function prefersReducedMotion () {
        return typeof window.matchMedia === 'function' &&
            window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    /**
     * Format a US$ amount compactly for labels, tooltips and table views: $3.02T, $412.5B, $18.0M.
     * @param {number} amount - The amount in US$.
     * @returns {string} The formatted amount.
     */
    function formatMoney (amount) {
        const absolute = Math.abs(amount);
        if (absolute >= 1e12) {
            return PFI18n.formatCompactMoney(amount, 'USD', { fractionDigits: 2 });
        }
        if (absolute >= 1e6) {
            return PFI18n.formatCompactMoney(amount, 'USD', { fractionDigits: 1 });
        }
        return PFI18n.formatMoney(amount, 'USD', { whole: true });
    }

    /**
     * Format a percentage in the page culture with a fixed number of decimals: 12.3% (en), 12,3% (vi).
     * @param {number} value - The percentage, e.g. 12.3 for 12.3%.
     * @param {number} digits - Decimals to show.
     * @returns {string} The formatted percentage.
     */
    function formatPercent (value, digits) {
        return PFI18n.formatNumber(value / 100,
            { style: 'percent', minimumFractionDigits: digits, maximumFractionDigits: digits });
    }

    /**
     * Format a month in the page culture: "June 2026" (en), "tháng 6 năm 2026" (vi).
     * @param {string} isoDate - Any day of the month, e.g. "2026-06-01".
     * @returns {string} The month.
     */
    function formatMonth (isoDate) {
        const [year, month] = isoDate.split('-').map(Number);
        return PFI18n.formatDate(new Date(Date.UTC(year, month - 1, 1)), { month: 'long', year: 'numeric', timeZone: 'UTC' });
    }

    /**
     * Format a day as the server's Research views do ("d MMMM yyyy"): day, the culture's month name, year:
     * "31 December 2025" (en), "31 tháng 12 2025" (vi).
     * @param {string} isoDate - The day, e.g. "2025-12-31".
     * @returns {string} The day.
     */
    function formatDay (isoDate) {
        const [year, month, day] = isoDate.split('-').map(Number);
        const monthName = PFI18n.formatDate(new Date(Date.UTC(year, month - 1, 1)), { month: 'long', timeZone: 'UTC' });
        return `${day} ${monthName} ${year}`;
    }

    /**
     * A list in words in the page language: "SPY, QQQ and VTI" (en), "SPY, QQQ và VTI" (vi).
     * @param {string[]} items - The items.
     * @returns {string} The list.
     */
    function formatList (items) {
        if (typeof Intl !== 'undefined' && typeof Intl.ListFormat === 'function') {
            return new Intl.ListFormat(PFI18n.lang, { style: 'long', type: 'conjunction' }).format(items);
        }
        return items.join(', ');
    }

    /**
     * A span of whole years in words: "1 year", "3 years".
     * @param {number} years - The number of years.
     * @returns {string} The span.
     */
    function formatYears (years) {
        return PFI18n.plural('Research.Js.Common.Years', years);
    }

    /**
     * Format a plain number in the page culture with a fixed number of decimals (0.85, 0,85).
     * @param {number} value - The number.
     * @param {number} digits - Decimals to show.
     * @returns {string} The formatted number.
     */
    function formatFixed (value, digits) {
        return PFI18n.formatNumber(value, { minimumFractionDigits: digits, maximumFractionDigits: digits });
    }

    /**
     * Parse a tab's JSON data island.
     * @param {string} elementId - The id of the `<script type="application/json">` element.
     * @param {string} label - What the data is, for the console warning, e.g. 'Research World data'.
     * @returns {Object|null} The camelCase snapshot, or null when it is missing or malformed.
     */
    function readData (elementId, label) {
        const element = document.getElementById(elementId);
        if (!element) {
            return null;
        }
        try {
            return JSON.parse(element.textContent || 'null');
        } catch (error) {
            // eslint-disable-next-line no-console
            console.warn(`${label} could not be parsed:`, error);
            return null;
        }
    }

    /**
     * Create a tooltip controller that shows the tooltip element beside the pointer.
     * @param {HTMLElement|null} tooltip - The tooltip element.
     * @param {function(function(string, string): HTMLSpanElement, ...*): (Node[]|null)} render - Builds the content
     *     from a `line(text, className)` span factory and the arguments given to show() after the event; null leaves
     *     the tooltip as it is.
     * @returns {{show: function(PointerEvent, ...*): void, hide: function(): void}} The controller.
     */
    function createTooltip (tooltip, render) {
        const line = (text, className) => {
            const span = document.createElement('span');
            span.className = className;
            span.textContent = text;
            return span;
        };

        const show = (event, ...args) => {
            if (!tooltip) {
                return;
            }
            const parts = render(line, ...args);
            if (!parts) {
                return;
            }
            tooltip.replaceChildren(...parts);
            tooltip.hidden = false;
            const x = Math.min(event.clientX + 14, window.innerWidth - tooltip.offsetWidth - 8);
            const y = Math.min(event.clientY + 14, window.innerHeight - tooltip.offsetHeight - 8);
            tooltip.style.left = `${Math.max(8, x)}px`;
            tooltip.style.top = `${Math.max(8, y)}px`;
        };

        const hide = () => {
            if (tooltip) {
                tooltip.hidden = true;
            }
        };

        return { show, hide };
    }

    /**
     * Put a sentence in the page's polite live region.
     * @param {string} message - The sentence to announce.
     * @returns {void}
     */
    function announce (message) {
        const live = document.getElementById(LIVE_ID);
        if (live && message) {
            live.textContent = message;
        }
    }

    /**
     * Draw the tab's table for its current period, or empty it when the table is off.
     * @param {Object} tab - The registered tab.
     * @returns {void}
     */
    function renderTable (tab) {
        const host = tab.tableHost;
        if (!host) {
            return;
        }
        host.hidden = !tab.tableOn;
        if (!tab.tableOn) {
            host.replaceChildren();
            return;
        }
        const model = tab.chart.table(tab.position);
        const table = document.createElement('table');
        table.className = 'research-table';
        const caption = document.createElement('caption');
        caption.textContent = model.caption;
        const headRow = document.createElement('tr');
        model.columns.forEach((column) => {
            const th = document.createElement('th');
            th.scope = 'col';
            th.textContent = column.label;
            if (column.numeric) {
                th.className = 'research-table__num';
            }
            headRow.append(th);
        });
        const head = document.createElement('thead');
        head.append(headRow);
        const body = document.createElement('tbody');
        model.rows.forEach((cells) => {
            const row = document.createElement('tr');
            cells.forEach((cell, index) => {
                const td = document.createElement('td');
                td.textContent = cell;
                if (model.columns[index] && model.columns[index].numeric) {
                    td.className = 'research-table__num';
                }
                row.append(td);
            });
            body.append(row);
        });
        table.append(caption, head, body);
        const wrap = document.createElement('div');
        wrap.className = 'research-table-wrap';
        wrap.append(table);
        host.replaceChildren(wrap);
    }

    /**
     * Show the play or pause state on a tab's play button.
     * @param {Object} tab - The registered tab.
     * @returns {void}
     */
    function syncPlayButton (tab) {
        if (!tab.play) {
            return;
        }
        const playing = playingTab === tab;
        tab.play.dataset.state = playing ? 'playing' : 'paused';
        tab.play.setAttribute('aria-label', playing
            ? PFI18n.t('Research.Js.Page.Pause')
            : (tab.chart.playLabel || PFI18n.t('Research.Js.Page.PlayYears')));
    }

    /**
     * Move a tab to a period: the chart, the slider, the period label and the table follow.
     * @param {Object} tab - The registered tab.
     * @param {number} index - The period index; clamped to the tab's periods.
     * @param {{announce: boolean}} options - Whether to announce the change.
     * @returns {void}
     */
    function setPosition (tab, index, options) {
        const last = tab.chart.periods.length - 1;
        tab.position = Math.max(0, Math.min(last, index));
        const label = tab.chart.periods[tab.position];
        tab.summary = tab.chart.update(tab.position, { animate: tab.drawn && !prefersReducedMotion() });
        tab.drawn = true;
        if (tab.range) {
            tab.range.value = String(tab.position);
            tab.range.setAttribute('aria-valuetext', label);
        }
        if (tab.period) {
            tab.period.textContent = label;
        }
        renderTable(tab);
        if (options.announce) {
            announce(tab.summary);
        }
    }

    /**
     * Stop playing, wherever it is playing.
     * @returns {void}
     */
    function stopPlay () {
        if (!playingTab) {
            return;
        }
        const tab = playingTab;
        clearInterval(playTimer);
        playTimer = null;
        playingTab = null;
        syncPlayButton(tab);
    }

    /**
     * Play the tab through its periods, one step at a time, stopping at the latest. From the latest it starts over.
     * @param {Object} tab - The registered tab.
     * @returns {void}
     */
    function startPlay (tab) {
        stopPlay();
        const last = tab.chart.periods.length - 1;
        if (tab.position >= last) {
            setPosition(tab, 0, { announce: false });
        }
        const step = prefersReducedMotion()
            ? MIN_STEP_MS
            : Math.max(MIN_STEP_MS, (tab.chart.transitionMs || 0) + STEP_GAP_MS);
        playingTab = tab;
        playTimer = setInterval(() => {
            const next = tab.position + 1;
            setPosition(tab, next, { announce: false });
            if (next >= last) {
                stopPlay();
                announce(tab.summary);
            }
        }, step);
        syncPlayButton(tab);
    }

    /**
     * Keep the selected tab in the address, replacing the current history entry. A ticker belongs to the Companies
     * tab only, so it is dropped on any other tab.
     * @param {string} tabId - The selected tab's id.
     * @returns {void}
     */
    function rememberTab (tabId) {
        if (!window.history || typeof window.history.replaceState !== 'function') {
            return;
        }
        const url = new URL(window.location.href);
        url.searchParams.set('tab', tabId);
        if (tabId !== 'companies') {
            url.searchParams.delete('ticker');
        }
        window.history.replaceState(window.history.state, '', url);
    }

    /**
     * Select a tab: show its panel, hide the others, and stop any play.
     * @param {HTMLElement} button - The tab button to select.
     * @param {boolean} focus - Whether to move focus to it.
     * @param {boolean} [byUser] - True when the user chose it: the address and listeners follow.
     * @returns {void}
     */
    function selectTab (button, focus, byUser) {
        const list = button.closest('[role="tablist"]');
        if (!list) {
            return;
        }
        stopPlay();
        list.querySelectorAll('[role="tab"]').forEach((tab) => {
            const selected = tab === button;
            tab.setAttribute('aria-selected', String(selected));
            tab.tabIndex = selected ? 0 : -1;
            const panel = document.getElementById(tab.getAttribute('aria-controls'));
            if (panel) {
                panel.hidden = !selected;
            }
        });
        if (focus) {
            button.focus();
        }
        const tabId = button.dataset.researchTab;
        if (byUser && tabId) {
            rememberTab(tabId);
            document.dispatchEvent(new CustomEvent('research:tabchange', { detail: { tabId } }));
        }
    }

    /**
     * Select a tab by its id, as if the user chose it.
     * @param {string} tabId - The tab's id, e.g. 'funds'.
     * @returns {void}
     */
    function selectTabById (tabId) {
        const button = document.querySelector(`.research [role="tab"][data-research-tab="${tabId}"]`);
        if (button && button.getAttribute('aria-selected') !== 'true') {
            selectTab(button, false, true);
        }
    }

    /**
     * Wire a tablist: click selects, arrow keys, Home and End move between tabs and select them.
     * @param {HTMLElement} list - The tablist element.
     * @returns {void}
     */
    function initTablist (list) {
        const buttons = () => Array.from(list.querySelectorAll('[role="tab"]'));
        list.addEventListener('click', (event) => {
            const button = event.target.closest('[role="tab"]');
            if (button) {
                selectTab(button, false, true);
            }
        });
        list.addEventListener('keydown', (event) => {
            const all = buttons();
            const index = all.indexOf(document.activeElement);
            if (index < 0) {
                return;
            }
            const moves = {
                ArrowRight: (index + 1) % all.length,
                ArrowLeft: (index - 1 + all.length) % all.length,
                Home: 0,
                End: all.length - 1
            };
            if (event.key in moves) {
                event.preventDefault();
                selectTab(all[moves[event.key]], true, true);
            }
        });
        const selected = buttons().find((b) => b.getAttribute('aria-selected') === 'true') || buttons()[0];
        if (selected) {
            selectTab(selected, false);
        }
    }

    /**
     * Register a tab's chart and wire its scrubber and table. The tab opens on its latest period.
     * @param {string} tabId - The tab's id, e.g. 'sectors'; its panel is `research-panel-<tabId>`.
     * @param {Object} chart - The chart: periods, transitionMs, update(index, options) and table(index).
     * @returns {void}
     */
    function registerTab (tabId, chart) {
        const panel = document.getElementById(`research-panel-${tabId}`);
        if (!panel || !chart || !Array.isArray(chart.periods) || chart.periods.length === 0) {
            return;
        }
        const tab = {
            id: tabId,
            chart,
            position: chart.periods.length - 1,
            drawn: false,
            summary: '',
            tableOn: false,
            play: panel.querySelector('[data-research-play]'),
            range: panel.querySelector('[data-research-range]'),
            period: panel.querySelector('[data-research-period]'),
            tableToggle: panel.querySelector('[data-research-table-toggle]'),
            tableHost: panel.querySelector('[data-research-table]')
        };
        tabs.set(tabId, tab);

        if (tab.range) {
            tab.range.min = '0';
            tab.range.max = String(chart.periods.length - 1);
            tab.range.step = '1';
            tab.range.addEventListener('input', () => {
                stopPlay();
                setPosition(tab, Number(tab.range.value), { announce: true });
            });
        }
        if (tab.play) {
            tab.play.addEventListener('click', () => {
                if (playingTab === tab) {
                    stopPlay();
                    announce(PFI18n.t('Research.Js.Page.PausedAt', { period: chart.periods[tab.position] }));
                } else {
                    startPlay(tab);
                }
            });
            syncPlayButton(tab);
        }
        if (tab.tableToggle) {
            tab.tableToggle.addEventListener('click', () => {
                tab.tableOn = !tab.tableOn;
                tab.tableToggle.setAttribute('aria-pressed', String(tab.tableOn));
                renderTable(tab);
            });
        }
        setPosition(tab, tab.position, { announce: false });
    }

    /**
     * The scrubber position of a registered tab.
     * @param {string} tabId - The tab's id.
     * @returns {number|null} The period index, or null when the tab is not registered.
     */
    function positionOf (tabId) {
        const tab = tabs.get(tabId);
        return tab ? tab.position : null;
    }

    /**
     * Redraw a registered tab's table for its current period, e.g. after the chart changed what the table lists.
     * @param {string} tabId - The tab's id.
     * @returns {void}
     */
    function refreshTable (tabId) {
        const tab = tabs.get(tabId);
        if (tab) {
            renderTable(tab);
        }
    }

    window.ResearchPage = {
        registerTab,
        positionOf,
        refreshTable,
        selectTabById,
        prefersReducedMotion,
        formatMoney,
        formatPercent,
        formatFixed,
        formatMonth,
        formatDay,
        formatYears,
        formatList,
        readData,
        createTooltip,
        announce
    };

    /**
     * Wire the page's tablists.
     * @returns {void}
     */
    function init () {
        document.querySelectorAll('.research [role="tablist"]').forEach(initTablist);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

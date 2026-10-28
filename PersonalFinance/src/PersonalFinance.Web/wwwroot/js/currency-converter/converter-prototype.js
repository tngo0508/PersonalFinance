/* global PFI18n */
/**
 * PROTOTYPE (#437) - throwaway interactions for the Currency Converter variants B, C and D.
 * STUB RATES ONLY: nothing is fetched, nothing persists. Do not merge to main.
 */
(function () {
    'use strict';

    const VARIANTS = ['A', 'B', 'C', 'D'];
    /** Approximate USD rates, for the look only. The real page reads stored ExchangeRate rows. */
    const USD_RATES = {
        USD: 1, VND: 25400, JPY: 152, EUR: 0.92, GBP: 0.77, CNY: 7.1, KRW: 1380, THB: 33.5, SGD: 1.31, AUD: 1.5, CAD: 1.37
    };
    const MINOR_UNITS = { USD: 2, VND: 0, JPY: 0, EUR: 2, GBP: 2, CNY: 2, KRW: 0, THB: 2, SGD: 2, AUD: 2, CAD: 2 };
    const SOURCE = 'Frankfurter';

    const root = document.querySelector('[data-cc-proto]');
    if (!root) {
        return;
    }
    const params = new URLSearchParams(window.location.search);
    const state = {
        variant: root.dataset.variant,
        from: root.dataset.from,
        to: root.dataset.to,
        reporting: root.dataset.reporting,
        amount: Number(root.dataset.amount) || 0,
        date: root.dataset.date || '',
        today: root.dataset.today,
        demo: params.get('demo') || 'normal'
    };
    const culture = (PFI18n && PFI18n.culture) || document.documentElement.getAttribute('data-culture') || 'en-US';

    const $ = (selector) => root.querySelector(selector);
    const $$ = (selector) => Array.prototype.slice.call(root.querySelectorAll(selector));

    /** @param {number} value @param {string} code @returns {string} The amount with its symbol or ISO code. */
    function money (value, code) {
        const decimals = MINOR_UNITS[code];
        return new Intl.NumberFormat(culture, {
            style: 'currency',
            currency: code,
            currencyDisplay: code === 'USD' || code === 'VND' ? 'narrowSymbol' : 'code',
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals
        }).format(value);
    }

    /** @param {number} value @param {string} code @returns {string} The plain number in the currency's minor units. */
    function plain (value, code) {
        return new Intl.NumberFormat(culture, { maximumFractionDigits: MINOR_UNITS[code] }).format(value);
    }

    /** @param {number} rate @returns {string} The rate to 6 significant digits without trailing zeros. */
    function rateText (rate) {
        return new Intl.NumberFormat(culture, { maximumFractionDigits: 20 }).format(Number(rate.toPrecision(6)));
    }

    /** @param {string} iso @returns {string} The day in the page culture. */
    function dayText (iso) {
        const [y, m, d] = iso.split('-').map(Number);
        return new Intl.DateTimeFormat(culture, { dateStyle: 'medium' }).format(new Date(y, m - 1, d));
    }

    /** @param {string} iso @param {number} days @returns {string} The ISO day shifted. */
    function shift (iso, days) {
        const [y, m, d] = iso.split('-').map(Number);
        const date = new Date(Date.UTC(y, m - 1, d + days));
        return date.toISOString().slice(0, 10);
    }

    /** @param {string} tag @param {string} className @param {string} [text] @returns {HTMLElement} A new element. */
    function el (tag, className, text) {
        const node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        if (text !== undefined) {
            node.textContent = text;
        }
        return node;
    }

    /** @param {string} code @returns {string} The translated currency name. */
    function name (code) {
        return PFI18n.t(`Common.Currency.Name.${code}`);
    }

    /**
     * The stub quote: cross rate through USD, the asked day (or today) as the rate date, and the demo state applied.
     * @param {string} from @param {string} to @returns {{rate: number, rateDate: string, stale: boolean, unavailable: boolean}}
     */
    function quote (from, to) {
        const asked = state.date || state.today;
        if (state.demo === 'unavailable' && from !== to) {
            return { rate: 0, rateDate: null, stale: false, unavailable: true };
        }
        const stale = state.demo === 'stale' && from !== to;
        return {
            rate: USD_RATES[to] / USD_RATES[from],
            rateDate: stale ? shift(asked, -5) : asked,
            stale: stale,
            unavailable: false
        };
    }

    /** Writes the shared rate / date / source / state lines present in the variant. */
    function renderCommon (q) {
        const rateLine = $('[data-cc-rate-line]');
        if (rateLine) {
            rateLine.textContent = q.unavailable ? '' : PFI18n.t('CurrencyConverter.Result.Rate', {
                from: state.from, rate: rateText(q.rate), to: state.to
            });
        }
        const inverse = $('[data-cc-inverse-line]');
        if (inverse) {
            inverse.textContent = q.unavailable ? '' : PFI18n.t('CurrencyConverter.Result.Rate', {
                from: state.to, rate: rateText(1 / q.rate), to: state.from
            });
        }
        const source = $('[data-cc-source]');
        if (source) {
            source.textContent = q.unavailable ? '' : `${PFI18n.t('CurrencyConverter.Result.Source', { source: SOURCE })} · ${PFI18n.t('CurrencyConverter.Result.RateDate', { date: dayText(q.rateDate) })}`;
        }
        const stale = $('[data-cc-stale]');
        if (stale) {
            stale.hidden = !q.stale;
            stale.textContent = q.stale ? PFI18n.t('Common.Conversion.RateAsOf', { date: dayText(q.rateDate) }) : '';
        }
        const unavailable = $('[data-cc-unavailable]');
        if (unavailable) {
            unavailable.hidden = !q.unavailable;
        }
        const dateLabel = $('[data-cc-date-label]');
        if (dateLabel) {
            dateLabel.textContent = state.date ? `Rate on ${dayText(state.date)}` : `Latest rate (${dayText(state.today)})`;
        }
    }

    function renderB () {
        const q = quote(state.from, state.to);
        $('[data-cc-result]').textContent = q.unavailable ? '—' : money(state.amount * q.rate, state.to);
        renderCommon(q);
    }

    function renderC (edited) {
        const q = quote(state.from, state.to);
        const toInput = $('[data-cc-amount-to]');
        const fromInput = $('[data-cc-amount]');
        if (edited !== 'to') {
            toInput.value = q.unavailable ? '' : plain(state.amount * q.rate, state.to);
        } else if (!q.unavailable) {
            fromInput.value = plain(state.amount, state.from);
        }
        toInput.placeholder = q.unavailable ? '—' : '';
        $('[data-cc-from-name]').textContent = name(state.from);
        $('[data-cc-to-name]').textContent = name(state.to);
        renderCommon(q);
    }

    function renderD () {
        const body = $('[data-cc-sheet]');
        const header = $('[data-cc-rate-header]');
        header.textContent = `1 ${state.from} =`;
        const codes = Object.keys(USD_RATES).filter((code) => code !== state.from);
        codes.sort((a, b) => (a === state.reporting ? -1 : b === state.reporting ? 1 : 0));
        let anyStale = null;
        body.replaceChildren();
        codes.forEach((code) => {
            const q = quote(state.from, code);
            if (q.stale) {
                anyStale = q;
            }
            const row = document.createElement('tr');
            row.className = code === state.reporting ? 'is-reporting' : '';

            const pick = el('button', 'ccp-d__pick');
            pick.type = 'button';
            pick.dataset.ccPick = code;
            pick.title = `Convert from ${code}`;
            const codeLine = el('span', '');
            codeLine.appendChild(el('span', 'ccp-d__code', code));
            if (code === state.reporting) {
                codeLine.appendChild(el('span', 'badge text-bg-success ccp-d__badge', 'Reporting currency'));
            }
            pick.appendChild(codeLine);
            pick.appendChild(el('span', 'ccp-d__cname', name(code)));
            const pickCell = el('td', '');
            pickCell.appendChild(pick);
            row.appendChild(pickCell);

            row.appendChild(el('td', `text-end ccp-d__amt ${q.unavailable ? 'is-unavailable' : ''}`,
                q.unavailable ? '—' : money(state.amount * q.rate, code)));
            row.appendChild(el('td', 'text-end ccp-d__rate d-none d-sm-table-cell',
                q.unavailable ? '' : `${rateText(q.rate)} ${code}`));
            body.appendChild(row);
        });
        const first = quote(state.from, codes[0]);
        renderCommon(anyStale || first);
        $$('[data-cc-pick]').forEach((button) => {
            button.addEventListener('click', () => {
                state.from = button.dataset.ccPick;
                $('[data-cc-from]').value = state.from;
                $('[data-cc-amount]').value = plain(state.amount, state.from);
                renderD();
            });
        });
    }

    function render (edited) {
        if (state.variant === 'B') {
            renderB();
        } else if (state.variant === 'C') {
            renderC(edited);
        } else if (state.variant === 'D') {
            renderD();
        }
    }

    /** Wires the inputs each variant has; a missing element is skipped. */
    function bind () {
        const amount = $('[data-cc-amount]');
        if (amount) {
            amount.addEventListener('input', () => {
                const value = PFI18n.parseNumber(amount.value);
                state.amount = Number.isNaN(value) ? 0 : value;
                render('from');
            });
            amount.addEventListener('blur', () => {
                amount.value = plain(state.amount, state.from);
            });
        }
        const toAmount = $('[data-cc-amount-to]');
        if (toAmount) {
            toAmount.addEventListener('input', () => {
                const value = PFI18n.parseNumber(toAmount.value);
                const q = quote(state.from, state.to);
                state.amount = Number.isNaN(value) || q.unavailable ? 0 : value / q.rate;
                render('to');
            });
            toAmount.addEventListener('blur', () => {
                render('from');
            });
        }
        const from = $('[data-cc-from]');
        if (from) {
            from.addEventListener('change', () => {
                state.from = from.value;
                if (amount) {
                    amount.value = plain(state.amount, state.from);
                }
                render('from');
            });
        }
        const to = $('[data-cc-to]');
        if (to) {
            to.addEventListener('change', () => {
                state.to = to.value;
                render('from');
            });
        }
        $$('[data-cc-swap]').forEach((button) => {
            button.addEventListener('click', () => {
                const q = quote(state.from, state.to);
                const previousFrom = state.from;
                state.from = state.to;
                state.to = previousFrom;
                if (state.variant === 'C' && !q.unavailable) {
                    state.amount = state.amount * q.rate;
                }
                if (from) {
                    from.value = state.from;
                }
                if (to) {
                    to.value = state.to;
                }
                if (amount) {
                    amount.value = plain(state.amount, state.from);
                }
                render('from');
            });
        });
        const date = $('[data-cc-date]');
        if (date) {
            date.addEventListener('change', () => {
                state.date = date.value;
                render('from');
            });
        }
        $$('[data-cc-date-clear]').forEach((button) => {
            button.addEventListener('click', () => {
                state.date = '';
                if (date) {
                    date.value = '';
                }
                render('from');
            });
        });
        const toggle = $('[data-cc-date-toggle]');
        if (toggle) {
            const panel = document.getElementById(toggle.getAttribute('aria-controls'));
            toggle.addEventListener('click', () => {
                const open = toggle.getAttribute('aria-expanded') === 'true';
                toggle.setAttribute('aria-expanded', String(!open));
                panel.hidden = open;
            });
        }
    }

    /** Variant A is the shipped GET form: keep ?variant= and ?demo= on its submit so the switcher survives. */
    function keepParamsOnShippedForm () {
        const form = document.getElementById('currency-converter-form');
        if (!form) {
            return;
        }
        ['variant', 'demo'].forEach((key) => {
            if (!params.has(key)) {
                return;
            }
            const hidden = document.createElement('input');
            hidden.type = 'hidden';
            hidden.name = key;
            hidden.value = params.get(key);
            form.appendChild(hidden);
        });
    }

    /** Cycles ?variant= from the floating bar and the arrow keys; the demo select sets ?demo=. */
    function initSwitcher () {
        const current = Math.max(0, VARIANTS.indexOf((params.get('variant') || 'A').toUpperCase()));

        function go (step) {
            params.set('variant', VARIANTS[(current + step + VARIANTS.length) % VARIANTS.length]);
            window.location.search = params.toString();
        }

        document.querySelectorAll('[data-cc-variant-step]').forEach((button) => {
            button.addEventListener('click', () => {
                go(Number(button.dataset.ccVariantStep));
            });
        });
        const demo = document.querySelector('[data-cc-demo]');
        if (demo) {
            demo.value = state.demo;
            demo.addEventListener('change', () => {
                state.demo = demo.value;
                params.set('demo', demo.value);
                window.history.replaceState(null, '', `?${params.toString()}`);
                render('from');
            });
        }
        document.addEventListener('keydown', (event) => {
            const target = event.target;
            if (target && (target.matches('input, textarea, select, button') || target.isContentEditable)) {
                return;
            }
            if (event.key === 'ArrowLeft') {
                go(-1);
            } else if (event.key === 'ArrowRight') {
                go(1);
            }
        });
    }

    function init () {
        keepParamsOnShippedForm();
        bind();
        render('from');
        initSwitcher();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

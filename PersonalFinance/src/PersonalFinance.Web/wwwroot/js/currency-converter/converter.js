/* global PFI18n */
/**
 * Currency Converter page (#490): the exchange rows update live. Any change (either amount, either currency, the date)
 * asks /CurrencyConverter/Quote for a new quote after a short debounce; editing the To amount asks for the reverse quote
 * and writes the From amount; swap exchanges the currencies and the amounts. The previous result stays until the new one
 * arrives, focus is never moved, and the query string keeps amount, from, to and date so the page stays shareable.
 * Without JavaScript the form is a plain GET form and the server renders the result.
 */
(function () {
    'use strict';

    const DEBOUNCE_MS = 250;
    const RATE_SIGNIFICANT_DIGITS = 6;
    const SOURCE_KEYS = {
        frankfurter: 'CurrencyConverter.Source.Frankfurter',
        'currency-api': 'CurrencyConverter.Source.CurrencyApi'
    };

    /**
     * A rate to six significant digits without trailing zeros, in the page culture, like the server's FormatRate.
     * @param {number} rate - Units of one currency per unit of the other.
     * @returns {string} The rate as the page shows it.
     */
    function formatRate (rate) {
        const rounded = rate > 0 ? Number(rate.toPrecision(RATE_SIGNIFICANT_DIGITS)) : 0;
        return PFI18n.formatNumber(rounded, { maximumFractionDigits: 20 });
    }

    /**
     * An ISO day (yyyy-MM-dd) as a short date in the page culture, like the server's "d" format.
     * @param {string} iso - The day.
     * @returns {string} The day as the page shows it.
     */
    function formatDay (iso) {
        const [year, month, day] = iso.split('-').map(Number);
        return PFI18n.formatDate(new Date(year, month - 1, day));
    }

    /**
     * Wire the converter form for live quotes.
     * @param {HTMLFormElement} form - The #currency-converter-form element.
     */
    function initConverter (form) {
        const el = {
            fromAmount: form.querySelector('#converter-amount'),
            toAmount: form.querySelector('[data-converter-amount]'),
            from: form.querySelector('#converter-from'),
            to: form.querySelector('#converter-to'),
            date: form.querySelector('#converter-date'),
            submit: form.querySelector('#converter-submit'),
            swap: form.querySelector('[data-converter-swap]'),
            latest: form.querySelector('[data-converter-latest]'),
            fromName: form.querySelector('[data-converter-from-name]'),
            toName: form.querySelector('[data-converter-to-name]'),
            pageErrors: form.querySelector('[data-converter-page-errors]'),
            summary: form.querySelector('[data-converter-summary]'),
            rate: form.querySelector('[data-converter-rate]'),
            inverse: form.querySelector('[data-converter-inverse]'),
            unavailable: form.querySelector('[data-converter-unavailable]'),
            stale: form.querySelector('[data-converter-stale]'),
            meta: form.querySelector('[data-converter-meta]'),
            source: form.querySelector('[data-converter-source]'),
            separator: form.querySelector('[data-converter-meta-separator]'),
            rateDate: form.querySelector('[data-converter-rate-date]')
        };
        if (Object.keys(el).some((key) => !el[key])) {
            return;
        }

        const quoteUrl = form.dataset.quoteUrl;
        /** The side the user last typed in: 'from' asks for the quote, 'to' for the reverse one. */
        let edited = 'from';
        let timer = null;
        let pending = null;

        /**
         * @param {HTMLSelectElement} select - A currency select.
         * @returns {number} The selected currency's minor units.
         */
        const minorUnits = (select) => Number(select.selectedOptions[0] && select.selectedOptions[0].dataset.minor) || 0;

        /**
         * @param {number} value - An amount.
         * @param {HTMLSelectElement} select - The select of its currency.
         * @returns {string} The plain number in the currency's minor units, in the page culture.
         */
        const formatAmount = (value, select) => {
            const decimals = minorUnits(select);
            return PFI18n.formatNumber(value, { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
        };

        const amountOf = (side) => (side === 'from' ? el.fromAmount : el.toAmount);
        const selectOf = (side) => (side === 'from' ? el.from : el.to);
        const other = (side) => (side === 'from' ? 'to' : 'from');

        /** Show each currency's full name beside its label. */
        function updateNames () {
            el.fromName.textContent = (el.from.selectedOptions[0] && el.from.selectedOptions[0].dataset.name) || '';
            el.toName.textContent = (el.to.selectedOptions[0] && el.to.selectedOptions[0].dataset.name) || '';
        }

        /** Remove every error message and invalid mark. */
        function clearErrors () {
            el.pageErrors.hidden = true;
            el.pageErrors.replaceChildren();
            form.querySelectorAll('[data-converter-errors-for]').forEach((node) => node.replaceChildren());
            form.querySelectorAll('.is-invalid').forEach((node) => node.classList.remove('is-invalid'));
            [el.fromAmount, el.toAmount].forEach((input) => input.removeAttribute('aria-invalid'));
        }

        /**
         * @param {HTMLElement} container - Where the messages go.
         * @param {string[]} messages - The messages.
         */
        function showMessages (container, messages) {
            container.replaceChildren(...messages.map((message) => {
                const line = document.createElement('div');
                line.textContent = message;
                return line;
            }));
        }

        /** @param {string} message - A message about the whole request, shown above the card. */
        function showPageError (message) {
            showMessages(el.pageErrors, [message]);
            el.pageErrors.hidden = false;
        }

        /**
         * Show a rejected quote's messages, each under its field. A reverse quote's fields are the page's swapped.
         * @param {{errors?: string[], fieldErrors?: Object<string, string[]>}} body - The 400 body.
         * @param {string} side - The side the quote was asked from.
         */
        function showErrors (body, side) {
            clearErrors();
            const general = (body && body.errors) || [];
            if (general.length > 0) {
                showMessages(el.pageErrors, general);
                el.pageErrors.hidden = false;
            }
            const targets = {
                amount: side === 'from' ? 'amount' : 'toamount',
                from: side === 'from' ? 'from' : 'to',
                to: side === 'from' ? 'to' : 'from',
                date: 'date'
            };
            Object.entries((body && body.fieldErrors) || {}).forEach(([field, messages]) => {
                const target = targets[field.toLowerCase()];
                const container = target && form.querySelector(`[data-converter-errors-for="${target}"]`);
                if (container) {
                    showMessages(container, messages);
                } else {
                    showMessages(el.pageErrors, messages);
                    el.pageErrors.hidden = false;
                }
            });
        }

        /** Hide the rate lines, the stale warning, the source and the unavailable message. */
        function clearResult () {
            [el.rate, el.inverse, el.unavailable, el.stale, el.meta].forEach((node) => {
                node.hidden = true;
            });
            el.summary.textContent = '';
        }

        /**
         * Keep the inputs in the query string, so a reload or a shared link shows the same conversion.
         * @param {number|null} amount - The From amount.
         */
        function updateUrl (amount) {
            const params = new URLSearchParams();
            if (amount !== null) {
                params.set('amount', String(amount));
            }
            params.set('from', el.from.value);
            params.set('to', el.to.value);
            if (el.date.value) {
                params.set('date', el.date.value);
            }
            window.history.replaceState(window.history.state, '', `${window.location.pathname}?${params.toString()}`);
        }

        /**
         * Write a quote into the page.
         * @param {object} dto - The CurrencyConversionDto, camel-cased.
         * @param {string} side - The side the quote was asked from.
         */
        function render (dto, side) {
            const reversed = side === 'to';
            const converted = !dto.unavailable && typeof dto.convertedAmount === 'number' && typeof dto.rate === 'number';
            const target = other(side);
            amountOf(target).value = converted ? formatAmount(dto.convertedAmount, selectOf(target)) : '';

            const pageFrom = reversed ? dto.to : dto.from;
            const pageTo = reversed ? dto.from : dto.to;
            clearResult();
            el.unavailable.hidden = converted;
            if (converted) {
                const fromAmount = reversed ? dto.convertedAmount : dto.amount;
                const toAmount = reversed ? dto.amount : dto.convertedAmount;
                el.summary.textContent = PFI18n.t('CurrencyConverter.Result.Summary', {
                    from: PFI18n.formatConvertible(fromAmount, pageFrom),
                    to: PFI18n.formatConvertible(toAmount, pageTo)
                });

                const rate = reversed ? 1 / dto.rate : dto.rate;
                el.rate.textContent = PFI18n.t('CurrencyConverter.Result.Rate',
                    { from: pageFrom, rate: formatRate(rate), to: pageTo });
                el.rate.hidden = false;
                if (dto.rate > 0) {
                    el.inverse.textContent = PFI18n.t('CurrencyConverter.Result.Rate',
                        { from: pageTo, rate: formatRate(1 / rate), to: pageFrom });
                    el.inverse.hidden = false;
                }

                const day = dto.rateDate ? formatDay(dto.rateDate) : null;
                el.stale.textContent = day && dto.isStale ? PFI18n.t('Common.Conversion.RateAsOf', { date: day }) : '';
                el.stale.hidden = !(day && dto.isStale);

                const sources = (dto.sources || []).map((source) => (SOURCE_KEYS[source] ? PFI18n.t(SOURCE_KEYS[source]) : source));
                el.source.textContent = sources.length > 0
                    ? PFI18n.t('CurrencyConverter.Result.Source', { source: sources.join(', ') })
                    : '';
                el.source.hidden = sources.length === 0;
                el.rateDate.textContent = day ? PFI18n.t('CurrencyConverter.Result.RateDate', { date: day }) : '';
                el.rateDate.hidden = !day;
                el.separator.hidden = sources.length === 0 || !day;
                el.meta.hidden = sources.length === 0 && !day;
            }

            updateUrl(reversed ? (converted ? dto.convertedAmount : null) : dto.amount);
        }

        /** Forget the quote in flight, so a late answer never overwrites a newer change. */
        function cancelPending () {
            if (pending) {
                pending.abort();
                pending = null;
            }
        }

        /**
         * Ask for a quote from the side the user last typed in, and write the answer into the other side.
         * @returns {Promise<void>} Resolves once the page is updated, the error shown, or the quote superseded.
         */
        async function quote () {
            clearTimeout(timer);
            cancelPending();
            const side = edited;
            const source = amountOf(side);
            const text = source.value.trim();
            if (text === '') {
                clearErrors();
                amountOf(other(side)).value = '';
                clearResult();
                return;
            }
            const amount = PFI18n.parseNumber(text);
            if (Number.isNaN(amount)) {
                source.classList.add('is-invalid');
                source.setAttribute('aria-invalid', 'true');
                return;
            }

            const params = new URLSearchParams({
                amount: String(amount),
                from: selectOf(side).value,
                to: selectOf(other(side)).value
            });
            if (el.date.value) {
                params.set('date', el.date.value);
            }

            const controller = new AbortController();
            pending = controller;
            try {
                const response = await fetch(`${quoteUrl}?${params.toString()}`, {
                    headers: { Accept: 'application/json' },
                    signal: controller.signal
                });
                if (response.status === 400) {
                    const body = await response.json().catch(() => null);
                    if (pending === controller) {
                        showErrors(body, side);
                    }
                    return;
                }
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                const dto = await response.json();
                if (pending !== controller) {
                    return;
                }
                clearErrors();
                render(dto, side);
            } catch (err) {
                if (err.name === 'AbortError' || pending !== controller) {
                    return;
                }
                // eslint-disable-next-line no-console
                console.error('Currency conversion failed:', err);
                showPageError(PFI18n.t('CurrencyConverter.Error.Failed'));
            } finally {
                if (pending === controller) {
                    pending = null;
                }
            }
        }

        /**
         * Ask for a new quote after the debounce; a quote in flight is forgotten at once.
         * @param {string} [side] - The side just typed in; the last one typed in when omitted.
         */
        function schedule (side) {
            if (side) {
                edited = side;
            }
            clearTimeout(timer);
            cancelPending();
            timer = setTimeout(quote, DEBOUNCE_MS);
        }

        /**
         * Turn a server-rendered number input into a text input holding the number in the page culture.
         * @param {HTMLInputElement} input - An amount input.
         * @param {HTMLSelectElement} select - The select of its currency.
         */
        function enhanceAmount (input, select) {
            const value = input.value === '' ? NaN : Number(input.value);
            input.type = 'text';
            input.removeAttribute('min');
            input.removeAttribute('step');
            input.readOnly = false;
            input.value = Number.isNaN(value) ? '' : formatAmount(value, select);
        }

        /**
         * On leaving an amount, show it in its currency's minor units; re-quote when that rounding changed it.
         * @param {string} side - The amount's side.
         */
        function tidyAmount (side) {
            const input = amountOf(side);
            const value = PFI18n.parseNumber(input.value);
            if (input.value.trim() === '' || Number.isNaN(value)) {
                return;
            }
            const formatted = formatAmount(value, selectOf(side));
            if (formatted !== input.value) {
                input.value = formatted;
                if (PFI18n.parseNumber(formatted) !== value) {
                    schedule(side);
                }
            }
        }

        enhanceAmount(el.fromAmount, el.from);
        enhanceAmount(el.toAmount, el.to);
        el.submit.hidden = true;
        el.swap.hidden = false;

        form.addEventListener('submit', (event) => {
            event.preventDefault();
            quote();
        });
        el.fromAmount.addEventListener('input', () => schedule('from'));
        el.toAmount.addEventListener('input', () => schedule('to'));
        el.fromAmount.addEventListener('blur', () => tidyAmount('from'));
        el.toAmount.addEventListener('blur', () => tidyAmount('to'));
        [el.from, el.to].forEach((select) => select.addEventListener('change', () => {
            updateNames();
            schedule();
        }));
        el.date.addEventListener('change', () => schedule());
        el.latest.addEventListener('click', (event) => {
            event.preventDefault();
            el.date.value = '';
            schedule();
        });
        el.swap.addEventListener('click', () => {
            const fromCode = el.from.value;
            el.from.value = el.to.value;
            el.to.value = fromCode;
            const fromText = el.fromAmount.value;
            el.fromAmount.value = el.toAmount.value;
            el.toAmount.value = fromText;
            updateNames();
            schedule('from');
        });
    }

    /** Find the converter form and wire it. */
    function init () {
        const form = document.getElementById('currency-converter-form');
        if (form && form.dataset.quoteUrl && window.PFI18n) {
            initConverter(form);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

/* global PFI18n */
(function () {
    'use strict';

    /**
     * Budget Planner interactive module: keeps the income, the ten category amounts, the Month Strip, the ledger rows
     * and the totals in step. All state is stored in integer cents to avoid floating-point drift.
     */
    const BudgetPlanner = {};

    // Category keys in ledger order; the PascalCase form (capitalize) is the form field and data-category value.
    const categories = [
        'housing', 'food', 'transportation', 'utilities', 'insurance',
        'healthcare', 'savings', 'debtRepayment', 'entertainment', 'miscellaneous'
    ];

    let incomeCents = 0;
    const amountCents = {};

    /**
     * Initialize the budget planner.
     */
    BudgetPlanner.init = function () {
        const initialData = window.budgetInitialData;
        if (initialData) {
            incomeCents = Math.round(parseFloat(initialData.monthlyIncome) * 100) || 0;
            categories.forEach((cat) => {
                amountCents[cat] = Math.round(parseFloat(initialData[cat]) * 100) || 0;
            });
        }

        syncInputsFromData();
        bindEvents();
        bindHighlight();
        watchStripSize();
        updateAll();
    };

    /**
     * Sync all input values from current state.
     */
    function syncInputsFromData () {
        categories.forEach((cat) => {
            const cents = amountCents[cat] || 0;
            const percent = toPercent(cents);
            const capitalized = capitalize(cat);

            const slider = document.getElementById(`slider-${ capitalized }`);
            const percentInput = document.querySelector(`.budget-percent-input[data-category="${ capitalized }"]`);
            const amountInput = document.querySelector(`.budget-amount-input[data-category="${ capitalized }"]`);

            if (slider) {
                setSlider(slider, percent);
            }
            if (percentInput) {
                percentInput.value = percent > 0 ? percent.toFixed(2) : '';
            }
            if (amountInput) {
                amountInput.value = cents > 0 ? PFI18n.formatAmount(cents / 100, currency()) : '';
            }
        });

        const incomeInput = document.querySelector('.budget-income-input');
        if (incomeInput && incomeCents > 0) {
            incomeInput.value = PFI18n.formatAmount(incomeCents / 100, currency());
        }
    }

    /**
     * Bind all event listeners.
     */
    function bindEvents () {
        const incomeInput = document.querySelector('.budget-income-input');
        if (incomeInput) {
            incomeInput.addEventListener('input', function () {
                incomeCents = Math.round(PFI18n.parseNumber(this.value) * 100) || 0;
                markOverBound(this, incomeCents / 100);
                updateAll();
            });
        }

        document.querySelectorAll('.budget-slider').forEach((slider) => {
            slider.addEventListener('input', function () {
                const pct = parseFloat(this.value) || 0;
                setCategoryFromPercent(normalizeCategory(this.getAttribute('data-category')), pct);
                updateAll();
            });
        });

        // Percent inputs are helper controls; the amount inputs are the form's fields
        document.querySelectorAll('.budget-percent-input').forEach((input) => {
            input.addEventListener('input', function () {
                const pct = Math.min(100, Math.max(0, parseFloat(this.value) || 0));
                setCategoryFromPercent(normalizeCategory(this.getAttribute('data-category')), pct);
                updateAll();
            });
        });

        document.querySelectorAll('.budget-amount-input').forEach((amountInput) => {
            amountInput.addEventListener('input', function () {
                const cents = Math.round(PFI18n.parseNumber(this.value) * 100) || 0;
                markOverBound(this, cents / 100);
                setCategoryFromCents(normalizeCategory(this.getAttribute('data-category')), cents);
                updateAll();
            });
        });

        // The currency selector reformats the inputs in place; the figures around them follow
        const currencySelect = document.getElementById('CurrencyCode');
        if (currencySelect) {
            currencySelect.addEventListener('change', updateAll);
        }

        // Sync JS state to form inputs before form submission, so the POST carries current values
        const form = document.getElementById('budgetForm');
        if (form) {
            form.addEventListener('submit', syncFormInputsFromData);
        }
    }

    /**
     * A row and its segment of the Month Strip light up together: hovering or focusing either marks both.
     */
    function bindHighlight () {
        document.querySelectorAll('.budget-row').forEach((row) => {
            const segment = segmentFor(row.getAttribute('data-category'));
            if (!segment) {
                return;
            }
            const on = () => {
                segment.classList.add('budget-strip__segment--active');
                row.classList.add('budget-row--active');
            };
            const off = () => {
                segment.classList.remove('budget-strip__segment--active');
                row.classList.remove('budget-row--active');
            };
            row.addEventListener('mouseenter', on);
            row.addEventListener('mouseleave', off);
            row.addEventListener('focusin', on);
            row.addEventListener('focusout', off);
            segment.addEventListener('mouseenter', on);
            segment.addEventListener('mouseleave', off);
        });
    }

    /**
     * Re-fit the segment labels whenever a segment changes size (a share changes, its width eases, the window is
     * resized), so a name shows only where it fits.
     */
    function watchStripSize () {
        const strip = document.getElementById('budgetStrip');
        if (!strip || typeof ResizeObserver === 'undefined') {
            return;
        }
        const observer = new ResizeObserver(() => {
            fitStripLabels();
        });
        observer.observe(strip);
        strip.querySelectorAll('.budget-strip__segment').forEach((segment) => observer.observe(segment));
    }

    /**
     * Mark a money input that is over the bound of the budget's currency: the data-max the view writes and the currency
     * selector rescales (#444). The server rejects the same amount, so this only warns early.
     * @param {HTMLInputElement} input - The income or a category amount input.
     * @param {number} amount - What it holds, in whole units.
     */
    function markOverBound (input, amount) {
        const max = Number(input.dataset.max);
        input.classList.toggle('is-invalid', Number.isFinite(max) && max > 0 && amount > max);
    }

    /**
     * Set a category's amount from a percentage of the income.
     * @param {string} cat - Normalized category key
     * @param {number} pct - Percentage value
     */
    function setCategoryFromPercent (cat, pct) {
        amountCents[cat] = Math.round((incomeCents * pct) / 100);
        syncRowControls(cat, pct, true);
    }

    /**
     * Set a category's amount in cents.
     * @param {string} cat - Normalized category key
     * @param {number} cents - Amount in cents
     */
    function setCategoryFromCents (cat, cents) {
        amountCents[cat] = cents;
        syncRowControls(cat, toPercent(cents), false);
    }

    /**
     * Bring a row's slider and percent field to a share, leaving whichever field has focus alone.
     * @param {string} cat - Normalized category key
     * @param {number} pct - The share, 0-100 (over 100 when an amount exceeds the income)
     * @param {boolean} fromPercent - Whether the change came from the slider or the percent field
     */
    function syncRowControls (cat, pct, fromPercent) {
        const capitalized = capitalize(cat);
        const slider = document.getElementById(`slider-${ capitalized }`);
        if (slider && document.activeElement !== slider) {
            setSlider(slider, pct);
        } else if (slider) {
            slider.style.setProperty('--pct', `${ Math.min(100, Math.max(0, pct)) }%`);
        }

        const percentInput = document.querySelector(`.budget-percent-input[data-category="${ capitalized }"]`);
        if (percentInput && document.activeElement !== percentInput) {
            percentInput.value = pct > 0 ? pct.toFixed(2) : '';
        }
        if (!fromPercent) {
            return;
        }
        const amountInput = document.querySelector(`.budget-amount-input[data-category="${ capitalized }"]`);
        if (amountInput) {
            markOverBound(amountInput, (amountCents[cat] || 0) / 100);
        }
    }

    /**
     * Move a slider to a share and fill its track up to the thumb.
     * @param {HTMLInputElement} slider - The row's range input
     * @param {number} pct - The share, 0-100
     */
    function setSlider (slider, pct) {
        const clamped = Math.min(100, Math.max(0, pct));
        slider.value = clamped;
        slider.style.setProperty('--pct', `${ clamped }%`);
    }

    /**
     * Calculate percentage from cents.
     * @param {number} cents - Amount in cents
     * @returns {number} Percentage value
     */
    function toPercent (cents) {
        if (incomeCents <= 0) {
            return 0;
        }
        return (cents / incomeCents) * 100;
    }

    /**
     * Sync all form input values from current state so the form POST carries the latest values.
     */
    function syncFormInputsFromData () {
        const incomeInput = document.querySelector('.budget-income-input');
        if (incomeInput) {
            incomeInput.value = PFI18n.formatAmount(incomeCents / 100, currency());
        }

        categories.forEach((cat) => {
            // The form field's id is the PascalCase property name (e.g. "Housing")
            const input = document.getElementById(capitalize(cat));
            if (input) {
                input.value = PFI18n.formatAmount((amountCents[cat] || 0) / 100, currency());
            }
        });
    }

    /**
     * Update every figure on the page: the amount fields, the remaining figure, the total, the strip and the hint.
     */
    function updateAll () {
        updateAmounts();
        updateRemaining();
        updateTotal();
        updateStrip();
        updateIncomeState();
    }

    /**
     * Sync the editable amount inputs with the state (only the ones not being typed in).
     */
    function updateAmounts () {
        categories.forEach((cat) => {
            const cents = amountCents[cat] || 0;
            const amountInput = document.querySelector(`.budget-amount-input[data-category="${ capitalize(cat) }"]`);
            if (amountInput && document.activeElement !== amountInput) {
                amountInput.value = cents > 0 ? PFI18n.formatAmount(cents / 100, currency()) : '';
            }
        });
    }

    /**
     * Update the remaining-to-allocate figure in the hero: plain, green when the budget is fully allocated, red when
     * the categories add up to more than the income.
     */
    function updateRemaining () {
        const totalCents = getTotalCents();
        const remainingCents = incomeCents - totalCents;
        const remainingPct = incomeCents > 0 ? (remainingCents / incomeCents) * 100 : 0;

        const valueEl = document.getElementById('budgetRemainingValue');
        const container = document.getElementById('budgetRemaining');
        const labelEl = container ? container.querySelector('.budget-remaining__label') : null;

        if (valueEl) {
            valueEl.textContent = `${ formatMoney(Math.abs(remainingCents) / 100) } (${ formatPercent(Math.abs(remainingPct)) })`;
        }
        if (!container) {
            return;
        }

        container.classList.remove('budget-remaining--over', 'budget-remaining--exact');
        let label = PFI18n.t('BudgetPlanner.Js.RemainingLabel');
        if (totalCents > incomeCents) {
            container.classList.add('budget-remaining--over');
            label = PFI18n.t('BudgetPlanner.Js.OverBudgetLabel');
        } else if (remainingCents === 0 && incomeCents > 0) {
            container.classList.add('budget-remaining--exact');
            label = PFI18n.t('BudgetPlanner.Js.BalancedLabel');
        }
        if (labelEl) {
            labelEl.textContent = label;
        }
    }

    /**
     * Update the total row under the ledger.
     */
    function updateTotal () {
        const totalCents = getTotalCents();
        const percentEl = document.getElementById('budgetSummaryTotalPercent');
        const amountEl = document.getElementById('budgetSummaryTotalAmount');
        if (percentEl) {
            percentEl.textContent = formatPercent(toPercent(totalCents));
        }
        if (amountEl) {
            amountEl.textContent = formatMoney(totalCents / 100);
        }
    }

    /**
     * Redraw the Month Strip: each segment's width is its share of the income, or of the total when the categories add
     * up to more than the income. What is left of the income is the hatched tail; what is over it is hatched red.
     */
    function updateStrip () {
        const strip = document.getElementById('budgetStrip');
        if (!strip) {
            return;
        }
        const totalCents = getTotalCents();
        const basis = Math.max(incomeCents, totalCents);

        categories.forEach((cat) => {
            const segment = segmentFor(capitalize(cat));
            if (!segment) {
                return;
            }
            const cents = amountCents[cat] || 0;
            segment.style.width = `${ basis > 0 ? (cents / basis) * 100 : 0 }%`;
            const pct = segment.querySelector('.budget-strip__pct');
            if (pct) {
                pct.textContent = cents > 0 && incomeCents > 0 ? formatPercent(toPercent(cents)) : '';
            }
        });

        const unallocated = document.getElementById('budgetStripUnallocated');
        if (unallocated) {
            const restCents = Math.max(0, incomeCents - totalCents);
            unallocated.style.width = basis > 0 ? `${ (restCents / basis) * 100 }%` : '100%';
            const pct = unallocated.querySelector('.budget-strip__pct');
            if (pct) {
                pct.textContent = restCents > 0 ? formatPercent(toPercent(restCents)) : '';
            }
        }

        const overflow = document.getElementById('budgetStripOverflow');
        if (overflow) {
            const over = totalCents > incomeCents && incomeCents > 0;
            overflow.hidden = !over;
            overflow.style.width = over ? `${ ((totalCents - incomeCents) / basis) * 100 }%` : '0';
        }

        fitStripLabels();
    }

    /**
     * Show each segment's name only where it fits, and its share only where both fit (the rows carry the figures).
     */
    function fitStripLabels () {
        document.querySelectorAll('.budget-strip__segment').forEach((segment) => {
            const label = segment.querySelector('.budget-strip__label');
            if (!label) {
                return;
            }
            // Bounding widths, not scrollWidth: the name and the share are inline spans, which scrollWidth reports as 0
            const room = segment.getBoundingClientRect().width;
            const padding = parseFloat(getComputedStyle(label).paddingLeft) * 2 || 0;
            label.classList.remove('budget-strip__label--hidden', 'budget-strip__label--no-pct');
            if (label.getBoundingClientRect().width <= room) {
                return;
            }
            label.classList.add('budget-strip__label--no-pct');
            const name = segment.querySelector('.budget-strip__name');
            if (!name || name.getBoundingClientRect().width + padding > room) {
                label.classList.add('budget-strip__label--hidden');
            }
        });
    }

    /**
     * Without an income there is nothing to divide by share: the sliders and percent fields wait, and a hint says so.
     */
    function updateIncomeState () {
        const hasIncome = incomeCents > 0;
        document.querySelectorAll('.budget-slider, .budget-percent-input').forEach((input) => {
            input.disabled = !hasIncome;
        });
        const hint = document.getElementById('budgetNoIncomeHint');
        if (hint) {
            hint.hidden = hasIncome;
        }
    }

    /**
     * The Month Strip segment of a category.
     * @param {string} capitalized - The PascalCase category name (e.g. "Housing")
     * @returns {HTMLElement|null} The segment
     */
    function segmentFor (capitalized) {
        return document.querySelector(`.budget-strip__segment[data-category="${ capitalized }"]`);
    }

    /**
     * Calculate total allocation in cents.
     * @returns {number} Total cents
     */
    function getTotalCents () {
        return categories.reduce((total, cat) => total + (amountCents[cat] || 0), 0);
    }

    /**
     * Format a percentage with one decimal in the page culture (12.5% in en-US, 12,5% in vi-VN).
     * @param {number} value - The percentage, 0-100
     * @returns {string} Formatted percentage
     */
    function formatPercent (value) {
        return `${ PFI18n.formatNumber(value, { minimumFractionDigits: 1, maximumFractionDigits: 1 }) }%`;
    }

    /**
     * The currency of the budget's amounts (ADR 0016): the document's one selector, read from the income input it governs.
     * @returns {string} ISO 4217 code
     */
    function currency () {
        const incomeInput = document.querySelector('.budget-income-input');
        return (incomeInput && incomeInput.dataset.currency) || 'USD';
    }

    /**
     * Format an amount with its symbol in the page culture, no decimals for whole numbers (1,500 or 1,500.50 in en-US).
     * @param {number} value - The amount to format
     * @returns {string} Formatted amount
     */
    function formatMoney (value) {
        return PFI18n.formatMoney(value, currency(), { whole: value === Math.floor(value) });
    }

    /**
     * Map a camelCase category key to the PascalCase property name used by the form fields.
     * @param {string} str - The camelCase category key (e.g., "debtRepayment")
     * @returns {string} PascalCase property name (e.g., "DebtRepayment")
     */
    function capitalize (str) {
        if (!str) {
            return str;
        }
        return str.charAt(0).toUpperCase() + str.slice(1);
    }

    /**
     * Normalize a category name to the camelCase data key.
     * @param {string} cat - Category name (e.g., "DebtRepayment")
     * @returns {string} Normalized category (e.g., "debtRepayment")
     */
    function normalizeCategory (cat) {
        if (!cat) {
            return cat;
        }
        return cat.charAt(0).toLowerCase() + cat.slice(1);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', BudgetPlanner.init);
    } else {
        BudgetPlanner.init();
    }

    window.BudgetPlanner = BudgetPlanner;
})();

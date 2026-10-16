(function () {
    'use strict';

    /**
     * Budget Planner interactive module.
     * Manages pie chart, slider/input sync, dollar amount calculation, and summary table.
     */
    const BudgetPlanner = {};

    // Category definitions in order
    const categories = [
        'housing', 'food', 'transportation', 'utilities', 'insurance',
        'healthcare', 'savings', 'debtRepayment', 'entertainment', 'miscellaneous'
    ];

    const categoryLabels = {
        housing: 'Housing',
        food: 'Food',
        transportation: 'Transportation',
        utilities: 'Utilities',
        insurance: 'Insurance',
        healthcare: 'Healthcare',
        savings: 'Savings',
        debtRepayment: 'Debt Repayment',
        entertainment: 'Entertainment',
        miscellaneous: 'Miscellaneous'
    };

    const categoryColors = [
        '#0078d4', '#00b7c3', '#71b307', '#d83b01', '#8764b8',
        '#e4396c', '#107c10', '#498205', '#005a9e', '#605e5c'
    ];

    let chart = null;
    let currentData = {};

    /**
     * Initialize the budget planner.
     */
    BudgetPlanner.init = function () {
        const initialData = window.budgetInitialData;
        if (initialData) {
            currentData = {
                monthlyIncome: initialData.monthlyIncome || 0,
                housing: initialData.housing || 0,
                food: initialData.food || 0,
                transportation: initialData.transportation || 0,
                utilities: initialData.utilities || 0,
                insurance: initialData.insurance || 0,
                healthcare: initialData.healthcare || 0,
                savings: initialData.savings || 0,
                debtRepayment: initialData.debtRepayment || 0,
                entertainment: initialData.entertainment || 0,
                miscellaneous: initialData.miscellaneous || 0
            };
        }

        // Sync initial slider values from data
        syncSlidersFromData();

        // Bind events
        bindEvents();

        // Initialize chart
        initChart();

        // Initial update
        updateAll();
    };

    /**
     * Sync slider and input values from currentData.
     */
    function syncSlidersFromData() {
        categories.forEach(function (cat) {
            const value = currentData[cat] || 0;
            const slider = document.getElementById('slider-' + capitalize(cat));
            const input = document.querySelector('.budget-percent-input[data-category="' + capitalize(cat) + '"]');
            const amountInput = document.querySelector('.budget-amount-input[data-category="' + capitalize(cat) + '"]');

            if (slider) {
                slider.value = value;
            }
            if (input) {
                input.value = value;
            }
            if (amountInput) {
                const income = currentData.monthlyIncome || 0;
                const amount = (income * value) / 100;
                amountInput.value = amount > 0 ? amount.toFixed(2) : '';
            }
        });

        // Sync income
        const incomeInput = document.querySelector('.budget-income-input');
        if (incomeInput && currentData.monthlyIncome) {
            incomeInput.value = currentData.monthlyIncome;
        }
    }

    /**
     * Bind all event listeners.
     */
    function bindEvents() {
        // Income input
        const incomeInput = document.querySelector('.budget-income-input');
        if (incomeInput) {
            incomeInput.addEventListener('input', function () {
                currentData.monthlyIncome = parseFloat(this.value) || 0;
                updateAll();
            });
        }

        // Sliders
        const sliders = document.querySelectorAll('.budget-slider');
        sliders.forEach(function (slider) {
            slider.addEventListener('input', function () {
                const cat = this.getAttribute('data-category');
                const value = parseFloat(this.value) || 0;
                const normalizedCat = normalizeCategory(cat);
                currentData[normalizedCat] = value;

                // Sync the percentage number input (this is the asp-for bound input)
                const input = document.querySelector('.budget-percent-input[data-category="' + cat + '"]');
                if (input) {
                    input.value = value;
                }

                // Sync dollar amount input
                syncAmountInput(cat, value);

                updateAll();
            });
        });

        // Number inputs (these are the asp-for bound inputs)
        const inputs = document.querySelectorAll('.budget-percent-input');
        inputs.forEach(function (input) {
            input.addEventListener('input', function () {
                const cat = this.getAttribute('data-category');
                let value = parseFloat(this.value) || 0;

                // Clamp to 0-100
                if (value < 0) value = 0;
                if (value > 100) value = 100;

                const normalizedCat = normalizeCategory(cat);
                currentData[normalizedCat] = value;

                // Sync slider
                const slider = document.getElementById('slider-' + cat);
                if (slider) {
                    slider.value = value;
                }

                // Sync dollar amount input
                syncAmountInput(cat, value);

                updateAll();
            });
        });

        // Dollar amount inputs
        const amountInputs = document.querySelectorAll('.budget-amount-input');
        amountInputs.forEach(function (amountInput) {
            amountInput.addEventListener('input', function () {
                const cat = this.getAttribute('data-category');
                const amount = parseFloat(this.value) || 0;
                const income = currentData.monthlyIncome || 0;

                // Convert amount to percentage
                let percent = 0;
                if (income > 0) {
                    percent = (amount * 100) / income;
                }

                // Clamp percentage to 0-100
                if (percent < 0) percent = 0;
                if (percent > 100) percent = 100;

                const normalizedCat = normalizeCategory(cat);
                currentData[normalizedCat] = percent;

                // Sync slider
                const slider = document.getElementById('slider-' + cat);
                if (slider) {
                    slider.value = percent;
                }

                // Sync percentage input
                const percentInput = document.querySelector('.budget-percent-input[data-category="' + cat + '"]');
                if (percentInput) {
                    percentInput.value = percent;
                }

                updateAll();
            });
        });

        // Sync JS state to form inputs before form submission, so the POST carries current values
        const form = document.getElementById('budgetForm');
        if (form) {
            form.addEventListener('submit', function () {
                syncFormInputsFromData();
            });
        }
    }

    /**
     * Sync all form input values from currentData so the form POST carries the latest values.
     */
    function syncFormInputsFromData() {
        // Sync income
        const incomeInput = document.querySelector('.budget-income-input');
        if (incomeInput) {
            incomeInput.value = currentData.monthlyIncome || 0;
        }

        // Sync each category percentage input
        categories.forEach(function (cat) {
            const value = currentData[cat] || 0;
            // The asp-for input ID matches the PascalCase property name (e.g. "Housing")
            const capitalized = capitalize(cat);
            const input = document.getElementById(capitalized);
            if (input) {
                input.value = value;
            }
            // Also update the data-category input as fallback
            const dataInput = document.querySelector('.budget-percent-input[data-category="' + capitalized + '"]');
            if (dataInput) {
                dataInput.value = value;
            }
        });
    }

    /**
     * Update all UI components: chart, amounts, summary, remaining.
     */
    function updateAll() {
        updateAmounts();
        updateRemaining();
        updateTotalBadge();
        updateSummaryTable();
        updateChart();
    }

    /**
     * Update dollar amount displays for each category.
     */
    function updateAmounts() {
        const income = currentData.monthlyIncome || 0;

        categories.forEach(function (cat) {
            const percent = currentData[cat] || 0;
            const amount = (income * percent) / 100;
            const capitalized = capitalize(cat);

            const amountEl = document.querySelector('.budget-category__amount[data-amount-for="' + capitalized + '"] span');
            if (amountEl) {
                amountEl.textContent = formatCurrency(amount);
            }

            // Sync the editable amount input (only if not currently focused, to avoid disrupting user typing)
            const amountInput = document.querySelector('.budget-amount-input[data-category="' + capitalized + '"]');
            if (amountInput && document.activeElement !== amountInput) {
                amountInput.value = amount > 0 ? amount.toFixed(2) : '';
            }
        });
    }

    /**
     * Sync the dollar amount input for a category based on its percentage.
     * @param {string} cat - Category name (PascalCase)
     * @param {number} percent - Percentage value
     */
    function syncAmountInput(cat, percent) {
        const income = currentData.monthlyIncome || 0;
        const amount = (income * percent) / 100;
        const amountInput = document.querySelector('.budget-amount-input[data-category="' + cat + '"]');
        if (amountInput && document.activeElement !== amountInput) {
            amountInput.value = amount > 0 ? amount.toFixed(2) : '';
        }
    }

    /**
     * Update remaining allocation indicator.
     */
    function updateRemaining() {
        const total = getTotal();
        const remaining = 100 - total;
        const remainingEl = document.getElementById('budgetRemainingValue');
        const remainingContainer = document.getElementById('budgetRemaining');

        if (remainingEl) {
            remainingEl.textContent = remaining.toFixed(1) + '%';
        }

        if (remainingContainer) {
            remainingContainer.classList.remove('budget-remaining--over', 'budget-remaining--exact');

            if (total > 100) {
                remainingContainer.classList.add('budget-remaining--over');
            } else if (Math.abs(total - 100) < 0.01) {
                remainingContainer.classList.add('budget-remaining--exact');
            }
        }
    }

    /**
     * Update total allocation badge.
     */
    function updateTotalBadge() {
        const total = getTotal();
        const badge = document.getElementById('budgetTotalBadge');
        const totalSpan = document.getElementById('budgetTotalPercent');

        if (totalSpan) {
            totalSpan.textContent = total.toFixed(1);
        }

        if (badge) {
            badge.classList.remove('budget-total-badge--over', 'budget-total-badge--exact');

            if (total > 100) {
                badge.classList.add('budget-total-badge--over');
            } else if (Math.abs(total - 100) < 0.01) {
                badge.classList.add('budget-total-badge--exact');
            }
        }
    }

    /**
     * Update the summary table.
     */
    function updateSummaryTable() {
        const income = currentData.monthlyIncome || 0;
        const tbody = document.getElementById('budgetSummaryBody');
        if (!tbody) return;

        let html = '';
        let totalPercent = 0;
        let totalAmount = 0;

        categories.forEach(function (cat, index) {
            const percent = currentData[cat] || 0;
            const amount = (income * percent) / 100;
            totalPercent += percent;
            totalAmount += amount;

            html += '<tr>';
            html += '<td><span class="budget-summary-row-label"><span class="budget-summary-row-dot" style="background:' + categoryColors[index] + ';"></span><span class="budget-summary-row-name" title="' + categoryLabels[cat] + '">' + categoryLabels[cat] + '</span></span></td>';
            html += '<td class="text-end">' + percent.toFixed(1) + '%</td>';
            html += '<td class="text-end">$' + formatCurrency(amount) + '</td>';
            html += '</tr>';
        });

        tbody.innerHTML = html;

        // Update totals
        const totalPercentEl = document.getElementById('budgetSummaryTotalPercent');
        const totalAmountEl = document.getElementById('budgetSummaryTotalAmount');

        if (totalPercentEl) {
            totalPercentEl.textContent = totalPercent.toFixed(1) + '%';
        }
        if (totalAmountEl) {
            totalAmountEl.textContent = '$' + formatCurrency(totalAmount);
        }
    }

    /**
     * Initialize the pie chart.
     */
    function initChart() {
        const canvas = document.getElementById('budgetPieChart');
        if (!canvas || typeof Chart === 'undefined') return;

        const ctx = canvas.getContext('2d');

        chart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: categories.map(function (c) {
                    return categoryLabels[c];
                }),
                datasets: [{
                    data: categories.map(function (c) {
                        return currentData[c] || 0;
                    }),
                    backgroundColor: categoryColors,
                    borderWidth: 2,
                    borderColor: '#ffffff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = context.parsed || 0;
                                const income = currentData.monthlyIncome || 0;
                                const amount = (income * value) / 100;
                                return label + ': ' + value.toFixed(1) + '% ($' + formatCurrency(amount) + ')';
                            }
                        }
                    }
                },
                cutout: '55%'
            }
        });
    }

    /**
     * Update the pie chart with current data.
     */
    function updateChart() {
        if (!chart) return;

        chart.data.datasets[0].data = categories.map(function (c) {
            return currentData[c] || 0;
        });
        chart.update('none');
    }

    /**
     * Calculate total allocation percentage.
     * @returns {number} Total percentage
     */
    function getTotal() {
        let total = 0;
        categories.forEach(function (cat) {
            total += currentData[cat] || 0;
        });
        return total;
    }

    /**
     * Format a number as currency (no $ sign, commas, no decimals for whole numbers).
     * @param {number} value - The amount to format
     * @returns {string} Formatted currency string
     */
    function formatCurrency(value) {
        if (value === Math.floor(value)) {
            return value.toLocaleString('en-US');
        }
        return value.toLocaleString('en-US', {minimumFractionDigits: 2, maximumFractionDigits: 2});
    }

    /**
     * Map a camelCase category key to the PascalCase property name used by asp-for inputs.
     * @param {string} str - The camelCase category key (e.g., "debtRepayment")
     * @returns {string} PascalCase property name (e.g., "DebtRepayment")
     */
    function capitalize(str) {
        if (!str) return str;
        // Explicit mapping from camelCase JS keys to PascalCase C# property names
        const map = {
            housing: 'Housing',
            food: 'Food',
            transportation: 'Transportation',
            utilities: 'Utilities',
            insurance: 'Insurance',
            healthcare: 'Healthcare',
            savings: 'Savings',
            debtRepayment: 'DebtRepayment',
            entertainment: 'Entertainment',
            miscellaneous: 'Miscellaneous'
        };
        return map[str] || str;
    }

    /**
     * Normalize category name to lowercase camelCase for data keys.
     * @param {string} cat - Category name (e.g., "DebtRepayment")
     * @returns {string} Normalized category (e.g., "debtRepayment")
     */
    function normalizeCategory(cat) {
        if (!cat) return cat;
        return cat.charAt(0).toLowerCase() + cat.slice(1);
    }

    // DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', BudgetPlanner.init);
    } else {
        BudgetPlanner.init();
    }

    window.BudgetPlanner = BudgetPlanner;

})();

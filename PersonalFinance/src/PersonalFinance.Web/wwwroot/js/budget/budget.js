(function () {
    'use strict';

    /**
     * Budget Planner interactive module.
     * Manages pie chart, slider/input sync, dollar amount calculation, and summary table.
     * All state is stored in integer cents to avoid floating-point drift.
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
    let incomeCents = 0;
    const amountCents = {};

    /**
     * Initialize the budget planner.
     */
    BudgetPlanner.init = function () {
        const initialData = window.budgetInitialData;
        if (initialData) {
            incomeCents = Math.round(parseFloat(initialData.monthlyIncome) * 100) || 0;
            categories.forEach(function (cat) {
                amountCents[cat] = Math.round(parseFloat(initialData[cat]) * 100) || 0;
            });
        }

        // Sync initial input values from data
        syncInputsFromData();

        // Set color box backgrounds
        setColorBoxes();

        // Bind events
        bindEvents();

        // Initialize chart
        initChart();

        // Initial update
        updateAll();
    };

    /**
     * Set background colors on category color boxes to match chart slice colors.
     */
    function setColorBoxes () {
        const colorBoxes = document.querySelectorAll('.budget-category-color-box');
        colorBoxes.forEach(function (box) {
            const index = parseInt(box.getAttribute('data-color-index'), 10);
            if (index >= 0 && index < categoryColors.length) {
                box.style.backgroundColor = categoryColors[index];
            }
        });
    }

    /**
     * Sync all input values from current state.
     */
    function syncInputsFromData () {
        categories.forEach(function (cat) {
            const cents = amountCents[cat] || 0;
            const amount = cents / 100;
            const percent = toPercent(cents);
            const capitalized = capitalize(cat);

            const slider = document.getElementById('slider-' + capitalized);
            const percentInput = document.querySelector('.budget-percent-input[data-category="' + capitalized + '"]');
            const amountInput = document.querySelector('.budget-amount-input[data-category="' + capitalized + '"]');

            if (slider) {
                slider.value = percent;
            }
            if (percentInput) {
                percentInput.value = percent > 0 ? percent.toFixed(2) : '';
            }
            if (amountInput) {
                amountInput.value = amount > 0 ? amount.toFixed(2) : '';
            }
        });

        // Sync income
        const incomeInput = document.querySelector('.budget-income-input');
        if (incomeInput && incomeCents > 0) {
            incomeInput.value = (incomeCents / 100).toFixed(2);
        }
    }

    /**
     * Bind all event listeners.
     */
    function bindEvents () {
        // Income input
        const incomeInput = document.querySelector('.budget-income-input');
        if (incomeInput) {
            incomeInput.addEventListener('input', function () {
                incomeCents = Math.round(parseFloat(this.value) * 100) || 0;
                updateAll();
            });
        }

        // Sliders
        const sliders = document.querySelectorAll('.budget-slider');
        sliders.forEach(function (slider) {
            slider.addEventListener('input', function () {
                const cat = this.getAttribute('data-category');
                const pct = parseFloat(this.value) || 0;
                const normalizedCat = normalizeCategory(cat);
                setCategoryFromPercent(normalizedCat, pct);
                updateAll();
            });
        });

        // Percent inputs (unbound helper controls)
        const percentInputs = document.querySelectorAll('.budget-percent-input');
        percentInputs.forEach(function (input) {
            input.addEventListener('input', function () {
                const cat = this.getAttribute('data-category');
                let pct = parseFloat(this.value) || 0;

                // Clamp to 0-100
                if (pct < 0) {
                    pct = 0;
                }
                if (pct > 100) {
                    pct = 100;
                }

                const normalizedCat = normalizeCategory(cat);
                setCategoryFromPercent(normalizedCat, pct);
                updateAll();
            });
        });

        // Dollar amount inputs (model-bound fields)
        const amountInputs = document.querySelectorAll('.budget-amount-input');
        amountInputs.forEach(function (amountInput) {
            amountInput.addEventListener('input', function () {
                const cat = this.getAttribute('data-category');
                const cents = Math.round(parseFloat(this.value) * 100) || 0;
                const normalizedCat = normalizeCategory(cat);
                setCategoryFromCents(normalizedCat, cents);
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
     * Set a category's amount from a percentage value.
     * @param {string} cat - Normalized category key
     * @param {number} pct - Percentage value
     */
    function setCategoryFromPercent (cat, pct) {
        const cents = Math.round((incomeCents * pct) / 100);
        amountCents[cat] = cents;

        // Sync slider
        const capitalized = capitalize(cat);
        const slider = document.getElementById('slider-' + capitalized);
        if (slider) {
            slider.value = pct;
        }

        // Sync percent input
        const percentInput = document.querySelector('.budget-percent-input[data-category="' + capitalized + '"]');
        if (percentInput && document.activeElement !== percentInput) {
            percentInput.value = pct > 0 ? pct.toFixed(2) : '';
        }
    }

    /**
     * Set a category's amount from cents value.
     * @param {string} cat - Normalized category key
     * @param {number} cents - Amount in cents
     */
    function setCategoryFromCents (cat, cents) {
        amountCents[cat] = cents;

        // Sync slider
        const capitalized = capitalize(cat);
        const slider = document.getElementById('slider-' + capitalized);
        if (slider) {
            const pct = toPercent(cents);
            slider.value = pct;
        }

        // Sync percent input
        const percentInput = document.querySelector('.budget-percent-input[data-category="' + capitalized + '"]');
        if (percentInput && document.activeElement !== percentInput) {
            const pct = toPercent(cents);
            percentInput.value = pct > 0 ? pct.toFixed(2) : '';
        }
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
        // Sync income
        const incomeInput = document.querySelector('.budget-income-input');
        if (incomeInput) {
            incomeInput.value = (incomeCents / 100).toFixed(2);
        }

        // Sync each category amount input (the asp-for bound field)
        categories.forEach(function (cat) {
            const cents = amountCents[cat] || 0;
            const amount = cents / 100;
            const capitalized = capitalize(cat);

            // The asp-for input ID matches the PascalCase property name (e.g. "Housing")
            const input = document.getElementById(capitalized);
            if (input) {
                input.value = amount.toFixed(2);
            }
        });
    }

    /**
     * Update all UI components: chart, amounts, summary, remaining.
     */
    function updateAll () {
        updateAmounts();
        updateRemaining();
        updateTotalBadge();
        updateSummaryTable();
        updateChart();
    }

    /**
     * Update dollar amount displays for each category.
     */
    function updateAmounts () {
        categories.forEach(function (cat) {
            const cents = amountCents[cat] || 0;
            const amount = cents / 100;
            const capitalized = capitalize(cat);

            const amountEl = document.querySelector('.budget-category__amount[data-amount-for="' + capitalized + '"] span');
            if (amountEl) {
                amountEl.textContent = formatCurrency(amount);
            }

            // Sync the editable amount input (only if not currently focused)
            const amountInput = document.querySelector('.budget-amount-input[data-category="' + capitalized + '"]');
            if (amountInput && document.activeElement !== amountInput) {
                amountInput.value = amount > 0 ? amount.toFixed(2) : '';
            }
        });
    }

    /**
     * Update remaining allocation indicator.
     */
    function updateRemaining () {
        const totalCents = getTotalCents();
        const remainingCents = incomeCents - totalCents;
        const remainingAmount = remainingCents / 100;
        const remainingPct = incomeCents > 0 ? (remainingCents / incomeCents) * 100 : 0;

        const remainingEl = document.getElementById('budgetRemainingValue');
        const remainingContainer = document.getElementById('budgetRemaining');

        if (remainingEl) {
            remainingEl.textContent = '$' + formatCurrency(Math.abs(remainingAmount)) + ' (' + remainingPct.toFixed(1) + '%)';
        }

        if (remainingContainer) {
            remainingContainer.classList.remove('budget-remaining--over', 'budget-remaining--exact');

            if (totalCents > incomeCents) {
                remainingContainer.classList.add('budget-remaining--over');
            } else if (remainingCents === 0) {
                remainingContainer.classList.add('budget-remaining--exact');
            }
        }
    }

    /**
     * Update total allocation badge.
     */
    function updateTotalBadge () {
        const totalCents = getTotalCents();
        const totalAmount = totalCents / 100;
        const incomeAmount = incomeCents / 100;

        const badge = document.getElementById('budgetTotalBadge');
        const totalSpan = document.getElementById('budgetTotalAmount');
        const incomeSpan = document.getElementById('budgetIncomeAmount');

        if (totalSpan) {
            totalSpan.textContent = '$' + formatCurrency(totalAmount);
        }
        if (incomeSpan) {
            incomeSpan.textContent = '$' + formatCurrency(incomeAmount);
        }

        if (badge) {
            badge.classList.remove('budget-total-badge--over', 'budget-total-badge--exact');

            if (totalCents > incomeCents) {
                badge.classList.add('budget-total-badge--over');
            } else if (totalCents === incomeCents && incomeCents > 0) {
                badge.classList.add('budget-total-badge--exact');
            }
        }
    }

    /**
     * Update the summary list using DOM APIs and textContent.
     */
    function updateSummaryTable () {
        const list = document.getElementById('budgetSummaryList');
        if (!list) {
            return;
        }

        // Find the max amount for scaling progress bars
        let maxCents = 0;
        categories.forEach(function (cat) {
            const cents = amountCents[cat] || 0;
            if (cents > maxCents) {
                maxCents = cents;
            }
        });

        let totalCents = 0;

        // Clear existing items
        list.innerHTML = '';

        categories.forEach(function (cat, index) {
            const cents = amountCents[cat] || 0;
            const amount = cents / 100;
            const percent = toPercent(cents);
            totalCents += cents;

            const barWidth = maxCents > 0 ? (cents / maxCents) * 100 : 0;
            const color = categoryColors[index];

            // Build row with DOM APIs
            const item = document.createElement('div');
            item.className = 'budget-summary-item';

            const dot = document.createElement('span');
            dot.className = 'budget-summary-item__dot';
            dot.style.background = color;
            dot.setAttribute('aria-hidden', 'true');

            const name = document.createElement('span');
            name.className = 'budget-summary-item__name';
            name.title = categoryLabels[cat];
            name.textContent = categoryLabels[cat];

            const barWrapper = document.createElement('div');
            barWrapper.className = 'budget-summary-item__bar-wrapper';

            const bar = document.createElement('div');
            bar.className = 'budget-summary-item__bar';
            bar.style.width = barWidth.toFixed(1) + '%';
            bar.style.background = color;

            barWrapper.appendChild(bar);

            const values = document.createElement('div');
            values.className = 'budget-summary-item__values';

            const percentSpan = document.createElement('span');
            percentSpan.className = 'budget-summary-item__percent';
            percentSpan.textContent = percent.toFixed(1) + '%';

            const amountSpan = document.createElement('span');
            amountSpan.className = 'budget-summary-item__amount';
            amountSpan.textContent = '$' + formatCurrency(amount);

            values.appendChild(percentSpan);
            values.appendChild(amountSpan);

            item.appendChild(dot);
            item.appendChild(name);
            item.appendChild(barWrapper);
            item.appendChild(values);

            list.appendChild(item);
        });

        // Update totals
        const totalAmount = totalCents / 100;
        const totalPercent = toPercent(totalCents);

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
     * Custom Chart.js plugin that draws labels with leader lines around the doughnut chart.
     * Scales label positions and adjusts layout padding based on chart size for responsiveness.
     */
    const leaderLinePlugin = {
        id: 'leaderLines',
        beforeUpdate: function (chart) {
            // Dynamically adjust layout padding based on chart canvas size
            const canvasWidth = chart.width;
            if (canvasWidth > 0) {
                let sidePad, topPad;
                if (canvasWidth >= 340) {
                    sidePad = 55;
                    topPad = 20;
                } else if (canvasWidth >= 260) {
                    sidePad = 40;
                    topPad = 15;
                } else if (canvasWidth >= 180) {
                    sidePad = 30;
                    topPad = 10;
                } else {
                    sidePad = 20;
                    topPad = 5;
                }
                chart.options.layout.padding.top = topPad;
                chart.options.layout.padding.bottom = topPad;
                chart.options.layout.padding.left = sidePad;
                chart.options.layout.padding.right = sidePad;
            }
        },
        afterDatasetsDraw: function (chart) {
            const ctx = chart.ctx;
            const dataset = chart.data.datasets[0];
            const meta = chart.getDatasetMeta(0);
            if (!meta || !meta.data) {
                return;
            }

            const total = dataset.data.reduce(function (sum, val) {
                return sum + val;
            }, 0);
            if (total === 0) {
                return;
            }

            const centerX = (meta.data[0].x + meta.data[meta.data.length - 1].x) / 2;
            const centerY = (meta.data[0].y + meta.data[meta.data.length - 1].y) / 2;
            const outerRadius = meta.data[0].outerRadius;

            // Scale label offsets based on chart size for responsiveness
            let scale = outerRadius / 140; // 140 is the approximate radius at max chart size
            scale = Math.max(0.5, Math.min(scale, 1.2)); // Clamp scale to reasonable range

            // Hide labels on very small charts where they won't fit
            if (outerRadius < 60) {
                return;
            }

            // Label positioning constants (scaled)
            const labelGap = 18 * scale;
            const lineMidGap = 8 * scale;
            const labelRadius = outerRadius + labelGap;
            const lineMidRadius = outerRadius + lineMidGap;
            const minLabelDist = 22 * scale;
            const minAngleGap = 0.15; // Minimum radians between labels to avoid overlap

            ctx.save();
            const fontSize = Math.max(9, Math.round(11 * scale));
            ctx.font = fontSize + 'px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
            ctx.fillStyle = '#505050';
            ctx.textBaseline = 'middle';

            const placedLabels = [];

            dataset.data.forEach(function (value, index) {
                if (value === 0) {
                    return;
                }

                const arc = meta.data[index];
                const startAngle = arc.startAngle;
                const endAngle = arc.endAngle;
                const midAngle = (startAngle + endAngle) / 2;

                // Point on outer edge of the slice
                const outerX = centerX + Math.cos(midAngle) * outerRadius;
                const outerY = centerY + Math.sin(midAngle) * outerRadius;

                // Mid point for the leader line (perpendicular to the angle)
                const midX = centerX + Math.cos(midAngle) * lineMidRadius;
                const midY = centerY + Math.sin(midAngle) * lineMidRadius;

                // Label position
                const labelX = centerX + Math.cos(midAngle) * labelRadius;
                const labelY = centerY + Math.sin(midAngle) * labelRadius;

                // Check for overlap with already placed labels
                let tooClose = false;
                for (let i = 0; i < placedLabels.length; i++) {
                    const dx = labelX - placedLabels[i].x;
                    const dy = labelY - placedLabels[i].y;
                    const dist = Math.sqrt(dx * dx + dy * dy);
                    if (dist < minLabelDist) {
                        tooClose = true;
                        break;
                    }
                }

                // Only draw if slice is significant enough and not overlapping
                const sliceAngle = endAngle - startAngle;
                if (sliceAngle < minAngleGap || tooClose) {
                    return;
                }

                // Draw leader line
                ctx.beginPath();
                ctx.moveTo(outerX, outerY);
                ctx.lineTo(midX, midY);
                ctx.lineTo(labelX, labelY);
                ctx.strokeStyle = dataset.backgroundColor[index];
                ctx.lineWidth = 1.5 * scale;
                ctx.globalAlpha = 0.6;
                ctx.stroke();
                ctx.globalAlpha = 1;

                // Draw dot at the outer edge
                ctx.beginPath();
                ctx.arc(outerX, outerY, 2 * scale, 0, Math.PI * 2);
                ctx.fillStyle = dataset.backgroundColor[index];
                ctx.fill();

                // Draw label text — percentage only
                const percent = ((value / total) * 100).toFixed(1) + '%';
                const displayText = percent;

                // Determine text alignment based on angle
                let textX = labelX;
                let textAlign = 'left';
                const textOffset = 4 * scale;
                if (midAngle > Math.PI / 2 && midAngle < Math.PI * 1.5) {
                    textAlign = 'right';
                    textX = labelX - textOffset;
                } else {
                    textX = labelX + textOffset;
                }

                ctx.textAlign = textAlign;
                ctx.fillStyle = '#505050';
                ctx.fillText(displayText, textX, labelY);

                placedLabels.push({ x: labelX, y: labelY });
            });

            ctx.restore();
        }
    };

    /**
     * Initialize the pie chart.
     */
    function initChart () {
        Chart.register(leaderLinePlugin);

        const canvas = document.getElementById('budgetPieChart');
        if (!canvas || typeof Chart === 'undefined') {
            return;
        }

        const ctx = canvas.getContext('2d');

        chart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: categories.map(function (c) {
                    return categoryLabels[c];
                }),
                datasets: [{
                    data: categories.map(function (c) {
                        return toPercent(amountCents[c] || 0);
                    }),
                    backgroundColor: categoryColors,
                    borderWidth: 2,
                    borderColor: '#ffffff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                layout: {
                    padding: {
                        top: 20,
                        right: 45,
                        bottom: 20,
                        left: 45
                    }
                },
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = context.parsed || 0;
                                const income = incomeCents / 100;
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
    function updateChart () {
        if (!chart) {
            return;
        }

        chart.data.datasets[0].data = categories.map(function (c) {
            return toPercent(amountCents[c] || 0);
        });
        chart.update('none');
    }

    /**
     * Calculate total allocation in cents.
     * @returns {number} Total cents
     */
    function getTotalCents () {
        let total = 0;
        categories.forEach(function (cat) {
            total += amountCents[cat] || 0;
        });
        return total;
    }

    /**
     * Format a number as currency (no $ sign, commas, no decimals for whole numbers).
     * @param {number} value - The amount to format
     * @returns {string} Formatted currency string
     */
    function formatCurrency (value) {
        if (value === Math.floor(value)) {
            return value.toLocaleString('en-US');
        }
        return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    /**
     * Map a camelCase category key to the PascalCase property name used by asp-for inputs.
     * @param {string} str - The camelCase category key (e.g., "debtRepayment")
     * @returns {string} PascalCase property name (e.g., "DebtRepayment")
     */
    function capitalize (str) {
        if (!str) {
            return str;
        }
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
    function normalizeCategory (cat) {
        if (!cat) {
            return cat;
        }
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

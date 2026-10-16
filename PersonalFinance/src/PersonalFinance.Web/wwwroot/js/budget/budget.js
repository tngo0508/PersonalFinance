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
    function setColorBoxes() {
        var colorBoxes = document.querySelectorAll('.budget-category-color-box');
        colorBoxes.forEach(function (box) {
            var index = parseInt(box.getAttribute('data-color-index'), 10);
            if (index >= 0 && index < categoryColors.length) {
                box.style.backgroundColor = categoryColors[index];
            }
        });
    }

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
     * Update the summary list.
     */
    function updateSummaryTable() {
        const income = currentData.monthlyIncome || 0;
        const list = document.getElementById('budgetSummaryList');
        if (!list) return;

        let html = '';
        let totalPercent = 0;
        let totalAmount = 0;

        // Find the max percent for scaling progress bars
        let maxPercent = 0;
        categories.forEach(function (cat) {
            const p = currentData[cat] || 0;
            if (p > maxPercent) maxPercent = p;
        });

        categories.forEach(function (cat, index) {
            const percent = currentData[cat] || 0;
            const amount = (income * percent) / 100;
            totalPercent += percent;
            totalAmount += amount;

            const barWidth = maxPercent > 0 ? (percent / maxPercent) * 100 : 0;
            const color = categoryColors[index];

            html += '<div class="budget-summary-item">';
            html += '<span class="budget-summary-item__dot" style="background:' + color + ';" aria-hidden="true"></span>';
            html += '<span class="budget-summary-item__name" title="' + categoryLabels[cat] + '">' + categoryLabels[cat] + '</span>';
            html += '<div class="budget-summary-item__bar-wrapper">';
            html += '<div class="budget-summary-item__bar" style="width:' + barWidth.toFixed(1) + '%;background:' + color + ';"></div>';
            html += '</div>';
            html += '<div class="budget-summary-item__values">';
            html += '<span class="budget-summary-item__percent">' + percent.toFixed(1) + '%</span>';
            html += '<span class="budget-summary-item__amount">$' + formatCurrency(amount) + '</span>';
            html += '</div>';
            html += '</div>';
        });

        list.innerHTML = html;

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
     * Custom Chart.js plugin that draws labels with leader lines around the doughnut chart.
     * Scales label positions and adjusts layout padding based on chart size for responsiveness.
     */
    var leaderLinePlugin = {
        id: 'leaderLines',
        beforeUpdate: function (chart) {
            // Dynamically adjust layout padding based on chart canvas size
            var canvasWidth = chart.width;
            if (canvasWidth > 0) {
                var sidePad, topPad;
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
            var ctx = chart.ctx;
            var dataset = chart.data.datasets[0];
            var meta = chart.getDatasetMeta(0);
            if (!meta || !meta.data) return;

            var total = dataset.data.reduce(function (sum, val) {
                return sum + val;
            }, 0);
            if (total === 0) return;

            var centerX = (meta.data[0].x + meta.data[meta.data.length - 1].x) / 2;
            var centerY = (meta.data[0].y + meta.data[meta.data.length - 1].y) / 2;
            var outerRadius = meta.data[0].outerRadius;

            // Scale label offsets based on chart size for responsiveness
            var scale = outerRadius / 140; // 140 is the approximate radius at max chart size
            scale = Math.max(0.5, Math.min(scale, 1.2)); // Clamp scale to reasonable range

            // Hide labels on very small charts where they won't fit
            if (outerRadius < 60) return;

            // Label positioning constants (scaled)
            var labelGap = 18 * scale;
            var lineMidGap = 8 * scale;
            var labelRadius = outerRadius + labelGap;
            var lineMidRadius = outerRadius + lineMidGap;
            var minLabelDist = 22 * scale;
            var minAngleGap = 0.15; // Minimum radians between labels to avoid overlap

            ctx.save();
            var fontSize = Math.max(9, Math.round(11 * scale));
            ctx.font = fontSize + 'px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
            ctx.fillStyle = '#505050';
            ctx.textBaseline = 'middle';

            var placedLabels = [];

            dataset.data.forEach(function (value, index) {
                if (value === 0) return;

                var arc = meta.data[index];
                var startAngle = arc.startAngle;
                var endAngle = arc.endAngle;
                var midAngle = (startAngle + endAngle) / 2;

                // Point on outer edge of the slice
                var outerX = centerX + Math.cos(midAngle) * outerRadius;
                var outerY = centerY + Math.sin(midAngle) * outerRadius;

                // Mid point for the leader line (perpendicular to the angle)
                var midX = centerX + Math.cos(midAngle) * lineMidRadius;
                var midY = centerY + Math.sin(midAngle) * lineMidRadius;

                // Label position
                var labelX = centerX + Math.cos(midAngle) * labelRadius;
                var labelY = centerY + Math.sin(midAngle) * labelRadius;

                // Check for overlap with already placed labels
                var tooClose = false;
                for (var i = 0; i < placedLabels.length; i++) {
                    var dx = labelX - placedLabels[i].x;
                    var dy = labelY - placedLabels[i].y;
                    var dist = Math.sqrt(dx * dx + dy * dy);
                    if (dist < minLabelDist) {
                        tooClose = true;
                        break;
                    }
                }

                // Only draw if slice is significant enough and not overlapping
                var sliceAngle = endAngle - startAngle;
                if (sliceAngle < minAngleGap || tooClose) return;

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
                var percent = ((value / total) * 100).toFixed(1) + '%';
                var displayText = percent;

                // Determine text alignment based on angle
                var textX = labelX;
                var textAlign = 'left';
                var textOffset = 4 * scale;
                if (midAngle > Math.PI / 2 && midAngle < Math.PI * 1.5) {
                    textAlign = 'right';
                    textX = labelX - textOffset;
                } else {
                    textX = labelX + textOffset;
                }

                ctx.textAlign = textAlign;
                ctx.fillStyle = '#505050';
                ctx.fillText(displayText, textX, labelY);

                placedLabels.push({x: labelX, y: labelY});
            });

            ctx.restore();
        }
    };

    /**
     * Initialize the pie chart.
     */
    function initChart() {
        Chart.register(leaderLinePlugin);

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

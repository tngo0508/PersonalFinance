/**
 * Investment Calculator — Compound growth projection.
 */
(function () {
    'use strict';

    let calcChartTooltip = {visible: false, x: 0, y: 0, text: ''};
    let calcChartResizeTimer = null;

    /**
     * Format a currency value.
     * @param {number} value - The value to format.
     * @returns {string} The formatted currency string.
     */
    function formatCurrency(value) {
        return new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: 'USD',
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(value);
    }

    /**
     * Initialize the calculator inputs and event listeners.
     */
    function initCalculator() {
        const inputs = [
            {
                rangeId: 'calcInitialRange',
                numId: 'calcInitial',
                valId: 'calcInitialValue',
                format: v => formatCurrency(v)
            },
            {
                rangeId: 'calcMonthlyRange',
                numId: 'calcMonthly',
                valId: 'calcMonthlyValue',
                format: v => formatCurrency(v)
            },
            {rangeId: 'calcRateRange', numId: 'calcRate', valId: 'calcRateValue', format: v => v + '%'},
            {rangeId: 'calcYearsRange', numId: 'calcYears', valId: 'calcYearsValue', format: v => v + ' years'}
        ];

        inputs.forEach(({rangeId, numId, valId, format}) => {
            const range = document.getElementById(rangeId);
            const num = document.getElementById(numId);
            const val = document.getElementById(valId);
            if (!range || !num || !val) return;

            range.addEventListener('input', () => {
                num.value = range.value;
                val.textContent = format(range.value);
                calculateInvestment();
            });
            num.addEventListener('input', () => {
                range.value = num.value;
                val.textContent = format(num.value);
                calculateInvestment();
            });
        });

        calculateInvestment();
        setupCalcChartTooltip();
        setupCalcChartResize();
    }

    /**
     * Setup resize handler for responsive chart.
     */
    function setupCalcChartResize() {
        const container = document.querySelector('.calculator-chart-container');
        if (!container) return;

        const observer = new ResizeObserver(() => {
            if (calcChartResizeTimer) cancelAnimationFrame(calcChartResizeTimer);
            calcChartResizeTimer = requestAnimationFrame(() => {
                const initial = parseFloat(document.getElementById('calcInitial')?.value) || 0;
                const monthly = parseFloat(document.getElementById('calcMonthly')?.value) || 0;
                const rate = parseFloat(document.getElementById('calcRate')?.value) || 0;
                const years = parseInt(document.getElementById('calcYears')?.value) || 0;
                drawCalcChart(initial, monthly, rate, years);
                calcChartResizeTimer = null;
            });
        });
        observer.observe(container);
    }

    /**
     * Calculate investment growth and update the UI.
     */
    function calculateInvestment() {
        const initial = parseFloat(document.getElementById('calcInitial')?.value) || 0;
        const monthly = parseFloat(document.getElementById('calcMonthly')?.value) || 0;
        const rate = parseFloat(document.getElementById('calcRate')?.value) || 0;
        const years = parseInt(document.getElementById('calcYears')?.value) || 0;

        const monthlyRate = rate / 100 / 12;
        const months = years * 12;

        let futureValue;
        if (monthlyRate === 0) {
            futureValue = initial + monthly * months;
        } else {
            futureValue = initial * Math.pow(1 + monthlyRate, months) + monthly * ((Math.pow(1 + monthlyRate, months) - 1) / monthlyRate);
        }

        const totalContributions = initial + monthly * months;
        const totalInterest = futureValue - totalContributions;

        const fvEl = document.getElementById('calcFutureValue');
        const tcEl = document.getElementById('calcTotalContributions');
        const tiEl = document.getElementById('calcTotalInterest');

        if (fvEl) fvEl.textContent = formatCurrency(futureValue);
        if (tcEl) tcEl.textContent = formatCurrency(totalContributions);
        if (tiEl) tiEl.textContent = formatCurrency(totalInterest);

        drawCalcChart(initial, monthly, rate, years);
    }

    /**
     * Get chart dimensions based on container size.
     * @returns {Object|null} The chart dimensions.
     */
    function getCalcChartDimensions() {
        const canvas = document.getElementById('calcChart');
        if (!canvas) return null;
        const container = canvas.parentElement;
        if (!container) return null;
        const containerWidth = container.clientWidth - 24;
        const minWidth = 300;
        const maxWidth = 800;
        const w = Math.max(minWidth, Math.min(maxWidth, containerWidth));
        const h = Math.round(w * 0.7);
        return {w, h};
    }

    /**
     * Draw the investment growth chart.
     * @param {number} initial - Initial investment.
     * @param {number} monthly - Monthly contribution.
     * @param {number} rate - Annual return rate.
     * @param {number} years - Time period in years.
     */
    function drawCalcChart(initial, monthly, rate, years) {
        const canvas = document.getElementById('calcChart');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const dpr = window.devicePixelRatio || 1;
        const dims = getCalcChartDimensions();
        if (!dims) return;
        const w = dims.w;
        const h = dims.h;
        canvas.width = w * dpr;
        canvas.height = h * dpr;
        canvas.style.width = w + 'px';
        canvas.style.height = h + 'px';
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

        const padding = {top: 16, right: 24, bottom: 50, left: 68};
        const chartW = w - padding.left - padding.right;
        const chartH = h - padding.top - padding.bottom;

        ctx.clearRect(0, 0, w, h);

        // Generate data points
        const monthlyRate = rate / 100 / 12;
        const points = [];
        const contribPoints = [];
        const step = Math.max(1, Math.floor(years / 50));

        for (let y = 0; y <= years; y += step) {
            const m = y * 12;
            let fv;
            if (monthlyRate === 0) {
                fv = initial + monthly * m;
            } else {
                fv = initial * Math.pow(1 + monthlyRate, m) + monthly * ((Math.pow(1 + monthlyRate, m) - 1) / monthlyRate);
            }
            const contrib = initial + monthly * m;
            points.push({x: y, y: fv});
            contribPoints.push({x: y, y: contrib});
        }

        const maxVal = Math.max(...points.map(p => p.y), 1);
        const niceMax = niceScaleMax(maxVal);
        const gridLines = 5;

        function toCanvasX(year) {
            return padding.left + (year / years) * chartW;
        }

        function toCanvasY(val) {
            return padding.top + chartH - (val / niceMax) * chartH;
        }

        // Background
        ctx.fillStyle = '#fafbfc';
        roundRect(ctx, 0, 0, w, h, 8);
        ctx.fill();

        // Grid lines
        ctx.strokeStyle = '#e8eaed';
        ctx.lineWidth = 1;
        ctx.setLineDash([4, 4]);
        for (let i = 0; i <= gridLines; i++) {
            const y = padding.top + (chartH / gridLines) * i;
            ctx.beginPath();
            ctx.moveTo(padding.left, y);
            ctx.lineTo(w - padding.right, y);
            ctx.stroke();

            const val = niceMax - (niceMax / gridLines) * i;
            ctx.fillStyle = '#6b7280';
            ctx.font = '11px system-ui, -apple-system, sans-serif';
            ctx.textAlign = 'right';
            ctx.textBaseline = 'middle';
            ctx.fillText(formatAxisValue(val), padding.left - 10, y);
        }
        ctx.setLineDash([]);

        // X-axis labels
        ctx.textAlign = 'center';
        ctx.textBaseline = 'top';
        ctx.fillStyle = '#6b7280';
        ctx.font = '11px system-ui, -apple-system, sans-serif';
        const xStep = Math.max(1, Math.floor(years / 7));
        for (let y = 0; y <= years; y += xStep) {
            const x = toCanvasX(y);
            const tickY = padding.top + chartH;
            ctx.beginPath();
            ctx.strokeStyle = '#d1d5db';
            ctx.lineWidth = 1;
            ctx.moveTo(x, tickY);
            ctx.lineTo(x, tickY + 4);
            ctx.stroke();
            ctx.fillText(y === 0 ? 'Now' : y + 'y', x, tickY + 8);
        }

        // Smooth curve helper
        function drawSmoothCurve(data) {
            if (data.length < 2) return;
            const coords = data.map(p => ({x: toCanvasX(p.x), y: toCanvasY(p.y)}));
            ctx.beginPath();
            ctx.moveTo(coords[0].x, coords[0].y);
            for (let i = 0; i < coords.length - 1; i++) {
                const p0 = coords[Math.max(0, i - 1)];
                const p1 = coords[i];
                const p2 = coords[i + 1];
                const p3 = coords[Math.min(coords.length - 1, i + 2)];
                const cp1x = p1.x + (p2.x - p0.x) / 6;
                const cp1y = p1.y + (p2.y - p0.y) / 6;
                const cp2x = p2.x - (p3.x - p1.x) / 6;
                const cp2y = p2.y - (p3.y - p1.y) / 6;
                ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, p2.x, p2.y);
            }
        }

        // Gradient fills
        const contribGrad = ctx.createLinearGradient(0, padding.top, 0, padding.top + chartH);
        contribGrad.addColorStop(0, 'rgba(99, 132, 255, 0.25)');
        contribGrad.addColorStop(1, 'rgba(99, 132, 255, 0.02)');
        ctx.beginPath();
        ctx.moveTo(toCanvasX(0), padding.top + chartH);
        contribPoints.forEach(p => {
            ctx.lineTo(toCanvasX(p.x), toCanvasY(p.y));
        });
        ctx.lineTo(toCanvasX(years), padding.top + chartH);
        ctx.closePath();
        ctx.fillStyle = contribGrad;
        ctx.fill();

        const valueGrad = ctx.createLinearGradient(0, padding.top, 0, padding.top + chartH);
        valueGrad.addColorStop(0, 'rgba(16, 185, 129, 0.3)');
        valueGrad.addColorStop(1, 'rgba(16, 185, 129, 0.02)');
        ctx.beginPath();
        ctx.moveTo(toCanvasX(0), padding.top + chartH);
        points.forEach(p => {
            ctx.lineTo(toCanvasX(p.x), toCanvasY(p.y));
        });
        ctx.lineTo(toCanvasX(years), padding.top + chartH);
        ctx.closePath();
        ctx.fillStyle = valueGrad;
        ctx.fill();

        // Smooth lines
        drawSmoothCurve(contribPoints);
        ctx.strokeStyle = '#6366f1';
        ctx.lineWidth = 2.5;
        ctx.lineJoin = 'round';
        ctx.lineCap = 'round';
        ctx.stroke();

        drawSmoothCurve(points);
        ctx.strokeStyle = '#10b981';
        ctx.lineWidth = 2.5;
        ctx.stroke();

        // End-point dots
        function drawDot(data, color) {
            const last = data[data.length - 1];
            const cx = toCanvasX(last.x);
            const cy = toCanvasY(last.y);
            ctx.beginPath();
            ctx.arc(cx, cy, 6, 0, Math.PI * 2);
            ctx.fillStyle = color.replace(')', ', 0.2)').replace('rgb', 'rgba');
            ctx.fill();
            ctx.beginPath();
            ctx.arc(cx, cy, 3.5, 0, Math.PI * 2);
            ctx.fillStyle = color;
            ctx.fill();
            ctx.strokeStyle = '#fff';
            ctx.lineWidth = 1.5;
            ctx.stroke();
        }

        drawDot(contribPoints, '#6366f1');
        drawDot(points, '#10b981');

        // Legend
        const legendItems = [
            {label: 'Total Value', color: '#10b981'},
            {label: 'Contributions', color: '#6366f1'}
        ];
        const legendY = 8;
        const legendGap = 20;
        ctx.font = '600 10px system-ui, -apple-system, sans-serif';
        let totalLegendW = 0;
        const legendMeasures = legendItems.map(item => {
            const tw = ctx.measureText(item.label).width;
            totalLegendW += tw + 14 + legendGap;
            return tw;
        });
        totalLegendW -= legendGap;
        let legendX = w - totalLegendW - 8;
        legendItems.forEach((item, idx) => {
            const lx = legendX;
            ctx.beginPath();
            ctx.arc(lx + 5, legendY + 6, 4, 0, Math.PI * 2);
            ctx.fillStyle = item.color;
            ctx.fill();
            ctx.fillStyle = '#374151';
            ctx.font = '600 10px system-ui, -apple-system, sans-serif';
            ctx.textAlign = 'left';
            ctx.textBaseline = 'middle';
            ctx.fillText(item.label, lx + 13, legendY + 6);
            legendX += legendMeasures[idx] + 14 + legendGap;
        });

        // Store chart metadata for tooltip
        canvas._chartMeta = {padding, chartW, chartH, points, contribPoints, niceMax, years, toCanvasX, toCanvasY};
    }

    /**
     * Round a max value up to a "nice" number for axis scaling.
     * @param {number} rawMax - The raw maximum value.
     * @returns {number} The nice maximum value.
     */
    function niceScaleMax(rawMax) {
        if (rawMax <= 0) return 100;
        const magnitude = Math.pow(10, Math.floor(Math.log10(rawMax)));
        const normalized = rawMax / magnitude;
        let nice;
        if (normalized <= 1) nice = 1;
        else if (normalized <= 1.5) nice = 1.5;
        else if (normalized <= 2) nice = 2;
        else if (normalized <= 3) nice = 3;
        else if (normalized <= 5) nice = 5;
        else if (normalized <= 7.5) nice = 7.5;
        else nice = 10;
        return nice * magnitude;
    }

    /**
     * Format a currency value for axis labels.
     * @param {number} val - The value to format.
     * @returns {string} The formatted string.
     */
    function formatAxisValue(val) {
        if (val >= 1000000) {
            return '$' + (val / 1000000).toFixed(1).replace(/\.0$/, '') + 'M';
        }
        if (val >= 1000) {
            return '$' + (val / 1000).toFixed(0) + 'k';
        }
        return '$' + val.toFixed(0);
    }

    /**
     * Draw a rounded rectangle path.
     * @param {CanvasRenderingContext2D} ctx - The canvas context.
     * @param {number} x - X coordinate.
     * @param {number} y - Y coordinate.
     * @param {number} w - Width.
     * @param {number} h - Height.
     * @param {number} r - Corner radius.
     */
    function roundRect(ctx, x, y, w, h, r) {
        ctx.beginPath();
        ctx.moveTo(x + r, y);
        ctx.lineTo(x + w - r, y);
        ctx.quadraticCurveTo(x + w, y, x + w, y + r);
        ctx.lineTo(x + w, y + h - r);
        ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
        ctx.lineTo(x + r, y + h);
        ctx.quadraticCurveTo(x, y + h, x, y + h - r);
        ctx.lineTo(x, y + r);
        ctx.quadraticCurveTo(x, y, x + r, y);
        ctx.closePath();
    }

    /**
     * Setup chart hover tooltip.
     */
    function setupCalcChartTooltip() {
        const canvas = document.getElementById('calcChart');
        if (!canvas) return;

        canvas.addEventListener('mousemove', (e) => {
            const meta = canvas._chartMeta;
            if (!meta) return;
            const rect = canvas.getBoundingClientRect();
            const mx = e.clientX - rect.left;
            const my = e.clientY - rect.top;

            const {padding, chartW, chartH, points, contribPoints, niceMax, years, toCanvasX, toCanvasY} = meta;

            if (mx < padding.left || mx > padding.left + chartW || my < padding.top || my > padding.top + chartH) {
                calcChartTooltip.visible = false;
                redrawCalcChartWithTooltip();
                return;
            }

            const ratio = (mx - padding.left) / chartW;
            const hoveredYear = Math.round(ratio * years);
            const clampedYear = Math.max(0, Math.min(years, hoveredYear));

            const valuePoint = points.find(p => p.x === clampedYear) || nearestPoint(points, clampedYear);
            const contribPoint = contribPoints.find(p => p.x === clampedYear) || nearestPoint(contribPoints, clampedYear);

            if (!valuePoint) {
                calcChartTooltip.visible = false;
                redrawCalcChartWithTooltip();
                return;
            }

            const cx = toCanvasX(valuePoint.x);
            calcChartTooltip = {
                visible: true,
                x: cx,
                year: valuePoint.x,
                value: valuePoint.y,
                contribution: contribPoint ? contribPoint.y : 0
            };
            redrawCalcChartWithTooltip();
        });

        canvas.addEventListener('mouseleave', () => {
            calcChartTooltip.visible = false;
            redrawCalcChartWithTooltip();
        });
    }

    /**
     * Find the nearest data point to a given year.
     * @param {Array} data - The data points.
     * @param {number} year - The target year.
     * @returns {Object} The nearest point.
     */
    function nearestPoint(data, year) {
        let best = data[0];
        let bestDist = Math.abs(best.x - year);
        for (let i = 1; i < data.length; i++) {
            const d = Math.abs(data[i].x - year);
            if (d < bestDist) {
                bestDist = d;
                best = data[i];
            }
        }
        return best;
    }

    /**
     * Redraw the chart with tooltip if visible.
     */
    function redrawCalcChartWithTooltip() {
        const initial = parseFloat(document.getElementById('calcInitial')?.value) || 0;
        const monthly = parseFloat(document.getElementById('calcMonthly')?.value) || 0;
        const rate = parseFloat(document.getElementById('calcRate')?.value) || 0;
        const years = parseInt(document.getElementById('calcYears')?.value) || 0;
        drawCalcChart(initial, monthly, rate, years);

        if (calcChartTooltip.visible) {
            drawCalcChartTooltip();
        }
    }

    /**
     * Draw the tooltip on the chart.
     */
    function drawCalcChartTooltip() {
        const canvas = document.getElementById('calcChart');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const meta = canvas._chartMeta;
        if (!meta) return;

        const {toCanvasX, toCanvasY, padding, chartH} = meta;
        const {x: cx, year, value, contribution} = calcChartTooltip;

        // Vertical guide line
        ctx.beginPath();
        ctx.strokeStyle = 'rgba(107, 114, 128, 0.3)';
        ctx.lineWidth = 1;
        ctx.setLineDash([3, 3]);
        ctx.moveTo(cx, padding.top);
        ctx.lineTo(cx, padding.top + chartH);
        ctx.stroke();
        ctx.setLineDash([]);

        // Dot on value line
        const vy = toCanvasY(value);
        ctx.beginPath();
        ctx.arc(cx, vy, 5, 0, Math.PI * 2);
        ctx.fillStyle = '#10b981';
        ctx.fill();
        ctx.strokeStyle = '#fff';
        ctx.lineWidth = 2;
        ctx.stroke();

        // Dot on contribution line
        const cy = toCanvasY(contribution);
        ctx.beginPath();
        ctx.arc(cx, cy, 5, 0, Math.PI * 2);
        ctx.fillStyle = '#6366f1';
        ctx.fill();
        ctx.strokeStyle = '#fff';
        ctx.lineWidth = 2;
        ctx.stroke();

        // Tooltip box
        const lines = [
            year === 0 ? 'Start' : year + ' Years',
            'Value: ' + formatCurrency(value),
            'Contrib: ' + formatCurrency(contribution)
        ];

        ctx.font = '600 11px system-ui, -apple-system, sans-serif';
        const lineH = 18;
        const boxW = 150;
        const boxH = lines.length * lineH + 12;
        const boxPad = 10;

        let boxX = cx + 12;
        let boxY = vy - boxH / 2;

        const canvasWidth = parseInt(canvas.style.width, 10) || 400;
        if (boxX + boxW > canvasWidth - padding.right) {
            boxX = cx - boxW - 12;
        }
        if (boxY < padding.top) {
            boxY = padding.top;
        }
        if (boxY + boxH > padding.top + chartH) {
            boxY = padding.top + chartH - boxH;
        }

        // Shadow
        ctx.shadowColor = 'rgba(0, 0, 0, 0.12)';
        ctx.shadowBlur = 12;
        ctx.shadowOffsetY = 4;

        // Background
        ctx.fillStyle = '#fff';
        roundRect(ctx, boxX, boxY, boxW, boxH, 8);
        ctx.fill();

        // Reset shadow
        ctx.shadowColor = 'transparent';
        ctx.shadowBlur = 0;
        ctx.shadowOffsetY = 0;

        // Border
        ctx.strokeStyle = '#e5e7eb';
        ctx.lineWidth = 1;
        roundRect(ctx, boxX, boxY, boxW, boxH, 8);
        ctx.stroke();

        // Text
        ctx.fillStyle = '#374151';
        ctx.textAlign = 'left';
        ctx.textBaseline = 'top';

        lines.forEach((line, i) => {
            const isTitle = i === 0;
            ctx.font = isTitle ? '700 11px system-ui, -apple-system, sans-serif' : '500 11px system-ui, -apple-system, sans-serif';
            ctx.fillStyle = isTitle ? '#111827' : '#4b5563';
            ctx.fillText(line, boxX + boxPad, boxY + boxPad + i * lineH);
        });
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCalculator);
    } else {
        initCalculator();
    }

})();

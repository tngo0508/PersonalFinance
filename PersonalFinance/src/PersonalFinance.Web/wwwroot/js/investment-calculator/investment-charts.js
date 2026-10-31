/* global d3, ChartPalette */
/**
 * Investment Calculator charts (#587, ADR 0009): D3 drawing for the page's tiles. Each function draws one chart into a
 * host element from plain data the page script built out of investment-math.js results; nothing here calculates.
 * Colours come from the caller (ChartPalette, so a theme switch is a redraw). Sizes are in multiples of the root font
 * size, so the charts scale with the text.
 *
 * window.InvestmentCharts.line(host, options): a multi-series line chart over years, with optional area fills, dashed
 *     series, vertical markers and a readout that follows the pointer or the arrow keys.
 * window.InvestmentCharts.donut(host, options): a two-or-more-part donut with a centre label.
 */
(function () {
    'use strict';

    /**
     * The root font size in pixels.
     * @returns {number} Pixels per rem.
     */
    function rem () {
        return parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
    }

    /**
     * The host's readout element, created once: a tooltip positioned inside the host.
     * @param {HTMLElement} host - The chart host.
     * @returns {HTMLElement} The readout.
     */
    function readoutOf (host) {
        let readout = host.querySelector('.ic-readout');
        if (!readout) {
            readout = document.createElement('div');
            readout.className = 'ic-readout';
            readout.hidden = true;
            readout.setAttribute('aria-hidden', 'true');
            host.appendChild(readout);
        }
        return readout;
    }

    /**
     * Draw a line chart over years.
     * @param {HTMLElement} host - The container; its width sets the chart's.
     * @param {object} options - The chart.
     * @param {Array<{name: string, color: string, values: Array<{x: number, y: number}>, area?: boolean,
     *     dash?: string, width?: number}>} options.series - The lines, drawn in order; values share the same x.
     * @param {Array<{x: number, label: string, color: string}>} [options.markers] - Vertical markers at a year.
     * @param {string} options.label - The chart's accessible description.
     * @param {object} options.palette - ChartPalette.read() colours (grid, axis, tooltip).
     * @param {Function} options.formatY - Formats an amount for the axis (compact).
     * @param {Function} options.formatValue - Formats an amount for the readout (whole).
     * @param {Function} options.formatX - Formats a year for the axis.
     * @param {Function} options.titleX - Formats a year for the readout's heading.
     * @param {number} [options.heightEm] - The height in rem; by default it follows the width.
     */
    function line (host, options) {
        if (!host || typeof d3 === 'undefined') {
            return;
        }
        const width = host.clientWidth;
        if (!width) {
            return;
        }
        const r = rem();
        const height = options.heightEm
            ? Math.round(options.heightEm * r)
            : Math.round(Math.min(Math.max(width * 0.45, r * 13), r * 22));
        const m = { top: r * 1.6, right: r, bottom: r * 1.8, left: r * 3.6 };
        const palette = options.palette;
        const xs = options.series[0].values.map((v) => v.x);
        const maxX = d3.max(xs) || 1;
        const maxY = d3.max(options.series, (s) => d3.max(s.values, (v) => v.y)) || 1;
        const x = d3.scaleLinear().domain([0, maxX]).range([m.left, width - m.right]);
        const y = d3.scaleLinear().domain([0, maxY]).nice().range([height - m.bottom, m.top]);

        d3.select(host).select('svg').remove();
        const svg = d3.select(host).insert('svg', ':first-child')
            .attr('class', 'ic-svg')
            .attr('viewBox', `0 0 ${width} ${height}`)
            .attr('width', width)
            .attr('height', height)
            .attr('role', 'img')
            .attr('tabindex', 0)
            .attr('aria-label', options.label);

        const tickCount = Math.max(2, Math.min(maxX, Math.floor(width / (r * 4.5))));
        svg.append('g')
            .attr('transform', `translate(0,${height - m.bottom})`)
            .call(d3.axisBottom(x).ticks(tickCount).tickFormat((v) => (Number.isInteger(v) ? options.formatX(v) : '')))
            .call((g) => g.selectAll('text').attr('fill', palette.axis))
            .call((g) => g.selectAll('line,path').attr('stroke', palette.grid));
        svg.append('g')
            .attr('transform', `translate(${m.left},0)`)
            .call(d3.axisLeft(y).ticks(Math.max(3, Math.floor(height / (r * 3.5)))).tickSize(-(width - m.left - m.right))
                .tickFormat(options.formatY))
            .call((g) => g.select('.domain').remove())
            .call((g) => g.selectAll('text').attr('fill', palette.axis))
            .call((g) => g.selectAll('line').attr('stroke', palette.grid).attr('stroke-dasharray', '3 3'));

        options.series.forEach((s) => {
            if (s.area) {
                svg.append('path').datum(s.values)
                    .attr('fill', ChartPalette.withAlpha(s.color, 0.14))
                    .attr('d', d3.area().x((d) => x(d.x)).y0(y(0)).y1((d) => y(d.y)).curve(d3.curveMonotoneX));
            }
            svg.append('path').datum(s.values)
                .attr('class', 'ic-series')
                .attr('data-series', s.name)
                .attr('fill', 'none')
                .attr('stroke', s.color)
                .attr('stroke-width', s.width || 2.5)
                .attr('stroke-dasharray', s.dash || null)
                .attr('d', d3.line().x((d) => x(d.x)).y((d) => y(d.y)).curve(d3.curveMonotoneX));
        });

        (options.markers || []).forEach((marker) => {
            const px = x(marker.x);
            const flip = px > width * 0.7;
            const g = svg.append('g').attr('class', 'ic-marker');
            g.append('line')
                .attr('x1', px).attr('x2', px).attr('y1', m.top).attr('y2', height - m.bottom)
                .attr('stroke', marker.color).attr('stroke-width', 1.5).attr('stroke-dasharray', '4 4');
            g.append('text')
                .attr('x', px + (flip ? -r * 0.3 : r * 0.3)).attr('y', m.top - r * 0.5)
                .attr('text-anchor', flip ? 'end' : 'start')
                .attr('fill', palette.axis).attr('font-size', r * 0.75).attr('font-weight', 600)
                .text(marker.label);
        });

        const guide = svg.append('line').attr('stroke', palette.axis).attr('stroke-dasharray', '3 3')
            .attr('y1', m.top).attr('y2', height - m.bottom).style('display', 'none');
        const dots = svg.append('g').style('display', 'none');
        options.series.forEach((s) => dots.append('circle').attr('r', r * 0.25).attr('fill', s.color));
        const readout = readoutOf(host);
        let current = null;

        /**
         * Show the readout for one year.
         * @param {number} year - The year, clamped to the chart.
         */
        function show (year) {
            current = Math.max(0, Math.min(maxX, Math.round(year)));
            const px = x(current);
            guide.style('display', null).attr('x1', px).attr('x2', px);
            dots.style('display', null).selectAll('circle').each(function (_, i) {
                const point = options.series[i].values.find((v) => v.x === current);
                d3.select(this).attr('cx', px).attr('cy', point ? y(point.y) : -100);
            });
            readout.replaceChildren();
            const heading = document.createElement('strong');
            heading.textContent = options.titleX(current);
            readout.appendChild(heading);
            options.series.forEach((s) => {
                const point = s.values.find((v) => v.x === current);
                if (!point) {
                    return;
                }
                const row = document.createElement('div');
                const swatch = document.createElement('span');
                swatch.className = 'ic-readout__swatch';
                swatch.style.background = s.color;
                row.append(swatch, `${s.name}: ${options.formatValue(point.y)}`);
                readout.appendChild(row);
            });
            readout.hidden = false;
            const box = readout.getBoundingClientRect();
            const left = px + r + box.width > width ? px - r - box.width : px + r;
            readout.style.left = `${Math.max(0, left)}px`;
            readout.style.top = `${m.top}px`;
        }

        /** Hide the readout. */
        function hide () {
            current = null;
            guide.style('display', 'none');
            dots.style('display', 'none');
            readout.hidden = true;
        }

        svg.append('rect')
            .attr('x', m.left).attr('y', m.top)
            .attr('width', Math.max(0, width - m.left - m.right)).attr('height', Math.max(0, height - m.top - m.bottom))
            .attr('fill', 'transparent')
            .on('pointermove', (event) => show(x.invert(d3.pointer(event)[0])))
            .on('pointerleave', hide);
        svg.on('keydown', (event) => {
            const steps = { ArrowRight: 1, ArrowUp: 1, ArrowLeft: -1, ArrowDown: -1 };
            if (event.key === 'Home' || event.key === 'End') {
                show(event.key === 'Home' ? 0 : maxX);
            } else if (steps[event.key]) {
                show(current === null ? (steps[event.key] > 0 ? 0 : maxX) : current + steps[event.key]);
            } else if (event.key === 'Escape') {
                hide();
                return;
            } else {
                return;
            }
            event.preventDefault();
        }).on('blur', hide);
    }

    /**
     * Draw a donut.
     * @param {HTMLElement} host - The container.
     * @param {object} options - The donut.
     * @param {Array<{name: string, value: number, color: string}>} options.parts - The slices, in order.
     * @param {string} options.centre - The centre text.
     * @param {string} options.label - The accessible description.
     * @param {object} options.palette - ChartPalette.read() colours (page for the gaps, axis for the text).
     */
    function donut (host, options) {
        if (!host || typeof d3 === 'undefined') {
            return;
        }
        const r = rem();
        const size = Math.round(Math.min(r * 11, Math.max(r * 8, host.clientWidth || r * 11)));
        d3.select(host).select('svg').remove();
        const svg = d3.select(host).append('svg')
            .attr('class', 'ic-svg')
            .attr('viewBox', `0 0 ${size} ${size}`)
            .attr('width', size)
            .attr('height', size)
            .attr('role', 'img')
            .attr('aria-label', options.label);
        const g = svg.append('g').attr('transform', `translate(${size / 2},${size / 2})`);
        const total = d3.sum(options.parts, (p) => Math.max(0, p.value));
        const parts = total > 0 ? options.parts : [{ name: '', value: 1, color: options.palette.grid }];
        const arc = d3.arc().innerRadius(size * 0.33).outerRadius(size * 0.48).cornerRadius(r * 0.15).padAngle(0.015);
        g.selectAll('path')
            .data(d3.pie().sort(null).value((p) => Math.max(0, p.value))(parts))
            .join('path')
            .attr('d', arc)
            .attr('fill', (d) => d.data.color)
            .attr('stroke', options.palette.page)
            .attr('stroke-width', 1);
        g.append('text')
            .attr('text-anchor', 'middle')
            .attr('dy', '0.35em')
            .attr('font-weight', 700)
            .attr('font-size', r * 0.95)
            .attr('fill', options.palette.axis)
            .text(options.centre);
    }

    window.InvestmentCharts = Object.freeze({ line, donut });
})();

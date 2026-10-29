/* global PFI18n, d3 */
/**
 * Net Worth runway chart: the Base projection by age, the range between the other two scenarios, the FI Target and
 * the age it is reached. Drawn with D3 into #nwRunway; colours come from css/networth.css, so dark mode needs no redraw.
 * A marker follows the pointer (or the arrow keys when the chart has focus) and the readout names that age's figures.
 * Depends on: networth-utils.js (window.NW)
 */
(function () {
    'use strict';

    const NW = window.NW;

    // The plot's inner margins, in multiples of the root font size, so the axis labels scale with the text.
    const MARGIN_EM = { top: 2.4, right: 1, bottom: 1.9, left: 3.6 };

    const state = { data: null, mode: 'projected', age: null, drawn: false };
    let resizeObserver = null;

    /**
     * One year's balance in the chosen view.
     * @param {Object} year - A projection year.
     * @param {string} mode - 'projected' or 'real' (today's money).
     * @returns {number} The ending balance.
     */
    function balanceOf (year, mode) {
        return mode === 'real' ? year.realEndingBalance : year.endingBalance;
    }

    /**
     * The age the Base scenario reaches the FI Target in the chosen view, or null.
     * @param {Object} data - The projection.
     * @param {string} mode - 'projected' or 'real'.
     * @returns {number|null} The age.
     */
    function fiAgeOf (data, mode) {
        const age = mode === 'real' ? data.base.realFiReachedAge : data.base.fiReachedAge;
        return age === undefined ? null : age;
    }

    /**
     * The chart's points: today's balance at the current age (the same in both views and all scenarios), then each
     * projected age's Base balance and the lowest and highest of the three scenarios.
     * @param {Object} data - The projection.
     * @param {string} mode - 'projected' or 'real'.
     * @returns {Array<{age: number, base: number, low: number, high: number}>} Today, then one point per projected year.
     */
    function pointsOf (data, mode) {
        const today = { age: data.currentAge, base: data.startingBalance, low: data.startingBalance, high: data.startingBalance };
        return [today].concat(data.base.years.map((year, i) => {
            const values = [year, data.conservative.years[i], data.aggressive.years[i]]
                .filter(Boolean)
                .map((y) => balanceOf(y, mode));
            return { age: year.age, base: balanceOf(year, mode), low: Math.min(...values), high: Math.max(...values) };
        }));
    }

    /**
     * Whether the visitor asked for less motion.
     * @returns {boolean} True to skip the first-draw animation.
     */
    function reducedMotion () {
        return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    /**
     * Draw (or redraw) the whole chart at the container's current width.
     */
    function draw () {
        const host = document.getElementById('nwRunway');
        const data = state.data;
        if (!host || !data || !data.base || !data.base.years.length || typeof d3 === 'undefined') { return; }

        const rem = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
        const width = host.clientWidth;
        if (!width) { return; }
        // Height follows the width, kept between 14 and 24 lines of text so the curve never flattens or towers.
        const height = Math.round(Math.min(Math.max(width * 0.42, rem * 14), rem * 24));
        const m = {
            top: MARGIN_EM.top * rem,
            right: MARGIN_EM.right * rem,
            bottom: MARGIN_EM.bottom * rem,
            left: MARGIN_EM.left * rem
        };

        const points = pointsOf(data, state.mode);
        const firstAge = points[0].age;
        const lastAge = points[points.length - 1].age;
        const target = data.fiTarget > 0 ? data.fiTarget : 0;
        // Scaled to the Base path and the FI Target, not the aggressive end, so the crossing stays readable; the
        // band is clipped at the top and the readout still names its full range.
        const top = Math.max(d3.max(points, (p) => p.base), target) * 1.3 || 1;

        const x = d3.scaleLinear().domain([firstAge, Math.max(lastAge, firstAge + 1)]).range([m.left, width - m.right]);
        const y = d3.scaleLinear().domain([0, top]).nice(4).range([height - m.bottom, m.top]);

        const svg = d3.select(host).selectAll('svg').data([null]).join('svg')
            .attr('width', width)
            .attr('height', height)
            .attr('viewBox', `0 0 ${width} ${height}`)
            .attr('aria-hidden', 'true');
        svg.selectAll('*').remove();

        // Horizontal grid with compact amounts.
        const grid = svg.append('g').attr('class', 'nw-rw-grid');
        y.ticks(4).forEach((tick) => {
            grid.append('line').attr('x1', m.left).attr('x2', width - m.right).attr('y1', y(tick)).attr('y2', y(tick));
            grid.append('text').attr('class', 'nw-rw-tick').attr('x', m.left - rem * 0.5).attr('y', y(tick))
                .attr('dy', '0.32em').attr('text-anchor', 'end').text(NW.formatCompact(tick));
        });

        // Ages along the bottom: every five years, plus the first and last.
        // A label keeps two and a half characters' room from the ends, so narrow charts never print "33 35".
        const room = rem * 2.5;
        const ages = d3.range(Math.ceil(firstAge / 5) * 5, lastAge + 1, 5)
            .filter((a) => x(a) - x(firstAge) >= room && x(lastAge) - x(a) >= room);
        [firstAge].concat(ages, lastAge > firstAge ? [lastAge] : []).forEach((age) => {
            svg.append('text').attr('class', 'nw-rw-tick').attr('x', x(age)).attr('y', height - m.bottom + rem * 1.3)
                .attr('text-anchor', 'middle').text(age);
        });

        svg.append('clipPath').attr('id', 'nwRunwayClip').append('rect')
            .attr('x', 0).attr('y', m.top - rem * 0.2).attr('width', width).attr('height', height);

        // The range of outcomes, then the FI Target, then the Base path on top.
        svg.append('path').attr('class', 'nw-rw-band').attr('clip-path', 'url(#nwRunwayClip)')
            .attr('d', d3.area().x((p) => x(p.age)).y0((p) => y(p.low)).y1((p) => y(p.high)).curve(d3.curveMonotoneX)(points));

        if (target > 0) {
            svg.append('line').attr('class', 'nw-rw-fi')
                .attr('x1', m.left).attr('x2', width - m.right).attr('y1', y(target)).attr('y2', y(target));
        }

        const line = svg.append('path').attr('class', 'nw-rw-base')
            .attr('d', d3.line().x((p) => x(p.age)).y((p) => y(p.base)).curve(d3.curveMonotoneX)(points));

        // Where you are now.
        svg.append('circle').attr('class', 'nw-rw-now').attr('cx', x(firstAge)).attr('cy', y(points[0].base)).attr('r', rem * 0.3);

        // The moment the Base path reaches the FI Target.
        const fiAge = fiAgeOf(data, state.mode);
        const flag = svg.append('g').attr('class', 'nw-rw-flag');
        if (fiAge !== null) {
            const point = points.find((p) => p.age === fiAge);
            const fx = x(fiAge);
            flag.append('line').attr('x1', fx).attr('x2', fx).attr('y1', rem * 1.4).attr('y2', height - m.bottom);
            flag.append('circle').attr('cx', fx).attr('cy', y(point ? point.base : target)).attr('r', rem * 0.36);
            // The label gets its own line above the plot, clear of the top amount on the axis.
            const label = flag.append('text').attr('x', fx + rem * 0.4).attr('y', rem * 1.1)
                .attr('text-anchor', 'start')
                .text(PFI18n.t('NetWorth.Js.Chart.FiFlag', { age: fiAge }));
            // The label's measured width decides its side, since translations run much longer than "Free at 47".
            const labelWidth = label.node().getComputedTextLength();
            if (fx + rem * 0.4 + labelWidth > width && fx - rem * 0.4 - labelWidth >= 0) {
                label.attr('x', fx - rem * 0.4).attr('text-anchor', 'end');
            }
        }

        // The marker the pointer, the keys, the table and the milestones move.
        const guide = svg.append('g').attr('class', 'nw-rw-guide');
        guide.append('line').attr('y1', m.top).attr('y2', height - m.bottom);
        guide.append('circle').attr('r', rem * 0.3);

        svg.append('rect').attr('class', 'nw-rw-hit')
            .attr('x', m.left).attr('y', m.top).attr('width', Math.max(0, width - m.left - m.right))
            .attr('height', Math.max(0, height - m.top - m.bottom))
            .on('pointermove', (event) => {
                const age = Math.round(x.invert(d3.pointer(event)[0]));
                setAge(Math.max(firstAge, Math.min(lastAge, age)));
            });

        state.scales = { x: x, y: y, points: points, guide: guide };

        // The one entrance: the path draws itself once, on first load.
        if (!state.drawn && !reducedMotion()) {
            const length = line.node().getTotalLength();
            line.attr('stroke-dasharray', `${length} ${length}`).attr('stroke-dashoffset', length)
                .transition().duration(900).ease(d3.easeCubicOut).attr('stroke-dashoffset', 0)
                .on('end', () => line.attr('stroke-dasharray', null));
            flag.attr('opacity', 0).transition().delay(700).duration(300).attr('opacity', 1);
        }
        state.drawn = true;

        const fallback = fiAge !== null ? fiAge : lastAge;
        setAge(state.age !== null && state.age >= firstAge && state.age <= lastAge ? state.age : fallback);
    }

    /**
     * Move the marker to an age and write that age's figures into the readout.
     * @param {number} age - An age within the projection.
     */
    function setAge (age) {
        state.age = age;
        const scales = state.scales;
        if (!scales) { return; }
        const point = scales.points.find((p) => p.age === age);
        if (!point) { return; }

        scales.guide.attr('opacity', 1);
        scales.guide.select('line').attr('x1', scales.x(age)).attr('x2', scales.x(age));
        scales.guide.select('circle').attr('cx', scales.x(age)).attr('cy', scales.y(point.base));

        const readout = document.getElementById('nwReadout');
        if (readout) {
            readout.textContent = PFI18n.t('NetWorth.Js.Chart.Readout', {
                age: age,
                base: NW.formatCompact(point.base),
                low: NW.formatCompact(point.low),
                high: NW.formatCompact(point.high)
            });
        }
        if (NW.highlightAge) { NW.highlightAge(age); }
    }

    /**
     * Arrow keys walk the marker a year at a time; Home and End jump to the ends.
     * @param {KeyboardEvent} event - The keydown.
     */
    function onKey (event) {
        const scales = state.scales;
        if (!scales) { return; }
        const first = scales.points[0].age;
        const last = scales.points[scales.points.length - 1].age;
        const steps = { ArrowLeft: -1, ArrowDown: -1, ArrowRight: 1, ArrowUp: 1 };
        let next = null;
        if (event.key in steps) { next = (state.age === null ? first : state.age) + steps[event.key]; }
        if (event.key === 'Home') { next = first; }
        if (event.key === 'End') { next = last; }
        if (next === null) { return; }
        event.preventDefault();
        setAge(Math.max(first, Math.min(last, next)));
    }

    /**
     * Draw the chart for a projection in the chosen view, and redraw it whenever its container changes width.
     * @param {Object} data - The projection.
     * @param {string} mode - 'projected' or 'real'.
     */
    function renderRunway (data, mode) {
        state.data = data;
        state.mode = mode;
        const host = document.getElementById('nwRunway');
        if (!host) { return; }
        if (!resizeObserver && 'ResizeObserver' in window) {
            let lastWidth = host.clientWidth;
            resizeObserver = new ResizeObserver(() => {
                if (host.clientWidth !== lastWidth) {
                    lastWidth = host.clientWidth;
                    draw();
                }
            });
            resizeObserver.observe(host);
            host.addEventListener('keydown', onKey);
        }
        draw();
    }

    window.NW = Object.assign(window.NW || {}, {
        renderRunway: renderRunway,
        setRunwayAge: setAge
    });

})();

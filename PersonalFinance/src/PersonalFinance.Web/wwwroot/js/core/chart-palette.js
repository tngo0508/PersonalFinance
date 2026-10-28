(function () {
    'use strict';

    /**
     * Chart palette (issue #483): the one place a chart takes its colours from. It reads the chart tokens of the theme
     * in effect (site.css, #482) from the computed style of <html>, and calls subscribers again when theme.js
     * dispatches "themechange", so a chart restyles without a reload.
     *
     * Moving a chart onto it:
     *
     *     const colours = ChartPalette.read();
     *     ctx.strokeStyle = colours.grid;              // gridlines
     *     ctx.fillStyle = colours.axis;                // axis labels
     *     ctx.strokeStyle = colours.series[0];         // the first categorical series; series[1] the second, ...
     *     ctx.fillStyle = ChartPalette.withAlpha(colours.series[1], 0.15);   // an area fill under a series
     *
     *     // Redraw (or update a Chart.js chart's options and call chart.update()) when the theme changes.
     *     const unsubscribe = ChartPalette.subscribe((colours) => draw(colours));
     *
     * Read the palette each time the chart draws rather than keeping it, and keep no colour literal in the chart.
     */
    window.ChartPalette = (function () {
        // The light theme's values (site.css), used when a token is missing, e.g. before the stylesheet loads.
        const TOKENS = {
            background: ['--app-surface', '#F8FAF9'],
            grid: ['--app-chart-grid', '#E1E7E4'],
            axis: ['--app-chart-axis', '#5B6B78'],
            tooltipBackground: ['--app-chart-tooltip-bg', '#14283B'],
            tooltipText: ['--app-chart-tooltip-text', '#FFFFFF'],
            tooltipBorder: ['--app-chart-tooltip-border', '#14283B'],
            positive: ['--app-accent-positive', '#1D6A50'],
            negative: ['--app-negative-fill', '#B42318'],
            warning: ['--app-warning-fill', '#D89B26']
        };
        const SERIES = [
            ['--app-chart-1', '#1D6A50'],
            ['--app-chart-2', '#2F6690'],
            ['--app-chart-3', '#8A6A1E'],
            ['--app-chart-4', '#B42318'],
            ['--app-chart-5', '#6B4C9A'],
            ['--app-chart-6', '#2B7A89']
        ];

        /**
         * The chart colours of the theme in effect.
         * @returns {{background: string, grid: string, axis: string,
         *     tooltip: {background: string, text: string, border: string}, series: string[], positive: string,
         *     negative: string, warning: string}} background: the chart's surface; grid: gridlines and ticks; axis:
         *     axis text; tooltip: the hover box; series: the six categorical colours in order; positive, negative,
         *     warning: the mark colours for gains, losses and over-budget (the green accent and the negative and
         *     warning fills, which meet 3:1 on the page and card in both themes).
         */
        function read () {
            const style = window.getComputedStyle(document.documentElement);
            const token = (entry) => style.getPropertyValue(entry[0]).trim() || entry[1];
            return {
                background: token(TOKENS.background),
                grid: token(TOKENS.grid),
                axis: token(TOKENS.axis),
                tooltip: {
                    background: token(TOKENS.tooltipBackground),
                    text: token(TOKENS.tooltipText),
                    border: token(TOKENS.tooltipBorder)
                },
                series: SERIES.map(token),
                positive: token(TOKENS.positive),
                negative: token(TOKENS.negative),
                warning: token(TOKENS.warning)
            };
        }

        /**
         * Calls a function with the new palette each time the theme changes.
         * @param {function(object): void} callback - Called with read()'s palette after each "themechange".
         * @returns {function(): void} Stops the calls.
         */
        function subscribe (callback) {
            const listener = function () {
                callback(read());
            };
            document.addEventListener('themechange', listener);
            return function () {
                document.removeEventListener('themechange', listener);
            };
        }

        /**
         * A palette colour made translucent, for area fills, halos and guide lines.
         * @param {string} colour - A hex (#rgb or #rrggbb) or rgb()/rgba() colour, as read() returns.
         * @param {number} alpha - The opacity, 0 to 1.
         * @returns {string} The colour as rgba(), or the colour unchanged when its form is not one of those.
         */
        function withAlpha (colour, alpha) {
            const value = String(colour).trim();
            let channels = null;
            const hex = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(value);
            if (hex) {
                const digits = hex[1].length === 3 ? hex[1].replace(/./g, '$&$&') : hex[1];
                channels = [0, 2, 4].map((start) => parseInt(digits.slice(start, start + 2), 16));
            } else {
                const rgb = /^rgba?\(\s*(\d+)[\s,]+(\d+)[\s,]+(\d+)/i.exec(value);
                if (rgb) {
                    channels = rgb.slice(1, 4).map(Number);
                }
            }
            return channels ? `rgba(${channels.join(', ')}, ${alpha})` : value;
        }

        return {
            read: read,
            subscribe: subscribe,
            withAlpha: withAlpha
        };
    })();
})();

'use strict';
/* eslint-env node */

/**
 * Tests for the chart palette helper (wwwroot/js/core/chart-palette.js, issue #483): it reads the chart colours from the
 * computed theme tokens (#482), falls back to the light theme where a token is missing, and calls its subscribers with
 * the new palette on "themechange".
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'core', 'chart-palette.js'), 'utf8');

// What the dark theme's computed style returns for the chart tokens (site.css, #482), as a browser returns them.
const DARK_TOKENS = {
    '--app-surface': ' #16222D',
    '--app-chart-grid': ' #2A3A47',
    '--app-chart-axis': ' #9AA8B4',
    '--app-chart-tooltip-bg': ' #1C2A36',
    '--app-chart-tooltip-text': ' #E6ECF0',
    '--app-chart-tooltip-border': ' #34495A',
    '--app-chart-1': ' #3FB884',
    '--app-chart-2': ' #6FA8DC',
    '--app-chart-3': ' #D4B25C',
    '--app-chart-4': ' #F0796B',
    '--app-chart-5': ' #A98BDD',
    '--app-chart-6': ' #4FC1D0',
    '--app-accent-positive': ' #2C9168',
    '--app-accent-negative': ' #F0796B',
    '--app-warning-fill': ' #E0B24A'
};

/**
 * Load chart-palette.js against a fake page whose root has the given computed tokens.
 * @param {object} tokens - Custom property name to computed value; missing names read as ''.
 * @returns {{palette: object, tokens: object, dispatch: Function, listenerCount: Function}} The helper, the live token
 * map, a way to fire a document event, and the number of listeners for an event.
 */
function load (tokens) {
    const live = Object.assign({}, tokens);
    const listeners = {};
    const documentElement = {};
    const window = {
        getComputedStyle: (element) => {
            assert.strictEqual(element, documentElement, 'reads the root element');
            return { getPropertyValue: (name) => (name in live ? live[name] : '') };
        },
        document: {
            documentElement: documentElement,
            addEventListener: (type, fn) => { (listeners[type] = listeners[type] || []).push(fn); },
            removeEventListener: (type, fn) => {
                listeners[type] = (listeners[type] || []).filter((listener) => listener !== fn);
            }
        }
    };
    window.window = window;
    vm.createContext(window);
    vm.runInContext(SOURCE, window);
    return {
        palette: window.ChartPalette,
        tokens: live,
        dispatch: (type, event) => (listeners[type] || []).slice().forEach((fn) => fn(event || { type: type })),
        listenerCount: (type) => (listeners[type] || []).length
    };
}

test('read takes each chart colour from its computed token, trimmed', () => {
    const colours = load(DARK_TOKENS).palette.read();
    assert.strictEqual(colours.background, '#16222D');
    assert.strictEqual(colours.grid, '#2A3A47');
    assert.strictEqual(colours.axis, '#9AA8B4');
    assert.deepStrictEqual(JSON.parse(JSON.stringify(colours.tooltip)),
        { background: '#1C2A36', text: '#E6ECF0', border: '#34495A' });
    assert.deepStrictEqual(Array.from(colours.series), ['#3FB884', '#6FA8DC', '#D4B25C', '#F0796B', '#A98BDD', '#4FC1D0']);
    assert.strictEqual(colours.positive, '#2C9168');
    assert.strictEqual(colours.negative, '#F0796B');
    assert.strictEqual(colours.warning, '#E0B24A');
});

test('a missing token falls back to its light-theme value', () => {
    const colours = load({ '--app-chart-grid': ' #2A3A47' }).palette.read();
    assert.strictEqual(colours.grid, '#2A3A47');
    assert.strictEqual(colours.background, '#F8FAF9');
    assert.strictEqual(colours.tooltip.background, '#14283B');
    assert.strictEqual(colours.series[1], '#2F6690');
    assert.strictEqual(colours.warning, '#D89B26');
});

test('subscribers are called with the new palette on themechange, until they unsubscribe', () => {
    const page = load({ '--app-chart-1': '#1D6A50' });
    const seen = [];
    const unsubscribe = page.palette.subscribe((colours) => seen.push(colours.series[0]));
    assert.deepStrictEqual(seen, [], 'not called until the theme changes');

    page.tokens['--app-chart-1'] = '#3FB884';
    page.dispatch('themechange', { detail: { theme: 'dark' } });
    assert.deepStrictEqual(seen, ['#3FB884']);

    unsubscribe();
    assert.strictEqual(page.listenerCount('themechange'), 0);
    page.dispatch('themechange', { detail: { theme: 'light' } });
    assert.deepStrictEqual(seen, ['#3FB884']);
});

test('withAlpha makes a hex or rgb() colour translucent and leaves other forms alone', () => {
    const { palette } = load({});
    assert.strictEqual(palette.withAlpha('#2F6690', 0.15), 'rgba(47, 102, 144, 0.15)');
    assert.strictEqual(palette.withAlpha('#fff', 0.3), 'rgba(255, 255, 255, 0.3)');
    assert.strictEqual(palette.withAlpha('rgb(138, 106, 30)', 0.25), 'rgba(138, 106, 30, 0.25)');
    assert.strictEqual(palette.withAlpha('rgb(138 106 30)', 0.25), 'rgba(138, 106, 30, 0.25)');
    assert.strictEqual(palette.withAlpha('oklch(70% 0.1 150)', 0.5), 'oklch(70% 0.1 150)');
});

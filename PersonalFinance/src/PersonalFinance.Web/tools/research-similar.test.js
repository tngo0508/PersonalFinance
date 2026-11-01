'use strict';
/* eslint-env node */

/**
 * Tests for the Fund page's "Similar funds" (wwwroot/js/research/research-similar.js): the other Tracked Funds ranked
 * by the snapshots' own pair figures, overlap of holdings and correlation of returns, highest first, never the fund
 * itself and never a fund without a figure. The module loads into a context without a document, so its DOM wiring
 * never runs.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(
    path.join(__dirname, '..', 'wwwroot', 'js', 'research', 'research-similar.js'), 'utf8');

/**
 * Load the module the way the page does, as a browser script that sets window.ResearchSimilar.
 * @returns {object} The ResearchSimilar namespace.
 */
function load () {
    const window = {};
    window.window = window;
    vm.createContext(window);
    vm.runInContext(SOURCE, window);
    return window.ResearchSimilar;
}

const similar = load();

// VTI overlaps SPY most, then QQQ; BND shares nothing and has no pair. A pair of VTI with itself is ignored.
const OVERLAP = {
    funds: [{ ticker: 'VTI' }, { ticker: 'SPY' }, { ticker: 'QQQ' }, { ticker: 'BND' }],
    pairs: [
        { a: 0, b: 2, overlap: 31.5 },
        { a: 1, b: 0, overlap: 82.1 },
        { a: 1, b: 2, overlap: 44.0 },
        { a: 0, b: 0, overlap: 100 }
    ]
};

// Over 3 years VTI moves most with SPY, then QQQ, and against BND; VXUS has no figure (too few months).
const RETURNS = {
    funds: [{ ticker: 'VTI' }, { ticker: 'SPY' }, { ticker: 'QQQ' }, { ticker: 'BND' }, { ticker: 'VXUS' }],
    windows: [
        {
            years: 3,
            correlations: [
                { a: 0, b: 1, correlation: 0.99 },
                { a: 2, b: 0, correlation: 0.91 },
                { a: 0, b: 3, correlation: -0.12 },
                { a: 1, b: 2, correlation: 0.93 }
            ]
        },
        { years: 1, correlations: [{ a: 0, b: 3, correlation: 0.4 }] }
    ]
};

const plain = (rows) => JSON.parse(JSON.stringify(rows));

test('by overlap: the other funds, highest first, without the fund itself', () => {
    assert.deepStrictEqual(plain(similar.byOverlap(OVERLAP, 'VTI')), [
        { ticker: 'SPY', percent: 82.1 },
        { ticker: 'QQQ', percent: 31.5 }
    ]);
});

test('by overlap: a fund with no pair figure is left out, and case does not matter', () => {
    assert.deepStrictEqual(plain(similar.byOverlap(OVERLAP, 'bnd')), []);
    assert.deepStrictEqual(plain(similar.byOverlap(OVERLAP, 'qqq')).map((r) => r.ticker), ['SPY', 'VTI']);
});

test('by correlation: the window\'s figures, highest first, a negative one last', () => {
    assert.deepStrictEqual(plain(similar.byCorrelation(RETURNS, 'VTI', 3)), [
        { ticker: 'SPY', value: 0.99 },
        { ticker: 'QQQ', value: 0.91 },
        { ticker: 'BND', value: -0.12 }
    ]);
    assert.deepStrictEqual(plain(similar.byCorrelation(RETURNS, 'VTI', 1)), [{ ticker: 'BND', value: 0.4 }]);
});

test('by correlation: a fund without a figure, an unknown fund or window, or no data gives nothing', () => {
    assert.deepStrictEqual(plain(similar.byCorrelation(RETURNS, 'VXUS', 3)), []);
    assert.deepStrictEqual(plain(similar.byCorrelation(RETURNS, 'XYZ', 3)), []);
    assert.deepStrictEqual(plain(similar.byCorrelation(RETURNS, 'VTI', 5)), []);
    assert.deepStrictEqual(plain(similar.byCorrelation(null, 'VTI', 3)), []);
    assert.deepStrictEqual(plain(similar.byOverlap(null, 'VTI')), []);
});

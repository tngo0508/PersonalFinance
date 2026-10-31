'use strict';
/* eslint-env node */

/**
 * Tests for the Research search box's pure parts (wwwroot/js/research/research-search.js): matching the search index
 * (Tracked Funds, SEC offices, industries, countries) to the typed text, and keeping only the latest company
 * suggestion so a slow answer cannot replace a newer one. The module loads into a context with a stub document that
 * never becomes ready, so its DOM wiring never runs.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(
    path.join(__dirname, '..', 'wwwroot', 'js', 'research', 'research-search.js'), 'utf8');

/**
 * Load the search module the way the page does, as a browser script that sets window.ResearchSearch.
 * @returns {object} The ResearchSearch namespace.
 */
function load () {
    const window = { document: { readyState: 'loading', addEventListener () {} } };
    window.window = window;
    vm.createContext(window);
    vm.runInContext(SOURCE, window);
    return window.ResearchSearch;
}

const search = load();

const INDEX = {
    funds: [
        { ticker: 'VOO', name: 'Vanguard S&P 500 ETF', role: 'US stocks' },
        { ticker: 'VTI', name: 'Vanguard Total Stock Market ETF', role: 'US stocks' },
        { ticker: 'BND', name: 'Vanguard Total Bond Market ETF', role: 'Bonds' }
    ],
    offices: ['Finance', 'Technology', 'Energy & Transportation'],
    industries: [
        { name: 'Banks', office: 'Finance' },
        { name: 'Semiconductors & Related Devices', office: 'Technology' },
        { name: 'Savings Institutions', office: 'Finance' }
    ],
    countries: ['Japan', 'United States', 'Germany']
};

test('a ticker finds its Tracked Fund first, linked to the Fund page', () => {
    const results = search.match(INDEX, 'vti');

    assert.strictEqual(results[0].kind, 'fund');
    assert.strictEqual(results[0].href, '/Research/Fund/VTI');
});

test('a fund name matches too, case-insensitively', () => {
    const results = search.match(INDEX, 'TOTAL BOND');

    assert.deepStrictEqual(Array.from(results, (r) => r.href), ['/Research/Fund/BND']);
});

test('an industry links to the Sectors page focused on its office and industry', () => {
    const results = search.match(INDEX, 'bank');

    const industry = results.find((r) => r.kind === 'industry');
    assert.strictEqual(industry.label, 'Banks');
    assert.strictEqual(industry.href, '/Research/Sectors?focus=Finance%2FBanks');
});

test('an office links to the Sectors page focused on it, URL-encoded', () => {
    const results = search.match(INDEX, 'energy');

    assert.deepStrictEqual(Array.from(results, (r) => [r.kind, r.href]),
        [['office', '/Research/Sectors?focus=Energy%20%26%20Transportation']]);
});

test('a country links to the World page', () => {
    const results = search.match(INDEX, 'japan');

    assert.deepStrictEqual(Array.from(results, (r) => [r.kind, r.label, r.href]),
        [['country', 'Japan', '/Research/World?country=Japan']]);
});

test('funds come first, then offices, industries and countries', () => {
    // "an" is in a fund name, an office, an industry and a country.
    const results = search.match(INDEX, 'an');

    const kinds = Array.from(results, (r) => r.kind);
    assert.deepStrictEqual(kinds, kinds.slice().sort((a, b) =>
        ['fund', 'office', 'industry', 'country'].indexOf(a) - ['fund', 'office', 'industry', 'country'].indexOf(b)));
    assert.deepStrictEqual(Array.from(new Set(kinds)), ['fund', 'office', 'industry', 'country']);
});

test('an empty or blank query matches nothing', () => {
    assert.strictEqual(search.match(INDEX, '').length, 0);
    assert.strictEqual(search.match(INDEX, '   ').length, 0);
});

test('never more than eight results', () => {
    const many = {
        funds: Array.from({ length: 20 }, (_, i) => ({ ticker: `X${i}`, name: `Fund ${i}`, role: null })),
        offices: [],
        industries: [],
        countries: []
    };

    assert.strictEqual(search.match(many, 'fund').length, 8);
});

test('an industry without an office links to the Sectors page by its own name', () => {
    const results = search.match({ industries: [{ name: 'Banks', office: '' }, { name: 'Bank holding', office: null }] }, 'bank');

    assert.deepStrictEqual(Array.from(results, (r) => r.href),
        ['/Research/Sectors?focus=Banks', '/Research/Sectors?focus=Bank%20holding']);
    assert.ok(results.every((r) => !r.href.includes('undefined') && !r.href.includes('null')));
});

test('a missing part of the index is skipped', () => {
    const results = search.match({ funds: INDEX.funds }, 'japan');

    assert.strictEqual(results.length, 0);
});

/**
 * A promise with its resolver, so a test decides when it settles.
 * @returns {{promise: Promise, resolve: function}} The pair.
 */
function deferred () {
    let resolve;
    const promise = new Promise((r) => { resolve = r; });
    return { promise, resolve };
}

test('latestOnly: an earlier call that answers last resolves to null; the latest gets its value', async () => {
    const first = deferred();
    const second = deferred();
    const pending = [first.promise, second.promise];
    const wrapped = search.latestOnly(() => pending.shift());

    const a = wrapped('ap');
    const b = wrapped('app');
    second.resolve(['APPL']);
    first.resolve(['APOG']);

    assert.deepStrictEqual(await b, ['APPL']);
    assert.strictEqual(await a, null);
});

test('latestOnly: a single call resolves to its value and passes its arguments', async () => {
    const wrapped = search.latestOnly((q) => Promise.resolve(`for ${q}`));

    assert.strictEqual(await wrapped('msft'), 'for msft');
});

test('withBase: puts the base path in front of a root-relative address without doubling the slash', () => {
    assert.strictEqual(search.withBase('/', '/Research/Fund/VTI'), '/Research/Fund/VTI');
    assert.strictEqual(search.withBase('/app/', '/Research/Fund/VTI'), '/app/Research/Fund/VTI');
    assert.strictEqual(search.withBase('/app', '/Research/World?country=Japan'), '/app/Research/World?country=Japan');
    assert.strictEqual(search.withBase(undefined, '/Research/Fund/VTI'), '/Research/Fund/VTI');
});

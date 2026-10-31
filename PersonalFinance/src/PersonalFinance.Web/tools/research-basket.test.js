'use strict';
/* eslint-env node */

/**
 * Tests for the Fund Basket's link helper (wwwroot/js/research/research-basket.js): every Research link carries the
 * basket in `basket=`, so it survives moving between pages. `withBasket` sets or replaces that one parameter and keeps
 * the rest of the link. The module loads into a context with a stub document that never becomes ready, so its DOM
 * wiring never runs.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(
    path.join(__dirname, '..', 'wwwroot', 'js', 'research', 'research-basket.js'), 'utf8');

/**
 * Load the basket module the way the page does, as a browser script that sets window.ResearchBasket.
 * @returns {object} The ResearchBasket namespace.
 */
function load () {
    const window = { document: { readyState: 'loading', addEventListener () {} } };
    window.window = window;
    vm.createContext(window);
    vm.runInContext(SOURCE, window);
    return window.ResearchBasket;
}

const basket = load();
const MIX = [{ ticker: 'VTI', weight: 60 }, { ticker: 'BND', weight: 40 }];

test('adds the basket to a link without a query', () => {
    assert.strictEqual(basket.withBasket('/Research/Fund/VTI', MIX), '/Research/Fund/VTI?basket=VTI%3A60%2CBND%3A40');
});

test('equal weights are written as tickers only', () => {
    const equal = [{ ticker: 'VTI', weight: null }, { ticker: 'BND', weight: null }];

    assert.strictEqual(basket.withBasket('/Research/Basket', equal), '/Research/Basket?basket=VTI%2CBND');
});

test('keeps the link\'s other query values and its hash', () => {
    assert.strictEqual(basket.withBasket('/Research/Funds?view=risk#growth', MIX),
        '/Research/Funds?view=risk&basket=VTI%3A60%2CBND%3A40#growth');
});

test('replaces an existing basket in place', () => {
    assert.strictEqual(basket.withBasket('/Research/Sectors?basket=VOO&focus=Finance%2FBanks', MIX),
        '/Research/Sectors?basket=VTI%3A60%2CBND%3A40&focus=Finance%2FBanks');
});

test('an empty basket removes the parameter and keeps the rest', () => {
    assert.strictEqual(basket.withBasket('/Research/World?basket=VTI&country=Japan#map', []),
        '/Research/World?country=Japan#map');
    assert.strictEqual(basket.withBasket('/Research/Basket?basket=VTI%3A100', []), '/Research/Basket');
});

test('a basket parameter in another case is replaced or removed, never doubled', () => {
    assert.strictEqual(basket.withBasket('/Research/Funds?Basket=VOO&view=risk', MIX),
        '/Research/Funds?basket=VTI%3A60%2CBND%3A40&view=risk');
    assert.strictEqual(basket.withBasket('/Research/Funds?view=risk&BASKET=VOO', []), '/Research/Funds?view=risk');
});

test('a link already right is left as it is', () => {
    assert.strictEqual(basket.withBasket('/Research', []), '/Research');
});

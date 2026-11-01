'use strict';
/* eslint-env node */

/**
 * Tests for the Fund page's "Where the money goes" by SEC office (wwwroot/js/research/research-fund.js): one fund's
 * industry shares summed by office, largest first, then the stocks SEC does not classify, bonds, and cash and other,
 * each only when the fund holds some. The module loads into a context without a document, so its DOM wiring never runs.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(
    path.join(__dirname, '..', 'wwwroot', 'js', 'research', 'research-fund.js'), 'utf8');

/**
 * Load the module the way the page does, as a browser script that sets window.ResearchFund.
 * @returns {object} The ResearchFund namespace.
 */
function load () {
    const window = {};
    window.window = window;
    vm.createContext(window);
    vm.runInContext(SOURCE, window);
    return window.ResearchFund;
}

const fund = load();
const NAMES = { unclassified: 'Not classified', bonds: 'Bonds', cash: 'Cash and other' };

// Two industries in Technology (office 0) and one in Finance (office 1).
const DATA = {
    offices: ['Office of Technology', 'Office of Finance'],
    industries: [{ office: 0 }, { office: 1 }, { office: 0 }],
    funds: [
        {
            ticker: 'AAA',
            industries: [
                { industry: 0, percent: 20 },
                { industry: 1, percent: 30 },
                { industry: 2, percent: 15 }
            ],
            unclassified: [{ percent: 4 }, { percent: 1 }],
            bonds: [{ percent: 10 }],
            cashAndOther: 2.5
        },
        { ticker: 'BBB', industries: [{ industry: 1, percent: 90 }], unclassified: [], bonds: [], cashAndOther: 0 }
    ]
};

const plain = (rows) => JSON.parse(JSON.stringify(rows));

test('sums each office\'s industries, largest first, then the parts outside the offices', () => {
    assert.deepStrictEqual(plain(fund.officeShares(DATA, 'aaa', NAMES)), [
        { name: 'Office of Technology', percent: 35 },
        { name: 'Office of Finance', percent: 30 },
        { name: 'Not classified', percent: 5 },
        { name: 'Bonds', percent: 10 },
        { name: 'Cash and other', percent: 2.5 }
    ]);
});

test('leaves out the parts the fund holds none of', () => {
    assert.deepStrictEqual(plain(fund.officeShares(DATA, 'BBB', NAMES)), [{ name: 'Office of Finance', percent: 90 }]);
});

test('a fund the snapshot does not hold, or no snapshot, gives null', () => {
    assert.strictEqual(fund.officeShares(DATA, 'XYZ', NAMES), null);
    assert.strictEqual(fund.officeShares(null, 'AAA', NAMES), null);
});

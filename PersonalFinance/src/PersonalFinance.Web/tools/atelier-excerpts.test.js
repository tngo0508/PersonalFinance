'use strict';
/* eslint-env node */

/**
 * Tests for atelier-excerpts.js. Run with: node --test PersonalFinance/src/PersonalFinance.Web/tools/atelier-excerpts.test.js
 * The allowlist is the real approved-sources.json; the files are the /atelier-draft skill's samples and small inline ones.
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const { checkExcerpts, parseArgs, parseExcerptFile, readSources } = require('./atelier-excerpts');

const SOURCES = readSources(parseArgs([]).contentDir);
const SAMPLES = path.join(__dirname, '..', '..', '..', '..', '.claude', 'skills', 'atelier-draft', 'samples');

/**
 * Checks an excerpt file's text against the real allowlist.
 * @param {string} text The file's text.
 * @returns {{ problems: string[], draft: object|null }} The result.
 */
function check (text) {
    return checkExcerpts(parseExcerptFile(text), SOURCES);
}

/**
 * Checks one of the skill's sample files.
 * @param {string} name The sample's file name.
 * @returns {{ problems: string[], draft: object|null }} The result.
 */
function checkSample (name) {
    return check(fs.readFileSync(path.join(SAMPLES, name), 'utf8'));
}

const HEADER = '---\nterm: Stock\nslug: stock\ntopic: investing-basics\nlesson: own-a-piece\n---\n\n';
const SEC_EXCERPT = '## excerpt\nsource: Investor.gov (SEC)\nname: Investor.gov, Stocks\n' +
    'url: https://www.investor.gov/introduction-investing/investing-basics/investment-products/stocks\n' +
    'accessed: 2026-10-30\n\nStocks are a type of security that gives stockholders a share of ownership in a company.\n\n';

test('the diversification sample passes and keeps only draft + cite text, with links as name and URL', () => {
    const { problems, draft } = checkSample('diversification.excerpts.md');

    assert.deepStrictEqual(problems, []);
    assert.strictEqual(draft.slug, 'diversification');
    assert.ok(draft.excerpts.length >= 1);
    assert.ok(draft.excerpts.every(e => e.source === 'Investor.gov (SEC)' && e.text.length > 0));
    assert.ok(draft.links.length >= 1);
    assert.ok(draft.links.every(l => Object.keys(l).every(k => ['source', 'usage', 'name', 'url'].includes(k))));
});

test('refuses the sample whose excerpt comes from a source that is not on the allowlist', () => {
    const { problems, draft } = checkSample('refused-unlisted-source.excerpts.md');

    assert.strictEqual(draft, null);
    assert.ok(problems.some(p => p.includes('is not an Approved Source')), problems.join('\n'));
});

test('refuses the sample whose excerpt comes from a source that is not draft + cite', () => {
    const { problems, draft } = checkSample('refused-not-draft-cite.excerpts.md');

    assert.strictEqual(draft, null);
    assert.ok(problems.some(p => p.includes('not draft + cite')), problems.join('\n'));
});

test('refuses a URL outside the named source\'s prefixes', () => {
    const { problems } = check(HEADER + SEC_EXCERPT.replace('https://www.investor.gov/', 'https://www.investor.gov.example.com/'));

    assert.ok(problems.some(p => p.includes('not under Investor.gov (SEC)\'s prefixes')), problems.join('\n'));
});

test('refuses an IRS excerpt without its tax year, and keeps the year when given', () => {
    const irs = '## excerpt\nsource: IRS\nname: IRS, Topic no. 409\nurl: https://www.irs.gov/taxtopics/tc409\n' +
        'accessed: 2026-10-30\n\nCapital gains are taxed.\n';

    assert.ok(check(HEADER + irs).problems.some(p => p.includes('needs its tax-year')));
    const withYear = check(HEADER + irs.replace('accessed:', 'tax-year: 2026\naccessed:'));
    assert.deepStrictEqual(withYear.problems, []);
    assert.strictEqual(withYear.draft.excerpts[0].taxYear, 2026);
});

test('refuses a file with no excerpt, a bad header or a link carrying pasted text', () => {
    const link = '## link\nsource: FINRA\nname: FINRA, Stocks\nurl: https://www.finra.org/investors/stocks\n';

    assert.ok(check(HEADER + link).problems.includes('the file has no ## excerpt block'));
    assert.ok(check(HEADER.replace('investing-basics', 'crypto') + SEC_EXCERPT).problems.some(p => p.startsWith('header: topic')));
    assert.ok(check(SEC_EXCERPT).problems.some(p => p.includes('front matter')));
    assert.ok(check(`${HEADER + SEC_EXCERPT + link }\nPasted FINRA text.\n`).problems.some(p => p.includes('carries no pasted text')));
});

test('a concept + cite source may be a link but not an excerpt', () => {
    const ssc = 'source: SSC / UBCKNN\nname: SSC, Investor guide\nurl: https://ssc.gov.vn/guide\n';

    assert.deepStrictEqual(check(`${HEADER + SEC_EXCERPT }## link\n${ ssc}`).problems, []);
    assert.ok(check(`${HEADER + SEC_EXCERPT }## excerpt\n${ ssc }accessed: 2026-10-30\n\nText.\n`).problems
        .some(p => p.includes('concept-cite-link, not draft + cite')));
});

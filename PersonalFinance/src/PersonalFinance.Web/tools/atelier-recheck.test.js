'use strict';
/* eslint-env node */

/**
 * Tests for atelier-recheck.js. Run with: node --test PersonalFinance/src/PersonalFinance.Web/tools/atelier-recheck.test.js
 * Each test builds a small content folder of Term Entries and checks which country notes are due on a fixed day.
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { findDueNotes, parseArgs, renderIssueBody } = require('./atelier-recheck');

const TODAY = new Date('2026-10-29T00:00:00Z');
const folders = [];

test.after(() => {
    for (const folder of folders) {
        fs.rmSync(folder, { recursive: true, force: true });
    }
});

/**
 * Creates an empty content folder, removed after the tests.
 * @returns {string} The folder path.
 */
function emptyFolder () {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'atelier-recheck-'));
    folders.push(root);
    return root;
}

/**
 * Builds a country note.
 * @param {number} rulesAsOf The Rules-as-of year.
 * @param {string} url The note's source URL.
 * @returns {object} The note.
 */
function note (rulesAsOf, url) {
    return { text: 'A note.', rulesAsOf, sources: [{ name: 'Source', url, accessed: '2026-01-01' }] };
}

/**
 * Builds a content folder holding the given Term Entry files.
 * @param {Record<string, object>} files The files under terms/, by file name.
 * @returns {string} The folder path.
 */
function contentFolder (files) {
    const root = emptyFolder();
    fs.mkdirSync(path.join(root, 'terms'));
    for (const [name, entry] of Object.entries(files)) {
        fs.writeFileSync(path.join(root, 'terms', name), JSON.stringify(entry));
    }
    return root;
}

/**
 * Builds a Term Entry.
 * @param {string} slug The slug.
 * @param {object} fields The fields that differ from a current, non-Tax entry with both notes.
 * @returns {object} The entry.
 */
function entry (slug, fields = {}) {
    return {
        slug,
        term: `Term ${slug}`,
        topic: 'borrowing-credit',
        inTheUs: note(2026, `https://www.irs.gov/${slug}`),
        inVietnam: note(2026, `https://vbpl.vn/${slug}`),
        sources: [{ name: 'Entry source', url: `https://www.investor.gov/${slug}`, accessed: '2026-01-01' }],
        lastReviewed: '2026-10-01',
        ...fields
    };
}

test('an empty content folder has nothing due', () => {
    assert.deepStrictEqual(findDueNotes(emptyFolder(), TODAY), []);
});

test('current notes are not due', () => {
    const root = contentFolder({
        'apr.json': entry('apr'),
        'roth-ira.json': entry('roth-ira', { topic: 'tax' }),
        'reviewed-a-year-ago.json': entry('reviewed-a-year-ago', { lastReviewed: '2025-10-29' })
    });
    assert.deepStrictEqual(findDueNotes(root, TODAY), []);
});

test('every country note on an entry reviewed more than 12 months ago is due', () => {
    const root = contentFolder({ 'apr.json': entry('apr', { lastReviewed: '2025-10-28' }) });
    assert.deepStrictEqual(findDueNotes(root, TODAY), [
        {
            slug: 'apr', term: 'Term apr', country: 'In the US', rulesAsOf: 2026,
            sourceUrl: 'https://www.irs.gov/apr', reasons: ['last reviewed 2025-10-28']
        },
        {
            slug: 'apr', term: 'Term apr', country: 'In Vietnam', rulesAsOf: 2026,
            sourceUrl: 'https://vbpl.vn/apr', reasons: ['last reviewed 2025-10-28']
        }
    ]);
});

test('a Tax note with rules from before the current year is due', () => {
    const root = contentFolder({
        'roth-ira.json': entry('roth-ira', { topic: 'tax', inTheUs: note(2025, 'https://www.irs.gov/roth') })
    });
    assert.deepStrictEqual(findDueNotes(root, TODAY), [{
        slug: 'roth-ira', term: 'Term roth-ira', country: 'In the US', rulesAsOf: 2025,
        sourceUrl: 'https://www.irs.gov/roth', reasons: ['Tax rules as of 2025']
    }]);
});

test('an old Rules-as-of year outside Tax is not due', () => {
    const root = contentFolder({ 'apr.json': entry('apr', { inTheUs: note(2020, 'https://www.irs.gov/apr') }) });
    assert.deepStrictEqual(findDueNotes(root, TODAY), []);
});

test('a note due by both rules is listed once with both reasons', () => {
    const root = contentFolder({
        'roth-ira.json': entry('roth-ira', {
            topic: 'tax', lastReviewed: '2024-05-01', inVietnam: undefined, inTheUs: note(2024, 'https://www.irs.gov/r')
        })
    });
    const due = findDueNotes(root, TODAY);
    assert.strictEqual(due.length, 1);
    assert.deepStrictEqual(due[0].reasons, ['last reviewed 2024-05-01', 'Tax rules as of 2024']);
});

test('Vietnamese counterparts and unreadable files are skipped', () => {
    const root = contentFolder({ 'apr.vi.json': entry('apr', { lastReviewed: '2020-01-01' }) });
    fs.writeFileSync(path.join(root, 'terms', 'broken.json'), '{ not json');
    assert.deepStrictEqual(findDueNotes(root, TODAY), []);
});

test('a note without its own source links the entry source', () => {
    const root = contentFolder({
        'apr.json': entry('apr', { lastReviewed: '2020-01-01', inVietnam: undefined, inTheUs: { rulesAsOf: 2020 } })
    });
    assert.strictEqual(findDueNotes(root, TODAY)[0].sourceUrl, 'https://www.investor.gov/apr');
});

test('the issue body lists one line per due note: entry, country, year and source link', () => {
    const body = renderIssueBody([{
        slug: 'roth-ira', term: 'Roth IRA', country: 'In the US', rulesAsOf: 2025,
        sourceUrl: 'https://www.irs.gov/roth', reasons: ['Tax rules as of 2025']
    }], TODAY);
    assert.match(body, /Checked on 2026-10-29/);
    assert.match(body, /1 note is due for a re-check/);
    assert.match(body, /^- Roth IRA \(`roth-ira`\), In the US, Rules as of 2025, \[source\]\(https:\/\/www\.irs\.gov\/roth\) \(Tax rules as of 2025\)$/m);
});

test('the issue body says when nothing is due', () => {
    const body = renderIssueBody([], TODAY);
    assert.match(body, /No notes are due for a re-check\./);
    assert.doesNotMatch(body, /^- /m);
});

test('the command line takes a content folder and a fixed day', () => {
    const { contentDir, today } = parseArgs(['some/folder', '--today', '2026-01-31']);
    assert.strictEqual(contentDir, path.resolve('some/folder'));
    assert.strictEqual(today.toISOString(), '2026-01-31T00:00:00.000Z');
    assert.throws(() => parseArgs(['--today', 'soon']), /YYYY-MM-DD/);
});

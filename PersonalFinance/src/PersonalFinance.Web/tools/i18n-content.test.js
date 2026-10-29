'use strict';
/* eslint-env node */

/**
 * Tests for i18n-content.js: drafting the Vietnamese counterparts of the Atelier content files (ADR 0019).
 * Run with: node --test PersonalFinance/src/PersonalFinance.Web/tools/i18n-content.test.js
 * The Translator is a fake, so nothing leaves the machine.
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { contentGlossary, translateContent } = require('./i18n-content');
const { run } = require('./i18n-translate');
const { sourceHash } = require('./i18n-common');

const apr = () => ({
    slug: 'apr',
    term: 'Annual percentage rate',
    vietnameseName: 'lãi suất phần trăm hằng năm',
    alsoKnownAs: ['APR'],
    topic: 'borrowing-credit',
    oneLine: 'The yearly cost of borrowing money.',
    explanation: ['APR shows the cost of a loan.', 'A lower APR costs less.'],
    whyItMatters: 'A higher APR costs more.',
    workedExample: 'You owe $1,000 at 24% APR.',
    inVietnam: {
        text: 'Banks show a yearly rate.',
        rulesAsOf: 2026,
        sources: [{ name: 'Law on Credit Institutions', url: 'https://vbpl.vn/law', accessed: '2026-10-27' }]
    },
    relatedTerms: ['credit-score'],
    sources: [{ name: 'CFPB, What is APR?', url: 'https://www.consumerfinance.gov/apr', accessed: '2026-10-27' }],
    furtherReading: [],
    lastReviewed: '2026-10-27'
});

const lesson = () => ({
    slug: 'borrow-wisely',
    day: 2,
    title: 'Borrow wisely',
    intro: 'Borrowing has a price.',
    terms: ['apr', 'credit-score'],
    check: [
        { format: 'myth-or-fact', prompt: 'A higher APR costs less.', options: ['Myth', 'Fact'], correctOption: 0, explanation: 'Myth.' },
        { format: 'what-happens-next', prompt: 'Nam pays late.', options: ['Up', 'Down', 'Same'], correctOption: 1, explanation: 'Down.' }
    ],
    tryIt: { label: 'Budget', url: '/Budget' },
    keepsake: { symbol: '📒', name: 'Ledger' }
});

/**
 * A content folder with the given files.
 * @param {Object<string, object>} files Path inside the folder to JSON content.
 * @returns {string} The folder.
 */
function contentFolder (files) {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'i18n-content-'));
    for (const [name, content] of Object.entries(files)) {
        fs.mkdirSync(path.dirname(path.join(dir, name)), { recursive: true });
        fs.writeFileSync(path.join(dir, name), JSON.stringify(content));
    }
    return dir;
}

const readFile = (dir, name) => JSON.parse(fs.readFileSync(path.join(dir, name), 'utf8'));

/**
 * A fake Translator: prefixes each text with "VI:" after stripping the markup, and records what it was sent.
 * @returns {Function} A translate(texts) with a .sent array.
 */
function fakeTranslate () {
    const sent = [];
    const translate = (texts) => {
        sent.push(...texts);
        return Promise.resolve(texts.map((text) => `VI:${text}`));
    };
    translate.sent = sent;
    return translate;
}

const draft = (dir, translate, glossary = {}) =>
    translateContent({ dir, language: 'vi', glossary }, { translate });

test('a new Term Entry gets a Vietnamese counterpart with only its text fields, and a lock', async () => {
    const dir = contentFolder({ 'terms/apr.json': apr() });
    const translate = fakeTranslate();

    const result = await draft(dir, translate);

    const vi = readFile(dir, 'terms/apr.vi.json');
    assert.deepStrictEqual(vi, {
        slug: 'apr',
        term: 'VI:Annual percentage rate',
        oneLine: 'VI:The yearly cost of borrowing money.',
        explanation: ['VI:APR shows the cost of a loan.', 'VI:A lower APR costs less.'],
        whyItMatters: 'VI:A higher APR costs more.',
        workedExample: 'VI:You owe $1,000 at 24% APR.',
        inVietnam: { text: 'VI:Banks show a yearly rate.' }
    });
    assert.strictEqual(result.translated.length, 7);
    const lock = readFile(dir, 'i18n.lock.json');
    assert.strictEqual(lock.vi['terms/apr'].oneLine, sourceHash('The yearly cost of borrowing money.'));
    assert.strictEqual(lock.vi['terms/apr']['explanation.1'], sourceHash('A lower APR costs less.'));
});

test('slugs, URLs, numbers, sources, the Topic and the Vietnamese name are never sent', async () => {
    const dir = contentFolder({ 'terms/apr.json': apr(), 'lessons/borrow-wisely.json': lesson() });
    const translate = fakeTranslate();

    await draft(dir, translate);

    for (const text of ['apr', 'borrowing-credit', 'lãi suất phần trăm hằng năm', 'CFPB, What is APR?', '/Budget', '📒', 'myth-or-fact']) {
        assert.ok(!translate.sent.includes(text), `${text} was sent`);
    }
    assert.ok(translate.sent.every((text) => !text.includes('https://')));
});

test('a Lesson gets its title, intro, Check, Try it label and keepsake name', async () => {
    const dir = contentFolder({ 'lessons/borrow-wisely.json': lesson() });

    await draft(dir, fakeTranslate());

    assert.deepStrictEqual(readFile(dir, 'lessons/borrow-wisely.vi.json'), {
        slug: 'borrow-wisely',
        title: 'VI:Borrow wisely',
        intro: 'VI:Borrowing has a price.',
        check: [
            { prompt: 'VI:A higher APR costs less.', options: ['VI:Myth', 'VI:Fact'], explanation: 'VI:Myth.' },
            { prompt: 'VI:Nam pays late.', options: ['VI:Up', 'VI:Down', 'VI:Same'], explanation: 'VI:Down.' }
        ],
        tryIt: { label: 'VI:Budget' },
        keepsake: { name: 'VI:Ledger' }
    });
});

test('an unchanged entry is skipped: nothing is sent and its file is not rewritten', async () => {
    const dir = contentFolder({ 'terms/apr.json': apr() });
    await draft(dir, fakeTranslate());
    const before = fs.readFileSync(path.join(dir, 'terms/apr.vi.json'), 'utf8');
    const lockBefore = fs.readFileSync(path.join(dir, 'i18n.lock.json'), 'utf8');
    fs.writeFileSync(path.join(dir, 'terms/apr.vi.json'), JSON.stringify(JSON.parse(before)));
    const compact = fs.readFileSync(path.join(dir, 'terms/apr.vi.json'), 'utf8');
    const translate = fakeTranslate();

    const result = await draft(dir, translate);

    assert.deepStrictEqual(translate.sent, []);
    assert.deepStrictEqual(result.translated, []);
    assert.strictEqual(fs.readFileSync(path.join(dir, 'terms/apr.vi.json'), 'utf8'), compact);
    assert.strictEqual(fs.readFileSync(path.join(dir, 'i18n.lock.json'), 'utf8'), lockBefore);
});

test('only the field whose English changed is sent again', async () => {
    const dir = contentFolder({ 'terms/apr.json': apr() });
    await draft(dir, fakeTranslate());
    const changed = { ...apr(), whyItMatters: 'A higher APR costs you more.' };
    fs.writeFileSync(path.join(dir, 'terms/apr.json'), JSON.stringify(changed));
    const translate = fakeTranslate();

    await draft(dir, translate);

    assert.deepStrictEqual(translate.sent, ['A higher APR costs you more.']);
    assert.strictEqual(readFile(dir, 'terms/apr.vi.json').whyItMatters, 'VI:A higher APR costs you more.');
});

test('an owner-written In Vietnam note is kept even when its English changes', async () => {
    const dir = contentFolder({
        'terms/apr.json': apr(),
        'terms/apr.vi.json': { slug: 'apr', inVietnam: { text: 'Ngân hàng công bố lãi suất năm.' } },
        'i18n.lock.json': { vi: { 'terms/apr': { 'inVietnam.text': sourceHash('An older English note.') } } }
    });
    const translate = fakeTranslate();

    await draft(dir, translate);

    assert.ok(!translate.sent.includes('Banks show a yearly rate.'));
    assert.strictEqual(readFile(dir, 'terms/apr.vi.json').inVietnam.text, 'Ngân hàng công bố lãi suất năm.');
});

test('the Vietnamese name in the English file is never changed', async () => {
    const dir = contentFolder({ 'terms/apr.json': apr() });
    const before = fs.readFileSync(path.join(dir, 'terms/apr.json'), 'utf8');

    await draft(dir, fakeTranslate());

    assert.strictEqual(fs.readFileSync(path.join(dir, 'terms/apr.json'), 'utf8'), before);
});

test('a hand-written field without a lock entry is adopted; a blank one is drafted', async () => {
    const dir = contentFolder({
        'terms/apr.json': apr(),
        'terms/apr.vi.json': { slug: 'apr', term: 'Lãi suất phần trăm hằng năm', explanation: ['APR là chi phí.', ''] }
    });
    const translate = fakeTranslate();

    const result = await draft(dir, translate);

    const vi = readFile(dir, 'terms/apr.vi.json');
    assert.strictEqual(vi.term, 'Lãi suất phần trăm hằng năm');
    assert.deepStrictEqual(vi.explanation, ['APR là chi phí.', 'VI:A lower APR costs less.']);
    assert.deepStrictEqual(result.adopted, ['terms/apr#term', 'terms/apr#explanation.0']);
    assert.strictEqual(readFile(dir, 'i18n.lock.json').vi['terms/apr'].term, sourceHash('Annual percentage rate'));
});

test('options follow the English count, so a translation always lines up with the correct option', async () => {
    const dir = contentFolder({
        'lessons/borrow-wisely.json': lesson(),
        'lessons/borrow-wisely.vi.json': {
            slug: 'borrow-wisely',
            check: [{ options: ['Lầm tưởng', 'Sự thật', 'Thừa'] }, { options: ['Tăng lên', 'Giảm xuống'] }]
        }
    });
    const translate = fakeTranslate();

    await draft(dir, translate);

    const vi = readFile(dir, 'lessons/borrow-wisely.vi.json');
    assert.deepStrictEqual(vi.check[0].options, ['Lầm tưởng', 'Sự thật']);
    assert.deepStrictEqual(vi.check[1].options, ['Tăng lên', 'Giảm xuống', 'VI:Same']);
});

test('the glossary maps each Term to its Vietnamese name, and the sent text uses it', async () => {
    const compound = { slug: 'compound-interest', term: 'Compound interest', vietnameseName: 'lãi kép', oneLine: 'Interest on interest.' };
    const dir = contentFolder({
        'terms/apr.json': { ...apr(), whyItMatters: 'Compound interest makes debt grow.' },
        'terms/compound-interest.json': compound
    });

    const glossary = contentGlossary(dir);

    assert.deepStrictEqual(glossary, { 'Annual percentage rate': 'lãi suất phần trăm hằng năm', 'Compound interest': 'lãi kép' });
    const translate = fakeTranslate();
    await draft(dir, translate, { budget: 'ngân sách' });
    assert.ok(translate.sent.includes(
        '<mstrans:dictionary translation="Lãi kép">Compound interest</mstrans:dictionary> makes debt grow.'));
});

test('a translation that drops a placeholder is not written', async () => {
    const dir = contentFolder({ 'terms/apr.json': { ...apr(), oneLine: 'Costs {amount} a year.' } });
    const translate = (texts) => Promise.resolve(texts.map(() => 'Không có'));

    const result = await draft(dir, translate);

    assert.deepStrictEqual(result.mismatched.map((m) => m.key), ['terms/apr#oneLine']);
    assert.strictEqual(readFile(dir, 'terms/apr.vi.json').oneLine, undefined);
});

test('a paragraph that fails the placeholder check stays as a blank, so the list keeps the English count', async () => {
    const dir = contentFolder({ 'terms/apr.json': { ...apr(), explanation: ['APR shows the cost.', 'It is {rate} a year.'] } });
    const translate = (texts) => Promise.resolve(texts.map((text) => (text.includes('notranslate') ? 'Mỗi năm.' : `VI:${text}`)));

    await draft(dir, translate);

    assert.deepStrictEqual(readFile(dir, 'terms/apr.vi.json').explanation, ['VI:APR shows the cost.', '']);
});

test('a counterpart field whose English is gone is removed, and its lock entry with it', async () => {
    const english = { slug: 'apr', term: 'APR', vietnameseName: 'lãi suất năm' };
    const dir = contentFolder({
        'terms/apr.json': english,
        'terms/apr.vi.json': { slug: 'apr', term: 'Lãi suất năm', inVietnam: { text: 'Cũ.' } },
        'i18n.lock.json': { vi: { 'terms/apr': { term: sourceHash('APR'), 'inVietnam.text': sourceHash('Old.') } } }
    });

    await draft(dir, fakeTranslate());

    assert.deepStrictEqual(readFile(dir, 'terms/apr.vi.json'), { slug: 'apr', term: 'Lãi suất năm' });
    assert.deepStrictEqual(readFile(dir, 'i18n.lock.json'), { vi: { 'terms/apr': { term: sourceHash('APR') } } });
});

test('a content folder with no Term Entries or Lessons writes nothing', async () => {
    const dir = contentFolder({ 'approved-sources.json': [] });
    const translate = fakeTranslate();

    const result = await draft(dir, translate);

    assert.deepStrictEqual(result.pending, []);
    assert.deepStrictEqual(fs.readdirSync(dir), ['approved-sources.json']);
});

test('a dry run lists the pending fields and writes nothing', async () => {
    const dir = contentFolder({ 'terms/apr.json': apr() });
    const translate = fakeTranslate();

    const result = await translateContent({ dir, language: 'vi', glossary: {} }, { translate, dryRun: true });

    assert.strictEqual(result.pending.length, 7);
    assert.deepStrictEqual(translate.sent, []);
    assert.deepStrictEqual(fs.readdirSync(dir), ['terms']);
    assert.deepStrictEqual(fs.readdirSync(path.join(dir, 'terms')), ['apr.json']);
});

test('run drafts the content too, through the same Translator request', async () => {
    const i18n = contentFolder({ 'en.json': { 'A.One': 'One' }, 'vi.json': {}, 'glossary.vi.json': { budget: 'ngân sách' } });
    const content = contentFolder({ 'terms/apr.json': apr() });
    const calls = [];
    const fetch = (url, init) => {
        const texts = JSON.parse(init.body).map((t) => t.Text);
        calls.push(texts);
        return Promise.resolve({ status: 200, ok: true, json: () => Promise.resolve(texts.map((text) => ({ translations: [{ text: `VI:${text}` }] }))) });
    };

    const results = await run({ dir: i18n, contentDir: content, env: { AZURE_TRANSLATOR_KEY: 'k', AZURE_TRANSLATOR_REGION: 'r' }, fetch, log: () => {} });

    assert.strictEqual(results.vi.translated.length, 1);
    assert.strictEqual(results.vi.content.translated.length, 7);
    assert.strictEqual(readFile(content, 'terms/apr.vi.json').oneLine, 'VI:The yearly cost of borrowing money.');
});

test('run without credentials fails when content is pending, and writes nothing', async () => {
    const i18n = contentFolder({ 'en.json': {}, 'vi.json': {} });
    const content = contentFolder({ 'terms/apr.json': apr() });

    await assert.rejects(run({ dir: i18n, contentDir: content, env: {}, log: () => {} }), /AZURE_TRANSLATOR_KEY/);
    assert.deepStrictEqual(fs.readdirSync(path.join(content, 'terms')), ['apr.json']);
});

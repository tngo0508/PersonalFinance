'use strict';
/* eslint-env node */

/**
 * Tests for i18n-translate.js. Run with: node --test PersonalFinance/src/PersonalFinance.Web/tools/
 * The Translator is a fake fetch, so nothing leaves the machine.
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { applyGlossary, batches, loadDotEnv, protect, run, translateLanguage, unprotect } = require('./i18n-translate');
const { sourceHash } = require('./i18n-common');

/**
 * A fake Translator: answers each text with a "VI:" prefix, keeping the markup, and records the requests.
 * @param {object} [options] { statuses: statuses to return before succeeding, retryAfter, transform }.
 * @returns {Function} A fetch with a .calls array.
 */
function fakeTranslator (options = {}) {
    const statuses = [...(options.statuses || [])];
    const calls = [];
    const fake = (url, init) => {
        calls.push({ url: new URL(url), init, texts: JSON.parse(init.body).map((t) => t.Text) });
        const status = statuses.shift();
        if (status) {
            return Promise.resolve({
                status,
                ok: false,
                headers: { get: (name) => (name === 'retry-after' ? options.retryAfter : null) },
                text: () => Promise.resolve('busy')
            });
        }
        const transform = options.transform || ((text) => `VI:${text}`);
        return Promise.resolve({
            status: 200,
            ok: true,
            headers: { get: () => null },
            json: () => Promise.resolve(calls[calls.length - 1].texts.map((text) => ({ translations: [{ text: transform(text), to: 'vi' }] })))
        });
    };
    fake.calls = calls;
    return fake;
}

const translator = (fetch, sleeps = []) => ({
    endpoint: 'https://translator.test/',
    key: 'test-key',
    region: 'westus2',
    fetch,
    sleep: (ms) => {
        sleeps.push(ms);
        return Promise.resolve();
    },
    log: () => {}
});

/**
 * A resource folder with the given files.
 * @param {Object<string, object>} files File name to JSON content.
 * @returns {string} The folder.
 */
function folder (files) {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'i18n-translate-'));
    for (const [name, content] of Object.entries(files)) {
        fs.writeFileSync(path.join(dir, name), JSON.stringify(content));
    }
    return dir;
}

test('protect wraps each placeholder as notranslate and escapes the text', () => {
    assert.strictEqual(protect('Save {amount} & <go> to {name}', {}),
        'Save <span class="notranslate">{amount}</span> &amp; &lt;go&gt; to <span class="notranslate">{name}</span>');
    assert.strictEqual(protect('The {0} field', {}), 'The <span class="notranslate">{0}</span> field');
});

test('protect applies the glossary as dynamic-dictionary markup, longest term first, whole words, keeping capitals', () => {
    const glossary = { 'net worth': 'giá trị tài sản ròng', net: 'mạng' };
    assert.strictEqual(applyGlossary('Your Net Worth and network', glossary),
        'Your <mstrans:dictionary translation="Giá trị tài sản ròng">Net Worth</mstrans:dictionary> and network');
    assert.strictEqual(applyGlossary('your net worth', glossary),
        'your <mstrans:dictionary translation="giá trị tài sản ròng">net worth</mstrans:dictionary>');
});

test('protect never puts glossary markup inside a placeholder', () => {
    assert.strictEqual(protect('{budget} budget', { budget: 'ngân sách' }),
        '<span class="notranslate">{budget}</span> <mstrans:dictionary translation="ngân sách">budget</mstrans:dictionary>');
});

test('unprotect strips the markup and entities the Translator returns', () => {
    assert.strictEqual(unprotect('Còn <span class="notranslate">{amount}</span> &amp; &quot;x&quot; &lt;b&gt;'),
        'Còn {amount} & "x" <b>');
    assert.strictEqual(unprotect(' <span class=notranslate>{name}</span> <mstrans:dictionary translation="a">a</mstrans:dictionary> '),
        '{name} a');
});

test('batches keep to 1,000 texts and 50,000 characters per request', () => {
    const many = Array.from({ length: 2500 }, () => 'x');
    assert.deepStrictEqual(batches(many).map((b) => b.length), [1000, 1000, 500]);
    const long = ['a'.repeat(30000), 'b'.repeat(30000), 'c'];
    assert.deepStrictEqual(batches(long), [[0], [1, 2]]);
});

test('only missing and stale keys are sent; a hand translation without a lock entry is adopted', async () => {
    const english = { 'A.New': 'New', 'A.Changed': 'Changed now', 'A.Same': 'Same', 'A.Hand': 'Hand' };
    const target = { 'A.Changed': 'cũ', 'A.Same': 'giống', 'A.Hand': 'tay', 'A.Gone': 'bỏ' };
    const lock = { 'A.Changed': sourceHash('Changed before'), 'A.Same': sourceHash('Same'), 'A.Gone': 'x' };
    const fetch = fakeTranslator();

    const result = await translateLanguage({ english, target, lock, glossary: {}, language: 'vi' }, translator(fetch));

    assert.deepStrictEqual(fetch.calls[0].texts, ['New', 'Changed now']);
    assert.deepStrictEqual(result.translated, ['A.New', 'A.Changed']);
    assert.deepStrictEqual(result.adopted, ['A.Hand']);
    assert.deepStrictEqual(result.removed, ['A.Gone']);
    assert.deepStrictEqual(result.strings, { 'A.New': 'VI:New', 'A.Changed': 'VI:Changed now', 'A.Same': 'giống', 'A.Hand': 'tay' });
    assert.deepStrictEqual(result.lock, {
        'A.New': sourceHash('New'),
        'A.Changed': sourceHash('Changed now'),
        'A.Same': sourceHash('Same'),
        'A.Hand': sourceHash('Hand')
    });
});

test('nothing is sent when every key is current', async () => {
    const fetch = fakeTranslator();
    const english = { 'A.Same': 'Same' };

    await translateLanguage({ english, target: { 'A.Same': 'giống' }, lock: { 'A.Same': sourceHash('Same') }, glossary: {}, language: 'vi' }, translator(fetch));

    assert.strictEqual(fetch.calls.length, 0);
});

test('a translation that drops a placeholder is not written and keeps its old text and lock', async () => {
    const english = { 'A.Left': '{amount} left' };
    const fetch = fakeTranslator({ transform: () => 'Còn lại' });
    const oldLock = { 'A.Left': sourceHash('{amount} remaining') };

    const result = await translateLanguage({ english, target: { 'A.Left': 'Còn {amount}' }, lock: oldLock, glossary: {}, language: 'vi' }, translator(fetch));

    assert.deepStrictEqual(result.mismatched.map((m) => m.key), ['A.Left']);
    assert.deepStrictEqual(result.strings, { 'A.Left': 'Còn {amount}' });
    assert.deepStrictEqual(result.lock, oldLock);
});

test('placeholders may be reordered by the translation', async () => {
    const english = { 'A.Of': '{current} of {target}' };
    const fetch = fakeTranslator({ transform: () => '<span class="notranslate">{target}</span> có <span class="notranslate">{current}</span>' });

    const result = await translateLanguage({ english, target: {}, lock: {}, glossary: {}, language: 'vi' }, translator(fetch));

    assert.deepStrictEqual(result.strings, { 'A.Of': '{target} có {current}' });
});

test('plurals: Vietnamese gets only .other, translated from the English .other', async () => {
    const english = { 'A.Goals.one': '{count} goal', 'A.Goals.other': '{count} goals' };
    const fetch = fakeTranslator();

    const result = await translateLanguage({ english, target: {}, lock: {}, glossary: {}, language: 'vi' }, translator(fetch));

    assert.deepStrictEqual(Object.keys(result.strings), ['A.Goals.other']);
    assert.deepStrictEqual(fetch.calls[0].texts, ['<span class="notranslate">{count}</span> goals']);
});

test('the request asks for html from en to the language, with the key and region headers', async () => {
    const fetch = fakeTranslator();

    await translateLanguage({ english: { 'A.B': 'Hi' }, target: {}, lock: {}, glossary: {}, language: 'vi' }, translator(fetch));

    const { url, init } = fetch.calls[0];
    assert.strictEqual(url.origin + url.pathname, 'https://translator.test/translate');
    assert.strictEqual(url.searchParams.get('api-version'), '3.0');
    assert.strictEqual(url.searchParams.get('from'), 'en');
    assert.strictEqual(url.searchParams.get('to'), 'vi');
    assert.strictEqual(url.searchParams.get('textType'), 'html');
    assert.strictEqual(init.headers['Ocp-Apim-Subscription-Key'], 'test-key');
    assert.strictEqual(init.headers['Ocp-Apim-Subscription-Region'], 'westus2');
});

test('429 backs off, honouring Retry-After, then succeeds', async () => {
    const fetch = fakeTranslator({ statuses: [429, 429], retryAfter: '2' });
    const sleeps = [];

    const result = await translateLanguage({ english: { 'A.B': 'Hi' }, target: {}, lock: {}, glossary: {}, language: 'vi' }, translator(fetch, sleeps));

    assert.deepStrictEqual(sleeps, [2000, 2000]);
    assert.strictEqual(fetch.calls.length, 3);
    assert.deepStrictEqual(result.translated, ['A.B']);
});

test('without Retry-After the back-off doubles, and it gives up after six attempts', async () => {
    const fetch = fakeTranslator({ statuses: [429, 503, 429, 429, 429, 429] });
    const sleeps = [];

    await assert.rejects(
        translateLanguage({ english: { 'A.B': 'Hi' }, target: {}, lock: {}, glossary: {}, language: 'vi' }, translator(fetch, sleeps)),
        /after 6 attempts/);
    assert.deepStrictEqual(sleeps, [1000, 2000, 4000, 8000, 16000]);
});

test('run writes the language file in English order and the lock', async () => {
    const dir = folder({ 'en.json': { 'B.Two': 'Two', 'A.One': 'One' }, 'vi.json': {}, 'glossary.vi.json': {} });
    const fetch = fakeTranslator();

    await run({ dir, contentDir: null, env: { AZURE_TRANSLATOR_KEY: 'k', AZURE_TRANSLATOR_REGION: 'r' }, fetch, sleep: () => Promise.resolve(), log: () => {} });

    const vi = JSON.parse(fs.readFileSync(path.join(dir, 'vi.json'), 'utf8'));
    assert.deepStrictEqual(Object.keys(vi), ['B.Two', 'A.One']);
    const lock = JSON.parse(fs.readFileSync(path.join(dir, 'i18n.lock.json'), 'utf8'));
    assert.deepStrictEqual(lock, { vi: { 'B.Two': sourceHash('Two'), 'A.One': sourceHash('One') } });
    assert.ok(!fs.readdirSync(dir).includes('glossary.vi.json.json'));
});

test('run without credentials fails when there is something to send, and writes nothing', async () => {
    const dir = folder({ 'en.json': { 'A.One': 'One' }, 'vi.json': {} });

    await assert.rejects(run({ dir, contentDir: null, env: {}, fetch: fakeTranslator(), log: () => {} }), /AZURE_TRANSLATOR_KEY/);
    assert.ok(!fs.existsSync(path.join(dir, 'i18n.lock.json')));
});

test('a dry run calls nothing and writes nothing', async () => {
    const dir = folder({ 'en.json': { 'A.One': 'One' }, 'vi.json': {} });
    const fetch = fakeTranslator();

    const results = await run({ dir, contentDir: null, env: {}, fetch, dryRun: true, log: () => {} });

    assert.deepStrictEqual(results.vi.pending, ['A.One']);
    assert.strictEqual(fetch.calls.length, 0);
    assert.ok(!fs.existsSync(path.join(dir, 'i18n.lock.json')));
});

test('loadDotEnv fills unset variables only and strips quotes', () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'i18n-env-'));
    const file = path.join(dir, '.env');
    fs.writeFileSync(file, '# comment\nAZURE_TRANSLATOR_REGION="westus2"\nexport AZURE_TRANSLATOR_KEY=abc\nSET=new\n');
    const env = { SET: 'old' };

    loadDotEnv(file, env);

    assert.deepStrictEqual(env, { SET: 'old', AZURE_TRANSLATOR_REGION: 'westus2', AZURE_TRANSLATOR_KEY: 'abc' });
});

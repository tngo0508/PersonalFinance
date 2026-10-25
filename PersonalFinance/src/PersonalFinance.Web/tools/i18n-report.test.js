'use strict';
/* eslint-env node */

/**
 * Tests for i18n-report.js. Run with: node --test PersonalFinance/src/PersonalFinance.Web/tools/
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { buildReport, formatMarkdown } = require('./i18n-report');
const { sourceHash } = require('./i18n-common');

/**
 * A resource folder with the given files; strings are written as given, objects as JSON.
 * @param {Object<string, object|string>} files File name to content.
 * @returns {string} The folder.
 */
function folder (files) {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'i18n-report-'));
    for (const [name, content] of Object.entries(files)) {
        fs.writeFileSync(path.join(dir, name), typeof content === 'string' ? content : JSON.stringify(content));
    }
    return dir;
}

test('missing keys are plural-aware: Vietnamese needs only .other', () => {
    const dir = folder({
        'en.json': { 'A.Goals.one': '{count} goal', 'A.Goals.other': '{count} goals', 'A.Title': 'Title' },
        'vi.json': { 'A.Goals.other': '{count} mục tiêu' }
    });

    const report = buildReport({ dir, sources: [] });

    assert.deepStrictEqual(report.languages.vi.missing, ['A.Title']);
    assert.deepStrictEqual(report.errors, []);
});

test('a translation whose English changed since the lock is stale, not an error', () => {
    const dir = folder({
        'en.json': { 'A.Save': 'Save now', 'A.Close': 'Close' },
        'vi.json': { 'A.Save': 'Lưu', 'A.Close': 'Đóng' },
        'i18n.lock.json': { vi: { 'A.Save': sourceHash('Save'), 'A.Close': sourceHash('Close') } }
    });

    const report = buildReport({ dir, sources: [] });

    assert.deepStrictEqual(report.languages.vi.stale, ['A.Save']);
    assert.deepStrictEqual(report.errors, []);
});

test('a placeholder mismatch is a failing error', () => {
    const dir = folder({
        'en.json': { 'A.Left': '{amount} left' },
        'vi.json': { 'A.Left': 'Còn {so_tien}' }
    });

    const report = buildReport({ dir, sources: [] });

    assert.strictEqual(report.errors.length, 1);
    assert.match(report.errors[0], /vi\.json "A\.Left": placeholders differ/);
});

test('malformed JSON in any resource file is a failing error', () => {
    const dir = folder({ 'en.json': { 'A.B': 'x' }, 'vi.json': '{ "A.B": "y", }', 'glossary.vi.json': '{' });

    const report = buildReport({ dir, sources: [] });

    assert.strictEqual(report.errors.length, 2);
    assert.match(report.errors.join('\n'), /vi\.json is not valid JSON/);
    assert.match(report.errors.join('\n'), /glossary\.vi\.json is not valid JSON/);
});

test('keys referenced in Razor, C# or JS but absent from en.json are reported', () => {
    const dir = folder({ 'en.json': { 'A.Here': 'x', 'A.Count.other': 'y' }, 'vi.json': {} });
    const sources = [
        '@L["A.Here"] @L["A.Gone"] @L.Named("A.Named", ("x", 1)) @L.Plural("A.Count", 2) @L.Plural("A.Many", 2)',
        '[Required(ErrorMessage = "A.Required")] [Display(Name = "A.Field")] _localizer["A.Ctrl"]',
        'PFI18n.t(\'A.Js\'); PFI18n.plural("A.Count", 3); ViewData["Title"] = "Not.A key";'
    ];

    const report = buildReport({ dir, sources });

    assert.deepStrictEqual(report.referencedMissing,
        ['A.Ctrl', 'A.Field', 'A.Gone', 'A.Js', 'A.Many.other', 'A.Named', 'A.Required']);
});

test('English keys nothing references are reported, keys under a used prefix are not', () => {
    const dir = folder({
        'en.json': { 'A.Used': 'x', 'A.Unused': 'y', 'Errors.Code1': 'z', 'Page.Js.Hi': 'h', 'A.Count.one': 'o', 'A.Count.other': 'p' },
        'vi.json': {}
    });
    const sources = ['L["A.Used"] "Errors." + code <js-strings prefix="Page.Js." /> L.Plural("A.Count", n)'];

    const report = buildReport({ dir, sources });

    assert.deepStrictEqual(report.unused, ['A.Unused']);
});

test('the Markdown lists errors, counts and the gaps', () => {
    const dir = folder({ 'en.json': { 'A.B': '{x}', 'A.C': 'c' }, 'vi.json': { 'A.B': 'b' } });

    const markdown = formatMarkdown(buildReport({ dir, sources: [] }));

    assert.match(markdown, /### Errors \(failing\)/);
    assert.match(markdown, /\| vi \| 1 \| 1 \|/);
    assert.match(markdown, /Missing in vi\.json: 1/);
});

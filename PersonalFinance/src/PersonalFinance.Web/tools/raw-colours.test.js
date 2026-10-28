'use strict';
/* eslint-env node */

/**
 * Tests for the raw-colour guard (raw-colours.js, issue #480): colours in the Web project's stylesheets come from
 * the token blocks of the Ledger and app token sheets, and the allow-list only ever shrinks.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { countRawColours, listStylesheets, checkRawColours, scanStylesheets, readAllowList } = require('./raw-colours');

const WEB_ROOT = path.join(__dirname, '..');

/**
 * Writes a file, creating its folders.
 * @param {string} root The project folder.
 * @param {string} relativePath The file path under the root, with forward slashes.
 * @param {string} [content] The file content.
 */
function write (root, relativePath, content = '') {
    const file = path.join(root, ...relativePath.split('/'));
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, content);
}

test('hex, rgb(), rgba(), hsl() and hsla() literals are counted', () => {
    const css = '.a { color: #fff; background: #1D6A50; border-color: #12345678; outline-color: #abcd; }\n' +
        '.b { color: rgb(1, 2, 3); background: rgba(0, 0, 0, 0.5); border-color: hsl(10 20% 30%); fill: HSLA(1, 2%, 3%, 0.4); }';
    assert.strictEqual(countRawColours(css, false), 8);
});

test('named colours, var() and the colour keywords are not literals', () => {
    const css = '.a { color: var(--app-text); background: transparent; border-color: currentColor; fill: white; }\n' +
        '.b { --bs-primary-rgb: 29, 106, 80; color: rgba(var(--bs-primary-rgb), 0.5); }';
    assert.strictEqual(countRawColours(css, false), 1);
});

test('comments, strings, url() and id selectors are not literals', () => {
    const css = '/* was #fff, then rgb(0, 0, 0) */\n' +
        '#fab, #add > .x { content: "#fff"; background: url(icon.svg#abc); mask: url("data:image/svg+xml,%23fff"); }\n' +
        '.c { grid-template-areas: "a b"; }';
    assert.strictEqual(countRawColours(css, false), 0);
});

test('hex runs that are not 3, 4, 6 or 8 digits long are not colours', () => {
    assert.strictEqual(countRawColours('.a { color: #12345; background: #1234567; }', false), 0);
});

test('literals inside @media and other at-rules are counted', () => {
    const css = '@media (max-width: 600px) { .a { color: #000; } @supports (display: grid) { .b { color: rgb(0 0 0); } } }';
    assert.strictEqual(countRawColours(css, false), 2);
});

test('the token blocks of a token sheet are skipped, its other rules are not', () => {
    const css = ':root { --lp-ink: #14283B; --app-shadow: 0 1px 2px rgba(0, 0, 0, 0.04); }\n' +
        ':root[data-theme="dark"] { --lp-ink: #E6ECEF; }\n' +
        ':root[data-theme="dark"] .lp-skyline { color: #fff; }\n' +
        '.topbar { color: #000; }';
    assert.strictEqual(countRawColours(css, true), 2);
});

test('only the first :root and dark :root blocks of a token sheet are token blocks', () => {
    const css = ':root { --lp-ink: #14283B; }\n:root[data-theme="dark"] { --lp-ink: #E6ECEF; }\n' +
        ':root { --gdrive-secondary: #5c2d91; }\n:root[data-theme="dark"] { --gdrive-secondary: #7a4fb0; }\n' +
        '@media (min-width: 1px) { :root { --x: #000; } }';
    assert.strictEqual(countRawColours(css, true), 3);
});

test('braces and comment markers inside strings do not shift the rules', () => {
    const css = '.a::before { content: "/* {"; color: #fff; }\n.b::after { content: \'}\'; color: rgb(0 0 0); }';
    assert.strictEqual(countRawColours(css, false), 2);
});

test('a :root block outside a token sheet is counted', () => {
    assert.strictEqual(countRawColours(':root { --budget-bar: #1D6A50; }', false), 1);
});

test('stylesheets are listed across the project, without vendored, built or build-output folders', () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'raw-colours-'));
    for (const file of [
        'wwwroot/css/site.css',
        'wwwroot/css/research/funds.css',
        'Views/Home/Index.cshtml.css',
        'wwwroot/lib/bootstrap/bootstrap.css',
        'wwwroot/Identity/lib/bootstrap/bootstrap.css',
        'wwwroot/dist/css/site.css',
        'bin/Debug/x.css',
        'obj/Debug/x.css',
        'node_modules/pkg/x.css',
        'wwwroot/js/site.js'
    ]) {
        write(root, file);
    }
    assert.deepStrictEqual(listStylesheets(root), [
        'Views/Home/Index.cshtml.css',
        'wwwroot/css/research/funds.css',
        'wwwroot/css/site.css'
    ]);
});

test('counts that match the allow-list pass, and clean unlisted files pass', () => {
    assert.deepStrictEqual(checkRawColours({ 'a.css': 3, 'b.css': 1, 'c.css': 0 }, { 'a.css': 3, 'b.css': 1 }), []);
});

test('a listed file whose count falls fails until its entry is lowered', () => {
    assert.deepStrictEqual(checkRawColours({ 'a.css': 2 }, { 'a.css': 3 }), [
        'a.css has 2 raw colour literal(s), down from 3: lower its allow-list entry to 2'
    ]);
});

test('a listed file whose count rises fails', () => {
    assert.deepStrictEqual(checkRawColours({ 'a.css': 4 }, { 'a.css': 3 }), [
        'a.css has 4 raw colour literal(s), up from 3 on the allow-list: use a token instead'
    ]);
});

test('an unlisted file with any literal fails', () => {
    assert.deepStrictEqual(checkRawColours({ 'new.css': 1 }, {}), [
        'new.css has 1 raw colour literal(s) and is not on the allow-list: use a token instead'
    ]);
});

test('a listed file that reaches zero fails until it is taken off the list', () => {
    assert.deepStrictEqual(checkRawColours({ 'a.css': 0 }, { 'a.css': 3 }), [
        'a.css has no raw colour literals left: remove it from the allow-list'
    ]);
});

test('a listed file that no longer exists fails until it is taken off the list', () => {
    assert.deepStrictEqual(checkRawColours({}, { 'gone.css': 2 }), [
        'gone.css is on the allow-list but was not found: remove it from the allow-list'
    ]);
});

test('the Web project\'s stylesheets match the allow-list', () => {
    assert.deepStrictEqual(checkRawColours(scanStylesheets(WEB_ROOT), readAllowList()), []);
});

test('adding a raw colour to a clean token sheet fails the guard', () => {
    const counts = scanStylesheets(WEB_ROOT);
    const ledger = fs.readFileSync(path.join(WEB_ROOT, 'wwwroot', 'css', 'ledger.css'), 'utf8');
    counts['wwwroot/css/ledger.css'] = countRawColours(`${ledger}\n.topbar { color: #fff; }\n`, true);
    assert.match(checkRawColours(counts, readAllowList()).join('\n'), /wwwroot\/css\/ledger\.css has 1 raw colour/);
});

test('adding a raw colour to a listed stylesheet fails the guard', () => {
    const counts = scanStylesheets(WEB_ROOT);
    const site = fs.readFileSync(path.join(WEB_ROOT, 'wwwroot', 'css', 'site.css'), 'utf8');
    counts['wwwroot/css/site.css'] = countRawColours(`${site}\n.app-footer { color: rgb(0, 0, 0); }\n`, true);
    assert.match(checkRawColours(counts, readAllowList()).join('\n'), /wwwroot\/css\/site\.css has \d+ raw colour literal\(s\), up from/);
});

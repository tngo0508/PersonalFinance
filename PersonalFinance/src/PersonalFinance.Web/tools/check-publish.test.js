'use strict';
/* eslint-env node */

/**
 * Tests for check-publish.js. Run with: node --test PersonalFinance/src/PersonalFinance.Web/tools/check-publish.test.js
 * Each test builds a small publish folder that passes, breaks one rule, and expects a problem naming it.
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { checkPublishOutput } = require('./check-publish');

/**
 * Writes a file, creating its folders.
 * @param {string} root The publish folder.
 * @param {string} relativePath The file path under the root, with forward slashes.
 * @param {string} [content] The file content.
 */
function write (root, relativePath, content = 'x') {
    const file = path.join(root, ...relativePath.split('/'));
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, content);
}

/**
 * Builds a publish folder that satisfies every rule.
 * @param {object[]} [endpoints] Extra endpoints for the static web assets manifest.
 * @returns {string} The folder path.
 */
function validPublish (endpoints = []) {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'check-publish-'));
    for (const file of ['wwwroot/dist/js/site.js', 'wwwroot/dist/css/site.css']) {
        write(root, file);
        write(root, `${file}.br`);
        write(root, `${file}.gz`);
    }
    write(root, 'wwwroot/lib/bootstrap/bootstrap.min.css.map');
    write(root, 'wwwroot/Identity/lib/bootstrap/bootstrap.min.css.map');
    const manifest = {
        Endpoints: [{ Route: 'dist/js/site.abc.js', AssetFile: 'dist/js/site.js.gz' }, ...endpoints]
    };
    write(root, 'PersonalFinance.Web.staticwebassets.endpoints.json', JSON.stringify(manifest));
    return root;
}

test('a publish with only minified assets passes', () => {
    assert.deepStrictEqual(checkPublishOutput(validPublish()), []);
});

test('missing dist js is reported', () => {
    const root = validPublish();
    fs.rmSync(path.join(root, 'wwwroot', 'dist', 'js'), { recursive: true });
    assert.match(checkPublishOutput(root).join('\n'), /wwwroot\/dist\/js has no \.js files/);
});

test('missing dist css is reported', () => {
    const root = validPublish();
    fs.rmSync(path.join(root, 'wwwroot', 'dist', 'css'), { recursive: true });
    assert.match(checkPublishOutput(root).join('\n'), /wwwroot\/dist\/css has no \.css files/);
});

test('a dist file without a compressed sibling is reported', () => {
    const root = validPublish();
    fs.rmSync(path.join(root, 'wwwroot', 'dist', 'js', 'site.js.br'));
    assert.match(checkPublishOutput(root).join('\n'), /wwwroot\/dist\/js\/site\.js has no \.br sibling/);
});

test('a source map outside the vendored lib folders is reported', () => {
    const root = validPublish();
    write(root, 'wwwroot/dist/js/site.js.map');
    const problems = checkPublishOutput(root);
    assert.strictEqual(problems.length, 1);
    assert.match(problems[0], /source map wwwroot\/dist\/js\/site\.js\.map/);
});

for (const forbidden of [
    'wwwroot/js/site.js',
    'wwwroot/css/site.css',
    'wwwroot/js/site.js.br',
    'wwwroot/package.json',
    'wwwroot/package-lock.json',
    'wwwroot/.eslintrc.js',
    'node_modules/esbuild/index.js',
    'wwwroot/lib/tool/node_modules/dep/index.js'
]) {
    test(`${forbidden} in the output is reported`, () => {
        const root = validPublish();
        write(root, forbidden);
        const top = forbidden.split('/').slice(0, forbidden.startsWith('wwwroot/') ? 2 : 1).join('/');
        assert.match(checkPublishOutput(root).join('\n'), new RegExp(`must not be published: ${top.replace(/\./g, '\\.')}`));
    });
}

test('a manifest endpoint serving unminified source is reported', () => {
    const root = validPublish([{ Route: 'js/site.abc.js', AssetFile: 'js/site.js.gz' }]);
    assert.match(checkPublishOutput(root).join('\n'), /manifest serves js\/site\.js$/m);
});

test('a publish folder without the manifest is reported', () => {
    const root = validPublish();
    fs.rmSync(path.join(root, 'PersonalFinance.Web.staticwebassets.endpoints.json'));
    assert.match(checkPublishOutput(root).join('\n'), /staticwebassets\.endpoints\.json not found/);
});

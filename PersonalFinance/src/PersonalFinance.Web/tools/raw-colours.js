'use strict';
/* eslint-env node */
/* eslint-disable no-console -- a check reports through the console */

/**
 * The raw-colour guard (issue #480): colours in the Web project's stylesheets come from tokens. A hex, rgb()/rgba()
 * or hsl()/hsla() literal is allowed only in the token blocks (:root and :root[data-theme="dark"]) of the Ledger
 * (ledger.css) and app (site.css) token sheets. raw-colours.allowlist.json records each not-yet-clean file with its
 * current literal count; raw-colours.test.js fails when a count rises, an unlisted file has a literal, or a listed
 * file is clean (or gone) but still listed.
 *
 * Usage: node PersonalFinance/src/PersonalFinance.Web/tools/raw-colours.js
 *   prints every stylesheet's current count, to update the allow-list after cleaning a file.
 */

const fs = require('fs');
const path = require('path');

const WEB_ROOT = path.resolve(__dirname, '..');
const ALLOW_LIST_PATH = path.join(__dirname, 'raw-colours.allowlist.json');
const TOKEN_SHEETS = ['wwwroot/css/ledger.css', 'wwwroot/css/site.css'];
const TOKEN_SELECTOR = /^:root(\[data-theme=(["']?)dark\2\])?$/;
// Vendored third-party files, the Release minify output, and build or package folders
const SKIPPED_FOLDERS = ['wwwroot/lib', 'wwwroot/Identity/lib', 'wwwroot/dist', 'bin', 'obj', 'node_modules'];
const HEX = /#([0-9a-f]+)(?![\w-])/gi;
const HEX_LENGTHS = [3, 4, 6, 8];
const COLOUR_FUNCTION = /(?<![\w-])(?:rgba?|hsla?)\(/gi;

/**
 * Counts the colour literals in one run of declarations.
 * @param {string} declarations The text between a rule's braces, without nested rules.
 * @returns {number} The number of literals.
 */
function countInDeclarations (declarations) {
    const values = declarations
        .replace(/"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'/g, '""')
        .replace(/url\([^)]*\)/gi, 'url()');
    const hexes = [...values.matchAll(HEX)].filter(match => HEX_LENGTHS.includes(match[1].length)).length;
    return hexes + (values.match(COLOUR_FUNCTION) || []).length;
}

/**
 * Counts the raw colour literals in a stylesheet. Only declarations count, so id selectors are not colours.
 * @param {string} css The stylesheet.
 * @param {boolean} isTokenSheet Whether its :root token blocks may hold literals.
 * @returns {number} The number of literals.
 */
function countRawColours (css, isTokenSheet) {
    const text = css.replace(/\/\*[\s\S]*?\*\//g, ' ');
    const selectors = [];
    let chunk = '';
    let count = 0;

    const add = (declarations) => {
        const selector = selectors[selectors.length - 1];
        if (selector !== undefined && !(isTokenSheet && TOKEN_SELECTOR.test(selector))) {
            count += countInDeclarations(declarations);
        }
    };

    for (const character of text) {
        if (character === '{') {
            // Declarations before a nested rule end at their last semicolon; the rest is the rule's selector.
            const cut = chunk.lastIndexOf(';') + 1;
            add(chunk.slice(0, cut));
            selectors.push(chunk.slice(cut).trim().replace(/\s+/g, ' '));
            chunk = '';
        } else if (character === '}') {
            add(chunk);
            selectors.pop();
            chunk = '';
        } else {
            chunk += character;
        }
    }

    return count;
}

/**
 * Lists the Web project's stylesheets, without vendored, built or build-output folders.
 * @param {string} root The Web project folder.
 * @returns {string[]} Paths relative to the root with forward slashes, sorted.
 */
function listStylesheets (root) {
    const found = [];
    const walk = (relative) => {
        for (const entry of fs.readdirSync(path.join(root, relative), { withFileTypes: true })) {
            const child = relative ? `${relative}/${entry.name}` : entry.name;
            if (entry.isDirectory() && !SKIPPED_FOLDERS.includes(child)) {
                walk(child);
            } else if (entry.isFile() && entry.name.endsWith('.css')) {
                found.push(child);
            }
        }
    };
    walk('');
    return found.sort();
}

/**
 * Counts the raw colour literals in each of the Web project's stylesheets.
 * @param {string} root The Web project folder.
 * @returns {Object<string, number>} The count per stylesheet path.
 */
function scanStylesheets (root) {
    const counts = {};
    for (const file of listStylesheets(root)) {
        const css = fs.readFileSync(path.join(root, ...file.split('/')), 'utf8');
        counts[file] = countRawColours(css, TOKEN_SHEETS.includes(file));
    }
    return counts;
}

/**
 * Reads the allow-list of not-yet-clean stylesheets.
 * @returns {Object<string, number>} The allowed count per stylesheet path.
 */
function readAllowList () {
    return JSON.parse(fs.readFileSync(ALLOW_LIST_PATH, 'utf8'));
}

/**
 * Compares the counts with the allow-list.
 * @param {Object<string, number>} counts The count per stylesheet path.
 * @param {Object<string, number>} allowList The allowed count per stylesheet path.
 * @returns {string[]} One message per problem; empty when the stylesheets match.
 */
function checkRawColours (counts, allowList) {
    const problems = [];
    for (const [file, count] of Object.entries(counts)) {
        const allowed = allowList[file];
        if (allowed === undefined) {
            if (count > 0) {
                problems.push(`${file} has ${count} raw colour literal(s) and is not on the allow-list: use a token instead`);
            }
        } else if (count === 0) {
            problems.push(`${file} has no raw colour literals left: remove it from the allow-list`);
        } else if (count > allowed) {
            problems.push(`${file} has ${count} raw colour literal(s), up from ${allowed} on the allow-list: use a token instead`);
        }
    }
    for (const file of Object.keys(allowList)) {
        if (!(file in counts)) {
            problems.push(`${file} is on the allow-list but was not found: remove it from the allow-list`);
        }
    }
    return problems;
}

if (require.main === module) {
    console.log(JSON.stringify(scanStylesheets(WEB_ROOT), null, 4));
}

module.exports = { countRawColours, listStylesheets, scanStylesheets, readAllowList, checkRawColours };

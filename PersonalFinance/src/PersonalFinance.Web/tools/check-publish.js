'use strict';
/* eslint-env node */
/* eslint-disable no-console -- a CI step reports through the console */

/**
 * Checks a Release publish of the Web project holds only what Production serves (spec #286).
 *
 * Usage: node PersonalFinance/src/PersonalFinance.Web/tools/check-publish.js <publish-dir>
 *
 * Fails (exit code 1, one line per problem) when:
 * - wwwroot/dist has no minified .js or .css files,
 * - a dist file lacks its .br or .gz sibling,
 * - a .map file exists outside the vendored lib folders,
 * - unminified source or tooling is published (wwwroot/js, wwwroot/css, the web root's
 *   package.json, package-lock.json and ESLint files, node_modules),
 * - the static web assets manifest still serves any of those.
 */

const fs = require('fs');
const path = require('path');

const manifestName = 'PersonalFinance.Web.staticwebassets.endpoints.json';
const vendoredLibPrefixes = ['wwwroot/lib/', 'wwwroot/Identity/lib/'];
const forbiddenWebRootEntries = ['js', 'css', 'package.json', 'package-lock.json', '.eslintrc.js', '.eslintignore'];
const compressedExtensions = ['.br', '.gz'];

/**
 * Lists every file under a folder.
 * @param {string} root The folder to walk.
 * @returns {string[]} Paths relative to the root with forward slashes, sorted.
 */
function listFiles (root) {
    if (!fs.existsSync(root)) {
        return [];
    }

    return fs.readdirSync(root, { recursive: true, withFileTypes: true })
        .filter(entry => entry.isFile())
        .map(entry => path.relative(root, path.join(entry.parentPath, entry.name)).split(path.sep).join('/'))
        .sort();
}

/**
 * Removes a trailing .br or .gz from a web-root path.
 * @param {string} file The path.
 * @returns {string} The path of the uncompressed asset.
 */
function stripCompression (file) {
    const extension = path.extname(file);
    return compressedExtensions.includes(extension) ? file.slice(0, -extension.length) : file;
}

/**
 * Says whether a web-root-relative asset path is unminified source or tooling.
 * @param {string} assetPath The path, relative to wwwroot, with forward slashes.
 * @returns {boolean} True when Production must not serve it.
 */
function isForbiddenAsset (assetPath) {
    const parts = stripCompression(assetPath).split('/');
    return forbiddenWebRootEntries.includes(parts[0]) || parts.includes('node_modules');
}

/**
 * Checks the dist folder holds minified files of the given type, each with compressed siblings.
 * @param {string[]} files All published file paths.
 * @param {string} folder js or css.
 * @returns {string[]} Problems found.
 */
function checkDist (files, folder) {
    const prefix = `wwwroot/dist/${folder}/`;
    const assets = files.filter(file => file.startsWith(prefix) && path.extname(file) === `.${folder}`);
    if (assets.length === 0) {
        return [`wwwroot/dist/${folder} has no .${folder} files: the Release minify step did not run`];
    }

    const present = new Set(files);
    return assets.flatMap(asset => compressedExtensions
        .filter(extension => !present.has(asset + extension))
        .map(extension => `${asset} has no ${extension} sibling: static asset compression did not run`));
}

/**
 * Checks the static web assets manifest serves no unminified source or tooling.
 * @param {string} publishDir The publish folder.
 * @returns {string[]} Problems found.
 */
function checkManifest (publishDir) {
    const manifestPath = path.join(publishDir, manifestName);
    if (!fs.existsSync(manifestPath)) {
        return [`${manifestName} not found: is this a publish of PersonalFinance.Web?`];
    }

    const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    const served = new Set((manifest.Endpoints || [])
        .map(endpoint => endpoint.AssetFile)
        .filter(assetFile => assetFile && isForbiddenAsset(assetFile))
        .map(stripCompression));
    return [...served].sort().map(assetFile => `the static web assets manifest serves ${assetFile}`);
}

/**
 * Checks a Release publish of the Web project.
 * @param {string} publishDir The publish folder.
 * @returns {string[]} One message per problem; empty when the output is clean.
 */
function checkPublishOutput (publishDir) {
    const files = listFiles(publishDir);
    const problems = [...checkDist(files, 'js'), ...checkDist(files, 'css')];

    for (const map of files.filter(file => path.extname(file) === '.map')) {
        if (!vendoredLibPrefixes.some(prefix => map.startsWith(prefix))) {
            problems.push(`source map ${map} must not be published`);
        }
    }

    const forbidden = new Set();
    for (const file of files) {
        const parts = file.split('/');
        const nodeModules = parts.indexOf('node_modules');
        if (nodeModules >= 0) {
            forbidden.add(parts.slice(0, nodeModules + 1).join('/'));
        } else if (parts[0] === 'wwwroot' && parts.length > 1 && forbiddenWebRootEntries.includes(stripCompression(parts[1]))) {
            forbidden.add(`wwwroot/${stripCompression(parts[1])}`);
        }
    }
    for (const entry of [...forbidden].sort()) {
        problems.push(`unminified source or tooling must not be published: ${entry}`);
    }

    return [...problems, ...checkManifest(publishDir)];
}

if (require.main === module) {
    const publishDir = process.argv[2];
    if (!publishDir || !fs.existsSync(publishDir)) {
        console.error('check-publish: pass the publish folder, e.g. node tools/check-publish.js ./publish');
        process.exit(2);
    }

    const problems = checkPublishOutput(publishDir);
    if (problems.length > 0) {
        console.error(`check-publish: ${problems.length} problem(s) in ${publishDir}`);
        for (const problem of problems) {
            console.error(`  - ${problem}`);
        }
        process.exit(1);
    }

    console.log(`check-publish: ${publishDir} holds only minified assets`);
}

module.exports = { checkPublishOutput };

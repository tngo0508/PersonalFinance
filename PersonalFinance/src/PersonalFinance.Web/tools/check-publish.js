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
 * - a .map file exists outside wwwroot/lib and wwwroot/Identity/lib. Those hold vendored third-party
 *   files, which are published as shipped and are out of scope (an agreed deviation from spec #286),
 * - unminified source or tooling is published (wwwroot/js, wwwroot/css, the web root's
 *   package.json, package-lock.json and ESLint files, node_modules),
 * - the static web assets manifest still serves any of those.
 */

const fs = require('fs');
const path = require('path');
const { listFiles } = require('./list-files');

const manifestName = 'PersonalFinance.Web.staticwebassets.endpoints.json';
// Vendored third-party files are published as shipped, maps included: out of scope (agreed deviation, spec #286)
const vendoredLibPrefixes = ['wwwroot/lib/', 'wwwroot/Identity/lib/'];
const forbiddenWebRootEntries = ['js', 'css', 'package.json', 'package-lock.json', '.eslintrc.js', '.eslintignore'];
const compressedExtensions = ['.br', '.gz'];

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
 * Finds the unminified source or tooling entry a published file belongs to.
 * @param {string} file The path, relative to the publish folder, with forward slashes.
 * @returns {string|null} The entry Production must not publish or serve (wwwroot/js, a node_modules folder, ...),
 *   or null when the file is allowed.
 */
function forbiddenEntry (file) {
    const parts = stripCompression(file).split('/');
    const nodeModules = parts.indexOf('node_modules');
    if (nodeModules >= 0) {
        return parts.slice(0, nodeModules + 1).join('/');
    }

    return parts[0] === 'wwwroot' && parts.length > 1 && forbiddenWebRootEntries.includes(parts[1])
        ? `wwwroot/${parts[1]}`
        : null;
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
        .filter(assetFile => assetFile && forbiddenEntry(`wwwroot/${assetFile}`) !== null)
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
            problems.push(`source map ${map} must not be published (only the vendored wwwroot/lib and wwwroot/Identity/lib may ship maps)`);
        }
    }

    const forbidden = new Set(files.map(forbiddenEntry).filter(entry => entry !== null));
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

'use strict';
/* eslint-env node */
/* eslint-disable no-console -- a build step reports through the console */

/**
 * Minifies each file under wwwroot/js and wwwroot/css one-to-one into wwwroot/dist,
 * keeping its relative path (js/core/utils.js becomes dist/js/core/utils.js).
 *
 * No bundling and no output format, so top-level names and window globals are kept.
 * No source maps, because Development serves the originals. The Release build runs
 * this through the MinifyWebAssets target in PersonalFinance.Web.csproj.
 */

const fs = require('fs');
const path = require('path');
const esbuild = require('esbuild');

const webRoot = path.resolve(__dirname, '..', 'wwwroot');
const distRoot = path.join(webRoot, 'dist');
const sources = [
    { folder: 'js', extension: '.js' },
    { folder: 'css', extension: '.css' }
];

/**
 * Lists the files under a web-root folder that have the given extension.
 * @param {string} folder The folder name under the web root.
 * @param {string} extension The file extension to keep, with its dot.
 * @returns {string[]} Absolute file paths, sorted.
 */
function listSources (folder, extension) {
    const root = path.join(webRoot, folder);
    if (!fs.existsSync(root)) {
        return [];
    }

    return fs.readdirSync(root, { recursive: true, withFileTypes: true })
        .filter(entry => entry.isFile() && path.extname(entry.name) === extension)
        .map(entry => path.join(entry.parentPath, entry.name))
        .sort();
}

fs.rmSync(distRoot, { recursive: true, force: true });

const entryPoints = sources.flatMap(source => listSources(source.folder, source.extension));
if (entryPoints.length === 0) {
    console.error('minify: no files found under wwwroot/js or wwwroot/css');
    process.exit(1);
}

esbuild.buildSync({
    entryPoints,
    outbase: webRoot,
    outdir: distRoot,
    minify: true,
    bundle: false,
    sourcemap: false,
    logLevel: 'warning'
});

console.log(`minify: wrote ${entryPoints.length} files to wwwroot/dist`);

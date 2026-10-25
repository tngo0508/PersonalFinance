'use strict';
/* eslint-env node */

/**
 * The file walk shared by the build tools (minify.js and check-publish.js).
 */

const fs = require('fs');
const path = require('path');

/**
 * Lists every file under a folder, at any depth.
 * @param {string} root The folder to walk.
 * @returns {string[]} Paths relative to the root with forward slashes, sorted; empty when the folder is missing.
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

module.exports = { listFiles };

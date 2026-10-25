'use strict';
/* eslint-env node */

/**
 * Shared by i18n-translate.js and i18n-report.js (ADR 0015, spec §2.4): reading the resource files, placeholders,
 * the plural-aware key map and the source hash stored in i18n.lock.json.
 */

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

/** The resource folder, Resources/i18n in the Web project. */
const RESOURCES_DIR = path.join(__dirname, '..', 'Resources', 'i18n');

/** The source language every other file is translated from. */
const SOURCE_LANGUAGE = 'en';

/** The lock file recording which English text each translation came from. */
const LOCK_FILE = 'i18n.lock.json';

const PLURAL_CATEGORIES = ['zero', 'one', 'two', 'few', 'many', 'other'];
const LANGUAGE_FILE = /^([a-z]{2,3}(?:-[A-Z]{2})?)\.json$/;
const PLACEHOLDER = /\{[A-Za-z0-9]+\}/g;

/**
 * Reads a JSON file of "Key": "text" pairs.
 * @param {string} file Absolute path.
 * @returns {Object<string, string>} The pairs, in file order.
 * @throws {Error} When the file is not valid JSON, not an object, or holds a non-string value.
 */
function readStrings (file) {
    const data = readJson(file);
    if (!data || typeof data !== 'object' || Array.isArray(data)) {
        throw new Error(`${path.basename(file)} must hold one JSON object of "Key": "text" pairs.`);
    }
    for (const [key, value] of Object.entries(data)) {
        if (typeof value !== 'string') {
            throw new Error(`${path.basename(file)}: the value of "${key}" must be a string.`);
        }
    }
    return data;
}

/**
 * Reads and parses a JSON file, naming the file in the error.
 * @param {string} file Absolute path.
 * @returns {*} The parsed value.
 */
function readJson (file) {
    const text = fs.readFileSync(file, 'utf8');
    try {
        return JSON.parse(text);
    } catch (error) {
        throw new Error(`${path.basename(file)} is not valid JSON: ${error.message}`);
    }
}

/**
 * Writes JSON the way the resource files are kept: two-space indent, UTF-8 (no \u escapes), trailing newline.
 * @param {string} file Absolute path.
 * @param {*} value The value to write.
 */
function writeJson (file, value) {
    fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`, 'utf8');
}

/**
 * The target languages: every <code>.json in the folder except English, the lock and the glossaries.
 * @param {string} dir The resource folder.
 * @returns {string[]} Language codes, sorted.
 */
function targetLanguages (dir) {
    return fs.readdirSync(dir)
        .map((name) => LANGUAGE_FILE.exec(name))
        .filter((match) => match && match[1] !== SOURCE_LANGUAGE)
        .map((match) => match[1])
        .sort();
}

/**
 * The placeholders in a text, sorted, duplicates kept: "{name}" and positional "{0}".
 * @param {string} text The text.
 * @returns {string[]} The placeholders.
 */
function placeholders (text) {
    return (text.match(PLACEHOLDER) || []).sort();
}

/**
 * True when two texts carry exactly the same placeholders, in any order.
 * @param {string} source The English text.
 * @param {string} translation The translated text.
 * @returns {boolean} Whether they match.
 */
function samePlaceholders (source, translation) {
    const a = placeholders(source);
    const b = placeholders(translation);
    return a.length === b.length && a.every((value, i) => value === b[i]);
}

/**
 * The stem of a plural key ("Goals.Count" for "Goals.Count.one") when the English file has its ".other" form.
 * @param {string} key The key.
 * @param {Object<string, string>} english The English strings.
 * @returns {string|null} The stem, or null when the key is not a plural form.
 */
function pluralStem (key, english) {
    const dot = key.lastIndexOf('.');
    if (dot <= 0 || !PLURAL_CATEGORIES.includes(key.slice(dot + 1))) {
        return null;
    }
    const stem = key.slice(0, dot);
    return Object.prototype.hasOwnProperty.call(english, `${stem}.other`) ? stem : null;
}

/**
 * The keys a language should have, each mapped to the English key it is translated from, in English file order.
 * Plural-aware: a plural stem gets the CLDR categories of the target language (Vietnamese only "other"), each
 * translated from the English form of that category, or from ".other" when English lacks it.
 * @param {Object<string, string>} english The English strings.
 * @param {string} language The target language code.
 * @returns {Map<string, string>} Target key to English key.
 */
function expectedKeys (english, language) {
    const categories = new Intl.PluralRules(language).resolvedOptions().pluralCategories;
    const expected = new Map();
    for (const key of Object.keys(english)) {
        const stem = pluralStem(key, english);
        if (stem === null) {
            expected.set(key, key);
            continue;
        }
        for (const category of categories) {
            const target = `${stem}.${category}`;
            if (!expected.has(target)) {
                const source = Object.prototype.hasOwnProperty.call(english, target) ? target : `${stem}.other`;
                expected.set(target, source);
            }
        }
    }
    return expected;
}

/**
 * The hash of an English text, recorded in the lock so a changed source marks its translations stale.
 * @param {string} text The English text.
 * @returns {string} The first 16 hex digits of its SHA-256.
 */
function sourceHash (text) {
    return crypto.createHash('sha256').update(text, 'utf8').digest('hex').slice(0, 16);
}

module.exports = {
    LOCK_FILE,
    RESOURCES_DIR,
    SOURCE_LANGUAGE,
    expectedKeys,
    placeholders,
    readJson,
    readStrings,
    samePlaceholders,
    sourceHash,
    targetLanguages,
    writeJson
};

'use strict';
/* eslint-env node */

/**
 * The Atelier content half of npm run i18n:translate (ADR 0019): drafts the missing and stale fields of each Term Entry
 * and Lesson's Vietnamese counterpart, terms/{slug}.vi.json and lessons/{slug}.vi.json, in the ApiService content
 * folder. i18n-translate.js calls it for each target language, with the same Translator requests.
 *
 * - Field by field, with the en.json lock approach: the content folder's i18n.lock.json holds, per file and field, a
 *   hash of the English text each translation came from. Only a missing, blank or stale field is sent; a field already
 *   in the counterpart with no lock entry is adopted, so a hand-written translation is not overwritten.
 * - Only the text fields the loader reads from a counterpart (LearningContentMerge.cs) are sent. Slugs, Topics,
 *   references, URLs, numbers, dates and sources stay in the English file and are never sent.
 * - Owner-written fields are never overwritten once present: the In Vietnam note here, and the Vietnamese name, which
 *   lives in the English file and is never written.
 * - Each Term's Vietnamese name is a glossary entry for Vietnamese, so every text that names the Term uses it.
 * - A list (paragraphs, Check questions, options) follows the English count, so a Check's options always line up with
 *   its correct option.
 */

const fs = require('fs');
const path = require('path');
const { LOCK_FILE, readJson, samePlaceholders, sourceHash, writeJson } = require('./i18n-common');
const { protect, unprotect } = require('./i18n-markup');

/** The Atelier content folder in the ApiService project. */
const CONTENT_DIR = path.join(__dirname, '..', '..', 'PersonalFinance.ApiService', 'Features', 'Learning', 'Content');

/** The language whose glossary gets each Term's Vietnamese name. */
const VIETNAMESE = 'vi';

const TEXT = 'text';

// The translated fields of each kind of file: TEXT is a string field, [x] a list of x, {...} a nested object.
const SCHEMAS = {
    terms: {
        term: TEXT,
        oneLine: TEXT,
        explanation: [TEXT],
        whyItMatters: TEXT,
        workedExample: TEXT,
        inTheUs: { text: TEXT },
        inVietnam: { text: TEXT },
        video: { title: TEXT }
    },
    lessons: {
        title: TEXT,
        intro: TEXT,
        check: [{ prompt: TEXT, options: [TEXT], explanation: TEXT }],
        tryIt: { label: TEXT },
        keepsake: { name: TEXT }
    }
};

/** Fields the owner writes or checks: drafted when missing, never replaced once present. */
const OWNER_FIELDS = new Set(['inVietnam.text']);

const ENGLISH_FILE = /^([^.]+)\.json$/;

const isObject = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);
const hasText = (value) => typeof value === 'string' && value.trim() !== '';

/**
 * The string fields of a document that its schema translates, in schema order.
 * @param {*} node The document, or a part of it.
 * @param {*} schema The schema of that part.
 * @param {Array<string|number>} [at] The path to the part.
 * @param {Array<Array<string|number>>} [out] Collects the paths.
 * @returns {Array<Array<string|number>>} One path per string field.
 */
function textPaths (node, schema, at = [], out = []) {
    if (schema === TEXT) {
        if (typeof node === 'string') {
            out.push(at);
        }
    } else if (Array.isArray(schema)) {
        if (Array.isArray(node)) {
            node.forEach((item, i) => textPaths(item, schema[0], [...at, i], out));
        }
    } else if (isObject(node)) {
        Object.keys(schema).forEach((key) => textPaths(node[key], schema[key], [...at, key], out));
    }
    return out;
}

/**
 * A copy of a counterpart without the translated fields its English file no longer has: each list cut to the English
 * count, each field or object English lacks removed. Fields outside the schema are kept.
 * @param {*} target The counterpart, or a part of it.
 * @param {*} english The English part at the same place.
 * @param {*} schema The schema of that part.
 * @returns {*} The pruned copy, or undefined when nothing is left of it.
 */
function prune (target, english, schema) {
    if (schema === TEXT) {
        return typeof english === 'string' ? target : undefined;
    }
    if (Array.isArray(schema)) {
        return Array.isArray(english) && Array.isArray(target)
            ? target.slice(0, english.length).map((item, i) => prune(item, english[i], schema[0]))
            : undefined;
    }
    if (!isObject(english) || !isObject(target)) {
        return undefined;
    }
    const result = { ...target };
    for (const key of Object.keys(schema)) {
        if (Object.prototype.hasOwnProperty.call(result, key)) {
            const value = prune(result[key], english[key], schema[key]);
            if (value === undefined) {
                delete result[key];
            } else {
                result[key] = value;
            }
        }
    }
    return result;
}

const valueAt = (node, at) => at.reduce((part, key) => (part === null || part === undefined ? undefined : part[key]), node);

/**
 * Sets a field, creating the objects and lists on its path. A gap in a list is filled with a blank, which the loader
 * treats as missing, so it falls back to English.
 * @param {object} root The document.
 * @param {Array<string|number>} at The path.
 * @param {string} value The text.
 */
function setAt (root, at, value) {
    let node = root;
    at.forEach((key, i) => {
        const last = i === at.length - 1;
        if (Array.isArray(node)) {
            while (node.length < key) {
                node.push(last ? '' : {});
            }
        }
        if (last) {
            node[key] = value;
        } else {
            if (node[key] === null || typeof node[key] !== 'object') {
                node[key] = typeof at[i + 1] === 'number' ? [] : {};
            }
            node = node[key];
        }
    });
}

/**
 * The English content files: terms/{slug}.json and lessons/{slug}.json, in path order.
 * @param {string} dir The content folder.
 * @returns {Array<object>} { id: "terms/apr", folder, slug, file, english }.
 */
function englishFiles (dir) {
    const files = [];
    for (const folder of Object.keys(SCHEMAS)) {
        const folderPath = path.join(dir, folder);
        if (!fs.existsSync(folderPath)) {
            continue;
        }
        for (const name of fs.readdirSync(folderPath).sort()) {
            const match = ENGLISH_FILE.exec(name);
            if (match) {
                const file = path.join(folderPath, name);
                const english = readJson(file);
                if (!isObject(english)) {
                    throw new Error(`${folder}/${name} must hold one JSON object.`);
                }
                files.push({ id: `${folder}/${match[1]}`, folder, slug: match[1], file, english });
            }
        }
    }
    return files;
}

/**
 * The glossary entries from the Term Entries: each English Term to its Vietnamese name.
 * @param {string} dir The content folder.
 * @returns {Object<string, string>} Term to Vietnamese name.
 */
function contentGlossary (dir) {
    return glossaryOf(englishFiles(dir));
}

/**
 * The glossary entries from English content files already read.
 * @param {Array<object>} files The englishFiles result.
 * @returns {Object<string, string>} Term to Vietnamese name.
 */
function glossaryOf (files) {
    const glossary = {};
    for (const { folder, english } of files) {
        if (folder === 'terms' && hasText(english.term) && hasText(english.vietnameseName)) {
            glossary[english.term.trim()] = english.vietnameseName.trim();
        }
    }
    return glossary;
}

/**
 * Drafts one language's counterparts of every content file, writing each changed counterpart and the lock.
 * @param {object} input { dir, language, glossary }: the content folder, the language and its glossary (the Term
 *   glossary is added for Vietnamese).
 * @param {object} options { translate, dryRun }: translate(texts) resolves to the Translator's HTML for each text.
 * @returns {Promise<object>} { translated, adopted, pending, mismatched, written }, each field as "terms/apr#oneLine".
 */
async function translateContent (input, options) {
    const { dir, language } = input;
    const result = { translated: [], adopted: [], pending: [], mismatched: [], written: [] };
    if (!dir || !fs.existsSync(dir)) {
        return result;
    }
    const files = englishFiles(dir);
    const glossary = language === VIETNAMESE ? { ...input.glossary, ...glossaryOf(files) } : input.glossary;
    const lockFile = path.join(dir, LOCK_FILE);
    const oldLocks = fs.existsSync(lockFile) ? readJson(lockFile) : {};
    const oldLock = oldLocks[language] || {};

    const jobs = [];
    for (const file of files) {
        const schema = SCHEMAS[file.folder];
        file.targetFile = path.join(dir, file.folder, `${file.slug}.${language}.json`);
        file.target = fs.existsSync(file.targetFile) ? readJson(file.targetFile) : {};
        file.output = prune({ slug: file.english.slug || file.slug, ...file.target }, file.english, schema);
        file.oldLock = oldLock[file.id] || {};
        file.lock = {};
        for (const at of textPaths(file.english, schema)) {
            const key = at.join('.');
            const id = `${file.id}#${key}`;
            const source = valueAt(file.english, at);
            const hash = sourceHash(source);
            const locked = file.oldLock[key];
            const has = hasText(valueAt(file.target, at));
            if (has && (locked === undefined || OWNER_FIELDS.has(key.replace(/\.\d+/g, '')))) {
                if (locked === undefined) {
                    result.adopted.push(id);
                }
                file.lock[key] = locked === undefined ? hash : locked;
            } else if (has && locked === hash) {
                file.lock[key] = hash;
            } else {
                result.pending.push(id);
                jobs.push({ file, at, key, id, source, hash, kept: has ? locked : undefined });
            }
        }
    }

    if (options.dryRun) {
        return result;
    }

    const outputs = jobs.length > 0 ? await options.translate(jobs.map((job) => protect(job.source, glossary))) : [];
    jobs.forEach((job, i) => {
        const translation = unprotect(outputs[i]);
        if (samePlaceholders(job.source, translation)) {
            setAt(job.file.output, job.at, translation);
            job.file.lock[job.key] = job.hash;
            result.translated.push(job.id);
        } else {
            // The previous translation, if any, stays with its old lock, so the field stays stale.
            result.mismatched.push({ key: job.id, source: job.source, translation });
            if (job.kept !== undefined) {
                job.file.lock[job.key] = job.kept;
            } else if (typeof job.at[job.at.length - 1] === 'number' && valueAt(job.file.output, job.at) === undefined) {
                // A blank keeps a list as long as the English, so the paragraph falls back instead of vanishing.
                setAt(job.file.output, job.at, '');
            }
        }
    });

    const lock = {};
    for (const file of files) {
        const changed = JSON.stringify(file.output) !== JSON.stringify(file.target);
        // A new counterpart is written only with something in it; an existing one is rewritten even when pruned empty.
        const existed = fs.existsSync(file.targetFile);
        if (changed && (existed || textPaths(file.output, SCHEMAS[file.folder]).length > 0)) {
            writeJson(file.targetFile, file.output);
            result.written.push(path.relative(dir, file.targetFile).split(path.sep).join('/'));
        }
        if (Object.keys(file.lock).length > 0) {
            lock[file.id] = file.lock;
        }
    }
    const locks = { ...oldLocks, [language]: lock };
    if (Object.keys(lock).length === 0) {
        delete locks[language];
    }
    const ordered = {};
    Object.keys(locks).sort().forEach((key) => {
        ordered[key] = locks[key];
    });
    if (JSON.stringify(ordered) !== JSON.stringify(oldLocks) && (fs.existsSync(lockFile) || Object.keys(ordered).length > 0)) {
        writeJson(lockFile, ordered);
    }
    return result;
}

module.exports = { CONTENT_DIR, contentGlossary, translateContent };

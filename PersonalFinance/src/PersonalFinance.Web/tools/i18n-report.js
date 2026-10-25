'use strict';
/* eslint-env node */

/**
 * npm run i18n:report — the CI i18n-report step (ADR 0015, spec §2.4). Writes a Markdown report to the job summary
 * ($GITHUB_STEP_SUMMARY, else stdout):
 * - reported, never failing: missing translations per language (plural-aware), stale translations (English changed
 *   since, per i18n.lock.json), keys referenced in Razor, C# or JS but absent from en.json, and English keys nothing
 *   references;
 * - failing (exit 1): a resource file that is not valid JSON, and a translation whose placeholders differ from the
 *   English.
 */

const fs = require('fs');
const path = require('path');
const {
    LOCK_FILE,
    RESOURCES_DIR,
    SOURCE_LANGUAGE,
    expectedKeys,
    readJson,
    readStrings,
    samePlaceholders,
    sourceHash,
    targetLanguages
} = require('./i18n-common');

const WEB_ROOT = path.join(__dirname, '..');
const KEY = '[A-Z][A-Za-z0-9]*(?:\\.[A-Za-z0-9]+)+';
const SKIPPED_DIRS = new Set(['bin', 'obj', 'node_modules', 'lib', 'dist', 'tools', 'Resources']);
const SOURCE_FILE = /\.(cshtml|cs|js)$/;

// A key used directly: the localizer's indexer and helpers, DataAnnotations attributes, PFI18n in JS.
const REFERENCE_PATTERNS = [
    new RegExp(`\\b(?:L|_localizer|localizer)\\[\\s*"(${KEY})"`, 'g'),
    new RegExp(`\\.(?:Named|NamedHtml)\\(\\s*"(${KEY})"`, 'g'),
    new RegExp(`ErrorMessage\\s*=\\s*"(${KEY})"`, 'g'),
    new RegExp(`Display\\(\\s*Name\\s*=\\s*"(${KEY})"`, 'g'),
    new RegExp(`PFI18n\\.t\\(\\s*['"\`](${KEY})['"\`]`, 'g')
];

// A plural stem: the key itself is never in en.json, its ".other" form is.
const PLURAL_PATTERNS = [
    new RegExp(`\\.Plural\\(\\s*"(${KEY})"`, 'g'),
    new RegExp(`PFI18n\\.plural\\(\\s*['"\`](${KEY})['"\`]`, 'g')
];

// A prefix literal ("Identity.Error.", prefix="SavingsGoals.Js."): every key under it counts as used.
const PREFIX_PATTERN = new RegExp(`["'\`](${KEY}\\.|[A-Z][A-Za-z0-9]*\\.)["'\`]`, 'g');

/**
 * The Razor, C# and JS files of the Web project, skipping build output, vendored libraries and this tooling.
 * @param {string} root The folder to walk.
 * @returns {string[]} Absolute paths.
 */
function sourceFiles (root) {
    const files = [];
    for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
        const full = path.join(root, entry.name);
        if (entry.isDirectory()) {
            if (!SKIPPED_DIRS.has(entry.name) && !entry.name.startsWith('.')) {
                files.push(...sourceFiles(full));
            }
        } else if (SOURCE_FILE.test(entry.name)) {
            files.push(full);
        }
    }
    return files;
}

/**
 * Collects every key, plural stem and key prefix the given sources mention.
 * @param {string[]} contents File contents.
 * @returns {{ keys: Set<string>, plurals: Set<string>, prefixes: Set<string> }} The references.
 */
function collectReferences (contents) {
    const keys = new Set();
    const plurals = new Set();
    const prefixes = new Set();
    for (const text of contents) {
        REFERENCE_PATTERNS.forEach((pattern) => {
            for (const match of text.matchAll(pattern)) {
                keys.add(match[1]);
            }
        });
        PLURAL_PATTERNS.forEach((pattern) => {
            for (const match of text.matchAll(pattern)) {
                plurals.add(match[1]);
            }
        });
        for (const match of text.matchAll(PREFIX_PATTERN)) {
            prefixes.add(match[1]);
        }
    }
    return { keys, plurals, prefixes };
}

/**
 * Builds the report data.
 * @param {object} [options] { dir: resource folder, sources: file contents to scan (default: the Web project) }.
 * @returns {object} { errors, languages: { code: { missing, stale } }, referencedMissing, unused }.
 */
function buildReport (options = {}) {
    const dir = options.dir || RESOURCES_DIR;
    const errors = [];
    const report = { errors, languages: {}, referencedMissing: [], unused: [] };

    let english;
    try {
        english = readStrings(path.join(dir, `${SOURCE_LANGUAGE}.json`));
    } catch (error) {
        errors.push(error.message);
        return report;
    }
    let lock = {};
    const lockFile = path.join(dir, LOCK_FILE);
    if (fs.existsSync(lockFile)) {
        try {
            lock = readJson(lockFile);
        } catch (error) {
            errors.push(error.message);
        }
    }
    for (const name of fs.readdirSync(dir).filter((n) => /^glossary\..+\.json$/.test(n))) {
        try {
            readStrings(path.join(dir, name));
        } catch (error) {
            errors.push(error.message);
        }
    }

    for (const language of targetLanguages(dir)) {
        let target;
        try {
            target = readStrings(path.join(dir, `${language}.json`));
        } catch (error) {
            errors.push(error.message);
            continue;
        }
        report.languages[language] = compareLanguage(english, target, lock[language] || {}, language, errors);
    }

    const contents = options.sources || sourceFiles(WEB_ROOT).map((file) => fs.readFileSync(file, 'utf8'));
    const references = collectReferences(contents);
    report.referencedMissing = [...references.keys].filter((key) => !(key in english))
        .concat([...references.plurals].filter((stem) => !(`${stem}.other` in english)).map((stem) => `${stem}.other`))
        .sort();
    const prefixes = [...references.prefixes];
    report.unused = Object.keys(english).filter((key) => {
        const dot = key.lastIndexOf('.');
        return !references.keys.has(key)
            && !references.plurals.has(key.slice(0, dot))
            && !prefixes.some((prefix) => key.startsWith(prefix));
    });
    return report;
}

/**
 * Missing and stale keys of one language; placeholder mismatches go to errors.
 * @param {Object<string, string>} english The English strings.
 * @param {Object<string, string>} target The language's strings.
 * @param {Object<string, string>} lock The language's lock entries.
 * @param {string} language The language code.
 * @param {string[]} errors Collected errors.
 * @returns {{ missing: string[], stale: string[] }} The gaps.
 */
function compareLanguage (english, target, lock, language, errors) {
    const missing = [];
    const stale = [];
    for (const [key, sourceKey] of expectedKeys(english, language)) {
        if (!Object.prototype.hasOwnProperty.call(target, key)) {
            missing.push(key);
            continue;
        }
        if (!samePlaceholders(english[sourceKey], target[key])) {
            errors.push(`${language}.json "${key}": placeholders differ from en.json ("${english[sourceKey]}").`);
        }
        if (lock[key] !== sourceHash(english[sourceKey])) {
            stale.push(key);
        }
    }
    return { missing, stale };
}

/**
 * Renders the report as Markdown for the job summary.
 * @param {object} report The buildReport result.
 * @returns {string} Markdown.
 */
function formatMarkdown (report) {
    const lines = ['## i18n report', ''];
    if (report.errors.length > 0) {
        lines.push('### Errors (failing)', '', ...report.errors.map((e) => `- ${e}`), '');
    }
    lines.push('| Language | Missing | Stale |', '|---|---|---|');
    for (const [language, result] of Object.entries(report.languages)) {
        lines.push(`| ${language} | ${result.missing.length} | ${result.stale.length} |`);
    }
    lines.push('');
    for (const [language, result] of Object.entries(report.languages)) {
        lines.push(...details(`Missing in ${language}.json`, result.missing));
        lines.push(...details(`Stale in ${language}.json (English changed since it was translated)`, result.stale));
    }
    lines.push(...details('Referenced in code but not in en.json', report.referencedMissing));
    lines.push(...details('In en.json but not referenced', report.unused));
    lines.push('Missing and stale keys fall back to English at runtime. Run `npm run i18n:translate` to fill them.', '');
    return lines.join('\n');
}

/**
 * A collapsible list, or nothing when the list is empty.
 * @param {string} title The summary line.
 * @param {string[]} items The entries.
 * @returns {string[]} Markdown lines.
 */
function details (title, items) {
    if (items.length === 0) {
        return [];
    }
    return [`<details><summary>${title}: ${items.length}</summary>`, '', ...items.map((i) => `- \`${i}\``), '', '</details>', ''];
}

if (require.main === module) {
    const report = buildReport();
    const markdown = formatMarkdown(report);
    if (process.env.GITHUB_STEP_SUMMARY) {
        fs.appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${markdown}\n`);
    }
    console.log(markdown); // eslint-disable-line no-console
    process.exitCode = report.errors.length > 0 ? 1 : 0;
}

module.exports = { buildReport, collectReferences, formatMarkdown };

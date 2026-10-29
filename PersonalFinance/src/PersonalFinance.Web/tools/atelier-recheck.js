'use strict';
/* eslint-env node */

/**
 * The monthly Atelier country-note re-check (#510, #526). Lists the In the US / In Vietnam notes of the Term Entries
 * in the content folder (ADR 0019) that are due for a re-check:
 * - every country note on an entry last reviewed more than 12 months ago;
 * - every Tax note whose Rules-as-of year is before the current year.
 * Prints the body of the "Atelier notes due for a re-check" issue to stdout and, on GitHub Actions, writes `due=N` to
 * $GITHUB_OUTPUT. It reads files only and never exits non-zero for content it cannot read.
 *
 * Usage: node atelier-recheck.js [contentFolder] [--today YYYY-MM-DD]
 */

const fs = require('fs');
const path = require('path');

const DEFAULT_CONTENT_DIR = path.join(
    __dirname, '..', '..', 'PersonalFinance.ApiService', 'Features', 'Learning', 'Content');
const TAX_TOPIC = 'tax';
const COUNTRY_NOTES = [
    { field: 'inTheUs', country: 'In the US' },
    { field: 'inVietnam', country: 'In Vietnam' }
];
// English files only: `apr.json`, not its `apr.vi.json` counterpart, which carries no review date of its own.
const ENGLISH_FILE = /^[^.]+\.json$/;

/**
 * Formats a date as YYYY-MM-DD in UTC.
 * @param {Date} date The date.
 * @returns {string} The ISO date.
 */
function isoDate (date) {
    return date.toISOString().slice(0, 10);
}

/**
 * Reads the English Term Entry files of a content folder, skipping (and reporting to stderr) any it cannot parse.
 * @param {string} contentDir The content folder.
 * @returns {{ file: string, entry: object }[]} The entries, in file name order.
 */
function readTermEntries (contentDir) {
    const termsDir = path.join(contentDir, 'terms');
    if (!fs.existsSync(termsDir)) {
        return [];
    }
    return fs.readdirSync(termsDir)
        .filter(name => ENGLISH_FILE.test(name))
        .sort()
        .flatMap(name => {
            try {
                const entry = JSON.parse(fs.readFileSync(path.join(termsDir, name), 'utf8'));
                return entry && typeof entry === 'object' ? [{ file: name, entry }] : [];
            } catch (error) {
                process.stderr.write(`Skipped terms/${name}: ${error.message}\n`);
                return [];
            }
        });
}

/**
 * Lists the country notes due for a re-check.
 * @param {string} contentDir The content folder.
 * @param {Date} today Today, in UTC.
 * @returns {{ slug: string, term: string, country: string, rulesAsOf: number|undefined, sourceUrl: string|undefined,
 *   reasons: string[] }[]} One item per due note, by slug, the US note before the Vietnam one.
 */
function findDueNotes (contentDir, today) {
    const currentYear = today.getUTCFullYear();
    // More than 12 months ago: before the same day a year back.
    const reviewCutoff = `${currentYear - 1}${isoDate(today).slice(4)}`;
    const due = [];
    for (const { file, entry } of readTermEntries(contentDir)) {
        const slug = entry.slug || file.replace(/\.json$/, '');
        const lastReviewed = typeof entry.lastReviewed === 'string' ? entry.lastReviewed : undefined;
        for (const { field, country } of COUNTRY_NOTES) {
            const note = entry[field];
            if (!note || typeof note !== 'object') {
                continue;
            }
            const reasons = [];
            if (lastReviewed && lastReviewed < reviewCutoff) {
                reasons.push(`last reviewed ${lastReviewed}`);
            }
            if (entry.topic === TAX_TOPIC && Number.isInteger(note.rulesAsOf) && note.rulesAsOf < currentYear) {
                reasons.push(`Tax rules as of ${note.rulesAsOf}`);
            }
            if (reasons.length === 0) {
                continue;
            }
            const source = (Array.isArray(note.sources) && note.sources.find(s => s && s.url))
                || (Array.isArray(entry.sources) && entry.sources.find(s => s && s.url));
            due.push({
                slug,
                term: entry.term || slug,
                country,
                rulesAsOf: note.rulesAsOf,
                sourceUrl: source ? source.url : undefined,
                reasons
            });
        }
    }
    return due;
}

/**
 * Renders the issue body: one line per due note (entry, country, year, source link), or a line saying none is due.
 * @param {ReturnType<typeof findDueNotes>} due The due notes.
 * @param {Date} today Today, in UTC.
 * @returns {string} The Markdown body.
 */
function renderIssueBody (due, today) {
    const lines = [
        `Checked on ${isoDate(today)} by the monthly Atelier re-check workflow (#510).`,
        'It lists country notes on entries last reviewed more than 12 months ago, and Tax notes whose Rules-as-of '
            + 'year is before the current year.',
        ''
    ];
    if (due.length === 0) {
        lines.push('No notes are due for a re-check.');
    } else {
        lines.push(`${due.length} note${due.length === 1 ? ' is' : 's are'} due for a re-check:`, '');
        for (const note of due) {
            const year = note.rulesAsOf === undefined ? 'no Rules-as-of year' : `Rules as of ${note.rulesAsOf}`;
            const source = note.sourceUrl ? `[source](${note.sourceUrl})` : 'no source';
            lines.push(`- ${note.term} (\`${note.slug}\`), ${note.country}, ${year}, ${source} (${note.reasons.join('; ')})`);
        }
        lines.push('', 'Check each note against its source, update the entry and its Last reviewed date in a content PR.');
    }
    return `${lines.join('\n')}\n`;
}

/**
 * Parses the command line.
 * @param {string[]} args The arguments after the script name.
 * @returns {{ contentDir: string, today: Date }} The content folder and today.
 */
function parseArgs (args) {
    let contentDir = DEFAULT_CONTENT_DIR;
    let today = new Date();
    for (let i = 0; i < args.length; i++) {
        if (args[i] === '--today') {
            today = new Date(`${args[++i]}T00:00:00Z`);
            if (Number.isNaN(today.getTime())) {
                throw new Error('--today needs a date as YYYY-MM-DD.');
            }
        } else {
            contentDir = path.resolve(args[i]);
        }
    }
    return { contentDir, today };
}

if (require.main === module) {
    const { contentDir, today } = parseArgs(process.argv.slice(2));
    const due = findDueNotes(contentDir, today);
    process.stdout.write(renderIssueBody(due, today));
    if (process.env.GITHUB_OUTPUT) {
        fs.appendFileSync(process.env.GITHUB_OUTPUT, `due=${due.length}\n`);
    }
}

module.exports = { findDueNotes, parseArgs, renderIssueBody };

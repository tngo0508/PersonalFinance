'use strict';
/* eslint-env node */

/**
 * The gate in front of /atelier-draft (#524, ADR 0019). Reads an owner-prepared excerpt file, checks every row against
 * the Approved Sources allowlist (`approved-sources.json`) and prints, as JSON, only what the drafter may use:
 * - the text of `## excerpt` rows, each from a draft + cite source;
 * - every `## link` row, as a name and URL only.
 * It refuses the whole file, exiting 1 with one line per problem on stderr, when a row names a source that is not on
 * the allowlist, a URL outside that source's prefixes, an excerpt from a source that is not draft + cite, or an IRS
 * excerpt without its tax year. The drafter reads this output, never the raw file.
 *
 * The excerpt file is Markdown:
 *
 *     ---
 *     term: Diversification
 *     slug: diversification
 *     topic: investing-basics
 *     lesson: own-a-piece
 *     ---
 *
 *     ## excerpt
 *     source: Investor.gov (SEC)
 *     name: Investor.gov, Diversify your investments
 *     url: https://www.investor.gov/...
 *     accessed: 2026-10-30
 *
 *     The pasted text, one or more paragraphs.
 *
 *     ## link
 *     source: FINRA
 *     name: FINRA, Asset allocation and diversification
 *     url: https://www.finra.org/investors/...
 *
 * Usage: node atelier-excerpts.js <excerptFile> [--content-dir <folder>]
 */

const fs = require('fs');
const path = require('path');

const DEFAULT_CONTENT_DIR = path.join(
    __dirname, '..', '..', 'PersonalFinance.ApiService', 'Features', 'Learning', 'Content');
const DRAFT_CITE = 'draft-cite';
const TOPICS = ['saving-banking', 'borrowing-credit', 'investing-basics', 'trading', 'tax', 'retirement'];
const SLUG = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const BLOCK_HEADING = /^##\s+(\S.*?)\s*$/;
const FIELD = /^([a-z][a-z-]*):\s*(.*?)\s*$/;

/**
 * Splits `key: value` lines into an object, stopping at the first line that is not one.
 * @param {string[]} lines The lines.
 * @returns {{ fields: object, rest: string[] }} The fields and the lines after them.
 */
function readFields (lines) {
    const fields = {};
    let i = 0;
    for (; i < lines.length; i++) {
        const match = FIELD.exec(lines[i]);
        if (!match) {
            break;
        }
        fields[match[1]] = match[2];
    }
    return { fields, rest: lines.slice(i) };
}

/**
 * Parses an excerpt file into its header and its blocks, without judging them.
 * @param {string} text The file's text.
 * @returns {{ header: object, blocks: { kind: string, line: number, fields: object, text: string }[], problems: string[] }} The parts.
 */
function parseExcerptFile (text) {
    const lines = text.replace(/^\uFEFF/, '').split(/\r?\n/);
    const problems = [];
    let header = {};
    let start = 0;
    if (lines[0] === '---') {
        const end = lines.indexOf('---', 1);
        if (end < 0) {
            problems.push('header: the front matter has no closing ---');
        } else {
            header = readFields(lines.slice(1, end)).fields;
            start = end + 1;
        }
    } else {
        problems.push('header: the file must start with front matter between --- lines');
    }

    const blocks = [];
    let current = null;
    for (let i = start; i < lines.length; i++) {
        const heading = BLOCK_HEADING.exec(lines[i]);
        if (heading) {
            current = { kind: heading[1].toLowerCase(), line: i + 1, lines: [] };
            blocks.push(current);
        } else if (current) {
            current.lines.push(lines[i]);
        } else if (lines[i].trim() !== '') {
            problems.push(`line ${i + 1}: text before the first ## excerpt or ## link block`);
        }
    }

    return {
        header,
        problems,
        blocks: blocks.map(block => {
            const body = block.lines.slice();
            while (body.length && body[0].trim() === '') {
                body.shift();
            }
            const { fields, rest } = readFields(body);
            return { kind: block.kind, line: block.line, fields, text: rest.join('\n').trim() };
        })
    };
}

/**
 * Checks a parsed excerpt file against the allowlist and returns what the drafter may read.
 * @param {{ header: object, blocks: object[], problems: string[] }} parsed The parsed file.
 * @param {{ name: string, country?: string, usage: string, urlPrefixes: string[], requiresTaxYear?: boolean, note?: string }[]} sources The allowlist rows.
 * @returns {{ problems: string[], draft: object|null }} The problems, or the drafter's input when there are none.
 */
function checkExcerpts (parsed, sources) {
    const problems = parsed.problems.slice();
    const { header } = parsed;
    if (!header.term) {
        problems.push('header: term is missing');
    }
    for (const key of ['slug', 'lesson']) {
        if (!SLUG.test(header[key] || '')) {
            problems.push(`header: ${key} must be a kebab-case slug`);
        }
    }
    if (!TOPICS.includes(header.topic)) {
        problems.push(`header: topic must be one of ${TOPICS.join(', ')}`);
    }

    const excerpts = [];
    const links = [];
    for (const block of parsed.blocks) {
        const where = `line ${block.line} (## ${block.kind})`;
        const { fields } = block;
        if (block.kind !== 'excerpt' && block.kind !== 'link') {
            problems.push(`${where}: a block is ## excerpt or ## link`);
            continue;
        }
        const source = sources.find(s => s.name === fields.source);
        if (!source) {
            problems.push(`${where}: "${fields.source || ''}" is not an Approved Source in approved-sources.json`);
            continue;
        }
        if (!fields.name) {
            problems.push(`${where}: name is missing`);
        }
        if (!(source.urlPrefixes || []).some(prefix => (fields.url || '').startsWith(prefix))) {
            problems.push(`${where}: the url is not under ${source.name}'s prefixes (${source.urlPrefixes.join(', ')})`);
        }

        if (block.kind === 'link') {
            if (block.text) {
                problems.push(`${where}: a link carries no pasted text; make it an ## excerpt if the source is draft + cite`);
            }
            links.push({ source: source.name, usage: source.usage, name: fields.name, url: fields.url });
            continue;
        }

        if (source.usage !== DRAFT_CITE) {
            problems.push(`${where}: ${source.name} is ${source.usage}, not draft + cite; add it as an ## link`);
        }
        if (!ISO_DATE.test(fields.accessed || '')) {
            problems.push(`${where}: accessed must be a YYYY-MM-DD date`);
        }
        if (source.requiresTaxYear && !/^\d{4}$/.test(fields['tax-year'] || '')) {
            problems.push(`${where}: ${source.name} needs its tax-year`);
        }
        if (!block.text) {
            problems.push(`${where}: the excerpt has no pasted text`);
        }
        const excerpt = {
            source: source.name,
            country: source.country,
            name: fields.name,
            url: fields.url,
            accessed: fields.accessed,
            text: block.text,
            citeRule: source.note
        };
        if (fields['tax-year']) {
            excerpt.taxYear = Number(fields['tax-year']);
        }
        excerpts.push(excerpt);
    }

    if (!parsed.blocks.some(block => block.kind === 'excerpt')) {
        problems.push('the file has no ## excerpt block');
    }

    return problems.length
        ? { problems, draft: null }
        : {
            problems,
            draft: { term: header.term, slug: header.slug, topic: header.topic, lesson: header.lesson, excerpts, links }
        };
}

/**
 * Reads the allowlist rows of a content folder.
 * @param {string} contentDir The content folder.
 * @returns {object[]} The rows.
 */
function readSources (contentDir) {
    return JSON.parse(fs.readFileSync(path.join(contentDir, 'approved-sources.json'), 'utf8')).sources;
}

/**
 * Parses the command line.
 * @param {string[]} argv The arguments after the script name.
 * @returns {{ file: string|undefined, contentDir: string }} The options.
 */
function parseArgs (argv) {
    const options = { file: undefined, contentDir: DEFAULT_CONTENT_DIR };
    for (let i = 0; i < argv.length; i++) {
        if (argv[i] === '--content-dir') {
            options.contentDir = argv[++i];
        } else {
            options.file = argv[i];
        }
    }
    return options;
}

if (require.main === module) {
    const options = parseArgs(process.argv.slice(2));
    if (!options.file) {
        process.stderr.write('Usage: node atelier-excerpts.js <excerptFile> [--content-dir <folder>]\n');
        process.exit(2);
    }
    const parsed = parseExcerptFile(fs.readFileSync(options.file, 'utf8'));
    const { problems, draft } = checkExcerpts(parsed, readSources(options.contentDir));
    if (problems.length) {
        process.stderr.write(`Refused ${options.file}:\n${ problems.map(p => `  - ${p}\n`).join('')}`);
        process.exit(1);
    }
    process.stdout.write(`${JSON.stringify(draft, null, 2) }\n`);
}

module.exports = { checkExcerpts, parseArgs, parseExcerptFile, readSources };

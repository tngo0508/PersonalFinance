'use strict';
/* eslint-env node */

/**
 * npm run i18n:translate — machine-drafts the missing and stale translations with Azure AI Translator
 * (ADR 0015, spec §2.4). Run by the author inside the feature PR and committed as its own commit,
 * "i18n: translate N keys (vi)". CI never runs it.
 *
 * - Sends only keys that are missing from <lang>.json or whose English text changed since they were translated
 *   (i18n.lock.json holds a hash of that English text). A translation already in the file with no lock entry is kept
 *   and adopted into the lock, so a hand-written translation is not overwritten.
 * - Protects placeholders with <span class="notranslate"> (textType=html) and applies glossary.<lang>.json as
 *   dynamic-dictionary markup; afterwards every English placeholder must be in the output, or nothing is written for
 *   that key and it is listed.
 * - Batches of at most 1,000 texts / 50,000 characters; backs off on 429 and 5xx.
 *
 * Credentials: AZURE_TRANSLATOR_KEY, AZURE_TRANSLATOR_REGION and optionally AZURE_TRANSLATOR_ENDPOINT, from the
 * environment or the repository's .env. The key is sent only to the Translator and never printed.
 *
 * Usage: npm run i18n:translate [-- --dry-run] [-- --lang=vi]
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
    targetLanguages,
    writeJson
} = require('./i18n-common');

const DEFAULT_ENDPOINT = 'https://api.cognitive.microsofttranslator.com/';
const MAX_TEXTS_PER_REQUEST = 1000;
const MAX_CHARS_PER_REQUEST = 50000;
const MAX_ATTEMPTS = 6;
const PLACEHOLDER_TOKEN = /(\{[A-Za-z0-9]+\})/;

/**
 * Escapes text for the html text type.
 * @param {string} text Plain text.
 * @returns {string} HTML-escaped text.
 */
function escapeHtml (text) {
    return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

/**
 * Reverses escapeHtml and the entities the Translator may emit.
 * @param {string} html Translated HTML text.
 * @returns {string} Plain text.
 */
function unescapeHtml (html) {
    return html
        .replace(/&lt;/g, '<')
        .replace(/&gt;/g, '>')
        .replace(/&quot;/g, '"')
        .replace(/&#39;|&#x27;|&apos;/g, '\'')
        .replace(/&amp;/g, '&');
}

/**
 * Wraps glossary terms in dynamic-dictionary markup, longest term first, whole words, ignoring case. The Translator
 * inserts the fixed translation verbatim, so it is capitalised when the English term is ("Savings Goals").
 * @param {string} escaped HTML-escaped literal text.
 * @param {Object<string, string>} glossary English term to its fixed translation.
 * @returns {string} The text with markup.
 */
function applyGlossary (escaped, glossary) {
    const terms = Object.keys(glossary).sort((a, b) => b.length - a.length);
    if (terms.length === 0) {
        return escaped;
    }
    const lookup = new Map(terms.map((term) => [term.toLowerCase(), glossary[term]]));
    const pattern = new RegExp(`\\b(${terms.map((t) => t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')})\\b`, 'gi');
    return escaped.replace(pattern, (match) => {
        const fixed = lookup.get(match.toLowerCase());
        const translation = /^\p{Lu}/u.test(match) ? fixed.charAt(0).toLocaleUpperCase() + fixed.slice(1) : fixed;
        return `<mstrans:dictionary translation="${escapeHtml(translation)}">${match}</mstrans:dictionary>`;
    });
}

/**
 * Prepares an English text for the Translator: literal text escaped and glossary-marked, each placeholder wrapped so
 * it is not translated.
 * @param {string} text The English text.
 * @param {Object<string, string>} glossary The language's glossary.
 * @returns {string} The HTML sent to the Translator.
 */
function protect (text, glossary) {
    return text.split(PLACEHOLDER_TOKEN).map((part, i) => {
        // split with a capture group puts the placeholders at the odd indexes
        return i % 2 === 1 ? `<span class="notranslate">${part}</span>` : applyGlossary(escapeHtml(part), glossary);
    }).join('');
}

/**
 * Turns the Translator's HTML back into the plain text stored in <lang>.json.
 * @param {string} html The translated HTML.
 * @returns {string} Plain text.
 */
function unprotect (html) {
    const text = html
        .replace(/<span\s+class\s*=\s*["']?notranslate["']?\s*>([\s\S]*?)<\/span>/gi, '$1')
        .replace(/<\/?mstrans:dictionary[^>]*>/gi, '');
    return unescapeHtml(text).trim();
}

/**
 * Splits texts into Translator requests within the per-request limits.
 * @param {string[]} texts The protected texts.
 * @param {number} [maxTexts] Texts per request.
 * @param {number} [maxChars] Characters per request.
 * @returns {number[][]} Index ranges into texts, one array per request.
 */
function batches (texts, maxTexts = MAX_TEXTS_PER_REQUEST, maxChars = MAX_CHARS_PER_REQUEST) {
    const result = [];
    let current = [];
    let chars = 0;
    texts.forEach((text, index) => {
        if (current.length > 0 && (current.length >= maxTexts || chars + text.length > maxChars)) {
            result.push(current);
            current = [];
            chars = 0;
        }
        current.push(index);
        chars += text.length;
    });
    if (current.length > 0) {
        result.push(current);
    }
    return result;
}

/**
 * The wait before retrying: Retry-After in seconds when the service sends it, else exponential from one second.
 * @param {Response} response The 429 or 5xx response.
 * @param {number} attempt Zero-based attempt number.
 * @returns {number} Milliseconds.
 */
function retryDelay (response, attempt) {
    const header = Number(response.headers && response.headers.get ? response.headers.get('retry-after') : NaN);
    return Number.isFinite(header) && header >= 0 ? header * 1000 : 1000 * 2 ** attempt;
}

/**
 * Translates one request's texts, retrying on 429 (the free tier's burst cap) and 5xx.
 * @param {string[]} texts Protected texts, at most one request's worth.
 * @param {object} options { endpoint, key, region, to, fetch, sleep, log }.
 * @param {number} [attempt] Zero-based attempt number.
 * @returns {Promise<string[]>} The translated HTML, in order.
 */
async function translateRequest (texts, options, attempt = 0) {
    const url = new URL('translate', options.endpoint);
    url.searchParams.set('api-version', '3.0');
    url.searchParams.set('from', SOURCE_LANGUAGE);
    url.searchParams.set('to', options.to);
    url.searchParams.set('textType', 'html');

    const response = await options.fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json; charset=UTF-8',
            'Ocp-Apim-Subscription-Key': options.key,
            'Ocp-Apim-Subscription-Region': options.region
        },
        body: JSON.stringify(texts.map((text) => ({ Text: text })))
    });
    if (response.status === 429 || response.status >= 500) {
        if (attempt + 1 >= MAX_ATTEMPTS) {
            throw new Error(`Translator still returned ${response.status} after ${MAX_ATTEMPTS} attempts.`);
        }
        const delay = retryDelay(response, attempt);
        options.log(`Translator returned ${response.status}; retrying in ${Math.round(delay / 1000)} s.`);
        await options.sleep(delay);
        return translateRequest(texts, options, attempt + 1);
    }
    if (!response.ok) {
        throw new Error(`Translator returned ${response.status}: ${await response.text()}`);
    }
    const body = await response.json();
    return body.map((item) => item.translations[0].text);
}

/**
 * Brings one language up to date in memory.
 * @param {object} input { english, target, lock, glossary, language }.
 * @param {object} options Translator options plus dryRun.
 * @returns {Promise<object>} { strings, lock, translated, adopted, removed, mismatched, pending }.
 */
async function translateLanguage (input, options) {
    const { english, target, glossary, language } = input;
    const oldLock = input.lock || {};
    const expected = expectedKeys(english, language);
    const pending = [];
    const adopted = [];
    const lock = {};
    for (const [key, sourceKey] of expected) {
        const hash = sourceHash(english[sourceKey]);
        const has = Object.prototype.hasOwnProperty.call(target, key);
        if (has && oldLock[key] === undefined) {
            adopted.push(key);
            lock[key] = hash;
        } else if (has && oldLock[key] === hash) {
            lock[key] = hash;
        } else {
            pending.push(key);
        }
    }
    const removed = Object.keys(target).filter((key) => !expected.has(key));

    const result = { translated: [], adopted, removed, mismatched: [], pending };
    const strings = {};
    if (!options.dryRun && pending.length > 0) {
        const texts = pending.map((key) => protect(english[expected.get(key)], glossary));
        for (const range of batches(texts)) {
            // One request at a time keeps under the free tier's hourly burst cap.
            // eslint-disable-next-line no-await-in-loop
            const outputs = await translateRequest(range.map((i) => texts[i]), { ...options, to: language });
            range.forEach((textIndex, i) => {
                const key = pending[textIndex];
                const source = english[expected.get(key)];
                const translation = unprotect(outputs[i]);
                if (samePlaceholders(source, translation)) {
                    strings[key] = translation;
                    lock[key] = sourceHash(source);
                    result.translated.push(key);
                } else {
                    result.mismatched.push({ key, source, translation });
                }
            });
        }
    }

    // English order; a key that failed verification keeps its previous translation, if any, and stays stale.
    result.strings = {};
    result.lock = {};
    for (const key of expected.keys()) {
        const value = Object.prototype.hasOwnProperty.call(strings, key) ? strings[key] : target[key];
        if (value !== undefined) {
            result.strings[key] = value;
        }
        if (lock[key] !== undefined) {
            result.lock[key] = lock[key];
        } else if (oldLock[key] !== undefined && value !== undefined) {
            result.lock[key] = oldLock[key];
        }
    }
    return result;
}

/**
 * Translates every target language in the resource folder and writes the files and the lock.
 * @param {object} options { dir, languages, env, fetch, sleep, log, dryRun }.
 * @returns {Promise<Object<string, object>>} Per-language results.
 */
async function run (options) {
    const dir = options.dir || RESOURCES_DIR;
    const env = options.env || process.env;
    const log = options.log || console.log; // eslint-disable-line no-console
    const english = readStrings(path.join(dir, `${SOURCE_LANGUAGE}.json`));
    const lockFile = path.join(dir, LOCK_FILE);
    const lock = fs.existsSync(lockFile) ? readJson(lockFile) : {};
    const languages = options.languages && options.languages.length ? options.languages : targetLanguages(dir);
    const translator = {
        endpoint: env.AZURE_TRANSLATOR_ENDPOINT || DEFAULT_ENDPOINT,
        key: env.AZURE_TRANSLATOR_KEY,
        region: env.AZURE_TRANSLATOR_REGION,
        fetch: options.fetch || fetch,
        sleep: options.sleep || ((ms) => new Promise((resolve) => setTimeout(resolve, ms))),
        log,
        dryRun: Boolean(options.dryRun)
    };

    const results = {};
    for (const language of languages) {
        const file = path.join(dir, `${language}.json`);
        const glossaryFile = path.join(dir, `glossary.${language}.json`);
        const input = {
            english,
            language,
            target: fs.existsSync(file) ? readStrings(file) : {},
            lock: lock[language],
            glossary: fs.existsSync(glossaryFile) ? readStrings(glossaryFile) : {}
        };
        const missingCredentials = !translator.dryRun && (!translator.key || !translator.region);
        // Languages run one after another, for the same burst cap.
        // eslint-disable-next-line no-await-in-loop
        const result = await translateLanguage(input, missingCredentials ? { ...translator, dryRun: true } : translator);
        if (missingCredentials && result.pending.length > 0) {
            throw new Error('AZURE_TRANSLATOR_KEY and AZURE_TRANSLATOR_REGION must be set (environment or .env).');
        }
        results[language] = result;
        if (!translator.dryRun) {
            writeJson(file, result.strings);
            lock[language] = result.lock;
        }
    }

    if (!translator.dryRun) {
        const ordered = {};
        for (const language of Object.keys(lock).sort()) {
            ordered[language] = lock[language];
        }
        writeJson(lockFile, ordered);
    }
    return results;
}

/**
 * Reads KEY=VALUE lines from a .env file into env, without overriding variables already set.
 * @param {string} file The .env path.
 * @param {Object<string, string>} env The environment to fill.
 */
function loadDotEnv (file, env) {
    if (!fs.existsSync(file)) {
        return;
    }
    for (const line of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
        const match = /^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$/.exec(line);
        if (match && env[match[1]] === undefined) {
            env[match[1]] = match[2].replace(/^(['"])(.*)\1$/, '$2');
        }
    }
}

/**
 * Prints one language's outcome.
 * @param {string} language The language code.
 * @param {object} result Its translateLanguage result.
 * @param {boolean} dryRun Whether nothing was sent.
 */
function report (language, result, dryRun) {
    /* eslint-disable no-console */
    if (dryRun) {
        console.log(`${language}: ${result.pending.length} key(s) would be sent, ${result.removed.length} removed.`);
        result.pending.forEach((key) => console.log(`  ${key}`));
        return;
    }
    console.log(`${language}: translated ${result.translated.length}, adopted ${result.adopted.length}, ` +
        `removed ${result.removed.length}, placeholder mismatches ${result.mismatched.length}.`);
    result.mismatched.forEach((m) => console.log(`  MISMATCH ${m.key}\n    en: ${m.source}\n    ${language}: ${m.translation}`));
    if (result.translated.length > 0) {
        console.log(`  Commit as: i18n: translate ${result.translated.length} keys (${language})`);
    }
    /* eslint-enable no-console */
}

/**
 * The command line: translate, print the outcome, exit 1 on a placeholder mismatch or an error.
 * @param {string[]} args Command-line arguments.
 * @param {Object<string, string>} env The environment, filled from .env.
 * @returns {Promise<void>} Resolves when done.
 */
async function main (args, env) {
    const dryRun = args.includes('--dry-run');
    const languages = args.filter((a) => a.startsWith('--lang=')).map((a) => a.slice('--lang='.length));
    loadDotEnv(path.resolve(__dirname, '..', '..', '..', '..', '.env'), env);
    let exitCode = 0;
    try {
        const results = await run({ dryRun, languages, env });
        Object.entries(results).forEach(([language, result]) => report(language, result, dryRun));
        exitCode = Object.values(results).some((result) => result.mismatched.length > 0) ? 1 : 0;
    } catch (error) {
        console.error(error.message); // eslint-disable-line no-console
        exitCode = 1;
    }
    process.exitCode = exitCode;
}

if (require.main === module) {
    main(process.argv.slice(2), process.env);
}

module.exports = { applyGlossary, batches, loadDotEnv, protect, retryDelay, run, translateLanguage, unprotect };

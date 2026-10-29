'use strict';
/* eslint-env node */

/**
 * The Translator markup shared by i18n-translate.js and i18n-content.js (ADR 0015): on the way in each placeholder is
 * wrapped as notranslate and each glossary term as dynamic-dictionary markup; on the way out both are stripped.
 */

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
 * Turns the Translator's HTML back into plain text.
 * @param {string} html The translated HTML.
 * @returns {string} Plain text.
 */
function unprotect (html) {
    const text = html
        .replace(/<span\s+class\s*=\s*["']?notranslate["']?\s*>([\s\S]*?)<\/span>/gi, '$1')
        .replace(/<\/?mstrans:dictionary[^>]*>/gi, '');
    return unescapeHtml(text).trim();
}

module.exports = { applyGlossary, protect, unprotect };

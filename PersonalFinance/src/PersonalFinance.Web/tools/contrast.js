'use strict';
/* eslint-env node */

/**
 * WCAG contrast for the theme tokens and the theme smoke (issue #482). Written here because axe-core is MPL-2.0,
 * which the license policy does not allow. theme-tokens.test.js checks the token pairs of the Ledger (ledger.css)
 * and app (site.css) token sheets; e2e-tests/theme-smoke.js checks the colours a page computes.
 */

const fs = require('fs');
const path = require('path');

const WEB_ROOT = path.resolve(__dirname, '..');
const TOKEN_SHEETS = ['wwwroot/css/ledger.css', 'wwwroot/css/site.css'];

/** WCAG AA: body text, and large text or UI parts. */
const AA_TEXT = 4.5;
const AA_UI = 3;

/**
 * Parses a CSS colour: hex (3, 4, 6 or 8 digits), rgb()/rgba() with commas or spaces, or color(srgb ...), which is
 * how browsers compute color-mix().
 * @param {string} text The colour.
 * @returns {{r: number, g: number, b: number, a: number}|null} Channels 0-255 and alpha 0-1, or null if unreadable.
 */
function parseColour (text) {
    const value = String(text).trim().toLowerCase();
    if (value === 'transparent') {
        return { r: 0, g: 0, b: 0, a: 0 };
    }
    const hex = /^#([0-9a-f]{3,8})$/.exec(value);
    if (hex && [3, 4, 6, 8].includes(hex[1].length)) {
        const digits = hex[1].length <= 4 ? hex[1].split('').map(d => d + d) : hex[1].match(/../g);
        const [r, g, b, a = 'ff'] = digits;
        return { r: parseInt(r, 16), g: parseInt(g, 16), b: parseInt(b, 16), a: parseInt(a, 16) / 255 };
    }
    const fn = /^(rgba?|color)\(([^)]*)\)$/.exec(value);
    if (!fn) {
        return null;
    }
    let body = fn[2].trim();
    let scale = 1;
    if (fn[1] === 'color') {
        if (!body.startsWith('srgb ')) {
            return null;
        }
        body = body.slice(5);
        scale = 255;
    }
    // rgba(1, 2, 3, 0.5) and rgb(1 2 3 / 50%) both end in the alpha
    const parts = body.split(/[\s,/]+/).filter(Boolean);
    if (parts.length !== 3 && parts.length !== 4) {
        return null;
    }
    const alpha = parts[3] === undefined ? '1' : parts[3];
    const a = alpha.endsWith('%') ? Number(alpha.slice(0, -1)) / 100 : Number(alpha);
    return { ...toChannels(parts, scale), a };
}

/**
 * Reads three channel strings.
 * @param {string[]} parts The red, green and blue channels.
 * @param {number} scale 255 for color(srgb) channels (0-1), 1 for rgb() channels (0-255).
 * @returns {{r: number, g: number, b: number}} Channels 0-255.
 */
function toChannels (parts, scale) {
    const [r, g, b] = parts.map(part => (part.endsWith('%') ? Number(part.slice(0, -1)) * 2.55 : Number(part) * scale));
    return { r, g, b };
}

/**
 * Lays a colour over another, as the browser paints it.
 * @param {{r: number, g: number, b: number, a: number}} top The upper colour.
 * @param {{r: number, g: number, b: number, a: number}} bottom The lower colour.
 * @returns {{r: number, g: number, b: number, a: number}} The colour seen.
 */
function composite (top, bottom) {
    const a = top.a + bottom.a * (1 - top.a);
    if (a === 0) {
        return { r: 0, g: 0, b: 0, a: 0 };
    }
    const mix = (t, b) => (t * top.a + b * bottom.a * (1 - top.a)) / a;
    return { r: mix(top.r, bottom.r), g: mix(top.g, bottom.g), b: mix(top.b, bottom.b), a };
}

/**
 * The background an element is painted on: its own and its ancestors' background colours, laid one over the
 * other from the canvas (white) up. Background images, gradients and opacity are not read.
 * @param {string[]} layers The computed background colours, outermost (html) first. An unreadable one is skipped.
 * @returns {{r: number, g: number, b: number, a: number}} The opaque colour seen.
 */
function flattenBackground (layers) {
    return layers.map(parseColour).filter(Boolean)
        .reduce((below, layer) => composite(layer, below), { r: 255, g: 255, b: 255, a: 1 });
}

/**
 * The WCAG relative luminance of an opaque colour.
 * @param {{r: number, g: number, b: number}} colour The colour.
 * @returns {number} 0 (black) to 1 (white).
 */
function relativeLuminance ({ r, g, b }) {
    const linear = (channel) => {
        const c = channel / 255;
        return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
    };
    return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b);
}

/**
 * The WCAG contrast ratio of a foreground over a background. A translucent foreground is laid over the background;
 * a translucent background is laid over white, as a page without one would show.
 * @param {string|object} foreground The text or UI colour.
 * @param {string|object} background The colour behind it.
 * @returns {number} 1 to 21.
 */
function contrastRatio (foreground, background) {
    const read = (colour) => {
        const parsed = typeof colour === 'string' ? parseColour(colour) : colour;
        if (!parsed) {
            throw new Error(`Not a colour: ${colour}`);
        }
        return parsed;
    };
    const bg = composite(read(background), { r: 255, g: 255, b: 255, a: 1 });
    const fg = composite(read(foreground), bg);
    const [light, dark] = [relativeLuminance(fg), relativeLuminance(bg)].sort((x, y) => y - x);
    return (light + 0.05) / (dark + 0.05);
}

/**
 * Reads the declarations of a stylesheet's first top-level block with this selector.
 * @param {string} css The stylesheet.
 * @param {string} selector The selector, e.g. ':root'.
 * @returns {Object<string, string>} The custom properties it declares.
 */
function readBlock (css, selector) {
    const text = css.replace(/\/\*[\s\S]*?\*\//g, ' ');
    const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const match = new RegExp(`(?:^|})\\s*${escaped}\\s*\\{([^}]*)\\}`).exec(text);
    const tokens = {};
    if (match) {
        for (const declaration of match[1].split(';')) {
            const colon = declaration.indexOf(':');
            const name = declaration.slice(0, colon).trim();
            if (colon > 0 && name.startsWith('--')) {
                tokens[name] = declaration.slice(colon + 1).trim();
            }
        }
    }
    return tokens;
}

/**
 * The token values a theme applies: the light blocks of the token sheets, then (for dark) their dark blocks.
 * @param {'light'|'dark'} theme The theme.
 * @param {string} [root] The Web project folder.
 * @returns {Object<string, string>} The raw value of each token, before var() is resolved.
 */
function readThemeTokens (theme, root = WEB_ROOT) {
    const sheets = TOKEN_SHEETS.map(file => fs.readFileSync(path.join(root, ...file.split('/')), 'utf8'));
    const tokens = {};
    for (const css of sheets) {
        Object.assign(tokens, readBlock(css, ':root'));
    }
    if (theme === 'dark') {
        for (const css of sheets) {
            Object.assign(tokens, readBlock(css, ':root[data-theme="dark"]'));
        }
    }
    return tokens;
}

/**
 * Resolves a token to its value, following var() references and their fallbacks.
 * @param {Object<string, string>} tokens The theme's tokens.
 * @param {string} name The token, e.g. '--app-text'.
 * @returns {string} The value.
 */
function resolveToken (tokens, name) {
    const seen = new Set();
    let value = tokens[name];
    while (value !== undefined) {
        const reference = /^var\(\s*(--[\w-]+)\s*(?:,\s*(.+))?\)$/.exec(value);
        if (!reference) {
            return value;
        }
        if (seen.has(reference[1])) {
            throw new Error(`${name} refers to itself`);
        }
        seen.add(reference[1]);
        value = tokens[reference[1]] !== undefined ? tokens[reference[1]] : reference[2];
    }
    throw new Error(`${name} is not defined`);
}

module.exports = {
    AA_TEXT, AA_UI, parseColour, composite, flattenBackground, relativeLuminance, contrastRatio, readThemeTokens, resolveToken
};

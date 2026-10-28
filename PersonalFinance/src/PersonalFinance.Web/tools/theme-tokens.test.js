'use strict';
/* eslint-env node */

/**
 * Tests for the semantic theme tokens (issue #482): the surface, text, border, state and chart tokens exist in the
 * Ledger (ledger.css) and app (site.css) token sheets, and each text/surface pair meets WCAG AA in the dark theme
 * (and in the light theme, where the light value is new or already passes). Contrast comes from contrast.js.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const { AA_TEXT, AA_UI, parseColour, contrastRatio, flattenBackground, readThemeTokens, resolveToken } = require('./contrast');

const BOTH = ['light', 'dark'];
const DARK = ['dark'];
const PAGE_SURFACES = ['--app-bg', '--app-surface', '--app-surface-secondary', '--app-surface-raised'];
const CHART_SERIES = ['--app-chart-1', '--app-chart-2', '--app-chart-3', '--app-chart-4', '--app-chart-5', '--app-chart-6'];

/**
 * Builds the pairs to check: each foreground on each background.
 * @param {string[]} foregrounds The text or UI tokens.
 * @param {string[]} backgrounds The surface tokens.
 * @param {number} minimum The ratio each pair needs.
 * @param {string[]} themes The themes it applies to.
 * @returns {{fg: string, bg: string, minimum: number, themes: string[]}[]} The pairs.
 */
function pairs (foregrounds, backgrounds, minimum, themes) {
    return foregrounds.flatMap(fg => backgrounds.map(bg => ({ fg, bg, minimum, themes })));
}

const PAIRS = [
    // Text levels on the surface levels (page, card, secondary, modal/popover)
    ...pairs(['--app-text', '--app-text-soft', '--app-text-muted'], PAGE_SURFACES, AA_TEXT, BOTH),
    // Links: the text form of the brand green, at rest and on hover
    ...pairs(['--app-brand-text', '--app-brand-text-hover'], PAGE_SURFACES, AA_TEXT, DARK),
    ...pairs(['--app-brand-text'], ['--app-brand-soft'], AA_TEXT, DARK),
    // Borders of form controls are UI parts (the light theme keeps its lighter rule)
    ...pairs(['--app-border-control'], PAGE_SURFACES, AA_UI, DARK),
    // Labels on filled buttons, badges and active items
    ...pairs(['--app-text-on-brand'], ['--app-brand-fill', '--app-brand-fill-hover', '--app-negative-fill', '--app-neutral-fill'], AA_TEXT, BOTH),
    ...pairs(['--app-on-warning'], ['--app-warning-fill'], AA_TEXT, BOTH),
    ...pairs(['--app-brand-fill', '--app-negative-fill', '--app-warning-fill'], ['--app-bg', '--app-surface'], AA_UI, DARK),
    // Positive, negative and warning states on their soft backgrounds; the info notice on the brand wash
    { fg: '--app-positive-text', bg: '--app-positive-bg', minimum: AA_TEXT, themes: BOTH },
    { fg: '--app-negative-text', bg: '--app-negative-bg', minimum: AA_TEXT, themes: BOTH },
    { fg: '--app-warning-text', bg: '--app-warning-bg', minimum: AA_TEXT, themes: BOTH },
    { fg: '--app-brand-on-soft', bg: '--app-brand-soft', minimum: AA_TEXT, themes: BOTH },
    { fg: '--lp-ledger-on-wash', bg: '--lp-ledger-wash', minimum: AA_TEXT, themes: BOTH },
    { fg: '--app-warning-fill', bg: '--app-bg', minimum: AA_UI, themes: DARK },
    // The info wash behind the Google Drive sample-data notice, the active sidebar link and a hovered row (#484)
    ...pairs(['--app-text', '--app-text-soft', '--app-brand-text'], ['--app-info-bg'], AA_TEXT, BOTH),
    // Labels on a chart series' fill, e.g. the Google Drive report's allocation bar (#484)
    ...pairs(['--app-on-chart'], ['--app-chart-1', '--app-chart-2'], AA_TEXT, BOTH),
    // Toasts
    { fg: '--app-text', bg: '--app-toast-bg', minimum: AA_TEXT, themes: BOTH },
    // The navy band and sidebar, navy in both themes
    ...pairs(['--lp-on-navy', '--lp-on-navy-soft', '--lp-on-navy-muted', '--lp-on-navy-accent'], ['--lp-navy'], AA_TEXT, BOTH),
    { fg: '--lp-navy', bg: '--lp-on-navy', minimum: AA_TEXT, themes: BOTH },
    { fg: '--lp-navy', bg: '--lp-on-navy-wash', minimum: AA_TEXT, themes: BOTH },
    // Charts: axis text, tooltip, and each categorical series as a UI part on the page and a card
    ...pairs(['--app-chart-axis'], ['--app-bg', '--app-surface'], AA_TEXT, BOTH),
    { fg: '--app-chart-tooltip-text', bg: '--app-chart-tooltip-bg', minimum: AA_TEXT, themes: BOTH },
    ...pairs(CHART_SERIES, ['--app-bg', '--app-surface'], AA_UI, BOTH)
];

const NEW_TOKENS = [
    '--app-surface-raised', '--app-brand-text', '--app-brand-text-hover', '--app-brand-fill', '--app-brand-fill-hover',
    '--app-brand-on-soft', '--app-border-control', '--app-positive-bg', '--app-positive-text',
    '--app-negative-bg', '--app-negative-text', '--app-negative-fill', '--app-warning-bg', '--app-warning-text',
    '--app-warning-fill', '--app-neutral-fill', '--app-toast-bg', '--app-overlay',
    '--app-overlay-strong', '--app-shadow-popover', '--app-shadow-dialog', '--app-shadow-banner',
    '--app-chart-grid', '--app-chart-axis', '--app-chart-tooltip-bg', '--app-chart-tooltip-text',
    '--app-chart-tooltip-border', '--app-shadow-positive', '--lp-raised', '--lp-ledger-fill', '--lp-ledger-text',
    '--lp-ledger-on-wash', '--lp-brass-glow', '--lp-shadow-float', ...CHART_SERIES,
    '--app-info-bg', '--app-border-subtle', '--app-on-chart', '--app-shadow-positive-hover'
];
// White and black labels, and what sits on the navy band, are the same in both themes on purpose
const SAME_IN_BOTH = [
    '--app-text-on-brand', '--app-on-warning', '--lp-on-ledger', '--lp-on-navy', '--lp-on-navy-soft', '--lp-on-navy-muted',
    '--lp-on-navy-accent', '--lp-on-navy-accent-wash', '--lp-on-navy-rule', '--lp-on-navy-hover', '--lp-on-navy-outline',
    '--lp-on-navy-wash'
];

for (const theme of BOTH) {
    test(`the semantic tokens are defined in the ${theme} theme`, () => {
        const tokens = readThemeTokens(theme);
        const missing = [...NEW_TOKENS, ...SAME_IN_BOTH].filter(name => {
            try {
                resolveToken(tokens, name);
                return false;
            } catch {
                return true;
            }
        });
        assert.deepStrictEqual(missing, []);
    });

    test(`every ${theme} text/surface pair meets WCAG AA`, () => {
        const tokens = readThemeTokens(theme);
        const failures = PAIRS.filter(pair => pair.themes.includes(theme))
            .map(({ fg, bg, minimum }) => ({ fg, bg, minimum, ratio: contrastRatio(resolveToken(tokens, fg), resolveToken(tokens, bg)) }))
            .filter(({ ratio, minimum }) => ratio < minimum)
            .map(({ fg, bg, minimum, ratio }) => `${fg} on ${bg}: ${ratio.toFixed(2)} < ${minimum}`);
        assert.deepStrictEqual(failures, []);
    });
}

test('the dark theme gives each new colour token its own value', () => {
    // A token that only follows a swapped --lp-* token still counts: what matters is that dark differs from light.
    const light = readThemeTokens('light');
    const dark = readThemeTokens('dark');
    const resolve = (tokens, name) => {
        const value = resolveToken(tokens, name);
        const colour = parseColour(value);
        return colour ? JSON.stringify(colour) : value;
    };
    const unchanged = NEW_TOKENS.filter(name => resolve(light, name) === resolve(dark, name));
    assert.deepStrictEqual(unchanged, []);
});

test('contrast ratios match the WCAG reference values', () => {
    assert.strictEqual(contrastRatio('#000', '#fff').toFixed(2), '21.00');
    assert.strictEqual(contrastRatio('#fff', '#fff').toFixed(2), '1.00');
    assert.strictEqual(contrastRatio('#767676', '#FFFFFF').toFixed(2), '4.54');
    assert.strictEqual(contrastRatio('rgb(118, 118, 118)', 'rgba(255, 255, 255, 1)').toFixed(2), '4.54');
});

test('a translucent colour is laid over what is behind it', () => {
    // Half-transparent black over white is mid grey
    assert.strictEqual(contrastRatio('rgba(0, 0, 0, 0.5)', '#fff').toFixed(2), contrastRatio('rgb(127.5, 127.5, 127.5)', '#fff').toFixed(2));
    assert.strictEqual(contrastRatio('#000', 'transparent').toFixed(2), '21.00');
});

test('an element\'s background is its ancestors\' colours laid over white', () => {
    // A transparent body over a navy page, under a half-white card
    const seen = flattenBackground(['rgb(20, 40, 60)', 'rgba(0, 0, 0, 0)', 'rgba(255, 255, 255, 0.5)', 'oklch(1 0 0)']);
    assert.deepStrictEqual(seen, { r: 137.5, g: 147.5, b: 157.5, a: 1 });
    assert.deepStrictEqual(flattenBackground([]), { r: 255, g: 255, b: 255, a: 1 });
});

test('colours are read in the forms a browser computes', () => {
    assert.deepStrictEqual(parseColour('rgb(1, 2, 3)'), { r: 1, g: 2, b: 3, a: 1 });
    assert.deepStrictEqual(parseColour('rgba(1, 2, 3, 0.5)'), { r: 1, g: 2, b: 3, a: 0.5 });
    assert.deepStrictEqual(parseColour('rgb(1 2 3 / 50%)'), { r: 1, g: 2, b: 3, a: 0.5 });
    assert.deepStrictEqual(parseColour('color(srgb 1 0 0.5)'), { r: 255, g: 0, b: 127.5, a: 1 });
    assert.deepStrictEqual(parseColour('#1D6A5080'), { r: 29, g: 106, b: 80, a: 128 / 255 });
    assert.strictEqual(parseColour('color-mix(in srgb, red, blue)'), null);
});

test('a token resolves through var() references and their fallbacks', () => {
    const tokens = { '--a': 'var(--b)', '--b': 'var(--missing, #123456)', '--loop': 'var(--loop)' };
    assert.strictEqual(resolveToken(tokens, '--a'), '#123456');
    assert.throws(() => resolveToken(tokens, '--loop'), /refers to itself/);
    assert.throws(() => resolveToken(tokens, '--nope'), /not defined/);
});

'use strict';
/* eslint-env node */

/**
 * Tests for the theme mode (wwwroot/js/core/theme.js, issue #296): with no stored choice the page follows the device
 * setting, a stored Light or Dark wins over it, System clears the choice, and the switchers show what is in effect.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'core', 'theme.js'), 'utf8');

/**
 * A button with just enough of the DOM for theme.js: attributes, classList, closest, and child spans by class.
 * @param {object} attributes - Initial attributes.
 * @param {string[]} childClasses - Classes of the spans inside it.
 * @returns {object} The fake element.
 */
function fakeButton (attributes, childClasses) {
    const attrs = Object.assign({}, attributes);
    const classes = new Set();
    const children = {};
    (childClasses || []).forEach((name) => {
        children[name] = { hidden: false };
    });
    const button = {
        getAttribute: (name) => (name in attrs ? attrs[name] : null),
        setAttribute: (name, value) => { attrs[name] = String(value); },
        hasAttribute: (name) => name in attrs,
        classList: {
            toggle: (name, on) => { if (on) { classes.add(name); } else { classes.delete(name); } },
            contains: (name) => classes.has(name)
        },
        querySelector: (selector) => children[selector.replace(/^\./, '')] || null,
        closest: (selector) => (button.hasAttribute(selector.replace(/^\[|\]$/g, '')) ? button : null),
        children: children
    };
    return button;
}

/**
 * Load theme.js against a fake page.
 * @param {object} options - stored: the localStorage value; deviceDark: prefers-color-scheme; blockStorage: throw on access.
 * @returns {object} The page: ThemeMode, root attributes, buttons, storage, and helpers to click or flip the device.
 */
function load (options) {
    const opts = options || {};
    const store = {};
    if (opts.stored !== undefined) {
        store.theme = opts.stored;
    }
    // What _Layout's head script sets before theme.js runs.
    const headTheme = opts.stored === 'light' || opts.stored === 'dark' ? opts.stored : (opts.deviceDark ? 'dark' : 'light');
    const rootAttrs = { 'data-theme': headTheme, 'data-bs-theme': headTheme };
    const meta = { content: '#ffffff', setAttribute (name, value) { this[name] = value; } };
    const menu = ['light', 'dark', 'system'].map((choice) => fakeButton({ 'data-theme-choice': choice }, ['theme-switcher__check']));
    const toggle = fakeButton({
        'data-theme-toggle': '',
        'data-label-to-dark': 'Switch to dark theme',
        'data-label-to-light': 'Switch to light theme'
    }, ['theme-toggle__sun', 'theme-toggle__moon']);
    const listeners = {};
    const mediaListeners = [];
    const media = { matches: Boolean(opts.deviceDark), addEventListener: (type, fn) => mediaListeners.push(fn) };
    const events = [];

    const guard = (fn) => function () {
        if (opts.blockStorage) {
            throw new Error('SecurityError');
        }
        return fn.apply(null, arguments);
    };

    const window = {
        localStorage: {
            getItem: guard((key) => (key in store ? store[key] : null)),
            setItem: guard((key, value) => { store[key] = String(value); }),
            removeItem: guard((key) => { delete store[key]; })
        },
        matchMedia: () => media,
        addEventListener: () => {},
        CustomEvent: function (type, init) { this.type = type; this.detail = init.detail; },
        Element: Object,
        document: {
            readyState: 'complete',
            documentElement: {
                getAttribute: (name) => (name in rootAttrs ? rootAttrs[name] : null),
                setAttribute: (name, value) => { rootAttrs[name] = value; }
            },
            querySelector: (selector) => (selector === 'meta[name="theme-color"]' ? meta : null),
            querySelectorAll: (selector) => {
                if (selector === '[data-theme-choice]') {
                    return menu;
                }
                return selector === '[data-theme-toggle]' ? [toggle] : [];
            },
            addEventListener: (type, fn) => { listeners[type] = fn; },
            dispatchEvent: (event) => events.push(event)
        }
    };
    window.window = window;
    vm.createContext(window);
    vm.runInContext(SOURCE, window);

    return {
        theme: window.ThemeMode,
        root: rootAttrs,
        meta: meta,
        menu: menu,
        toggle: toggle,
        store: store,
        events: events,
        click: (element) => listeners.click({ target: element }),
        setDevice: (dark) => {
            media.matches = dark;
            mediaListeners.forEach((fn) => fn());
        }
    };
}

test('with no stored choice the page follows a dark device setting', () => {
    const page = load({ deviceDark: true });
    assert.strictEqual(page.root['data-theme'], 'dark');
    assert.strictEqual(page.root['data-bs-theme'], 'dark');
    assert.strictEqual(page.meta.content, '#0E1821');
    assert.strictEqual(page.theme.getChoice(), 'system');
});

test('a stored Light wins over a dark device setting', () => {
    const page = load({ stored: 'light', deviceDark: true });
    assert.strictEqual(page.root['data-theme'], 'light');
    assert.strictEqual(page.root['data-bs-theme'], 'light');
});

test('an unknown stored value reads as System', () => {
    const page = load({ stored: 'sepia', deviceDark: false });
    assert.strictEqual(page.theme.getChoice(), 'system');
    assert.strictEqual(page.root['data-theme'], 'light');
});

test('choosing Dark in the menu stores it and marks only Dark as current', () => {
    const page = load({ deviceDark: false });
    assert.strictEqual(page.events.length, 0);
    page.click(page.menu[1]);
    assert.strictEqual(page.store.theme, 'dark');
    assert.strictEqual(page.root['data-theme'], 'dark');
    assert.deepStrictEqual(page.menu.map((b) => b.getAttribute('aria-current')), ['false', 'true', 'false']);
    assert.deepStrictEqual(page.menu.map((b) => b.children['theme-switcher__check'].hidden), [true, false, true]);
    assert.strictEqual(page.events.length, 1);
    assert.strictEqual(page.events[0].detail.theme, 'dark');
});

test('choosing System clears the stored choice and returns to the device setting', () => {
    const page = load({ stored: 'dark', deviceDark: false });
    page.click(page.menu[2]);
    assert.ok(!('theme' in page.store));
    assert.strictEqual(page.root['data-theme'], 'light');
});

test('a device change is followed under System and ignored under an explicit choice', () => {
    const followed = load({ deviceDark: false });
    followed.setDevice(true);
    assert.strictEqual(followed.root['data-theme'], 'dark');

    const pinned = load({ stored: 'light', deviceDark: false });
    pinned.setDevice(true);
    assert.strictEqual(pinned.root['data-theme'], 'light');
});

test('the top bar toggle flips the theme in effect and relabels itself', () => {
    const page = load({ deviceDark: true });
    assert.strictEqual(page.toggle.getAttribute('aria-label'), 'Switch to light theme');
    assert.strictEqual(page.toggle.getAttribute('aria-pressed'), 'true');
    assert.strictEqual(page.toggle.children['theme-toggle__sun'].hidden, false);

    page.click(page.toggle);
    assert.strictEqual(page.store.theme, 'light');
    assert.strictEqual(page.root['data-theme'], 'light');
    assert.strictEqual(page.toggle.getAttribute('aria-label'), 'Switch to dark theme');
    assert.strictEqual(page.toggle.children['theme-toggle__moon'].hidden, false);
    assert.strictEqual(page.toggle.children['theme-toggle__sun'].hidden, true);
});

test('blocked storage still switches the page', () => {
    const page = load({ deviceDark: false, blockStorage: true });
    assert.strictEqual(page.theme.getChoice(), 'system');
    page.click(page.menu[1]);
    assert.strictEqual(page.root['data-theme'], 'dark');
});
